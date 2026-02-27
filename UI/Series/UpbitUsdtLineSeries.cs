using System;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 KRW-USDT 실시간 가격선 시리즈 (ScottPlot 5 최신 버전 대응)
    /// </summary>
    public class UpbitUsdtLineSeries : IChartSeries
    {
        public ExchangeSource Source => ExchangeSource.Upbit;
        public SeriesType Type => SeriesType.PriceLine;
        public string Label => "Upbit - KRW-USDT";
        public override string ToString() => Label;
        public bool DefaultOn => true;
        public bool IsVisible { get; set; } = true;

        private const int MAX_BUFFER = 3000;
        private readonly List<(DateTime Time, double Price)> _buffer = new(MAX_BUFFER);

        // ScottPlot 5.x 플로터
        private ScottPlot.Plottables.Scatter? _scatterPlot;

        public void UpdateData(object payload)
        {
            lock (_buffer)
            {
                if (payload is double price)
                {
                    _buffer.Add((DateTime.Now, price));
                }
                else if (payload is List<(DateTime Time, double Price)> history)
                {
                    _buffer.AddRange(history);
                }

                if (_buffer.Count > MAX_BUFFER)
                {
                    _buffer.RemoveRange(0, _buffer.Count - MAX_BUFFER);
                }
            }
        }

        public void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot)
        {
            // 1. 체크박스 꺼져있으면 우측 축 숨기고 제거
            if (!IsVisible)
            {
                if (_scatterPlot != null)
                {
                    candlePlot.Remove(_scatterPlot);
                    _scatterPlot = null;
                    candlePlot.Axes.Right.IsVisible = false;
                }
                return;
            }

            double[] xs;
            double[] ys;

            lock (_buffer)
            {
                if (_buffer.Count < 2) return;
                var sorted = _buffer.OrderBy(x => x.Time).ToList();
                xs = sorted.Select(x => x.Time.ToOADate()).ToArray();
                ys = sorted.Select(x => x.Price).ToArray();
            }

            // 2. Scatter 플롯 생성 및 갱신
            // ScottPlot 5에서는 매번 생성하여 추가하는 것이 가장 안정적입니다 (Clear() 이후 호출됨)
            _scatterPlot = candlePlot.Add.Scatter(xs, ys);
            _scatterPlot.Axes.YAxis = candlePlot.Axes.Right; // 우측 축 사용
            _scatterPlot.Color = Colors.Gold.WithAlpha(0.8);
            _scatterPlot.LineWidth = 2;
            _scatterPlot.MarkerSize = 0;

            // 3. 우측 Y축 범위 강제 고정 (요구사항 반영)
            // 에이다 420원 위치에 USDT 1400원이 오도록 좌측 축의 범위를 참조하여 설정
            double rightCenter = 1400;
            var leftRange = candlePlot.Axes.Left.Range;
            double leftSpan = leftRange.Span;

            // 좌측 가격 폭이 너무 작을 경우를 대비한 방어 코드
            if (leftSpan <= 0) leftSpan = 100;

            // 좌측과 동일한 '보여지는 가격 폭'을 적용하여 중앙 정렬
            candlePlot.Axes.Right.Range.Set(rightCenter - (leftSpan / 2), rightCenter + (leftSpan / 2));

            // 4. 우측 축 라벨 및 스타일 설정
            candlePlot.Axes.Right.Label.Text = "USDT (KRW)";
            candlePlot.Axes.Right.Label.ForeColor = Colors.Gold;
            candlePlot.Axes.Right.IsVisible = true;

            // 눈금 포맷팅 (숫자가 반복되지 않게 자동 설정)
            candlePlot.Axes.Right.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        }

        public void Clear()
        {
            lock (_buffer)
            {
                _buffer.Clear();
            }
            _scatterPlot = null;
        }

        public (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            // USDT 선은 우측 별도 축을 쓰므로 전체 Y축 자동조절(Left)에 영향을 주지 않도록 null 반환
            // 만약 Left 축 범위에 USDT 가격을 포함시키고 싶다면 아래 주석을 해제하세요.
            return null;

            /*
            lock (_buffer)
            {
                var vis = _buffer.Where(x => {
                    double oa = x.Time.ToOADate();
                    return oa >= minOA && oa <= maxOA;
                }).ToList();

                if (!vis.Any()) return null;
                return (vis.Min(v => v.Price), vis.Max(v => v.Price));
            }
            */
        }
    }
}