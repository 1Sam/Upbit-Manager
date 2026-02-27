// UI/Series/UpbitVolumeSeries.cs

using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 거래량 바 시리즈 (하단 volumePlot에 렌더링).
    /// UpbitCandleSeries와 동일한 payload를 공유합니다.
    /// </summary>
    public class UpbitVolumeSeries : IChartSeries
    {
        public ExchangeSource Source => ExchangeSource.Upbit;
        public SeriesType Type => SeriesType.Volume;
        public string Label => "Upbit - 거래량";
        public override string ToString() => Label;  // "Upbit - 거래량"
        public bool DefaultOn => true;
        public bool IsVisible { get; set; }

        private const int MAX_BUFFER = 1200;
        private readonly RingBuffer<double> _buyBuffer = new RingBuffer<double>(MAX_BUFFER);
        private readonly RingBuffer<double> _sellBuffer = new RingBuffer<double>(MAX_BUFFER);
        private readonly RingBuffer<DateTime> _timeBuffer = new RingBuffer<DateTime>(MAX_BUFFER);

        public void UpdateData(object payload)
        {
            if (payload is List<CommonCandle> candles)
            {
                _buyBuffer.Clear();
                _sellBuffer.Clear();
                _timeBuffer.Clear();

                foreach (var c in candles)
                {
                    // historical: split volume evenly when buy/sell breakdown unavailable
                    double half = c.Volume / 2.0;
                    _buyBuffer.Add(half);
                    _sellBuffer.Add(half);
                    _timeBuffer.Add(c.Time);
                }
            }
            else if (payload is ValueTuple<double, double, string, bool> tpl)
            {
                var (price, vol, side, isRising) = tpl;
                var now = DateTime.Now;

                bool? isBuyBySide = null;
                if (!string.IsNullOrEmpty(side))
                    isBuyBySide = side.ToUpperInvariant() == "BID" || side.ToUpperInvariant() == "BUY";

                if (_timeBuffer.Count == 0)
                {
                    _timeBuffer.Add(now);
                    bool isBuy = isBuyBySide ?? isRising;
                    if (isBuy) { _buyBuffer.Add(vol); _sellBuffer.Add(0); }
                    else { _sellBuffer.Add(vol); _buyBuffer.Add(0); }
                    return;
                }

                var lastTime = _timeBuffer[_timeBuffer.Count - 1];
                bool isNew = (now - lastTime).TotalSeconds >= 60;
                if (isNew)
                {
                    _timeBuffer.Add(now);
                    bool isBuy = isBuyBySide ?? isRising;
                    if (isBuy) { _buyBuffer.Add(vol); _sellBuffer.Add(0); }
                    else { _sellBuffer.Add(vol); _buyBuffer.Add(0); }
                }
                else
                {
                    bool isBuy = isBuyBySide ?? isRising;
                    if (isBuy)
                    {
                        double updated = _buyBuffer[_buyBuffer.Count - 1] + vol;
                        _buyBuffer.ReplaceLast(updated);
                    }
                    else
                    {
                        double updated = _sellBuffer[_sellBuffer.Count - 1] + vol;
                        _sellBuffer.ReplaceLast(updated);
                    }
                }
            }
            else if (payload is UpbitRealtimePayload rt)
            {
                var now = DateTime.Now;

                // if no time entry exists yet, create new bar
                if (_timeBuffer.Count == 0)
                {
                    _timeBuffer.Add(now);
                    if (rt.Side?.ToUpper() == "BID") { _buyBuffer.Add(rt.Volume); _sellBuffer.Add(0); }
                    else { _sellBuffer.Add(rt.Volume); _buyBuffer.Add(0); }
                    return;
                }

                var lastTime = _timeBuffer[_timeBuffer.Count - 1];
                bool isNew = (now - lastTime).TotalSeconds >= 60;

                if (isNew)
                {
                    _timeBuffer.Add(now);
                    if (rt.Side?.ToUpper() == "BID") { _buyBuffer.Add(rt.Volume); _sellBuffer.Add(0); }
                    else { _sellBuffer.Add(rt.Volume); _buyBuffer.Add(0); }
                }
                else
                {
                    if (rt.Side?.ToUpper() == "BID")
                    {
                        double updated = _buyBuffer[_buyBuffer.Count - 1] + rt.Volume;
                        _buyBuffer.ReplaceLast(updated);
                    }
                    else
                    {
                        double updated = _sellBuffer[_sellBuffer.Count - 1] + rt.Volume;
                        _sellBuffer.ReplaceLast(updated);
                    }
                }
            }

            // legacy tuple payload handled earlier removed
        }

        public void Render(ScottPlot.Plot candlePlot, ScottPlot.Plot volumePlot)
        {
            var times = _timeBuffer.ToArray();
            var buys = _buyBuffer.ToArray();
            var sells = _sellBuffer.ToArray();
            for (int i = 0; i < times.Length; i++)
            {
                double x = times[i].ToOADate();
                double sell = i < sells.Length ? sells[i] : 0;
                double buy = i < buys.Length ? buys[i] : 0;
                // draw sell (blue) first
                var rectSell = volumePlot.Add.Rectangle(x - 0.0003, x + 0.0003, 0, sell);
                rectSell.FillStyle.Color = Colors.Blue.WithAlpha(0.3);
                rectSell.LineStyle.Width = 0;
                // draw buy stacked on top
                if (buy > 0)
                {
                    var rectBuy = volumePlot.Add.Rectangle(x - 0.0003, x + 0.0003, sell, sell + buy);
                    rectBuy.FillStyle.Color = Colors.Red.WithAlpha(0.3);
                    rectBuy.LineStyle.Width = 0;
                }
            }
        }

        public void Clear()
        {
            _buyBuffer.Clear();
            _sellBuffer.Clear();
            _timeBuffer.Clear();
        }

        // 거래량은 Y축 가격 범위에 영향 없음
        public (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;

        public IReadOnlyList<double> GetBuffer()
        {
            var b = _buyBuffer.ToArray();
            var s = _sellBuffer.ToArray();
            int n = Math.Max(b.Length, s.Length);
            var arr = new double[n];
            for (int i = 0; i < n; i++) arr[i] = (i < s.Length ? s[i] : 0) + (i < b.Length ? b[i] : 0);
            return arr;
        }
        public IReadOnlyList<DateTime> GetTimeBuffer() => _timeBuffer.ToArray();
    }
}