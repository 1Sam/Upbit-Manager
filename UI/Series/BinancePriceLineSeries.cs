using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Interfaces;
using System.Security.Cryptography;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 바이낸스 실시간 가격을 가져와 지렁이 선으로 표시하고, 업비트 가격과 비교하여 김프를 계산합니다.
    /// </summary>
    public class BinancePriceLineSeries : ChartSeriesBase, IChartSeries
    {
        // ─── 속성 정의 (IChartSeries 구현) ──────────────────────────────────────────

        public override ExchangeSource Source => ExchangeSource.Binance;
        public override SeriesType Type => SeriesType.PriceLine;

        // 에러 해결: AxisGroup이 정확히 Upbit_Manager.Models.Common.AxisGroup을 반환하도록 설정
        public override AxisGroup TargetGroup => AxisGroup.Price;

        public override string Label => "Binance - 실시간";
        public override bool DefaultOn => false;

        // ─── 데이터 버퍼 및 상태 ──────────────────────────────────────────

        private double _lastUpbitPrice = 0;
        private const int MAX_BUFFER = 2000;

        // 시간(OADate)과 가격을 쌍으로 저장하는 내부 버퍼
        private readonly List<(double TimeOA, double Price)> _buffer = new();

        // ─── 데이터 업데이트 로직 ──────────────────────────────────────────

        public override void UpdateData(object payload)
        {
            lock (_buffer)
            {
                // 1. 업비트 실시간 가격 수신 (MainController에서 튜플로 전달된 경우)
                if (payload is ValueTuple<double, ExchangeSource> data && data.Item2 == ExchangeSource.Upbit)
                {
                    _lastUpbitPrice = data.Item1;
                    return;
                }

                // 2. 바이낸스 실시간 가격 수신 (단일 double 값)
                if (payload is double binancePrice)
                {
                    // 중복 데이터 방지
                    if (_buffer.Count > 0 && _buffer.Last().Price == binancePrice) return;
                    _buffer.Add((DateTime.Now.ToOADate(), binancePrice));
                }
                // 3. 바이낸스 과거 데이터(History) 수신
                else if (payload is IEnumerable<(DateTime Time, double Price)> history)
                {
                    foreach (var h in history)
                    {
                        _buffer.Add((h.Time.ToOADate(), h.Price));
                    }
                }

                // 4. 버퍼 사이즈 제한
                if (_buffer.Count > MAX_BUFFER)
                {
                    var lastData = _buffer.OrderBy(x => x.TimeOA).TakeLast(MAX_BUFFER).ToList();
                    _buffer.Clear();
                    _buffer.AddRange(lastData);
                }
            }
        }

        // ─── 렌더링 로직 ──────────────────────────────────────────

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible) return;

            double lastBinancePrice = 0;
            double lastTimeOA = 0;
            double[] xs, ys;

            lock (_buffer)
            {
                if (_buffer.Count < 1) return;

                // 실시간성을 위해 현재 시간까지 선이 이어지도록 마지막 포인트 추가
                var displayList = _buffer.ToList();
                lastTimeOA = DateTime.Now.ToOADate();
                lastBinancePrice = displayList.Last().Price;
                displayList.Add((lastTimeOA, lastBinancePrice));

                xs = displayList.Select(b => b.TimeOA).ToArray();
                ys = displayList.Select(b => b.Price).ToArray();
            }

            // 1. 김프 계산 및 색상 결정
            ScottPlot.Color statusColor = Colors.Orange;
            string kimpText = "0.00%";

            if (_lastUpbitPrice > 0 && lastBinancePrice > 0)
            {
                double kimp = ((_lastUpbitPrice / lastBinancePrice) - 1) * 100;
                kimpText = $"{kimp:+0.00;-0.00;0.00}%";

                // 조건별 색상 로직 적용
                if (kimp >= 3.0) statusColor = Colors.Blue;        // 4% 이상 빨강
                else if (kimp > 2.0) statusColor = Colors.Yellow;  // 3% 이상 녹색
                else if (kimp > 1.5) statusColor = Colors.Green; // 2% 이상 노랑
                else if (kimp > 1.0) statusColor = Colors.Red; // 1% 이상 오렌지
                else statusColor = Colors.Orange.WithAlpha(0.6);   // 1% 미만 흐린 오렌지
            }

            // 2. 바이낸스 가격선 그리기
            var scatter = plot.Add.ScatterLine(xs, ys);
            scatter.Color = statusColor;
            scatter.LineWidth = 2;
            scatter.Axes.YAxis = targetAxis;


            //// 3. 김프 레이블 표시
            //if (_lastUpbitPrice > 0 && lastBinancePrice > 0)
            //{
            //    var txt = plot.Add.Text(kimpText, lastTimeOA, lastBinancePrice);

            //    txt.LabelFontName = "맑은 고딕";
            //    txt.LabelFontSize = 10;
            //    // 노랑 배경일 때만 검은 글씨 사용
            //    txt.LabelFontColor = (statusColor == Colors.Yellow) ? Colors.Black : Colors.White;
            //    txt.LabelBackgroundColor = statusColor;
            //    txt.LabelPadding = 2;
            //    txt.LabelBorderRadius = 3;
            //    txt.LabelAlignment = Alignment.MiddleLeft;
            //    txt.Axes.YAxis = targetAxis;
            //}

            // 3. 일반적인 방식: 현재가 수평선 및 우측 축 레이블
            // HorizontalLine은 차트 전체를 가로지르는 선을 만들고 우측에 레이블을 붙여줍니다.
            var hl = plot.Add.HorizontalLine(lastBinancePrice);
            hl.LinePattern = LinePattern.Dashed; // 점선으로 표시하여 보조지표임을 명시
            hl.LineWidth = 1;
            hl.Color = statusColor;

            // 우측 가격 축에 표시될 텍스트 설정
            hl.LabelText = kimpText;
            hl.LabelFontSize = 13;
            hl.LabelFontColor = (statusColor == Colors.Yellow) ? Colors.Black : Colors.White;
            hl.LabelBackgroundColor = statusColor;
            hl.TextAlignment = Alignment.MiddleLeft;
            hl.TextRotation = 0;
            hl.LabelOppositeAxis = true;
            hl.LabelStyle.OffsetX = 2;
            //hl.LabelAlignment = Alignment.MiddleLeft; // 가격 축 텍스트 정렬

            hl.Axes.YAxis = targetAxis;
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            lock (_buffer)
            {
                var visible = _buffer.Where(b => b.TimeOA >= minOA && b.TimeOA <= maxOA).Select(b => b.Price).ToList();
                return visible.Any() ? (visible.Min(), visible.Max()) : null;
            }
        }

        public override void Clear()
        {
            lock (_buffer)
            {
                _buffer.Clear();
                _lastUpbitPrice = 0;
            }
        }
    }
}