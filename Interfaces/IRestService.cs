using System.Collections.Generic;
using System.Threading.Tasks;
using Upbit_Manager.Controllers;
using Upbit_Manager.Core;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Interfaces
{
    public interface IRestService
    {
        // 1. 캔들 데이터 (공통 모델)
        // market: "KRW-BTC", count: 가져올 갯수
        Task<List<CommonCandle>> GetCandlesAsync(string market, int count);

        // 2. 계좌 정보 (가공된 공통 모델)
        // UI나 로직에서 즉시 사용하기 좋은 형태
        Task<List<AssetItem>> GetAccountsAsync();

        // ⭐ 3. 원본 JSON 데이터 가져오기 (추가 권장)
        // 가끔 가공되지 않은 원본 데이터(보유 수량의 상세 소수점 등)가 필요할 때가 있습니다.
        // 또한 로깅이나 디버깅 시 매우 유용합니다.
        Task<string> GetAccountsJsonAsync();

        Task<string> GetOpenOrdersJsonAsync(); // 👈 미체결 주문 조회를 위해 반드시 필요

//        💡 왜 GetAccountsJsonAsync가 필요한가요?
//데이터 무결성: AssetItem으로 변환하는 과정에서 계산 실수나 반올림 오차가 생길 수 있습니다.원본 JSON을 들고 있으면 나중에 AccountManager에서 다시 정밀하게 파싱할 기회가 생깁니다.

//확장성: 바이낸스와 업비트는 응답 구조가 완전히 다릅니다. 인터페이스에서 string으로 던져주면, 받는 쪽(MainController나 AccountManager) 에서 거래소별 파서를 따로 돌리기가 훨씬 수월합니다.
    }
}