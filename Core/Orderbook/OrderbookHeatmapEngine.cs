using System;
using System.Collections.Generic;
using System.Linq;

namespace Upbit_Manager.Core.Orderbook
{
    public sealed class OrderbookHeatmapEngine
    {
        #region 설정

        public double PriceBucketSize { get; set; } = 1.0;
        public int TimeBucketSeconds { get; set; } = 5;
        public int HistoryMinutes { get; set; } = 360;

        public int SpoofingThresholdSeconds { get; set; } = 15;
        public double LargeOrderMultiplier { get; set; } = 3.0;

        private const double HeatmapEmaAlpha = 0.20;

        #endregion

        #region 내부 상태

        private readonly Dictionary<(double Price, double TimeOA), HeatmapCell> _cells = new();

        private readonly Dictionary<(double Price, string Side),
            (DateTime FirstSeen, double Volume)> _largeOrders = new();

        private readonly HashSet<double> _filledPrices = new();

        private readonly object _lock = new();

        private OrderbookSnapshot? _prevSnapshot;

        private double _avgUnitVolume = 100_000.0;

        private int _snapshotCount = 0;

        private const int WarmupSnapshots = 5;

        #endregion

        #region 이벤트

        public event Action<SpoofingEvent>? OnSpoofingDetected;

        public event Action<double, double, string>? OnLargeOrderDetected;

        #endregion

        public bool IsLoadingHistory { get; set; } = false;

        #region PUBLIC API

        public void ProcessSnapshot(OrderbookSnapshot snapshot)
        {
            lock (_lock)
            {
                _snapshotCount++;

                UpdateAverageVolume(snapshot);

                UpdateHeatmapCells(snapshot);

                DetectLargeOrders(snapshot);

                if (_prevSnapshot != null)
                    DetectSpoofing(snapshot);

                PruneOldCells();

                _prevSnapshot = snapshot;
            }
        }

        public List<HeatmapCell> GetCells()
        {
            lock (_lock)
                return _cells.Values.ToList();
        }

        public List<HeatmapCell> GetCells(double minOA, double maxOA)
        {
            lock (_lock)
                return _cells.Values
                    .Where(c => c.TimeOA >= minOA && c.TimeOA <= maxOA)
                    .ToList();
        }

        public void RegisterTrade(double price)
        {
            lock (_lock)
                _filledPrices.Add(BucketPrice(price));
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cells.Clear();
                _largeOrders.Clear();
                _filledPrices.Clear();
                _prevSnapshot = null;
            }
        }

        #endregion

        #region HEATMAP

        private void UpdateHeatmapCells(OrderbookSnapshot snapshot)
        {
            double timeOA = BucketTime(snapshot.Time);

            foreach (var unit in snapshot.Units)
            {
                UpdateCell(unit.AskPrice, unit.AskSize, snapshot.Time, timeOA);
                UpdateCell(unit.BidPrice, unit.BidSize, snapshot.Time, timeOA);
            }
        }

        private void UpdateCell(double price, double volume, DateTime time, double timeOA)
        {
            double priceBucket = BucketPrice(price);

            var key = (priceBucket, timeOA);

            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = new HeatmapCell
                {
                    Price = priceBucket,
                    TimeOA = timeOA,
                    FirstSeen = time,
                    LastSeen = time,
                    Volume = volume
                };

                _cells[key] = cell;
            }
            else
            {
                cell.Volume = cell.Volume * (1 - HeatmapEmaAlpha)
                              + volume * HeatmapEmaAlpha;

                cell.LastSeen = time;
            }
        }

        #endregion

        #region LARGE ORDER

        private void DetectLargeOrders(OrderbookSnapshot snapshot)
        {
            if (_snapshotCount < WarmupSnapshots)
                return;

            foreach (var unit in snapshot.Units)
            {
                if (unit.AskSize > _avgUnitVolume * LargeOrderMultiplier)
                {
                    double price = BucketPrice(unit.AskPrice);
                    var key = (price, "ASK");

                    if (!_largeOrders.ContainsKey(key))
                    {
                        _largeOrders[key] = (snapshot.Time, unit.AskSize);

                        OnLargeOrderDetected?.Invoke(price, unit.AskSize, "ASK");
                    }
                }

                if (unit.BidSize > _avgUnitVolume * LargeOrderMultiplier)
                {
                    double price = BucketPrice(unit.BidPrice);
                    var key = (price, "BID");

                    if (!_largeOrders.ContainsKey(key))
                    {
                        _largeOrders[key] = (snapshot.Time, unit.BidSize);

                        OnLargeOrderDetected?.Invoke(price, unit.BidSize, "BID");
                    }
                }
            }
        }

        #endregion

        #region SPOOFING

        private void DetectSpoofing(OrderbookSnapshot current)
        {
            var currentAskPrices = new HashSet<double>(
                current.Units.Select(u => BucketPrice(u.AskPrice)));

            var currentBidPrices = new HashSet<double>(
                current.Units.Select(u => BucketPrice(u.BidPrice)));

            var toRemove = new List<(double, string)>();

            foreach (var kv in _largeOrders)
            {
                double price = kv.Key.Price;
                string side = kv.Key.Side;

                DateTime first = kv.Value.FirstSeen;

                if ((current.Time - first).TotalSeconds < SpoofingThresholdSeconds)
                    continue;

                bool stillExists = side == "ASK"
                    ? currentAskPrices.Contains(price)
                    : currentBidPrices.Contains(price);

                if (!stillExists)
                {
                    bool wasFilled = WasPriceFilled(price);

                    if (!wasFilled)
                    {
                        OnSpoofingDetected?.Invoke(new SpoofingEvent
                        {
                            DetectedAt = current.Time,
                            Price = price,
                            Volume = kv.Value.Volume,
                            Side = side,
                            Duration = current.Time - first
                        });

                        MarkCellState(price, first, HeatmapCellState.SpoofingSuspect);
                    }
                    else
                    {
                        MarkCellState(price, first, HeatmapCellState.FilledAndGone);
                    }

                    toRemove.Add(kv.Key);
                }
            }

            foreach (var key in toRemove)
                _largeOrders.Remove(key);

            _filledPrices.Clear();
        }

        private bool WasPriceFilled(double price)
        {
            foreach (var p in _filledPrices)
            {
                if (Math.Abs(p - price) <= PriceBucketSize)
                    return true;
            }

            return false;
        }

        #endregion

        #region 상태 표시

        private void MarkCellState(double price, DateTime firstSeen, HeatmapCellState state)
        {
            double priceBucket = BucketPrice(price);

            double timeOA = BucketTime(firstSeen);

            var key = (priceBucket, timeOA);

            if (_cells.TryGetValue(key, out var cell))
                cell.State = state;
        }

        #endregion

        #region 평균 볼륨

        private void UpdateAverageVolume(OrderbookSnapshot snapshot)
        {
            if (snapshot.Units.Length == 0)
                return;

            double avg = snapshot.Units
                .Average(u => (u.AskSize + u.BidSize) / 2.0);

            _avgUnitVolume = _avgUnitVolume * 0.95 + avg * 0.05;
        }

        #endregion

        #region MEMORY

        private void PruneOldCells()
        {
            if (IsLoadingHistory)
                return;

            double cutoff = DateTime.Now
                .AddMinutes(-HistoryMinutes)
                .ToOADate();

            var oldKeys = _cells.Keys
                .Where(k => k.TimeOA < cutoff)
                .ToList();

            foreach (var key in oldKeys)
                _cells.Remove(key);
        }

        #endregion

        #region BUCKET

        private double BucketPrice(double price)
            => Math.Round(price / PriceBucketSize) * PriceBucketSize;

        private double BucketTime(DateTime time)
        {
            long ticks = time.Ticks;

            long bucketTicks =
                TimeSpan.FromSeconds(TimeBucketSeconds).Ticks;

            long bucketed =
                (ticks / bucketTicks) * bucketTicks;

            return new DateTime(bucketed).ToOADate();
        }

        #endregion
    }
}