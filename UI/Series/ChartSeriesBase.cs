// UI/Series/ChartSeriesBase.cs

using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// IChartSeries 공통 구현 기반 클래스.
    /// ToString()만 고정하고 나머지는 각 구현체에서 자유롭게 정의합니다.
    /// </summary>
    public abstract class ChartSeriesBase : IChartSeries
    {
        // ↓ abstract 제거 → 각 구현체가 인터페이스를 통해 직접 구현
        public abstract ExchangeSource Source { get; }
        public abstract SeriesType Type { get; }
        public abstract string Label { get; }
        public abstract bool DefaultOn { get; }
        public bool IsVisible { get; set; }

        public abstract void UpdateData(object payload);
        public abstract void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot);
        public abstract void Clear();
        public abstract (double Min, double Max)? GetPriceRange(double minOA, double maxOA);

        /// <summary>CheckedListBox 표시용 - Label을 반환</summary>
        public override string ToString() => Label;
    }
}