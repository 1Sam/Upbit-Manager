using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Models.Common;
using UpbitManager.Models.Upbit;
using Upbit_Manager.Core.Alarms;

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
                    PrevPrice = LastPrice;
                    LastPrice = rt.Price;

                    DateTime now = DateTime.Now;
                    DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                    if (_ohlcBuffer.Count > 0 && _lastCandleTime == currentMinute)
                    {
                        // 기존 캔들 업데이트
                        var last = _ohlcBuffer[_ohlcBuffer.Count - 1];
                        last.High = Math.Max(last.High, rt.Price);
                        last.Low = Math.Min(last.Low, rt.Price);
                        last.Close = rt.Price;
                        _ohlcBuffer.ReplaceLast(last);

                        // 거래량 누적 업데이트
                        double currentVol = _volumeBuffer[_volumeBuffer.Count - 1];
                        _volumeBuffer.ReplaceLast(currentVol + rt.Volume);
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
            if (_ohlcBuffer.Count == 0) return;

            // 1. 캔들스틱 추가
            var ohlcs = _ohlcBuffer.ToArray();
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

        public override void Clear()
        {
            lock (_ohlcBuffer)
            {
                _ohlcBuffer.Clear();
                _volumeBuffer.Clear();
                _lastCandleTime = DateTime.MinValue;
                LastPrice = PrevPrice = 0;
            }
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
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

        #region [ IVolumeDataProvider 구현 ]

        /// <summary>
        /// 최근 N개 캔들의 평균 거래량을 계산 (현재 진행형 캔들 제외)
        /// </summary>
        public double GetAverageVolume(int lookbackCount)
        {
            lock (_ohlcBuffer)
            {
                // 현재 봉(진행중)을 제외해야 하므로 데이터가 최소 2개 이상 필요
                if (_volumeBuffer.Count < 2) return 0;

                // 마지막 봉을 제외한 과거 봉 리스트 추출
                int takeCount = Math.Min(lookbackCount, _volumeBuffer.Count - 1);

                // 링버퍼 특성을 고려하여 과거 데이터 리스트화 후 평균 계산
                var list = _volumeBuffer.ToList();
                return list.Take(list.Count - 1).TakeLast(takeCount).Average();
            }
        }

        /// <summary>
        /// 실시간 누적 중인 현재 캔들의 거래량 반환
        /// </summary>
        public double GetCurrentCandleVolume()
        {
            lock (_ohlcBuffer)
            {
                return _volumeBuffer.Count > 0 ? _volumeBuffer[_volumeBuffer.Count - 1] : 0;
            }
        }

        #endregion
    }
}