namespace Upbit_Manager.Core
{
    public static class ApiConfig
    {
        // 기본값은 빈 문자열로 두고, 런타임에 ApiKeyStore에서 읽어 옵니다.
        public static string AccessKey { get; set; } = string.Empty;
        public static string SecretKey { get; set; } = string.Empty;

        public const string RestApiBaseUrl = "https://api.upbit.com";
        public const string WebSocketUrl = "wss://api.upbit.com/websocket/v1";
    }
}