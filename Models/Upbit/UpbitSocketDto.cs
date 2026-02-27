using System.Text.Json.Serialization;

namespace Upbit_Manager.Models.Upbit
{
    public class UpbitSocketDto
    {
        [JsonPropertyName("trade_price")] public double TradePrice { get; set; }
        [JsonPropertyName("trade_volume")] public double TradeVolume { get; set; }
        [JsonPropertyName("ask_bid")] public string Side { get; set; }
        [JsonPropertyName("market")] public string Market { get; set; }
    }
}
