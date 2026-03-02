namespace Upbit_Manager.Core.Alarms.Conditions
{
    public class PriceThresholdAlarm : AlarmBase
    {
        public enum PriceDirection { Above, Below }
        private readonly PriceDirection _direction;
        public double TargetPrice { get; set; }
        private readonly string _customName;

        public override string Name => _customName ?? $"가격 {(_direction == PriceDirection.Above ? "상향" : "하향")} 돌파 ({TargetPrice:N0})";

        public PriceThresholdAlarm(double targetPrice, PriceDirection direction, string name = null)
        {
            TargetPrice = targetPrice;
            _direction = direction;
            _customName = name;
        }

        public override bool Check(double currentPrice, double currentVolume)
        {
            return _direction == PriceDirection.Above
                ? currentPrice >= TargetPrice
                : currentPrice <= TargetPrice;
        }
    }
}