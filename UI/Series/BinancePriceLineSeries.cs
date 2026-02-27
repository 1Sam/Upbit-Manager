// UI/Series/BinancePriceLineSeries.cs

using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 바이낸스 KRW 환산 가격선 시리즈.
    /// payload 종류:
    ///   - double              : 실시간 가격 1건
    ///   - List&lt;(DateTime, double)&gt; : 초기 히스토리 일괄 로드
    /// </summary>
    public class BinancePriceLineSeries : IChartSeries
    {
        public ExchangeSource Source => ExchangeSource.Binance;
        public SeriesType Type => SeriesType.PriceLine;
        public string Label => "Binance - 가격선";
        public override string ToString() => Label;  // "Binance - 가격선"
        public bool DefaultOn => false;
        public bool IsVisible { get; set; }

        private const int MAX_BUFFER = 3000;
        private readonly RingBuffer<(DateTime Time, double Price)> _buffer = new RingBuffer<(DateTime, double)>(MAX_BUFFER);
        private object? _linePlottable;

        public void UpdateData(object payload)
        {
            if (payload is double price)
            {
                _buffer.Add((DateTime.Now, price));
            }
            else if (payload is List<(DateTime Time, double Price)> history)
            {
                // 히스토리 병합 (중복 시간 제거, 시간순 정렬)
                var combined = _buffer
                    .Concat(history)
                    .GroupBy(x => x.Time)
                    .Select(g => g.First())
                    .OrderBy(x => x.Time)
                    .TakeLast(MAX_BUFFER)
                    .ToList();

                _buffer.Clear();
                foreach (var it in combined) _buffer.Add(it);
            }
        }

        public void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot)
        {
            if (_buffer.Count < 2) return;

            // 시간순 정렬 후 산포도(선형)으로 렌더링
            var arr = _buffer.ToArray();
            var sorted = arr.OrderBy(x => x.Time).ToArray();
            double[] xs = sorted.Select(b => b.Time.ToOADate()).ToArray();
            double[] ys = sorted.Select(b => b.Price).ToArray();

            if (_linePlottable == null)
            {
                _linePlottable = candlePlot.Add.Scatter(xs, ys);
                dynamic d = _linePlottable;
                d.Color = Colors.Orange.WithAlpha(0.8);
                d.LineWidth = 2;
                d.MarkerSize = 0;
                d.Label = "Binance (KRW)";
            }
            else
            {
                try
                {
                    dynamic d = _linePlottable;
                    d.Update(xs, ys);
                }
                catch
                {
                    // fallback: recreate
                    _linePlottable = candlePlot.Add.Scatter(xs, ys);
                    dynamic d = _linePlottable;
                    d.Color = Colors.Orange.WithAlpha(0.8);
                    d.LineWidth = 2;
                    d.MarkerSize = 0;
                    d.Label = "Binance (KRW)";
                }
            }
        }

        public void Clear() => _buffer.Clear();

        public (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            var visible = _buffer
                .Where(b => b.Time.ToOADate() >= minOA)
                .ToList();

            if (!visible.Any()) return null;
            return (visible.Min(b => b.Price), visible.Max(b => b.Price));
        }
    }
}