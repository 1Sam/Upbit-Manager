using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 모든 차트 시리즈의 공통 기능을 정의하는 추상 베이스 클래스.
    /// ScottPlot 5의 멀티 패널 레이아웃 규격에 맞춰 수정되었습니다.
    /// </summary>
    public abstract class ChartSeriesBase : IChartSeries
    {
        // --- [자식 클래스에서 반드시 구현해야 할 추상 속성] ---
        public abstract ExchangeSource Source { get; }
        public abstract SeriesType Type { get; }
        public abstract string Label { get; }
        public abstract bool DefaultOn { get; }

        /// <summary>
        /// 이 시리즈가 어느 영역(상단 가격/하단 거래량 등)에 그려질지 정의합니다.
        /// </summary>
        public abstract AxisGroup TargetGroup { get; }

        // --- [공통 상태 관리] ---
        public bool IsVisible { get; set; }

        protected ChartSeriesBase()
        {
            // 초기 가시성은 기본값 설정을 따름 (생성 시점에서 처리)
            IsVisible = false;
        }

        // --- [필수 구현 메서드 (Abstract)] ---

        /// <summary>데이터를 업데이트합니다. payload의 형식은 시리즈마다 다를 수 있습니다.</summary>
        public abstract void UpdateData(object payload);

        /// <summary>
        /// ⭐ 핵심: ScottPlot 5 규격에 맞는 렌더링 메서드.
        /// ChartManager가 호출할 때 '현재 도화지'와 '배정된 축'을 넘겨줍니다.
        /// </summary>
        public abstract void Render(Plot plot, IYAxis targetAxis);

        /// <summary>현재 화면 범위 내에서 Y축의 최소/최대값을 계산하여 자동 축 조절에 기여합니다.</summary>
        public abstract (double Min, double Max)? GetPriceRange(double minOA, double maxOA);

        // --- [가상 메서드 (Virtual - 필요 시 오버라이드)] ---

        /// <summary>내부 버퍼나 데이터를 초기화합니다.</summary>
        public virtual void Clear() { }

        /// <summary>CheckedListBox 등 UI 표시용 이름</summary>
        public override string ToString() => Label;
    }
}