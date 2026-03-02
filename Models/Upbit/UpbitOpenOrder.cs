// 파일명: UpbitOpenOrder.cs
using System.Text.Json.Serialization;

namespace Upbit_Manager.Models.Upbit
{
    public class UpbitOpenOrder
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = string.Empty;

        [JsonPropertyName("market")] // JSON에서는 market으로 옴
        public string Market { get; set; } // 이 부분이 대문자 Market인지 확인!

        [JsonPropertyName("side")]
        public string Side { get; set; } = string.Empty; // bid: 매수, ask: 매도

        [JsonPropertyName("price")]
        public string PriceString { get; set; } = "0";

        [JsonPropertyName("remaining_volume")]
        public string VolumeString { get; set; } = "0";

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        // 계산 편의를 위한 속성 (선택 사항)
        public double Price => double.TryParse(PriceString, out var p) ? p : 0;
        public double Volume => double.TryParse(VolumeString, out var v) ? v : 0;
    }
}