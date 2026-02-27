namespace Upbit_Manager.Models.Common
{
    public class AssetItem
    {
        public string Exchange { get; set; } = "Upbit"; // 어느 거래소 자산인지 구분
        public string Symbol { get; set; } = string.Empty;
        public double TotalInventory { get; set; }
        public double AvgBuyPrice { get; set; }
        public double CurrentPrice { get; set; }

        // --- 계산된 속성들 (UI에서 바로 사용) ---
        public double TotalBuyAmount => TotalInventory * AvgBuyPrice;
        public double EvaluationAmount => TotalInventory * CurrentPrice;
        public double ProfitLoss => EvaluationAmount - TotalBuyAmount;
        public double ProfitRate => TotalBuyAmount > 0 ? (ProfitLoss / TotalBuyAmount) * 100 : 0;
    }
}