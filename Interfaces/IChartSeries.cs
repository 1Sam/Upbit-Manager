using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Interfaces
{

    public enum AxisGroup { Price, Volume, Indicator }

    public interface IChartSeries
    {
        ExchangeSource Source { get; }
        SeriesType Type { get; }
        AxisGroup TargetGroup { get; }
        string Label { get; }
        bool DefaultOn { get; }
        bool IsVisible { get; set; }

        void UpdateData(object payload);

        // 매니저가 전용 축(targetAxis)을 직접 배달하도록 설계
        void Render(Plot plot, IYAxis targetAxis);

        void Clear();
        (double Min, double Max)? GetPriceRange(double minOA, double maxOA);
    }
}