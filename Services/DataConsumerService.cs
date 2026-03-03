using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Upbit_Manager.Infrastructure.Services
{
    /// <summary>
    /// Shared Memory(MemoryMappedFile)를 통해 수집기로부터 데이터를 수신하는 인프라 서비스입니다.
    /// </summary>
    public class DataConsumerService : IDisposable
    {
        private const string MapName = "UpbitDataExchange";
        private const int BufferSize = 1024 * 10; // 10KB

        private CancellationTokenSource? _cts;
        private bool _isDisposed;

        public event Action<string>? OnDataReceived;

        public void Start()
        {
            if (_cts != null) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => ReadLoop(_cts.Token));
        }

        private async Task ReadLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 수집기가 생성한 메모리 맵 파일 오픈 시도
                    using var mmf = MemoryMappedFile.OpenExisting(MapName);
                    using var accessor = mmf.CreateViewAccessor(0, BufferSize, MemoryMappedFileAccess.Read);

                    byte[] buffer = new byte[BufferSize];
                    accessor.ReadArray(0, buffer, 0, buffer.Length);

                    string data = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        OnDataReceived?.Invoke(data);
                    }
                }
                catch (FileNotFoundException)
                {
                    // 수집기가 아직 실행되지 않았거나 메모리를 생성하지 않은 상태
                }
                catch (Exception ex)
                {
                    // 시스템 진단 로그에 기록
                    System.Diagnostics.Debug.WriteLine($"Error reading MMF: {ex.Message}");
                }

                await Task.Delay(100, token); // 폴링 간격
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Stop();
                _isDisposed = true;
            }
        }
    }
}