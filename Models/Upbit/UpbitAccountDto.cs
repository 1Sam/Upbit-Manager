using System.Text.Json.Serialization;

namespace Upbit_Manager.Models.Upbit
{
    public class UpbitAccountDto
    {
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("balance")]
        public string BalanceString { get; set; } = "0";

        [JsonPropertyName("locked")]
        public string LockedString { get; set; } = "0";

        [JsonPropertyName("avg_buy_price")]
        public string AvgBuyPriceString { get; set; } = "0";

        [JsonPropertyName("unit_currency")]
        public string UnitCurrency { get; set; } = "KRW";

        // 서비스 계층에서 쓰기 편하게 더블 형변환 속성 추가
        public double Balance => double.TryParse(BalanceString, out var v) ? v : 0;
        public double Locked => double.TryParse(LockedString, out var v) ? v : 0;
        public double AvgBuyPrice => double.TryParse(AvgBuyPriceString, out var v) ? v : 0;
    }
}