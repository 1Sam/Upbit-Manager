using System;

namespace Upbit_Manager.Core.Alarms
{
    public interface IAlarmCondition
    {
        string Name { get; }
        bool IsEnabled { get; set; }
        DateTime LastTriggerTime { get; }
        int CooldownSeconds { get; set; }
        bool IsDiscordNotify { get; set; }

        // 알람 조건 체크 (현재가, 현재 거래량 전달)
        bool Check(double currentPrice, double currentVolume);

        // 알람 발생 시 실행될 액션
        void Execute(double price, double volume);
    }
}