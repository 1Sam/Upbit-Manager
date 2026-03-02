using System;
using System.Collections.Generic;
using System.Linq;

namespace Upbit_Manager.Core.Alarms
{
    public class AlarmManager
    {
        private readonly List<IAlarmCondition> _alarms = new();
        private readonly object _lock = new();

        public event Action<IAlarmCondition, double, double>? AlarmTriggered;

        public void AddAlarm(IAlarmCondition alarm)
        {
            lock (_lock) _alarms.Add(alarm);
        }

        public void ClearAlarms()
        {
            lock (_lock) _alarms.Clear();
        }

        public void CheckAll(double price, double volume)
        {
            lock (_lock)
            {
                foreach (var alarm in _alarms.Where(a => a.IsEnabled))
                {
                    // 쿨다운 체크
                    if ((DateTime.Now - alarm.LastTriggerTime).TotalSeconds < alarm.CooldownSeconds)
                        continue;

                    if (alarm.Check(price, volume))
                    {
                        alarm.Execute(price, volume);
                        AlarmTriggered?.Invoke(alarm, price, volume);
                    }
                }
            }
        }
    }
}