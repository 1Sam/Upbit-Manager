using System;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using Upbit_Manager.Models.Common;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 KRW-USDT 가격 라인 시리즈 (김치 프리미엄 확인용)
    /// </summary>
    public class UpbitUsdtLineSeries : ChartSeriesBase // ⬅️ 상속 구조로 변경
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.PriceLine;
        public override AxisGroup TargetGroup => AxisGroup.Price; // 상단 가격 패널 배치
        public override string Label => "Upbit - KRW-USDT";
        public override bool DefaultOn => false;

        private const int MAX_BUFFER = 3000;
        private readonly List<(DateTime Time, double Price)> _buffer = new List<(DateTime, double)>(MAX_BUFFER);

        public override void UpdateData(object payload)
        {
            lock (_buffer)
            {
                // 1. 히스토리 데이터 주입 (가장 중요)
                if (payload is List<(DateTime Time, double Price)> history)
                {
                    _buffer.Clear();
                    _buffer.AddRange(history);
                }
                // 2. 캔들 리스트 주입
                else if (payload is IEnumerable<CommonCandle> candles)
                {
                    _buffer.Clear();
                    _buffer.AddRange(candles.Select(c => (c.Time, c.Close)));
                }
                // 3. 실시간 틱 주입
                else if (payload is double currentPrice)
                {
                    // 실시간 데이터는 버퍼 끝에 추가
                    _buffer.Add((DateTime.Now, currentPrice));
                }

                // ─── 데이터 정제: 시간순 정렬 및 중복 제거 ───
                if (_buffer.Count > 0)
                {
                    var processed = _buffer
                        .Where(x => x.Price > 0)
                        .OrderBy(x => x.Time) // 시간순 정렬이 안 되면 선이 꼬입니다.
                        .GroupBy(x => x.Time.Ticks / TimeSpan.FromSeconds(1).Ticks) // 1초 단위 중복 제거
                        .Select(g => g.First())
                        .TakeLast(MAX_BUFFER)
                        .ToList();

                    _buffer.Clear();
                    _buffer.AddRange(processed);
                }
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible) return;

            double[] xs, ys;
            lock (_buffer)
            {
                if (_buffer.Count < 2) return;
                xs = _buffer.Select(x => x.Time.ToOADate()).ToArray();
                ys = _buffer.Select(x => x.Price).ToArray();
            }

            // 1. 스캐터 추가
            var scatter = plot.Add.ScatterLine(xs, ys); // ScatterLine은 선만 그릴 때 최적화됨
            scatter.Color = Colors.Gold.WithAlpha(0.8);
            scatter.LineWidth = 2;

            // 2. 우측 Y축(USDT 전용) 연결
            var rightAxis = plot.Axes.Right;
            rightAxis.IsVisible = true;
            rightAxis.Label.Text = "USDT (KRW)";
            scatter.Axes.YAxis = rightAxis;

            // 3. ⭐ 우측 Y축 범위(Range) 정상화 로직
            // 현재 화면에 보이는 X축 범위 내의 데이터만 추출하여 Y축 범위를 잡습니다.
            var xRange = plot.Axes.Bottom.Range;
            var visibleYs = _buffer
                .Where(d => d.Time.ToOADate() >= xRange.Min && d.Time.ToOADate() <= xRange.Max)
                .Select(d => d.Price)
                .ToList();

            if (visibleYs.Any())
            {
                double min = visibleYs.Min();
                double max = visibleYs.Max();
                double span = max - min;

                // 변동폭이 너무 적으면(수평선 방지) 상하로 2원 정도 공간 확보
                if (span < 1.0) span = 4.0;

                // 데이터 상하로 20% 여유를 둠 (비정상적으로 좁은 축 방지)
                rightAxis.Range.Set(min - (span * 0.5), max + (span * 0.5));
            }
        }

        private void ApplyDynamicRatioScaling(Plot plot, IYAxis rightAxis, double[] ys)
        {
            if (ys.Length == 0) return;

            // 왼쪽 가격 축의 범위를 가져와서 그 비율만큼 USDT 축도 벌려줌 (시각적 평행 유지)
            var leftRange = plot.Axes.Left.Range;
            double usdtLast = ys.Last();

            // 왼쪽 축의 현재 확장 비율(Span) 계산
            double leftSpan = leftRange.Max - leftRange.Min;
            double leftCenter = (leftRange.Max + leftRange.Min) / 2.0;

            // 비율 계산 (0 나누기 방지)
            double ratio = usdtLast / (leftCenter == 0 ? 1 : leftCenter);
            double smartSpan = leftSpan * ratio;

            rightAxis.Range.Set(usdtLast - smartSpan * 0.5, usdtLast + smartSpan * 0.5);
        }

        public override void Clear()
        {
            lock (_buffer) _buffer.Clear();
        }

        // 보조 지표이므로 메인 가격 차트의 Y축 자동 범위 계산에는 영향을 주지 않음
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA) => null;
    }
}