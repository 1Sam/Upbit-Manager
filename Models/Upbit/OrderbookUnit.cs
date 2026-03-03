using System.Collections.Generic;

namespace Upbit_Manager.Models.Upbit
{
    /// <summary>
    /// 업비트 개별 호가 유닛 정보
    /// </summary>
    public class OrderbookUnit
    {
        /// <summary>매도 호가</summary>
        public double AskPrice { get; set; }

        /// <summary>매수 호가</summary>
        public double BidPrice { get; set; }

        /// <summary>매도 잔량</summary>
        public double AskSize { get; set; }

        /// <summary>매수 잔량</summary>
        public double BidSize { get; set; }
    }

    /// <summary>
    /// 차트 시리즈로 전달될 호가 데이터 패키지
    /// </summary>
    public class UpbitOrderbookPayload
    {
        public string Symbol { get; set; } = string.Empty;

        /// <summary>호가 생성 시각 (Timestamp)</summary>
        public long Timestamp { get; set; }

        /// <summary>총 매도 잔량</summary>
        public double TotalAskSize { get; set; }

        /// <summary>총 매수 잔량</summary>
        public double TotalBidSize { get; set; }

        /// <summary>호가 리스트 (보통 15호가)</summary>
        public List<OrderbookUnit> Units { get; set; } = new List<OrderbookUnit>();
    }
}