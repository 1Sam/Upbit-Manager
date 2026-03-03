using System.Runtime.InteropServices;

namespace Upbit_Manager.Models.Upbit
{
    /// <summary>
    /// 콘솔 앱의 OrderbookUnit과 1:1 대응되는 바이너리 구조체
    /// Pack = 1 설정을 통해 데이터 정렬 어긋남을 방지합니다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedOrderbookLevel
    {
        public double AskPrice; // ap
        public double BidPrice; // bp
        public double AskSize;  // as
        public double BidSize;  // bs
    }
}