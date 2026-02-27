using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Upbit_Manager.Core
{
    public static class ApiKeyStore
    {
        // 사용자 문서 폴더 하위에 저장합니다(사용자가 파일을 쉽게 찾고 백업할 수 있도록).
        public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Upbit_Manager", "ApiKey.cfg");

        public static void SaveKeys(string accessKey, string secretKey)
        {
            var obj = new { access = accessKey ?? string.Empty, secret = secretKey ?? string.Empty };
            var json = JsonSerializer.Serialize(obj);
            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

            var dir = Path.GetDirectoryName(FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 파일 쓰기 시 권한 오류를 피하기 위해 예외를 호출자에게 전달
            File.WriteAllText(FilePath, Convert.ToBase64String(encrypted));
        }

        public static (string access, string secret)? ReadKeys()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                string b64 = File.ReadAllText(FilePath);
                byte[] encrypted = Convert.FromBase64String(b64);
                byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var dict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(Encoding.UTF8.GetString(plain));
                if (dict == null) return null;
                dict.TryGetValue("access", out var a);
                dict.TryGetValue("secret", out var s);
                return (a ?? string.Empty, s ?? string.Empty);
            }
            catch
            {
                return null;
            }
        }
    }
}
