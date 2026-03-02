using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    public class UpbitSimulateSeries : ChartSeriesBase // ChartSeriesBase 상속으로 변경
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.SimulatedAvgPriceLine;
        public override string Label => "물타기 시뮬레이션";
        public override bool DefaultOn => false;

        // 상단 가격 영역(80%)에 배정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        private double _simulatedPrice = 0;

        public override void UpdateData(object payload)
        {
            if (payload is double price)
            {
                _simulatedPrice = price;
            }
        }

        /// <summary>
        /// ⭐ 핵심: 매니저가 전달한 targetAxis를 사용하여 예상 평단가 선을 그립니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            // 선이 0이거나 가시성이 꺼져있으면 그리지 않음
            if (!IsVisible || _simulatedPrice <= 0) return;

            // 1. 수평선 추가
            var hline = plot.Add.HorizontalLine(_simulatedPrice);

            // ⭐ 중요: 전달받은 가격 전용 축에 할당
            hline.Axes.YAxis = targetAxis;

            // 스타일 설정
            hline.Color = Colors.LimeGreen.WithAlpha(0.8); // 시인성 좋은 초록색
            hline.LinePattern = LinePattern.Dashed;        // 점선 스타일
            hline.LineStyle.Width = 2;

            // 2. 우측 가격 라벨 설정
            hline.Text = $"예상 평단가: {_simulatedPrice:N1}";
            hline.LabelOppositeAxis = true;               // 우측 눈금에 표시
            hline.LabelBackgroundColor = Colors.LimeGreen;
            hline.LabelFontColor = Colors.White;
            hline.LabelFontSize = 12;
            //hline.LabelStyle.FontName = SystemFonts.DefaultFont.Name;
            //hline.LabelStyle.FontName = "Malgun Gothic";

            //hline.LabelBold = true;
            hline.TextAlignment = Alignment.MiddleLeft;
            hline.TextRotation = 0;
            hline.LabelStyle.OffsetX = 1; // 숫자가 클수록 오른쪽으로 밀립니다.
        }

        public override void Clear() => _simulatedPrice = 0;

        /// <summary>
        /// ⭐ 팁: 시뮬레이션 가격이 너무 높거나 낮으면 차트 축소의 원인이 됩니다.
        /// 자동 축 범위 계산(AutoScale)에서 이 선은 제외하도록 null을 반환합니다.
        /// </summary>
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}