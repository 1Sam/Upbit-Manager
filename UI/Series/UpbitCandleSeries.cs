// UI/Series/UpbitCandleSeries.cs

using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 1분봉 캔들스틱 시리즈.
    /// UpdateData payload: CommonCandle (초기 로드) 또는 RealtimePayload (실시간)
    /// </summary>
    public class UpbitCandleSeries : IChartSeries
    {
        public ExchangeSource Source => ExchangeSource.Upbit;
        public SeriesType Type => SeriesType.Candle;
        public string Label => "Upbit - 캔들";
        public override string ToString() => Label;  // "Upbit - 거래량"
        public bool DefaultOn => true;
        public bool IsVisible { get; set; }

        // 캔들 데이터 버퍼
        private const int MAX_BUFFER = 3000;
        private readonly RingBuffer<OHLC> _ohlcBuffer = new RingBuffer<OHLC>(MAX_BUFFER);

        // 실시간 업데이트용 상태
        private DateTime _lastCandleTime = DateTime.MinValue;
        public double LastPrice { get; private set; } = 0;
        public double PrevPrice { get; private set; } = 0;

        // ─── 데이터 업데이트 ───────────────────────────────────────────

        /// <summary>
        /// payload 종류:
        ///   - List&lt;CommonCandle&gt; : 초기 데이터 일괄 로드
        ///   - UpbitRealtimePayload  : 실시간 체결 1건
        /// </summary>
        public void UpdateData(object payload)
        {
            if (payload is List<CommonCandle> candles)
            {
                // 초기 로드
                _ohlcBuffer.Clear();
                foreach (var c in candles)
                    _ohlcBuffer.Add(new OHLC(c.Open, c.High, c.Low, c.Close,
                                             c.Time, TimeSpan.FromMinutes(1)));

                if (candles.Any())
                {
                    _lastCandleTime = candles.Last().Time;
                    LastPrice = candles.Last().Close;
                    PrevPrice = LastPrice;
                }
            }
            else if (payload is UpbitRealtimePayload rt)
            {
                // 실시간 체결
                PrevPrice = LastPrice;
                LastPrice = rt.Price;
                DateTime now = DateTime.Now;

                bool isNew = _ohlcBuffer.Count == 0
                          || (now - _lastCandleTime).TotalSeconds >= 60;

                if (!isNew)
                {
                    // 마지막 캔들 갱신
                    var last = _ohlcBuffer[_ohlcBuffer.Count - 1];
                    last.High = Math.Max(last.High, rt.Price);
                    last.Low = Math.Min(last.Low, rt.Price);
                    last.Close = rt.Price;
                    // overwrite
                    // read all, replace last, re-add
                    var arr = _ohlcBuffer.ToArray();
                    arr[^1] = last;
                    _ohlcBuffer.Clear();
                    foreach (var a in arr) _ohlcBuffer.Add(a);
                }
                else
                {
                    // 새 캔들 추가
                    _ohlcBuffer.Add(new OHLC(rt.Price, rt.Price, rt.Price, rt.Price,
                                             now, TimeSpan.FromMinutes(1)));
                    _lastCandleTime = now;
                }
            }
        }

        // ─── 렌더링 ───────────────────────────────────────────────────

        public void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot)
        {
            if (_ohlcBuffer.Count == 0) return;

            // 캔들스틱 추가
            var candles = candlePlot.Add.Candlestick(_ohlcBuffer.ToArray());
            candles.RisingColor = Colors.Red;
            candles.FallingColor = Colors.Blue;

            // 현재가 수평선
            if (LastPrice > 0)
            {
                var hline = candlePlot.Add.HorizontalLine(LastPrice);
                hline.LineStyle.Width = 1;
                hline.LinePattern = LinePattern.Dashed;
                hline.Text = LastPrice.ToString("N0");
                hline.LabelOppositeAxis = true;
                hline.LabelRotation = 0;
                hline.LabelBackgroundColor = LastPrice >= PrevPrice
                                            ? Colors.Red : Colors.Blue;
            }
        }

        public void Clear()
        {
            _ohlcBuffer.Clear();
            _lastCandleTime = DateTime.MinValue;
            LastPrice = PrevPrice = 0;
        }

        public (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            var visible = _ohlcBuffer
                .Where(o => o.DateTime.ToOADate() >= minOA)
                .ToList();

            if (!visible.Any()) return null;
            return (visible.Min(o => o.Low), visible.Max(o => o.High));
        }

        // 외부(ChartManager)에서 캔들 버퍼를 읽어야 할 때 사용
        public IReadOnlyList<OHLC> GetBuffer() => _ohlcBuffer.ToArray();
    }

    /// <summary>실시간 체결 데이터 전달용 페이로드</summary>
    public record UpbitRealtimePayload(double Price, double Volume, string Side);
}