using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 미체결 예약주문 수평선 시리즈.
    /// </summary>
    public class UpbitOpenOrderSeries : ChartSeriesBase // 1. 베이스 클래스 상속
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.OpenOrder;
        public override string Label => "Upbit - 예약주문";
        public override bool DefaultOn => false;

        // ⭐ 상단 가격 영역(80%)에 그려지도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        private List<UpbitOpenOrder> _orders = new List<UpbitOpenOrder>();

        public override void UpdateData(object payload)
        {
            // 리스트 형식이거나, 단일 객체일 경우를 대비해 유연하게 처리
            if (payload is List<UpbitOpenOrder> orders)
            {
                _orders = orders;
            }
            else if (payload is UpbitOpenOrder singleOrder)
            {
                _orders.Add(singleOrder);
            }
        }

        /// <summary>
        /// ⭐ 핵심: 매니저가 배정해준 targetAxis를 사용하여 렌더링합니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible || _orders == null) return;

            foreach (var order in _orders)
            {
                bool isAsk = order.Side.ToLower().Contains("ask");

                // 1. 색상 설정
                var lineColor = isAsk ? Colors.Blue.WithAlpha(0.5) : Colors.Red.WithAlpha(0.5);
                var boxColor = isAsk ? Colors.Blue : Colors.Red;

                // 2. 수평선 추가
                var hline = plot.Add.HorizontalLine(order.Price);
                hline.Axes.YAxis = targetAxis;

                // 3. 선 스타일 (ScottPlot 5 규격)
                hline.LineWidth = 1;
                hline.LinePattern = LinePattern.Dashed;
                hline.Color = lineColor;

                // 4. 레이블 텍스트 및 디자인
                hline.LabelText = $"{order.Price:N0} ({order.Volume:N0})";
                hline.LabelBackgroundColor = boxColor;
                hline.LabelFontColor = Colors.White;
                hline.LabelFontSize = 10;
                hline.LabelBold = true;
                
                hline.TextRotation = 0;
                hline.LabelBorderRadius = 3;
                hline.LabelPixelPadding = new PixelPadding(3, 3, 0, 2);//좌우하상

                // 2. 우측 Y축 레이블 영역에 표시하도록 설정
                hline.LabelOppositeAxis = true;

                // 5. ⭐ 좌우 분할 핵심 로직
                // LabelOppositeAxis를 true로 설정하면 기본 축의 반대편(오른쪽)에 그려집니다.
                if (isAsk)
                {
                    // 매도는 왼쪽(차트 내부 방향)에 배치하기 위해 정렬 조정
                    hline.TextAlignment = Alignment.MiddleRight;
                    hline.LabelOppositeAxis = false;
                    hline.LabelStyle.OffsetX = -1; // 숫자가 클수록 오른쪽으로 밀립니다.

                }
                else
                {
                    // 매수는 오른쪽(차트 바깥쪽 방향)에 배치
                    hline.TextAlignment = Alignment.MiddleLeft;
                    hline.LabelOppositeAxis = true;
                    hline.LabelStyle.OffsetX = 1; // 숫자가 클수록 오른쪽으로 밀립니다.

                }

                hline.ExcludeFromLegend = true;
            }
        }

        public override void Clear()
        {
            _orders.Clear();
        }

        // 주문선은 현재가와 멀리 떨어져 있을 수 있으므로 자동 범위 계산에서는 제외합니다.
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}