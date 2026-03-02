using System;
using System.Threading;
using System.Threading.Tasks;
using ScottPlot;

namespace Upbit_Manager.Interfaces
{
    /// <summary>
    /// 차트 데이터 공급을 위한 공통 인터페이스입니다.
    /// 업비트, 바이낸스 등 거래소 확장이 용이하도록 규격화합니다.
    /// </summary>
    public interface ICandleProvider
    {
        event Action<OHLC> OnCandleUpdated;
        Task StartAsync(string market, CancellationToken ct);
    }
}