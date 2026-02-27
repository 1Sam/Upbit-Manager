// Models/Common/ChartOverlay.cs

namespace Upbit_Manager.Models.Common
{
    public enum ExchangeSource
    {
        Upbit,
        Binance,
    }

    public enum SeriesType
    {
        [System.ComponentModel.Description("캔들")]
        Candle,

        [System.ComponentModel.Description("거래량")]
        Volume,

        [System.ComponentModel.Description("예약주문")]
        OpenOrder,

        [System.ComponentModel.Description("RSI")]
        RSI,

        [System.ComponentModel.Description("가격선")]
        PriceLine,
    }

    public class ChartSeriesItem
    {
        public ExchangeSource Source { get; init; }
        public SeriesType Type { get; init; }

        public override string ToString() => $"{Source} - {Type.ToLabel()}";
    }

    public static class EnumExtensions
    {
        public static string ToLabel<T>(this T value) where T : Enum
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttributes(
                typeof(System.ComponentModel.DescriptionAttribute), false)
                as System.ComponentModel.DescriptionAttribute[];

            return attr?.Length > 0 ? attr[0].Description : value.ToString();
        }
    }
}