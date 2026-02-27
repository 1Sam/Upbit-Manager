using System.Text.Json.Serialization;

namespace Upbit_Manager.Models.Upbit
{
    public class UpbitCandleDto
    {
        [JsonPropertyName("candle_date_time_kst")]
        public DateTime CandleDateTimeKst { get; set; }

        [JsonPropertyName("opening_price")]
        public double OpeningPrice { get; set; }

        [JsonPropertyName("high_price")]
        public double HighPrice { get; set; }

        [JsonPropertyName("low_price")]
        public double LowPrice { get; set; }

        [JsonPropertyName("trade_price")]
        public double TradePrice { get; set; }

        [JsonPropertyName("candle_acc_trade_volume")]
        public double CandleAccTradeVolume { get; set; }
    }
}