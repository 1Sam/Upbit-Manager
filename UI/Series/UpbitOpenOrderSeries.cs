// UI/Series/UpbitOpenOrderSeries.cs

using ScottPlot;
using Upbit_Manager.Models.Common;
using UpbitManager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 미체결 예약주문 수평선 시리즈.
    /// payload: List&lt;UpbitOpenOrder&gt;
    /// </summary>
    public class UpbitOpenOrderSeries : IChartSeries
    {
        public ExchangeSource Source => ExchangeSource.Upbit;
        public SeriesType Type => SeriesType.OpenOrder;
        public string Label => "Upbit - 예약주문";
        public override string ToString() => Label;  // "Upbit - 예약주문"
        public bool DefaultOn => false;
        public bool IsVisible { get; set; }

        private List<UpbitOpenOrder> _orders = new();

        public void UpdateData(object payload)
        {
            if (payload is List<UpbitOpenOrder> orders)
                _orders = orders ?? new();
        }

        public void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot)
        {
            foreach (var order in _orders)
            {
                bool isAsk = order.Side.ToLower().Contains("ask");
                var color = isAsk
                    ? Colors.Blue.WithAlpha(0.6)
                    : Colors.Red.WithAlpha(0.6);

                var line = candlePlot.Add.HorizontalLine(order.Price);
                line.LineStyle.Width = 1;
                line.LinePattern = LinePattern.Dashed;
                line.Color = color;
                line.Text = $"{order.Price:N0} ({order.Volume:N0})";
                line.LabelRotation = 0;
                line.LabelBackgroundColor = color;

                if (isAsk)
                {
                    line.LabelOppositeAxis = false;
                    line.LabelAlignment = Alignment.MiddleRight;
                }
                else
                {
                    line.LabelOppositeAxis = true;
                    line.LabelAlignment = Alignment.MiddleLeft;
                }
            }
        }

        public void Clear() => _orders.Clear();

        // 주문선은 Y축 자동범위에 포함하지 않음 (가격이 현재가와 멀 수 있음)
        public (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}