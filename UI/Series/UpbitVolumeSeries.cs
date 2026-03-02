using System;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.Core.Collections;

namespace Upbit_Manager.UI.Series
{
    public class UpbitVolumeSeries : ChartSeriesBase
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Volume;

        // ⭐ 하단 거래량 패널(25%)에 그려지도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Volume;
        public override string Label => "Upbit - 거래량";
        public override bool DefaultOn => true;

        private const int MAX_BUFFER = 1200;
        private readonly RingBuffer<double> _buyBuffer = new RingBuffer<double>(MAX_BUFFER);
        private readonly RingBuffer<double> _sellBuffer = new RingBuffer<double>(MAX_BUFFER);
        private readonly RingBuffer<DateTime> _timeBuffer = new RingBuffer<DateTime>(MAX_BUFFER);

        public override void UpdateData(object payload)
        {
            // 1. 초기 캔들 데이터 대량 삽입
            if (payload is List<CommonCandle> candles)
            {
                _buyBuffer.Clear(); _sellBuffer.Clear(); _timeBuffer.Clear();
                foreach (var c in candles)
                {
                    double half = c.Volume / 2.0;
                    _buyBuffer.Add(half); _sellBuffer.Add(half); _timeBuffer.Add(c.Time);
                }
            }
            // 2. 실시간 틱 데이터 업데이트
            else if (payload is UpbitRealtimePayload rt)
            {
                var now = DateTime.Now;
                // 1분(60초) 기준 새로운 거래량 바 생성
                if (_timeBuffer.Count == 0 || (now - _timeBuffer[_timeBuffer.Count - 1]).TotalSeconds >= 60)
                {
                    _timeBuffer.Add(now);
                    if (rt.Side == "BID") { _buyBuffer.Add(rt.Volume); _sellBuffer.Add(0); }
                    else { _sellBuffer.Add(rt.Volume); _buyBuffer.Add(0); }
                }
                else
                {
                    // 현재 바에 거래량 합산
                    if (rt.Side == "BID") _buyBuffer.ReplaceLast(_buyBuffer[_buyBuffer.Count - 1] + rt.Volume);
                    else _sellBuffer.ReplaceLast(_sellBuffer[_sellBuffer.Count - 1] + rt.Volume);
                }
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            var times = _timeBuffer.ToArray();
            var buys = _buyBuffer.ToArray();
            var sells = _sellBuffer.ToArray();
            if (times.Length == 0) return;

            // 바 너비 설정 (약 1분 간격에 맞춤)
            double barWidth = 0.0003;

            for (int i = 0; i < times.Length; i++)
            {
                double x = times[i].ToOADate();
                double total = buys[i] + sells[i];
                if (total <= 0) continue;

                // 1. 매도 거래량(Blue) 하단 배치
                var rectSell = plot.Add.Rectangle(x - barWidth, x + barWidth, 0, sells[i]);
                rectSell.Axes.YAxis = targetAxis; // 매니저가 전달한 하단 Y축 사용
                rectSell.FillStyle.Color = Colors.Blue.WithAlpha(0.5);
                rectSell.LineStyle.Width = 0;

                // 2. 매수 거래량(Red) 매도 위에 누적(Stacked)
                if (buys[i] > 0)
                {
                    var rectBuy = plot.Add.Rectangle(x - barWidth, x + barWidth, sells[i], total);
                    rectBuy.Axes.YAxis = targetAxis; // 매니저가 전달한 하단 Y축 사용
                    rectBuy.FillStyle.Color = Colors.Red.WithAlpha(0.5);
                    rectBuy.LineStyle.Width = 0;
                }
            }
        }

        public override void Clear()
        {
            _buyBuffer.Clear();
            _sellBuffer.Clear();
            _timeBuffer.Clear();
        }

        // 거래량 데이터는 가격 차트의 Y축 범위 계산에 영향을 주지 않음
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}