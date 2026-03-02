using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Upbit_Manager.Core.Alarms
{
    // AlarmBase.cs 파일 상단의 DiscordWebhookUrl에 본인의 디스코드 채널 웹훅 주소를 넣으면 즉시 알림이 발송됩니다.
    public abstract class AlarmBase : IAlarmCondition
    {
        public abstract string Name { get; }
        public bool IsEnabled { get; set; } = true;
        public DateTime LastTriggerTime { get; protected set; } = DateTime.MinValue;
        public int CooldownSeconds { get; set; } = 60;
        public bool IsDiscordNotify { get; set; } = false;

        // 디스코드 웹훅 주소 (나중에 환경설정으로 이동 권장)
        private const string DiscordWebhookUrl = "YOUR_DISCORD_WEBHOOK_URL_HERE";

        public abstract bool Check(double currentPrice, double currentVolume);

        public virtual void Execute(double price, double volume)
        {
            LastTriggerTime = DateTime.Now;

            // 1. 시스템 로그 및 소리 (Console/UI 호출은 매니저에서 담당)
            System.Media.SystemSounds.Exclamation.Play();

            // 2. 디스코드 알람 (선택 시)
            if (IsDiscordNotify)
            {
                _ = SendDiscordMessage($"[알람 발생] {Name}\n가격: {price:N0} / 거래량: {volume:N0}");
            }
        }

        protected async Task SendDiscordMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(DiscordWebhookUrl) || DiscordWebhookUrl.Contains("YOUR")) return;

                using var client = new HttpClient();
                var content = new StringContent($"{{\"content\": \"{message}\"}}", Encoding.UTF8, "application/json");
                await client.PostAsync(DiscordWebhookUrl, content);
            }
            catch { /* 프로는 알람 실패가 메인 로직을 멈추게 하지 않습니다 */ }
        }
    }
}