using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace Upbit_Manager.Services
{
    public class UpbitAuthenticator
    {
        private readonly string _accessKey;
        private readonly string _secretKey;

        public UpbitAuthenticator(string accessKey, string secretKey)
        {
            _accessKey = accessKey;
            _secretKey = secretKey;
        }

        public string CreateJwtToken(string queryString = "")
        {
            if (string.IsNullOrWhiteSpace(_accessKey) || string.IsNullOrWhiteSpace(_secretKey))
            {
                throw new InvalidOperationException("API 키가 설정되지 않았습니다. 먼저 API 키를 등록하세요.");
            }
            // 1. 페이로드 구성
            var payload = new JwtPayload
    {
        { "access_key", _accessKey },
        { "nonce", Guid.NewGuid().ToString() }
    };

            // 2. [핵심] 쿼리 스트링(파라미터)이 있는 경우 해싱 처리
            if (!string.IsNullOrEmpty(queryString))
            {
                using var sha512 = SHA512.Create();
                byte[] queryHashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(queryString));
                string queryHash = BitConverter.ToString(queryHashBytes).Replace("-", "").ToLower();

                payload.Add("query_hash", queryHash);
                payload.Add("query_hash_alg", "SHA512");
            }

            // 3. 서명 설정 (SecretKey 사용)
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var header = new JwtHeader(credentials);

            // 4. 최종 토큰 생성 및 문자열 반환
            var secToken = new JwtSecurityToken(header, payload);
            return new JwtSecurityTokenHandler().WriteToken(secToken);
        }
    }
}