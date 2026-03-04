using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Upbit_Manager.Core
{
    /// <summary>
    /// 내 문서/Upbit_Manager 폴더에 로그를 기록하고 관리하는 클래스입니다.
    /// </summary>
    public static class Logger
    {
        public static event Action<string>? OnLogAdded;

        // ⭐ 경로 변경: 내 문서/Upbit_Manager
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Upbit_Manager");

        static Logger()
        {
            // 폴더가 없으면 미리 생성
            if (!Directory.Exists(LogDirectory))
                Directory.CreateDirectory(LogDirectory);
        }

        public static void Log(string message, string level = "INFO")
        {
            string logTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string cleanMessage = message.Replace(Environment.NewLine, " ").Replace("\n", " ");
            string pureMessage = $"[{logTime}] [{level}] {cleanMessage}";

            // 1. UI 이벤트 발생
            try { OnLogAdded?.Invoke(pureMessage); } catch { }

            // 2. 파일 기록
            try
            {
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
                string filePath = Path.Combine(LogDirectory, fileName);

                // 실제 파일에는 줄바꿈을 포함한 원본 메시지 기록
                File.AppendAllTextAsync(filePath, $"[{logTime}] [{level}] {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 파일 기록 실패: {ex.Message}");
            }
        }

        public static void Error(Exception ex, string contextMessage = "")
        {
            string fullMessage = $"{contextMessage} - {ex.Message} {Environment.NewLine}[StackTrace]{Environment.NewLine}{ex.StackTrace}";
            Log(fullMessage, "ERROR");
        }

        /// <summary>
        /// 내 문서 폴더 내 로그 파일들을 뒤져서 가장 최근 로그 count개를 가져옵니다.
        /// </summary>
        public static List<string> GetLastLogs(int count)
        {
            List<string> lastLogs = new List<string>();
            try
            {
                if (!Directory.Exists(LogDirectory)) return lastLogs;

                // .log 파일들을 이름(날짜) 역순으로 정렬
                var files = Directory.GetFiles(LogDirectory, "*.log")
                                     .OrderByDescending(f => f)
                                     .ToList();

                foreach (var file in files)
                {
                    // 파일 내용을 읽어 역순으로 뒤집어 최신 내용부터 검사
                    var lines = File.ReadAllLines(file).Reverse();

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        lastLogs.Add(line);

                        if (lastLogs.Count >= count) return lastLogs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"과거 로그 로드 실패: {ex.Message}");
            }
            return lastLogs;
        }
    }
}