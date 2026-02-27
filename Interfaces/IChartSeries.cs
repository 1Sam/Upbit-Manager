// UI/Series/IChartSeries.cs

using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 차트에 그려지는 모든 시리즈의 공통 규격.
    /// 새 시리즈를 추가할 때 이 인터페이스를 구현하고
    /// ChartManager._seriesList에 등록만 하면 됩니다.
    /// </summary>
    public interface IChartSeries
    {
        /// <summary>데이터 출처 거래소</summary>
        ExchangeSource Source { get; }

        /// <summary>시리즈 종류 (캔들, 거래량 등)</summary>
        SeriesType Type { get; }

        /// <summary>체크리스트에 표시될 레이블</summary>
        string Label { get; }

        /// <summary>체크리스트 초기 체크 여부</summary>
        bool DefaultOn { get; }

        /// <summary>현재 표시 여부 (체크박스 상태와 연동)</summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// 실시간/초기 데이터를 시리즈 내부 버퍼에 반영합니다.
        /// payload 타입은 각 구현체에서 정의합니다.
        /// </summary>
        void UpdateData(object payload);

        /// <summary>
        /// 내부 버퍼를 기반으로 플롯에 실제로 그립니다.
        /// candlePlot: 가격/지표용 상단 플롯
        /// volumePlot: 거래량용 하단 플롯
        /// </summary>
        void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot);

        /// <summary>종목 변경 등으로 버퍼를 전부 초기화할 때 호출</summary>
        void Clear();

        /// <summary>
        /// Y축 자동 범위 계산에 포함될 가격 범위를 반환합니다.
        /// 현재 화면의 X축 범위(minOA ~ maxOA)에 해당하는 데이터만 반환하세요.
        /// 이 시리즈가 Y축에 영향을 주지 않는다면 null을 반환하세요.
        /// </summary>
        (double Min, double Max)? GetPriceRange(double minOA, double maxOA);

        // ↓ 이것만 추가
        string ToString() => Label;
    }
}