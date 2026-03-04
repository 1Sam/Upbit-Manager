using System;
using Crypto.Collector.Shared;

namespace Upbit_Manager.Core.Orderbook
{
    /// <summary>
    /// 특정 시점의 오더북 스냅샷
    /// 히트맵 렌더링 및 Spoofing 감지에 사용
    /// </summary>
    public sealed class OrderbookSnapshot
    {
        public DateTime Time { get; init; }
        public long TimestampMs { get; init; }
        public OrderbookUnit[] Units { get; init; } = Array.Empty<OrderbookUnit>();
        public double TotalAskSize { get; init; }
        public double TotalBidSize { get; init; }

        /// <summary>
        /// 잔량 불균형 지수 (-1 ~ +1)
        /// +1: 완전 매수 우세 / -1: 완전 매도 우세
        /// </summary>
        public double Imbalance =>
            (TotalAskSize + TotalBidSize) == 0 ? 0
            : (TotalBidSize - TotalAskSize) / (TotalBidSize + TotalAskSize);
    }

    /// <summary>
    /// 가격-시간 셀 단위의 히트맵 데이터
    /// </summary>
    public sealed class HeatmapCell
    {
        public double Price { get; init; }
        public double TimeOA { get; init; }
        public double Volume { get; set; }
        public HeatmapCellState State { get; set; } = HeatmapCellState.Active;
        public DateTime FirstSeen { get; init; }
        public DateTime LastSeen { get; set; }
    }

    public enum HeatmapCellState
    {
        Active,           // 현재 활성 잔량
        FilledAndGone,    // 체결로 소멸 → 진짜 지지/저항
        SpoofingSuspect,  // 체결 없이 소멸 → Spoofing 의심
    }

    /// <summary>Spoofing 감지 이벤트 데이터</summary>
    public sealed class SpoofingEvent
    {
        public DateTime DetectedAt { get; init; }
        public double Price { get; init; }
        public double Volume { get; init; }
        public string Side { get; init; } = string.Empty;
        public TimeSpan Duration { get; init; }
    }
}