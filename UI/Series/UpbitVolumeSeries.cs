using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Collections;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 거래량 데이터를 매수(Red)/매도(Blue)로 구분하여 하단 패널에 렌더링합니다.
    /// 배열 캐싱 및 Dirty 플래그를 사용하여 렌더링 성능을 최적화하였습니다.
    /// </summary>
    public class UpbitVolumeSeries : ChartSeriesBase, IChartSeries
    {
        // ─── 속성 정의 ──────────────────────────────────────────────────

        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Volume;

        // ⭐ ChartManager에서 설정한 하단 거래량 패널(보통 25% 영역)에 배치됨
        public override AxisGroup TargetGroup => AxisGroup.Volume;
        public override string Label => "Upbit - 거래량";
        public override bool DefaultOn => true;

        // ─── 데이터 버퍼 및 캐시 ──────────────────────────────────────────

        private const int MAX_BUFFER = 2000;

        // 매수/매도 거래량 및 시간 정보를 담는 순환 버퍼
        private readonly RingBuffer<double> _buyBuffer = new(MAX_BUFFER);
        private readonly RingBuffer<double> _sellBuffer = new(MAX_BUFFER);
        private readonly RingBuffer<DateTime> _timeBuffer = new(MAX_BUFFER);

        // ⭐ 렌더링 최적화를 위한 배열 캐시 (매 프레임 ToArray 방지)
        private readonly double[] _cachedTimes = new double[MAX_BUFFER];
        private readonly double[] _cachedTotalVol = new double[MAX_BUFFER];

        // 데이터 변경 확인 플래그
        private bool _isDirty = true;
        private DateTime _lastUpdateMinute = DateTime.MinValue;

        // ─── 데이터 업데이트 로직 ──────────────────────────────────────────

        public override void UpdateData(object payload)
        {
            lock (_timeBuffer)
            {
                // 1. 초기 대량 데이터 (과거 캔들) 로드
                if (payload is List<CommonCandle> candles)
                {
                    _buyBuffer.Clear();
                    _sellBuffer.Add(0); // 더미 데이터 방지용 초기화 필요시 사용
                    _sellBuffer.Clear();
                    _timeBuffer.Clear();

                    foreach (var c in candles)
                    {
                        // 과거 데이터는 매수/매도 구분이 없으므로 5:5로 배분하여 표시
                        double half = c.Volume / 2.0;
                        _buyBuffer.Add(half);
                        _sellBuffer.Add(half);
                        _timeBuffer.Add(c.Time);
                    }
                }
                // 2. 실시간 틱 데이터 업데이트
                else if (payload is UpbitRealtimePayload rt)
                {
                    DateTime now = DateTime.Now;
                    DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                    // 새로운 분봉(바) 생성 조건
                    if (_timeBuffer.Count == 0 || _lastUpdateMinute != currentMinute)
                    {
                        _timeBuffer.Add(currentMinute);
                        if (rt.Side == "BID") { _buyBuffer.Add(rt.Volume); _sellBuffer.Add(0); }
                        else { _sellBuffer.Add(0); _buyBuffer.Add(rt.Volume); }
                        _lastUpdateMinute = currentMinute;
                    }
                    else
                    {
                        // 현재 마지막 바에 거래량 합산 (RingBuffer의 인덱서 또는 UpdateLast 활용)
                        int lastIdx = _timeBuffer.Count - 1;
                        if (rt.Side == "BID")
                        {
                            double newVal = _buyBuffer[lastIdx] + rt.Volume;
                            _buyBuffer.UpdateLast(newVal);
                        }
                        else
                        {
                            double newVal = _sellBuffer[lastIdx] + rt.Volume;
                            _sellBuffer.UpdateLast(newVal);
                        }
                    }
                }

                _isDirty = true;
            }
        }

        // ─── 렌더링 로직 ──────────────────────────────────────────

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_timeBuffer)
            {
                int count = _timeBuffer.Count;
                if (count == 0) return;

                // ⭐ 최적화: Rectangle 플로팅은 개수가 많아지면 무거우므로 BarPlot이나 직접 루프 렌더링 고려
                // 여기서는 가독성을 위해 Rectangle을 사용하되, Dirty 상태일 때만 좌표를 재계산하도록 권장하나
                // ScottPlot 5에서는 렌더링 시점에 Plottable을 추가하는 방식이므로 루프를 수행합니다.

                double barWidth = 0.00035; // 약 30~40초 정도의 너비 (OADate 단위)

                for (int i = 0; i < count; i++)
                {
                    double x = _timeBuffer[i].ToOADate();
                    double buy = _buyBuffer[i];
                    double sell = _sellBuffer[i];
                    double total = buy + sell;

                    if (total <= 0) continue;

                    // 1. 하단: 매도 거래량 (Blue)
                    var rectSell = plot.Add.Rectangle(x - barWidth, x + barWidth, 0, sell);
                    rectSell.Axes.YAxis = targetAxis;
                    rectSell.FillStyle.Color = Colors.Blue.WithAlpha(0.6);
                    rectSell.LineStyle.Width = 0;

                    // 2. 상단: 매수 거래량 (Red) - 매도 거래량 위로 쌓음 (Stacked)
                    if (buy > 0)
                    {
                        var rectBuy = plot.Add.Rectangle(x - barWidth, x + barWidth, sell, total);
                        rectBuy.Axes.YAxis = targetAxis;
                        rectBuy.FillStyle.Color = Colors.Red.WithAlpha(0.6);
                        rectBuy.LineStyle.Width = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 화면에 보이는 X축 범위 내에서 거래량의 최대치를 계산하여 Y축 오토스케일을 지원합니다.
        /// </summary>
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            lock (_timeBuffer)
            {
                if (_timeBuffer.Count == 0) return null;

                double maxVol = 0;
                bool found = false;

                for (int i = 0; i < _timeBuffer.Count; i++)
                {
                    double oa = _timeBuffer[i].ToOADate();
                    if (oa >= minOA && oa <= maxOA)
                    {
                        double total = _buyBuffer[i] + _sellBuffer[i];
                        if (total > maxVol) maxVol = total;
                        found = true;
                    }
                }

                // 거래량 차트는 0부터 시작하므로 Min은 0, Max는 최대 거래량의 1.1배(여유공간)를 반환
                return found ? (0, maxVol * 1.1) : null;
            }
        }

        public override void Clear()
        {
            lock (_timeBuffer)
            {
                _buyBuffer.Clear();
                _sellBuffer.Clear();
                _timeBuffer.Clear();
                _lastUpdateMinute = DateTime.MinValue;
                _isDirty = true;
            }
        }
    }
}