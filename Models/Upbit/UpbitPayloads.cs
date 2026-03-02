using System;
using System.Collections.Generic;
using Upbit_Manager.Models.Common;

namespace UpbitManager.Models.Upbit
{
    /// <summary>
    /// 업비트 실시간 체결 데이터를 전달하기 위한 페이로드.
    /// CandleSeries와 VolumeSeries 등에서 공통으로 사용됩니다.
    /// </summary>
    /// <param name="Price">현재 체결가 (KRW)</param>
    /// <param name="Volume">현재 체결량</param>
    /// <param name="Side">체결 구분 ("ASK": 매도, "BID": 매수)</param>
    public record UpbitRealtimePayload(double Price, double Volume, string Side);

    /// <summary>
    /// 초기 데이터 로드 시 사용하는 페이로드.
    /// 리스트 형태로 전달되어 RingBuffer를 한꺼번에 채울 때 사용됩니다.
    /// </summary>
    public record UpbitHistoryPayload(List<CommonCandle> Candles);

    /// <summary>
    /// 바이낸스나 다른 거래소의 가격 정보를 전달할 때 사용하는 페이로드.
    /// </summary>
    /// <param name="Price">KRW 환산 가격</param>
    /// <param name="Time">데이터 발생 시간</param>
    public record ExternalPricePayload(double Price, DateTime Time);

    /// <summary>
    /// 계좌 정보나 주문 정보를 전달할 때 사용하는 페이로드.
    /// </summary>
    public record AccountInfoPayload(double AvgBuyPrice, double ProfitRate);
}