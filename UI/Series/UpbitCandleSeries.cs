// ✅ [수정] UI/Series/UpbitCandleSeries.cs
// 🔥 GetRenderSnapshot(): HeatmapForm에 OHLC 스냅샷 전달용 메서드 추가
//    - _renderList를 직접 노출하지 않음 (스레드 안전)
//    - 호출 시점의 복사본 반환 → HeatmapForm이 lock 없이 사용 가능

using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Alarms;
using Upbit_Manager.Core.Collections;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 캔들 데이터를 관리하고 ScottPlot에 최적화된 방식으로 렌더링하는 클래스입니다.
    /// 배열 캐싱, Dirty 플래그, Plottable 객체 재사용을 통해 GC 부하를 최소화합니다.
    /// </summary>
    public class UpbitCandleSeries : ChartSeriesBase, IVolumeDataProvider
    {
        // ─── 속성 정의 (IChartSeries 구현) ──────────────────────────────────────────

        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Candle;
        public override string Label => "Upbit - 캔들";
        public override bool DefaultOn => true;
        public override AxisGroup TargetGroup => AxisGroup.Price;

        // ─── 데이터 버퍼 및 캐싱 상태 ──────────────────────────────────────────

        private const int MAX_BUFFER = 3000;

        private readonly RingBuffer<OHLC> _ohlcBuffer = new(MAX_BUFFER);
        private readonly RingBuffer<double> _volumeBuffer = new(MAX_BUFFER);

        private readonly OHLC[] _cachedOhlcArray = new OHLC[MAX_BUFFER];
        private readonly List<OHLC> _renderList = new(MAX_BUFFER);

        private bool _arrayDirty = true;

        private CandlestickPlot? _candlePlotObject;
        private HorizontalLine? _priceLineObject;

        private DateTime _lastCandleTime = DateTime.MinValue;
        public double LastPrice { get; private set; }
        public double PrevPrice { get; private set; }

        // ─── 🔥 HeatmapForm 공유용 ──────────────────────────────────────────────

        /// <summary>
        /// 현재 렌더링 중인 OHLC 스냅샷을 복사본으로 반환합니다.
        /// HeatmapForm이 lock 없이 안전하게 사용할 수 있도록 복사본 제공.
        /// </summary>
        public List<OHLC> GetRenderSnapshot()
        {
            lock (_ohlcBuffer)
            {
                // _arrayDirty 상태와 무관하게 항상 최신 복사본 반환
                var snapshot = new List<OHLC>(_renderList.Count);
                snapshot.AddRange(_renderList);
                return snapshot;
            }
        }

        /// <summary>
        /// 현재 마켓 심볼 (코인 종류 필터링용)
        /// </summary>
        public string Market { get; set; } = "KRW-ADA";

        // ─── 데이터 업데이트 로직 ──────────────────────────────────────────

        public override void UpdateData(object payload)
        {
            lock (_ohlcBuffer)
            {
                if (payload is List<CommonCandle> candles)
                {
                    _ohlcBuffer.Clear();
                    _volumeBuffer.Clear();

                    foreach (var c in candles)
                    {
                        _ohlcBuffer.Add(new OHLC(c.Open, c.High, c.Low, c.Close, c.Time, TimeSpan.FromMinutes(1)));
                        _volumeBuffer.Add(c.Volume);
                    }

                    if (candles.Count > 0)
                    {
                        var last = candles[^1];
                        _lastCandleTime = last.Time;
                        LastPrice = last.Close;
                        PrevPrice = LastPrice;
                    }
                }
                else if (payload is UpbitRealtimePayload rt)
                {
                    ProcessTick(rt.Price, rt.Volume);
                }
                else if (payload is ValueTuple<double, double, string, DateTime> tick)
                {
                    ProcessTick(tick.Item1, tick.Item2);
                }

                _arrayDirty = true;
            }
        }

        private void ProcessTick(double price, double volume)
        {
            PrevPrice = LastPrice;
            LastPrice = price;

            DateTime now = DateTime.Now;
            DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            if (_ohlcBuffer.Count > 0 && _lastCandleTime == currentMinute)
            {
                var last = _ohlcBuffer.Last;
                var updated = new OHLC(
                    last.Open,
                    Math.Max(last.High, price),
                    Math.Min(last.Low, price),
                    price,
                    currentMinute,
                    TimeSpan.FromMinutes(1));
                _ohlcBuffer.UpdateLast(updated);
                _volumeBuffer.UpdateLast(_volumeBuffer.Last + volume);
            }
            else
            {
                _ohlcBuffer.Add(new OHLC(price, price, price, price, currentMinute, TimeSpan.FromMinutes(1)));
                _volumeBuffer.Add(volume);
                _lastCandleTime = currentMinute;
            }
        }

        // ─── 렌더링 로직 ──────────────────────────────────────────

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_ohlcBuffer)
            {
                if (_ohlcBuffer.Count == 0) return;

                if (_arrayDirty)
                {
                    int count = 0;
                    foreach (var item in _ohlcBuffer)
                    {
                        if (count >= MAX_BUFFER) break;
                        _cachedOhlcArray[count++] = item;
                    }

                    _renderList.Clear();
                    for (int i = 0; i < count; i++)
                        _renderList.Add(_cachedOhlcArray[i]);

                    _arrayDirty = false;
                }

                if (_candlePlotObject == null || !plot.GetPlottables().Contains(_candlePlotObject))
                {
                    _candlePlotObject = plot.Add.Candlestick(_renderList);
                    _candlePlotObject.RisingColor = Colors.Red;
                    _candlePlotObject.FallingColor = Colors.Blue;
                    _candlePlotObject.Sequential = false;
                    _candlePlotObject.Axes.YAxis = targetAxis;
                }

                if (LastPrice > 0)
                {
                    if (_priceLineObject == null || !plot.GetPlottables().Contains(_priceLineObject))
                    {
                        _priceLineObject = plot.Add.HorizontalLine(LastPrice);
                        _priceLineObject.Axes.YAxis = targetAxis;
                        _priceLineObject.LineStyle.Width = 1;
                        _priceLineObject.LinePattern = LinePattern.Dashed;
                        _priceLineObject.LabelFontColor = Colors.White;
                    }

                    _priceLineObject.Y = LastPrice;
                    _priceLineObject.Text = LastPrice.ToString("N0");
                    _priceLineObject.LineStyle.Color = Colors.Yellow.WithAlpha(0.6);
                    _priceLineObject.LabelBackgroundColor = LastPrice >= PrevPrice ? Colors.Red : Colors.Blue;
                    _priceLineObject.TextAlignment = Alignment.MiddleLeft;
                    _priceLineObject.TextRotation = 0;
                    _priceLineObject.LabelOppositeAxis = true;
                    _priceLineObject.LabelStyle.OffsetX = 2;
                }
            }
        }

        public override void Clear()
        {
            lock (_ohlcBuffer)
            {
                _ohlcBuffer.Clear();
                _volumeBuffer.Clear();
                _renderList.Clear();
                _lastCandleTime = DateTime.MinValue;
                LastPrice = PrevPrice = 0;
                _candlePlotObject = null;
                _priceLineObject = null;
                _arrayDirty = true;
            }
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            lock (_ohlcBuffer)
            {
                double high = double.MinValue;
                double low = double.MaxValue;
                bool found = false;

                foreach (var o in _ohlcBuffer)
                {
                    double oa = o.DateTime.ToOADate();
                    if (oa >= minOA && oa <= maxOA)
                    {
                        if (o.High > high) high = o.High;
                        if (o.Low < low) low = o.Low;
                        found = true;
                    }
                }

                return found ? (low, high) : null;
            }
        }

        // ─── IVolumeDataProvider ──────────────────────────────────────────────────

        public double GetAverageVolume(int lookbackCount)
        {
            lock (_ohlcBuffer)
            {
                int totalCount = _volumeBuffer.Count;
                if (totalCount < 2) return 0;

                int actualLookback = Math.Min(lookbackCount, totalCount - 1);
                double sum = 0;

                for (int i = 0; i < actualLookback; i++)
                    sum += _volumeBuffer[totalCount - 2 - i];

                return sum / actualLookback;
            }
        }

        public double GetCurrentCandleVolume()
        {
            lock (_ohlcBuffer)
                return _volumeBuffer.Count > 0 ? _volumeBuffer.Last : 0;
        }
    }
}