using System;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 미체결 예약주문 수평선 시리즈.
    /// 동일 가격의 주문들을 그룹화하여 레이블이 겹치지 않게 렌더링합니다.
    /// </summary>
    public class UpbitOpenOrderSeries : ChartSeriesBase
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.OpenOrder;
        public override string Label => "Upbit - 예약주문";
        public override bool DefaultOn => false;

        // 상단 가격 영역(AxisGroup.Price)에 그려지도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        private List<UpbitOpenOrder> _orders = new List<UpbitOpenOrder>();

        // 2줄 레이블 사용 시 가로 폭이 줄어들므로 적절한 오프셋 간격 설정 (픽셀 단위)
        private const float LABEL_SPACING_WIDTH = 30f;

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
        /// 매니저가 배정해준 targetAxis를 사용하여 동일 가격 주문들을 중첩 없이 렌더링합니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible || _orders == null || !_orders.Any()) return;

            // 1. 가격(Price)과 매매 방향(Side)을 기준으로 주문을 그룹화합니다.
            // 같은 가격에 여러 주문이 있을 경우 옆으로 나열하기 위함입니다.
            var groupedOrders = _orders
                .GroupBy(o => new { o.Price, IsAsk = o.Side.ToLower().Contains("ask") });

            foreach (var group in groupedOrders)
            {
                double price = group.Key.Price;
                bool isAsk = group.Key.IsAsk;

                int orderIndex = 0;

                foreach (var order in group)
                {
                    // 2. 색상 설정 (선은 조금 더 투명하게 하여 겹쳐도 부담 없게 설정)
                    var lineColor = isAsk ? Colors.Blue.WithAlpha(0.3) : Colors.Red.WithAlpha(0.3);
                    var boxColor = isAsk ? Colors.Blue : Colors.Red;

                    // 3. 수평선 추가 및 축 설정
                    var hline = plot.Add.HorizontalLine(price);
                    hline.Axes.YAxis = targetAxis;

                    // 4. 선 스타일 설정
                    hline.LineWidth = 1;
                    hline.LinePattern = LinePattern.Dashed;
                    hline.Color = lineColor;

                    // 5. 레이블 텍스트 구성 (\n을 사용하여 가격과 수량을 2줄로 표시)
                    hline.LabelText = $"{order.Price:N0}\n{order.Volume:N1}";
                    hline.LabelBackgroundColor = boxColor;
                    hline.LabelFontColor = Colors.White;
                    hline.LabelFontSize = 9; // 2줄 레이블은 9~10pt가 적당합니다.
                    hline.LabelBold = true;
                    hline.LabelBorderRadius = 3;

                    // 상하 여백을 줄여 레이블 박스 높이를 최적화 (좌, 우, 하, 상)
                    hline.LabelPixelPadding = new PixelPadding(5, 5, 1, 2);
                    hline.TextRotation = 0;

                    // 6. 레이블 겹침 방지 및 좌우 배치 로직
                    if (isAsk)
                    {
                        // [매도] 차트 내부(왼쪽) 방향으로 나열
                        hline.TextAlignment = Alignment.MiddleRight;
                        hline.LabelOppositeAxis = false;

                        // 인덱스에 따라 왼쪽으로 밀어냄 (-1, -76, -151...)
                        hline.LabelStyle.OffsetX = -1 - (orderIndex * LABEL_SPACING_WIDTH);
                    }
                    else
                    {
                        // [매수] 차트 바깥쪽(오른쪽 Y축 영역)으로 나열
                        hline.TextAlignment = Alignment.MiddleLeft;
                        hline.LabelOppositeAxis = true;

                        // 인덱스에 따라 오른쪽으로 밀어냄 (1, 76, 151...)
                        hline.LabelStyle.OffsetX = 1 + (orderIndex * LABEL_SPACING_WIDTH);
                    }

                    hline.ExcludeFromLegend = true;

                    // 다음 주문의 레이블 위치 이동을 위해 인덱스 증가
                    orderIndex++;
                }
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