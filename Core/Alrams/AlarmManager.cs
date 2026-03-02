using System;
using System.Collections.Generic;
using System.Linq;

namespace Upbit_Manager.Core.Alarms
{
    public class AlarmManager
    {
        private readonly List<IAlarmCondition> _alarms = new();
        private readonly object _lock = new();

        // ⭐ [활용방안]: 로그 창에 기록 출력, 텔레그램 메시지 전송, 윈도우 알림 팝업 트리거
        public event Action<IAlarmCondition, double, double>? AlarmTriggered;

        // ⭐ [활용방안]: 알람 목록 UI(리스트박스 등)에서 항목을 추가/삭제 시 동기화하기 위해 사용
        public event Action? OnAlarmListChanged;

        public void AddAlarm(IAlarmCondition alarm)
        {
            lock (_lock) _alarms.Add(alarm);
        }

        public void ClearAlarms()
        {
            lock (_lock) _alarms.Clear();
        }

        /// <summary>
        /// 컨트롤러에서 알람 목록에 접근하여 설정을 변경할 수 있도록 추가합니다.
        /// 원본의 lock 메커니즘을 따릅니다.
        /// </summary>
        public List<IAlarmCondition> GetAlarms()
        {
            lock (_lock)
            {
                // 외부에서 리스트를 순회할 때 안정성을 위해 복사본을 반환하거나 ToList()를 사용합니다.
                return _alarms.ToList();
            }
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

                    // 원본 메서드 명칭인 Check와 Execute를 유지합니다.
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