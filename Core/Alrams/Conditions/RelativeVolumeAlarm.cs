using System;

namespace Upbit_Manager.Core.Alarms.Conditions
{
    public class RelativeVolumeAlarm : AlarmBase
    {
        private readonly Func<double> _avgVolumeProvider;
        public double Multiplier { get; set; }

        public override string Name => $"거래량 급증 ({Multiplier}배)";

        public RelativeVolumeAlarm(Func<double> avgVolumeProvider, double multiplier)
        {
            _avgVolumeProvider = avgVolumeProvider;
            Multiplier = multiplier;
        }

        public override bool Check(double currentPrice, double currentVolume)
        {
            double avg = _avgVolumeProvider.Invoke();
            if (avg <= 0) return false;

            return currentVolume >= (avg * Multiplier);
        }
    }
}