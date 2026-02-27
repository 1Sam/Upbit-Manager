using System;
using System.IO;

namespace Upbit_Manager.Core
{
    /// <summary>
    /// 프로그램의 실행 로그 및 에러를 텍스트 파일로 기록하는 클래스입니다.
    /// </summary>
    public static class Logger
    {
        // UI에서 구독할 수 있는 이벤트 추가
        public static event Action<string>? OnLogAdded;

        // 로그 저장 폴더: 실행파일위치/Logs
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        public static void Log(string message, string level = "INFO")
        {
            // 1. UI 알림을 파일 쓰기보다 먼저 수행 (파일 에러가 나더라도 UI엔 표시되게)
            // 단, 메시지에 포함된 줄바꿈을 제거하여 UI 리스트가 깨지지 않게 방어
            string cleanMessage = message.Replace(Environment.NewLine, " ").Replace("\n", " ");
            string logTime = DateTime.Now.ToString("HH:mm:ss");
            string pureMessage = $"[{logTime}] [{level}] {cleanMessage}";

            try
            {
                OnLogAdded?.Invoke(pureMessage);
            }
            catch { /* UI 이벤트 핸들러 에러 방어 */ }

            try
            {
                if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);

                string fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
                string filePath = Path.Combine(LogDirectory, fileName);

                // 실제 파일에는 줄바꿈을 포함한 원본 메시지 기록
                File.AppendAllText(filePath, $"[{logTime}] [{level}] {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // ★ 절대 여기서 Log()나 Error()를 다시 부르면 안 됩니다!
                System.Diagnostics.Debug.WriteLine($"파일 기록 실패: {ex.Message}");
            }
        }

        public static void Error(Exception ex, string contextMessage = "")
        {
            // Error 메서드는 단순히 문자열을 조립해서 Log에 던지는 역할만 수행
            string fullMessage = $"{contextMessage} - {ex.Message} {Environment.NewLine}[StackTrace]{Environment.NewLine}{ex.StackTrace}";
            Log(fullMessage, "ERROR");
        }



        //이 메서드는 지정된 개수만큼 가장 최근 로그를 파일에서 읽어옵니다.
        public static List<string> GetLastLogs(int count)
        {
            List<string> lastLogs = new List<string>();
            try
            {
                if (!Directory.Exists(LogDirectory)) return lastLogs;

                // 1. 로그 파일을 날짜 역순(최신순)으로 정렬하여 가져옴
                var files = Directory.GetFiles(LogDirectory, "*.log")
                                     .OrderByDescending(f => f)
                                     .ToList();

                foreach (var file in files)
                {
                    // 2. 파일의 모든 줄을 읽어서 역순으로 뒤집음 (최신 내용이 위로)
                    var lines = File.ReadAllLines(file).Reverse();

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        lastLogs.Add(line);

                        // 3. 원하는 개수를 채우면 중단
                        if (lastLogs.Count >= count) return lastLogs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 불러오기 실패: {ex.Message}");
            }
            return lastLogs;
        }
    }
}
