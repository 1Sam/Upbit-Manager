// ✅ [수정] Core/Orderbook/OrderbookHeatmapEngine.cs
// 🔧 using Crypto.Collector.Shared 제거 → 자체 모델(HeatmapCell, SpoofingEvent) 사용

using System;
using System.Collections.Generic;
using System.Linq;
using Crypto.Collector.Shared;

namespace Upbit_Manager.Core.Orderbook
{
    /// <summary>
    /// 오더북 히스토리를 가격-시간 히트맵 데이터로 변환하는 엔진
    ///
    /// 설계:
    /// - 가격 버킷팅: 틱 사이즈 기준으로 가격을 그룹화
    /// - 시간 버킷팅: N초 단위로 시간을 그룹화
    /// - 잔량 누적: 동일 버킷에 잔량 합산
    /// - 상태 추적: 체결/취소 여부로 Spoofing 감지
    /// </summary>
    public sealed class OrderbookHeatmapEngine
    {
        #region [ 설정 ]

        /// <summary>가격 버킷 크기 (원 단위)</summary>
        public double PriceBucketSize { get; set; } = 1.0;

        /// <summary>시간 버킷 크기 (초)</summary>
        public int TimeBucketSeconds { get; set; } = 5;

        /// <summary>히트맵 보관 시간 (분)</summary>
        // OrderbookHeatmapEngine.cs
        public int HistoryMinutes { get; set; } = 360; // 🔥 필요하면 360(6시간)으로 늘리기

        /// <summary>Spoofing 감지 임계 시간 (초)</summary>
        public int SpoofingThresholdSeconds { get; set; } = 15;

        /// <summary>대량 호가 감지 기준 (평균 잔량 대비 배수)</summary>
        public double LargeOrderMultiplier { get; set; } = 3.0;

        #endregion

        #region [ 내부 상태 ]

        // (가격버킷, 시간버킷OA) → HeatmapCell
        private readonly Dictionary<(double Price, double TimeOA), HeatmapCell> _cells = new();

        // 대량 호가 추적: 가격 → (첫 출현 시각, 잔량, 방향)
        private readonly Dictionary<double, (DateTime FirstSeen, double Volume, string Side)> _largeOrders = new();

        // 체결된 가격 추적 (외부에서 주입)
        private readonly HashSet<double> _filledPrices = new();

        private readonly object _lock = new();

        private OrderbookSnapshot? _prevSnapshot;
        //private double _avgUnitVolume = 100.0; // 초기 평균값
        private double _avgUnitVolume = 100_000.0; // 🔥 초기값을 현실적인 값으로
        private int _snapshotCount = 0;            // 🔥 워밍업 카운터
        private const int WarmupSnapshots = 5;     // 🔥 5회 워밍업 후 감지 시작
        #endregion

        #region [ 이벤트 ]

        /// <summary>Spoofing 감지 이벤트</summary>
        public event Action<SpoofingEvent>? OnSpoofingDetected;

        /// <summary>대량 호가 출현 이벤트 (price, volume, side)</summary>
        public event Action<double, double, string>? OnLargeOrderDetected;

        #endregion

        // 🔥 히스토리 로딩 중 Prune 스킵
        public bool IsLoadingHistory { get; set; } = false;


        #region [ 공개 API ]

        /// <summary>
        /// 새 오더북 스냅샷 처리
        /// </summary>
        public void ProcessSnapshot(OrderbookSnapshot snapshot)
        {
            lock (_lock)
            {
                _snapshotCount++;              // 🔥 카운터 증가
                UpdateAverageVolume(snapshot);
                UpdateHeatmapCells(snapshot);
                DetectLargeOrders(snapshot);

                if (_prevSnapshot != null)
                    DetectSpoofing(snapshot);

                PruneOldCells();
                _prevSnapshot = snapshot;
            }
        }

        /// <summary>
        /// 현재 히트맵 셀 목록 반환 (렌더링용)
        /// </summary>
        public List<HeatmapCell> GetCells()
        {
            lock (_lock)
                return _cells.Values.ToList();
        }

        /// <summary>
        /// 특정 시간 범위의 셀만 반환
        /// </summary>
        public List<HeatmapCell> GetCells(double minOA, double maxOA)
        {
            lock (_lock)
                return _cells.Values
                    .Where(c => c.TimeOA >= minOA && c.TimeOA <= maxOA)
                    .ToList();
        }

        /// <summary>
        /// 체결 데이터 주입 (UpbitSocketService.OnTradeUpdated에서 호출)
        /// </summary>
        public void RegisterTrade(double price)
        {
            lock (_lock)
                _filledPrices.Add(BucketPrice(price));
        }

        /// <summary>
        /// 전체 셀 초기화
        /// </summary>
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

        #region [ 내부 처리 ]

        private void UpdateHeatmapCells(OrderbookSnapshot snapshot)
        {
            double timeOA = BucketTime(snapshot.Time);

            foreach (var unit in snapshot.Units)
            {
                // 매도 호가
                double askBucket = BucketPrice(unit.AskPrice);
                var askKey = (askBucket, timeOA);

                if (!_cells.TryGetValue(askKey, out var askCell))
                {
                    askCell = new HeatmapCell
                    {
                        Price = askBucket,
                        TimeOA = timeOA,
                        FirstSeen = snapshot.Time,
                        LastSeen = snapshot.Time,
                        State = HeatmapCellState.Active
                    };
                    _cells[askKey] = askCell;
                }

                // 최대 잔량으로 업데이트 (가장 두꺼웠던 순간 표시)
                if (unit.AskSize > askCell.Volume)
                    askCell.Volume = unit.AskSize;
                askCell.LastSeen = snapshot.Time;

                // 매수 호가
                double bidBucket = BucketPrice(unit.BidPrice);
                var bidKey = (bidBucket, timeOA);

                if (!_cells.TryGetValue(bidKey, out var bidCell))
                {
                    bidCell = new HeatmapCell
                    {
                        Price = bidBucket,
                        TimeOA = timeOA,
                        FirstSeen = snapshot.Time,
                        LastSeen = snapshot.Time,
                        State = HeatmapCellState.Active
                    };
                    _cells[bidKey] = bidCell;
                }

                if (unit.BidSize > bidCell.Volume)
                    bidCell.Volume = unit.BidSize;
                bidCell.LastSeen = snapshot.Time;
            }
        }

        private void DetectLargeOrders(OrderbookSnapshot snapshot)
        {
            if (_snapshotCount < WarmupSnapshots) return; // 🔥 워밍업 중 감지 스킵

            foreach (var unit in snapshot.Units)
            {
                // 매도 대량 호가 감지
                if (unit.AskSize > _avgUnitVolume * LargeOrderMultiplier)
                {
                    double key = BucketPrice(unit.AskPrice);
                    if (!_largeOrders.ContainsKey(key))
                    {
                        _largeOrders[key] = (snapshot.Time, unit.AskSize, "ASK");
                        OnLargeOrderDetected?.Invoke(unit.AskPrice, unit.AskSize, "ASK");
                    }
                }

                // 매수 대량 호가 감지 (음수 키로 ASK/BID 구분)
                if (unit.BidSize > _avgUnitVolume * LargeOrderMultiplier)
                {
                    double key = -BucketPrice(unit.BidPrice);
                    if (!_largeOrders.ContainsKey(key))
                    {
                        _largeOrders[key] = (snapshot.Time, unit.BidSize, "BID");
                        OnLargeOrderDetected?.Invoke(unit.BidPrice, unit.BidSize, "BID");
                    }
                }
            }
        }

        private void DetectSpoofing(OrderbookSnapshot current)
        {
            if (_prevSnapshot == null) return;

            var currentAskPrices = new HashSet<double>(
                current.Units.Select(u => BucketPrice(u.AskPrice)));
            var currentBidPrices = new HashSet<double>(
                current.Units.Select(u => BucketPrice(u.BidPrice)));

            var toRemove = new List<double>();

            foreach (var kvp in _largeOrders)
            {
                double key = kvp.Key;
                double price = Math.Abs(key);
                string side = kvp.Value.Side;
                DateTime first = kvp.Value.FirstSeen;
                double volume = kvp.Value.Volume;

                // 아직 감지 시간 미달
                if ((current.Time - first).TotalSeconds < SpoofingThresholdSeconds)
                    continue;

                bool stillExists = side == "ASK"
                    ? currentAskPrices.Contains(price)
                    : currentBidPrices.Contains(price);

                if (!stillExists)
                {
                    bool wasFilled = _filledPrices.Contains(price);

                    if (!wasFilled)
                    {
                        // Spoofing 의심
                        OnSpoofingDetected?.Invoke(new SpoofingEvent
                        {
                            DetectedAt = current.Time,
                            Price = price,
                            Volume = volume,
                            Side = side,
                            Duration = current.Time - first
                        });

                        MarkCellState(price, first, HeatmapCellState.SpoofingSuspect);
                    }
                    else
                    {
                        MarkCellState(price, first, HeatmapCellState.FilledAndGone);
                    }

                    toRemove.Add(key);
                }
            }

            foreach (var key in toRemove)
                _largeOrders.Remove(key);

            // 🔧 스냅샷 간 _filledPrices 초기화 (누적 방지)
            _filledPrices.Clear();
        }

        private void MarkCellState(double price, DateTime firstSeen, HeatmapCellState state)
        {
            double priceBucket = BucketPrice(price);
            double timeOA = BucketTime(firstSeen);
            var key = (priceBucket, timeOA);

            if (_cells.TryGetValue(key, out var cell))
                cell.State = state;
        }

        private void UpdateAverageVolume(OrderbookSnapshot snapshot)
        {
            if (snapshot.Units.Length == 0) return;

            double avg = snapshot.Units.Average(u => (u.AskSize + u.BidSize) / 2.0);

            // EMA 방식으로 평균 업데이트
            _avgUnitVolume = (_avgUnitVolume * 0.95) + (avg * 0.05);
        }

        private void PruneOldCells()
        {
            // 🔥 히스토리 로딩 중엔 Prune 스킵
            if (IsLoadingHistory) return;

            double cutoff = DateTime.Now
                .AddMinutes(-HistoryMinutes)
                .ToOADate();

            var oldKeys = _cells.Keys
                .Where(k => k.TimeOA < cutoff)
                .ToList();

            foreach (var key in oldKeys)
                _cells.Remove(key);
        }

        private double BucketPrice(double price)
            => Math.Round(price / PriceBucketSize) * PriceBucketSize;

        private double BucketTime(DateTime time)
        {
            long ticks = time.Ticks;
            long bucketTicks = TimeSpan.FromSeconds(TimeBucketSeconds).Ticks;
            long bucketed = (ticks / bucketTicks) * bucketTicks;
            return new DateTime(bucketed).ToOADate();
        }

        #endregion
    }
}