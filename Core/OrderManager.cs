using System;
using System.Threading.Tasks;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.Core
{
    /// <summary>
    /// 개별 주문의 실행 및 상태 변화를 관리하는 매니저입니다.
    /// </summary>
    public class OrderManager
    {
        private readonly IRestService _upbitRest;

        // ⭐ [활용방안]: 주문이 정상적으로 접수되었을 때 UI에 메시지를 띄우거나 로그를 남김
        public event Action<string, string>? OnOrderPlaced;

        // ⭐ [활용방안]: 주문이 완전히 체결되었을 때 '체결 사운드'를 재생하거나 내 자산 정보를 즉시 갱신하도록 트리거
        public event Action<string, string>? OnOrderExecuted;

        // ⭐ [활용방안]: 잔고 부족, 최소 주문 금액 미달 등으로 주문 실패 시 사용자에게 경고 알림 표시
        public event Action<string, string>? OnOrderFailed;

        public OrderManager(IRestService upbitRest)
        {
            _upbitRest = upbitRest;
        }

        /// <summary>
        /// 지정가 주문을 실행합니다.
        /// </summary>
        public async Task PlaceLimitOrder(string market, string side, double price, double volume)
        {
            try
            {
                // 실제 API 호출 (예시)
                // var result = await _upbitRest.PostOrderAsync(market, side, volume, price, "limit");

                // 성공 시 이벤트 발생
                OnOrderPlaced?.Invoke(market, $"{side} 주문 접수: {price}원 / {volume}개");

                // 웹소켓 등으로 체결 확인 시 OnOrderExecuted 호출하도록 로직 확장 가능
            }
            catch (Exception ex)
            {
                OnOrderFailed?.Invoke(market, ex.Message);
            }
        }

        /// <summary>
        /// 주문을 취소합니다.
        /// </summary>
        public async Task CancelOrder(string uuid)
        {
            try
            {
                // await _upbitRest.DeleteOrderAsync(uuid);
                OnOrderPlaced?.Invoke("SYSTEM", $"주문 취소 완료: {uuid}");
            }
            catch (Exception ex)
            {
                OnOrderFailed?.Invoke("SYSTEM", $"취소 실패: {ex.Message}");
            }
        }
    }
}