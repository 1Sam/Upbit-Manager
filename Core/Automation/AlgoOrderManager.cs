using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Upbit_Manager.Core;

namespace Upbit_Manager.Core.Automation
{
    /// <summary>
    /// 주문 집행 전략 정의
    /// </summary>
    public enum ExecutionStrategy
    {
        Immediate,    // 즉시 실행
        Grid,         // 그리드 (거미줄) 매매
        TWAP,         // 시간 가중 평균 가격 (Time Weighted Average Price)
        Iceberg       // 빙산 주문 (Hidden Size 주문)
    }

    /// <summary>
    /// 알고리즘 주문 단위 정보
    /// </summary>
    public class OrderItem
    {
        public double Price { get; set; }
        public double Quantity { get; set; }
        public string OrderType { get; set; } = "limit"; // limit, price(시장가 매수), market(시장가 매도)
    }

    /// <summary>
    /// [최종 명칭] AlgoOrderManager
    /// 다양한 매매 알고리즘에 따라 OrderManager를 제어하여 주문을 실행하는 전략 엔진입니다.
    /// </summary>
    public class AlgoOrderManager
    {
        // UI 및 로그 기록을 위한 이벤트
        public event Action<int, int, string>? OnExecutionProgress;
        public event Action<string>? OnExecutionCompleted;
        public event Action<string, string>? OnLogMessage;

        private readonly OrderManager _orderManager; // 실제 주문 도구와 연동
        private readonly Random _random = new();

        /// <summary>
        /// 생성자에서 OrderManager를 주입받아 연동합니다.
        /// </summary>
        public AlgoOrderManager(OrderManager orderManager)
        {
            _orderManager = orderManager;
        }

        /// <summary>
        /// TWAP (시간 분할) 전략 실행
        /// </summary>
        public async Task ExecuteTWAP(string market, double totalAmount, int totalMinutes, int splitCount)
        {
            double amountPerOrder = totalAmount / splitCount;
            int intervalMs = (totalMinutes * 60 * 1000) / splitCount;

            OnLogMessage?.Invoke("SYSTEM", $"TWAP 엔진 시작: {totalMinutes}분 동안 {splitCount}회 분할 집행");

            for (int i = 1; i <= splitCount; i++)
            {
                int jitter = _random.Next(-intervalMs / 5, intervalMs / 5);
                int finalDelay = Math.Max(1000, intervalMs + jitter);

                try
                {
                    // ⭐ OrderManager를 통한 실제 시장가 주문 연동 예시
                    // await _orderManager.PlaceMarketOrder(market, "bid", amountPerOrder);

                    Logger.Log($"[TWAP] {market} | {i}/{splitCount}차 주문 완료. (다음 주문까지 약 {finalDelay / 1000}초)");
                    OnExecutionProgress?.Invoke(i, splitCount, $"TWAP 집행 중... ({i}/{splitCount})");
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke("ERROR", $"TWAP {i}차 실패: {ex.Message}");
                }

                if (i < splitCount) await Task.Delay(finalDelay);
            }

            OnExecutionCompleted?.Invoke("TWAP 알고리즘 집행 완료");
        }

        /// <summary>
        /// Iceberg (빙산) 주문 실행
        /// </summary>
        public async Task ExecuteIceberg(string market, double targetPrice, double totalQuantity, double visibleQuantity)
        {
            double remained = totalQuantity;
            int step = 1;

            while (remained > 0)
            {
                double currentOrderQty = Math.Min(remained, visibleQuantity);

                try
                {
                    // ⭐ OrderManager를 통한 지정가 주문 연동 예시
                    // await _orderManager.PlaceLimitOrder(market, "bid", targetPrice, currentOrderQty);

                    Logger.Log($"[Iceberg] {market} | {step}회차 {currentOrderQty}개 노출 중...");

                    // 실제로는 여기서 주문이 체결되었는지 OrderManager의 상태를 확인하는 로직이 필요함
                    remained -= currentOrderQty;
                    step++;
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke("ERROR", $"Iceberg {step}회차 실패: {ex.Message}");
                    break;
                }

                await Task.Delay(1500);
            }

            OnExecutionCompleted?.Invoke("빙산 주문 처리 완료");
        }

        /// <summary>
        /// 그리드(거미줄) 주문 실행
        /// </summary>
        public async Task ExecuteGridOrders(string market, List<OrderItem> orders)
        {
            if (orders == null || !orders.Any()) return;

            int total = orders.Count;
            for (int i = 0; i < total; i++)
            {
                var order = orders[i];
                try
                {
                    // ⭐ OrderManager 연동
                    // await _orderManager.PlaceLimitOrder(market, "bid", order.Price, order.Quantity);

                    Logger.Log($"[그리드] {market} | {order.Price:N0}원에 {order.Quantity}개 예약 ({i + 1}/{total})");
                    OnExecutionProgress?.Invoke(i + 1, total, "그리드 주문 송신 중");
                    await Task.Delay(250);
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke("ERROR", $"그리드 {i + 1}차 실패: {ex.Message}");
                }
            }
            OnExecutionCompleted?.Invoke("그리드 일괄 예약 완료");
        }

        public List<OrderItem> GenerateGrid(double startPrice, double gapPercentage, int steps, double amountPerStep)
        {
            var list = new List<OrderItem>();
            for (int i = 0; i < steps; i++)
            {
                double targetPrice = startPrice * (1.0 - (gapPercentage * (i + 1) / 100.0));
                list.Add(new OrderItem { Price = targetPrice, Quantity = amountPerStep / targetPrice });
            }
            return list;
        }
    }
}