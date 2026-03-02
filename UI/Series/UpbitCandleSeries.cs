using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Core; // RingBuffer가 위치한 네임스페이스 (UpbitManagerCore.cs 참조)
using Upbit_Manager.Core.Alarms;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.Interfaces;


namespace Upbit_Manager.UI.Series
{
    public class UpbitCandleSeries : ChartSeriesBase, IVolumeDataProvider
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Candle;
        public override string Label => "Upbit - 캔들";
        public override bool DefaultOn => true;

        // 상단 가격 영역(80%)에 그려지도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        private const int MAX_BUFFER = 3000;

        // RingBuffer<T>는 UpbitManager.Core 또는 Upbit_Manager.Core에 정의됨
        private readonly RingBuffer<OHLC> _ohlcBuffer = new(MAX_BUFFER);

        // ⭐ ScottPlot.OHLC에 없는 거래량 데이터를 별도로 관리하는 버퍼
        private readonly RingBuffer<double> _volumeBuffer = new(MAX_BUFFER);

        private DateTime _lastCandleTime = DateTime.MinValue;
        public double LastPrice { get; private set; }
        public double PrevPrice { get; private set; }

        public override void UpdateData(object payload)
        {
            // 데이터 변경 시 락을 걸어 스레드 안전성 확보
            lock (_ohlcBuffer)
            {
                if (payload is List<CommonCandle> candles)
                {
                    // RingBuffer에 Clear 메서드가 없을 경우 새로 할당하거나 구현 필요
                    // 여기서는 기존 데이터를 모두 덮어쓰는 방식으로 동작하도록 유도
                    _ohlcBuffer.Add(new OHLC()); // 임시 처리 (실제 RingBuffer에 Clear 구현 권장)

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
                    PrevPrice = LastPrice;
                    LastPrice = rt.Price;

                    DateTime now = DateTime.Now;
                    DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                    if (_ohlcBuffer.Count > 0 && _lastCandleTime == currentMinute)
                    {
                        // 기존 캔들 업데이트
                        var last = _ohlcBuffer.Last;
                        double newHigh = Math.Max(last.High, rt.Price);
                        double newLow = Math.Min(last.Low, rt.Price);

                        // OHLC는 struct이므로 새로 생성하여 UpdateLast 호출
                        var updated = new OHLC(last.Open, newHigh, newLow, rt.Price, currentMinute, TimeSpan.FromMinutes(1));
                        _ohlcBuffer.UpdateLast(updated);

                        // 거래량 누적 업데이트
                        double currentVol = _volumeBuffer.Last;
                        _volumeBuffer.UpdateLast(currentVol + rt.Volume);
                    }
                    else
                    {
                        // 새 캔들 추가
                        _ohlcBuffer.Add(new OHLC(rt.Price, rt.Price, rt.Price, rt.Price, currentMinute, TimeSpan.FromMinutes(1)));
                        _volumeBuffer.Add(rt.Volume);
                        _lastCandleTime = currentMinute;
                    }
                }
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_ohlcBuffer)
            {
                if (_ohlcBuffer.Count == 0) return;

                // 1. 캔들스틱 추가
                var ohlcs = _ohlcBuffer.ToList();
                var cp = plot.Add.Candlestick(ohlcs);
                cp.RisingColor = Colors.Red;
                cp.FallingColor = Colors.Blue;
                cp.Sequential = false;

                // 전달받은 가격 전용 축에 할당
                cp.Axes.YAxis = targetAxis;

                // 2. 현재가 수평 점선 및 라벨 렌더링
                if (LastPrice > 0)
                {
                    var hline = plot.Add.HorizontalLine(LastPrice);
                    hline.Axes.YAxis = targetAxis;

                    hline.LineStyle.Width = 1;
                    hline.LinePattern = LinePattern.Dashed;
                    hline.LineStyle.Color = Colors.Yellow.WithAlpha(0.5);

                    hline.Text = LastPrice.ToString("N0");
                    hline.LabelOppositeAxis = true;
                    hline.LabelBackgroundColor = LastPrice >= PrevPrice ? Colors.Red : Colors.Blue;
                    hline.LabelFontColor = Colors.White;
                    hline.TextAlignment = Alignment.MiddleLeft;
                    hline.LabelStyle.OffsetX = 1;
                }
            }
        }

        public override void Clear()
        {
            lock (_ohlcBuffer)
            {
                // RingBuffer에 Clear가 없으므로 로직상 초기화 필요
                _lastCandleTime = DateTime.MinValue;
                LastPrice = PrevPrice = 0;
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

        #region [ IVolumeDataProvider 구현 ]

        public double GetAverageVolume(int lookbackCount)
        {
            lock (_ohlcBuffer)
            {
                if (_volumeBuffer.Count < 2) return 0;

                int takeCount = Math.Min(lookbackCount, _volumeBuffer.Count - 1);
                var list = _volumeBuffer.ToList();

                // 마지막 봉(진행중) 제외 후 평균 계산
                return list.Take(list.Count - 1).TakeLast(takeCount).Average();
            }
        }

        public double GetCurrentCandleVolume()
        {
            lock (_ohlcBuffer)
            {
                return _volumeBuffer.Count > 0 ? _volumeBuffer.Last : 0;
            }
        }

        #endregion
    }
}