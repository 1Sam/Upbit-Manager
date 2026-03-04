// ✅ [수정] Services/MMFBridgeService.cs
// 🔥 히스토리 구간: 배치 처리로 UI 블로킹 방지
// 🔥 OnHistorySnapshotReceived: 히스토리 전용 이벤트 (렌더링 스킵)
// 🔥 OnHistoryLoadCompleted: 소진 완료 시 렌더링 1회 트리거

using Crypto.Collector.Shared;
using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using Upbit_Manager.Core;

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
    /// 🔥 - 히스토리 구간: 500개씩 배치 처리 → UI 블로킹 방지
    /// 🔥 - 히스토리 완료 후 렌더링 1회 트리거
    /// 🔥 - 실시간 구간: 5ms 간격 유지
    /// </summary>
    public sealed class MMFBridgeService : IDisposable
    {
        private const string MMF_NAME = "MMF_ORDERBOOK_KRW-ADA";

        // 🔥 전체 히스토리 읽기 (capacity와 동일)
        private const long HistoryLookback = 1_200_000L;

        // 🔥 배치 처리: 500개 읽고 10ms 대기 → UI 스레드 숨 돌리기
        private const int HistoryBatchSize = 500;
        private const int HistoryBatchDelay = 10;

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private CancellationTokenSource? _cts;
        private Task? _worker;

        private long _lastReadSequence = 0;
        private bool _isConnected;

        /// <summary>Collector 연결 상태 변경 이벤트</summary>
        public event Action<bool>? OnConnectionStatusChanged;

        /// <summary>실시간 오더북 데이터 수신 이벤트 (ChartManager + HeatmapForm)</summary>
        public event Action<long, OrderbookUnit[]>? OnOrderbookReceived;

        /// <summary>
        /// 🔥 히스토리 스냅샷 이벤트 (HeatmapForm 전용)
        /// 렌더링 없이 엔진에만 데이터 주입
        /// </summary>
        public event Action<long, OrderbookUnit[]>? OnHistorySnapshotReceived;

        /// <summary>
        /// 🔥 히스토리 소진 완료 이벤트
        /// HeatmapForm에서 이 이벤트 수신 시 렌더링 1회 강제 실행
        /// </summary>
        public event Action? OnHistoryLoadCompleted;

        public void Start()
        {
            if (_worker != null) return;
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => MonitorLoop(_cts.Token));
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _mmf = MemoryMappedFile.OpenExisting(MMF_NAME);
                    _accessor = _mmf.CreateViewAccessor();
                    UpdateConnectionStatus(true);
                    await ReadLoop(token);
                }
                catch (FileNotFoundException)
                {
                    UpdateConnectionStatus(false);
                }
                catch
                {
                    UpdateConnectionStatus(false);
                }

                await Task.Delay(1000, token);
            }
        }

        private async Task ReadLoop(CancellationToken token)
        {
            if (_accessor == null) return;

            // ── 히스토리 시작 시퀀스 설정 ──────────────────────
            _accessor.Read(0, out MmfHeader initHeader);

            long lookback = Math.Min(HistoryLookback, initHeader.WriteSequence);
            _lastReadSequence = initHeader.WriteSequence - lookback;

            Logger.Log($"[MMF] 히스토리 읽기 시작 | {lookback}개 | 시퀀스 {_lastReadSequence}~{initHeader.WriteSequence}");

            // ── 히스토리 구간: 배치 처리 ───────────────────────
            long historyEnd = initHeader.WriteSequence;
            int batchCount = 0;

            while (_lastReadSequence < historyEnd && !token.IsCancellationRequested)
            {
                try
                {
                    _accessor.Read(0, out MmfHeader header);

                    long nextSeq = _lastReadSequence + 1;
                    long index = (nextSeq - 1) % header.Capacity;
                    int snapshotSize = MmfLayout.SnapshotSize(header.Depth);
                    long offset = MmfLayout.HeaderSize + (index * snapshotSize);

                    long timestamp = _accessor.ReadInt64(offset);
                    int depth = _accessor.ReadInt32(offset + 8);

                    if (depth > 0 && depth <= header.Depth)
                    {
                        var units = new OrderbookUnit[depth];
                        _accessor.ReadArray(offset + 12, units, 0, depth);

                        // 🔥 히스토리 전용 이벤트 (HeatmapForm에서 렌더링 스킵)
                        OnHistorySnapshotReceived?.Invoke(timestamp, units);
                    }

                    _lastReadSequence = nextSeq;
                    batchCount++;

                    // 🔥 배치 단위마다 잠깐 양보 → UI 스레드 숨 돌리기
                    if (batchCount % HistoryBatchSize == 0)
                        await Task.Delay(HistoryBatchDelay, token);
                }
                catch
                {
                    UpdateConnectionStatus(false);
                    Cleanup();
                    return;
                }
            }

            // 🔥 히스토리 소진 완료 → 렌더링 1회 트리거
            Logger.Log($"[MMF] 히스토리 소진 완료 | 총 {batchCount}개");
            OnHistoryLoadCompleted?.Invoke();

            // ── 실시간 구간 ────────────────────────────────────
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _accessor.Read(0, out MmfHeader header);

                    long currentSeq = header.WriteSequence;

                    if (currentSeq <= _lastReadSequence)
                    {
                        await Task.Delay(5, token);
                        continue;
                    }

                    long nextSeq = _lastReadSequence + 1;
                    long index = (nextSeq - 1) % header.Capacity;
                    int snapshotSize = MmfLayout.SnapshotSize(header.Depth);
                    long offset = MmfLayout.HeaderSize + (index * snapshotSize);

                    long timestamp = _accessor.ReadInt64(offset);
                    int depth = _accessor.ReadInt32(offset + 8);

                    if (depth <= 0 || depth > header.Depth)
                    {
                        _lastReadSequence = nextSeq;
                        continue;
                    }

                    var units = new OrderbookUnit[depth];
                    _accessor.ReadArray(offset + 12, units, 0, depth);

                    // 실시간: ChartManager + HeatmapForm 모두 수신
                    OnOrderbookReceived?.Invoke(timestamp, units);

                    _lastReadSequence = nextSeq;

                    await Task.Delay(5, token);
                }
                catch
                {
                    UpdateConnectionStatus(false);
                    Cleanup();
                    return;
                }
            }
        }

        private void UpdateConnectionStatus(bool connected)
        {
            if (_isConnected == connected) return;
            _isConnected = connected;
            OnConnectionStatusChanged?.Invoke(connected);
        }

        private void Cleanup()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
            _accessor = null;
            _mmf = null;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            try { _worker?.Wait(); } catch { }
            Cleanup();
            _cts?.Dispose();
        }
    }
}