using Crypto.Collector.Shared;
using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Upbit_Manager.Services
{
    /// <summary>
    /// Crypto.Collector가 생성한 MemoryMappedFile을 읽어
    /// 오더북 데이터만 외부로 전달하는 브릿지 서비스
    ///
    /// 설계 원칙:
    /// - ChartManager를 직접 참조하지 않는다 (계층 분리)
    /// - 오더북 데이터는 이벤트로만 전달
    /// - Lock-Free RingBuffer 구조 준수
    /// - WriteSequence 기반 최신 데이터만 읽음
    /// </summary>
    public sealed class MMFBridgeService : IDisposable
    {

        private const string MMF_NAME = "MMF_ORDERBOOK_KRW-ADA";

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;

        private CancellationTokenSource? _cts;
        private Task? _worker;

        // 마지막으로 읽은 시퀀스
        private long _lastReadSequence = 0;

        // 연결 상태 캐시
        private bool _isConnected;

        /// <summary>
        /// Collector 연결 상태 변경 이벤트
        /// </summary>
        public event Action<bool>? OnConnectionStatusChanged;

        /// <summary>
        /// 오더북 데이터 수신 이벤트
        /// MainController에서 구독하여 ChartManager로 전달
        /// </summary>
        public event Action<long, OrderbookUnit[]>? OnOrderbookReceived;

        /// <summary>
        /// 브릿지 루프 시작 (중복 실행 방지)
        /// </summary>
        public void Start()
        {
            if (_worker != null)
                return;

            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => MonitorLoop(_cts.Token));
        }

        /// <summary>
        /// MMF 존재 여부를 감시하는 상위 루프
        /// Collector가 실행되면 자동 연결
        /// 종료되면 자동 재시도
        /// </summary>
        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Collector가 생성한 MMF 열기 시도
                    _mmf = MemoryMappedFile.OpenExisting(MMF_NAME);
                    _accessor = _mmf.CreateViewAccessor();

                    UpdateConnectionStatus(true);

                    // 실제 읽기 루프 진입
                    await ReadLoop(token);
                }
                catch (FileNotFoundException)
                {
                    // Collector 미실행 상태
                    UpdateConnectionStatus(false);
                }
                catch
                {
                    // 예외 발생 시 연결 해제 처리
                    UpdateConnectionStatus(false);
                }

                // 재시도 간격
                await Task.Delay(1000, token);
            }
        }

        /// <summary>
        /// RingBuffer 기반 Snapshot 읽기 루프
        /// 최신 WriteSequence 기준으로 처리
        /// </summary>
        private async Task ReadLoop(CancellationToken token)
        {
            if (_accessor == null)
                return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1️⃣ Header 읽기
                    MmfHeader header;
                    _accessor.Read(0, out header);

                    long currentSeq = header.WriteSequence;

                    // 새로운 데이터가 없으면 대기
                    if (currentSeq == _lastReadSequence)
                    {
                        await Task.Delay(5, token);
                        continue;
                    }

                    // 2️⃣ 읽을 인덱스 계산 (RingBuffer)
                    long index = currentSeq % header.Capacity;

                    int snapshotSize = MmfLayout.SnapshotSize(header.Depth);
                    long offset = MmfLayout.HeaderSize + (index * snapshotSize);

                    // 3️⃣ Snapshot 기본 정보 읽기
                    long timestamp = _accessor.ReadInt64(offset);
                    int depth = _accessor.ReadInt32(offset + 8);

                    // 데이터 무결성 검증
                    if (depth <= 0 || depth > header.Depth)
                    {
                        _lastReadSequence = currentSeq;
                        continue;
                    }

                    long unitOffset = offset + 12;

                    var units = new OrderbookUnit[depth];

                    // 4️⃣ 실제 오더북 배열 읽기
                    _accessor.ReadArray(
                        unitOffset,
                        units,
                        0,
                        depth);

                    // 5️⃣ 외부로 이벤트 발행
                    OnOrderbookReceived?.Invoke(timestamp, units);

                    _lastReadSequence = currentSeq;
                }
                catch
                {
                    // 읽기 중 예외 발생 시 연결 해제 처리
                    UpdateConnectionStatus(false);
                    Cleanup();
                    return;
                }

                await Task.Delay(5, token);
            }
        }

        /// <summary>
        /// 연결 상태 변경 감지 후 이벤트 발행
        /// </summary>
        private void UpdateConnectionStatus(bool connected)
        {
            if (_isConnected == connected)
                return;

            _isConnected = connected;
            OnConnectionStatusChanged?.Invoke(connected);
        }

        /// <summary>
        /// MMF 자원 정리
        /// </summary>
        private void Cleanup()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();

            _accessor = null;
            _mmf = null;
        }

        /// <summary>
        /// 서비스 종료 및 자원 해제
        /// </summary>
        public void Dispose()
        {
            _cts?.Cancel();

            try
            {
                _worker?.Wait();
            }
            catch
            {
                // Task 취소 예외 무시
            }

            Cleanup();
            _cts?.Dispose();
        }
    }
}