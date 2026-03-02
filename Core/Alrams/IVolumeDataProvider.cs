namespace Upbit_Manager.Core.Alarms
{
    public interface IVolumeDataProvider
    {
        double GetAverageVolume(int lookbackCount);
        double GetCurrentCandleVolume();
    }
}