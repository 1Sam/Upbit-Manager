using ScottPlot;
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
    /// 업비트 캔들 데이터를 관리하고 ScottPlot에 렌더링하는 클래스입니다.
    /// ChartSeriesBase 상속과 IChartSeries 인터페이스 명시를 통해 형변환 에러를 해결합니다.
    /// </summary>
    public class UpbitCandleSeries : ChartSeriesBase, IChartSeries, IVolumeDataProvider
    {
        // ─── 속성 정의 (IChartSeries 구현) ──────────────────────────────────────────

        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Candle;
        public override string Label => "Upbit - 캔들";
        public override bool DefaultOn => true;

        // 가격 축(Price Axis)에 렌더링되도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        // ─── 데이터 버퍼 및 상태 ──────────────────────────────────────────

        private const int MAX_BUFFER = 3000;

        // Upbit_Manager.Core.RingBuffer 사용
        private readonly RingBuffer<OHLC> _ohlcBuffer = new(MAX_BUFFER);

        // ScottPlot.OHLC에 포함되지 않는 거래량 데이터를 별도로 관리하는 버퍼
        private readonly RingBuffer<double> _volumeBuffer = new(MAX_BUFFER);

        private DateTime _lastCandleTime = DateTime.MinValue;
        public double LastPrice { get; private set; }
        public double PrevPrice { get; private set; }

        // ─── 데이터 업데이트 로직 ──────────────────────────────────────────

        /// <summary>
        /// 초기화 데이터(List) 또는 실시간 데이터(Payload)를 수신하여 버퍼를 갱신합니다.
        /// </summary>
        public override void UpdateData(object payload)
        {
            // 데이터 변경 시 렌더링 스레드와의 충돌을 방지하기 위해 lock 사용
            lock (_ohlcBuffer)
            {
                if (payload is List<CommonCandle> candles)
                {
                    // 1. 과거 캔들 데이터 초기화 및 로드
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
                    // 2. 실시간 체결 데이터 처리
                    PrevPrice = LastPrice;
                    LastPrice = rt.Price;

                    DateTime now = DateTime.Now;
                    // 분 단위로 캔들 생성 기준 설정
                    DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                    if (_ohlcBuffer.Count > 0 && _lastCandleTime == currentMinute)
                    {
                        // 기존 현재분 캔들 업데이트
                        var last = _ohlcBuffer.Last;
                        var updated = new OHLC(
                            last.Open,
                            Math.Max(last.High, rt.Price),
                            Math.Min(last.Low, rt.Price),
                            rt.Price,
                            currentMinute,
                            TimeSpan.FromMinutes(1)
                        );
                        _ohlcBuffer.UpdateLast(updated);

                        // 거래량 합산 업데이트
                        double currentVol = _volumeBuffer.Last;
                        _volumeBuffer.UpdateLast(currentVol + rt.Volume);
                    }
                    else
                    {
                        // 새로운 분봉 시작
                        _ohlcBuffer.Add(new OHLC(rt.Price, rt.Price, rt.Price, rt.Price, currentMinute, TimeSpan.FromMinutes(1)));
                        _volumeBuffer.Add(rt.Volume);
                        _lastCandleTime = currentMinute;
                    }
                }
            }
        }

        // ─── 렌더링 로직 ──────────────────────────────────────────

        /// <summary>
        /// ScottPlot 객체에 캔들스틱과 현재가 수평선을 추가합니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_ohlcBuffer)
            {
                if (_ohlcBuffer.Count == 0) return;

                // 1. 캔들스틱 차트 생성
                var ohlcs = _ohlcBuffer.ToList();
                var cp = plot.Add.Candlestick(ohlcs);
                cp.RisingColor = Colors.Red;
                cp.FallingColor = Colors.Blue;
                cp.Sequential = false; // 시간 축 기준 정렬

                // 인자로 전달된 Y축(가격축)에 할당
                cp.Axes.YAxis = targetAxis;

                // 2. 현재가 라인 표시
                if (LastPrice > 0)
                {
                    var hline = plot.Add.HorizontalLine(LastPrice);
                    hline.Axes.YAxis = targetAxis;

                    hline.LineStyle.Width = 1;
                    hline.LinePattern = LinePattern.Dashed;
                    hline.LineStyle.Color = Colors.Yellow.WithAlpha(0.6);

                    hline.Text = LastPrice.ToString("N0");
                    hline.LabelOppositeAxis = true; // 우측 축에 가격 표시
                    hline.LabelBackgroundColor = LastPrice >= PrevPrice ? Colors.Red : Colors.Blue;
                    hline.LabelFontColor = Colors.White;
                }
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

        /// <summary>
        /// 현재 보이는 X축 범위 내에서 최저가/최고가를 계산하여 Y축 오토스케일을 지원합니다.
        /// </summary>
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

        // ─── IVolumeDataProvider 구현 (알람 엔진 연동) ──────────────────────────────────

        /// <summary>
        /// 최근 N개의 캔들 평균 거래량을 반환합니다. (현재 진행 중인 캔들은 제외)
        /// </summary>
        public double GetAverageVolume(int lookbackCount)
        {
            lock (_ohlcBuffer)
            {
                if (_volumeBuffer.Count < 2) return 0;

                // 마지막 인덱스는 현재 생성 중인 봉이므로 제외
                int takeCount = Math.Min(lookbackCount, _volumeBuffer.Count - 1);
                var list = _volumeBuffer.ToList();

                return list.Take(list.Count - 1).TakeLast(takeCount).Average();
            }
        }

        /// <summary>
        /// 현재 실시간으로 쌓이고 있는 캔들의 거래량을 반환합니다.
        /// </summary>
        public double GetCurrentCandleVolume()
        {
            lock (_ohlcBuffer)
            {
                return _volumeBuffer.Count > 0 ? _volumeBuffer.Last : 0;
            }
        }
    }
}