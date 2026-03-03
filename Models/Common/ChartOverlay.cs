// Models/Common/ChartOverlay.cs

namespace Upbit_Manager.Models.Common
{
    public enum ExchangeSource
    {
        Upbit,
        Binance,
    }

    public enum AxisGroup
    {
        Price,   // 상단 가격 영역
        Volume,  // 하단 거래량 영역
        Indicator, // (선택 사항) 보조 지표 영역
        Orderbook
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

        [System.ComponentModel.Description("평단가선")]
        AvgPriceLine,

        [System.ComponentModel.Description("가상평단가선")]
        SimulatedAvgPriceLine,

        [System.ComponentModel.Description("볼륨알람선")]
        VolumeLimit,

        [System.ComponentModel.Description("Orderbook")]
        Orderbook,

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