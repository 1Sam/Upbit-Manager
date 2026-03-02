using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    public class UpbitAvgPriceSeries : ChartSeriesBase // 1. 베이스 클래스 상속으로 변경
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.AvgPriceLine;
        public override string Label => "평단가 (Avg Price)";
        public override bool DefaultOn => true;

        // 상단 가격 영역(80%) 배정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        private double _avgPrice = 0;
        private double _profitRate = 0;

        public override void UpdateData(object payload)
        {
            // 튜플로 (평단가, 수익률)을 받거나, 평단가 단일값 처리
            if (payload is (double avg, double profit))
            {
                _avgPrice = avg;
                _profitRate = profit;
            }
            else if (payload is double avgOnly)
            {
                _avgPrice = avgOnly;
            }
        }

        /// <summary>
        /// ⭐ 핵심: 매니저가 전달한 targetAxis를 사용하여 평단가 선을 그립니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible || _avgPrice <= 0) return;

            // 2. 수평선 추가
            var hline = plot.Add.HorizontalLine(_avgPrice);

            // ⭐ 중요: 전달받은 가격 전용 축에 할당
            hline.Axes.YAxis = targetAxis;

            // 스타일 설정
            hline.LineWidth = 1.5f;
            hline.LinePattern = LinePattern.Dashed;

            // 수익률에 따른 색상 변경 (수익: 빨강, 손실: 파랑)
            hline.Color = _profitRate > 0 ? Colors.Red.WithAlpha(0.6) :
                         _profitRate < 0 ? Colors.Blue.WithAlpha(0.6) :
                         Colors.DeepSkyBlue;

            // 우측 라벨 설정
            hline.Text = $"평단 {_avgPrice:N1} ({_profitRate:F1}%)";
            hline.LabelOppositeAxis = true;
            hline.LabelBackgroundColor = hline.Color;
            hline.LabelFontColor = Colors.White;
            hline.TextAlignment = Alignment.MiddleLeft;
            hline.TextRotation = 0;
            hline.LabelStyle.OffsetX = 2;
            //hline.LabelBorderRadius = 3;
            //hline.LabelPixelPadding = new PixelPadding(3, 3, 0, 2);//좌우하상


        }

        public override void Clear()
        {
            _avgPrice = 0;
            _profitRate = 0;
        }

        // 평단가가 차트 밖 멀리 있을 때 캔들이 축소되는 것을 방지하기 위해 null 반환
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}