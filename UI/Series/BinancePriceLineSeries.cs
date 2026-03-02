using ScottPlot;
using Upbit_Manager.Models.Common;
using static System.Windows.Forms.LinkLabel;
using Upbit_Manager.Interfaces;

namespace Upbit_Manager.UI.Series
{
    public class BinancePriceLineSeries : ChartSeriesBase
    {
        public override ExchangeSource Source => ExchangeSource.Binance;
        public override SeriesType Type => SeriesType.PriceLine;
        public override AxisGroup TargetGroup => AxisGroup.Price;
        public override string Label => "Binance - 실시간";
        public override bool DefaultOn => false;


        // ⭐ 이 필드가 빠져서 에러가 났던 겁니다!
        private double _lastUpbitPrice = 0;

        private const int MAX_BUFFER = 2000;
        private readonly List<(double TimeOA, double Price)> _buffer = new();

        public override void UpdateData(object payload)
        {
            lock (_buffer)
            {
                // 1. [신규] 업비트 실시간 가격 수신 (MainController에서 튜플로 쏜 경우)
                if (payload is ValueTuple<double, ExchangeSource> data && data.Item2 == ExchangeSource.Upbit)
                {
                    _lastUpbitPrice = data.Item1;
                    return; // 버퍼에 쌓지 않고 가격만 저장 후 종료
                }

                // 2. 바이낸스 실시간 가격 수신 (double 단일 값)
                if (payload is double binancePrice)
                {
                    if (_buffer.Count > 0 && _buffer.Last().Price == binancePrice) return;
                    _buffer.Add((DateTime.Now.ToOADate(), binancePrice));
                }
                // 3. 바이낸스 과거 데이터(History) 수신
                else if (payload is IEnumerable<(DateTime Time, double Price)> history)
                {
                    _buffer.AddRange(history.Select(h => (h.Time.ToOADate(), h.Price)));
                }

                // 4. 버퍼 사이즈 제한 로직 (기존 로직 유지)
                if (_buffer.Count > MAX_BUFFER)
                {
                    var lastData = _buffer.OrderBy(x => x.TimeOA).TakeLast(MAX_BUFFER).ToList();
                    _buffer.Clear();
                    _buffer.AddRange(lastData);
                }
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible) return;

            double lastBinancePrice = 0;
            double lastTimeOA = 0;
            double[] xs, ys;

            lock (_buffer)
            {
                if (_buffer.Count < 1) return;

                var displayList = _buffer.ToList();
                lastTimeOA = DateTime.Now.ToOADate();
                lastBinancePrice = displayList.Last().Price;
                displayList.Add((lastTimeOA, lastBinancePrice));

                xs = displayList.Select(b => b.TimeOA).ToArray();
                ys = displayList.Select(b => b.Price).ToArray();
            }

            // 1. 김프 계산 및 색상 결정
            ScottPlot.Color statusColor = Colors.Orange; // 기본값 (1% 미만)
            string kimpText = "0.00%";

            if (_lastUpbitPrice > 0 && lastBinancePrice > 0)
            {
                double kimp = ((_lastUpbitPrice / lastBinancePrice) - 1) * 100;
                kimpText = $"{kimp:+0.00;-0.00;0.00}%";

                // ⭐ 요청하신 조건별 색상 로직
                if (kimp >= 4.0) statusColor = Colors.Red;        // 4% 이상 빨강
                else if (kimp >= 3.0) statusColor = Colors.Green;  // 3% 이상 녹색
                else if (kimp >= 2.0) statusColor = Colors.Yellow; // 2% 이상 노랑
                else if (kimp >= 1.0) statusColor = Colors.Orange; // 1% 이상 오렌지
                else statusColor = Colors.Orange.WithAlpha(0.6);   // 1% 미만 흐린 오렌지
            }

            // 2. 바이낸스 지렁이 선 (결정된 색상 적용)
            var scatter = plot.Add.ScatterLine(xs, ys);
            scatter.Color = statusColor;
            scatter.LineWidth = 2;
            scatter.Axes.YAxis = targetAxis;

            // 3. 김프 레이블 (결정된 색상 적용)
            if (_lastUpbitPrice > 0 && lastBinancePrice > 0)
            {
                var txt = plot.Add.Text(kimpText, lastTimeOA, lastBinancePrice);

                txt.LabelFontName = "Malgun Gothic";
                txt.LabelFontSize = 10;
                txt.LabelFontColor = (statusColor == Colors.Yellow) ? Colors.Black : Colors.White; // 노랑 배경엔 검은 글씨가 잘 보임
                txt.LabelBackgroundColor = statusColor;
                txt.LabelPadding = 2;
                txt.LabelBorderRadius = 3;
                txt.LabelAlignment = Alignment.MiddleLeft;
                txt.Axes.YAxis = targetAxis;
            }
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            lock (_buffer)
            {
                var visible = _buffer.Where(b => b.TimeOA >= minOA && b.TimeOA <= maxOA).Select(b => b.Price).ToList();
                return visible.Any() ? (visible.Min(), visible.Max()) : null;
            }
        }

        public override void Clear() { lock (_buffer) _buffer.Clear(); }
    }
}