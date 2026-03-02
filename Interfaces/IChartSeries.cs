using ScottPlot;
using Upbit_Manager.Models.Common; // 기존에 정의된 AxisGroup, ExchangeSource, SeriesType을 사용
using Upbit_Manager.Interfaces;

namespace Upbit_Manager.Interfaces
{
    /// <summary>
    /// 차트에 그려질 모든 시리즈(캔들, 선, 지표 등)의 공통 규격입니다.
    /// </summary>
    public interface IChartSeries
    {
        ExchangeSource Source { get; }
        SeriesType Type { get; }

        // Models.Common에 정의된 AxisGroup을 사용합니다.
        AxisGroup TargetGroup { get; }

        string Label { get; }
        bool DefaultOn { get; }
        bool IsVisible { get; set; }

        /// <summary>
        /// 데이터를 수신하여 내부 버퍼를 업데이트합니다.
        /// </summary>
        void UpdateData(object payload);

        /// <summary>
        /// ScottPlot 객체에 실제 그림을 그립니다.
        /// 매니저가 지정해준 축(targetAxis)에 그려야 레이아웃이 깨지지 않습니다.
        /// </summary>
        void Render(Plot plot, IYAxis targetAxis);

        /// <summary>
        /// 종목 변경 시 기존 데이터를 초기화합니다.
        /// </summary>
        void Clear();

        /// <summary>
        /// 현재 화면 범위 내의 최저/최고가를 반환하여 Y축 오토스케일을 지원합니다.
        /// </summary>
        (double Min, double Max)? GetPriceRange(double minOA, double maxOA);
    }
}