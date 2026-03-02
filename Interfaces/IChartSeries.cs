using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Interfaces
{
    /// <summary>
    /// 차트에 그려질 모든 시리즈(캔들, 선, 지표 등)의 공통 규격입니다.
    /// </summary>
    public interface IChartSeries
    {
        /// <summary>
        /// 데이터의 출처 (Upbit, Binance 등)
        /// </summary>
        ExchangeSource Source { get; }

        /// <summary>
        /// 시리즈의 종류 (Candle, Volume, PriceLine 등)
        /// </summary>
        SeriesType Type { get; }

        /// <summary>
        /// 차트 내에서 어느 축(영역)에 그려질지를 결정하는 그룹입니다.
        /// Models.Common.AxisGroup (Price, Volume 등)을 사용합니다.
        /// </summary>
        AxisGroup TargetGroup { get; }

        /// <summary>
        /// UI(체크박스 리스트 등)에 표시될 명칭
        /// </summary>
        string Label { get; }

        /// <summary>
        /// 프로그램 시작 시 기본적으로 체크(활성화)되어 있을지 여부
        /// </summary>
        bool DefaultOn { get; }

        /// <summary>
        /// 현재 차트상에 표시되고 있는지 여부
        /// </summary>
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