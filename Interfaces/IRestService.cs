using Upbit_Manager.Models.Common;

namespace Upbit_Manager.Interfaces
{
    public interface IRestService
    {
        // 1. 캔들 데이터 (공통 모델)
        Task<List<CommonCandle>> GetCandlesAsync(string market, int count);

        // 2. 계좌 정보 (공통 모델인 AssetItem으로 반환)
        Task<List<AssetItem>> GetAccountsAsync();
    }
}