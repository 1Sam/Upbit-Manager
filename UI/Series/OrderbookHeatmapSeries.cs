using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Core.Orderbook;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;

// 🔥 ScottPlot.Color vs System.Drawing.Color 충돌 방지
using SColor = ScottPlot.Color;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 오더북 히스토리를 가격-시간 히트맵으로 렌더링 (4번째 패널 전용)
    ///
    /// UpbitOrderbookSeries (3번째, Type=Orderbook, 실시간 호가창) 와의 차이:
    /// - 이 시리즈는 Type=OrderbookHeatmap 으로 ChartManager에서 _heatmapPlot(4번째)에 라우팅됨
    /// - X축(시간)을 _pricePlot과 공유하므로 캔들 차트와 시간이 일치함
    /// - Y축은 가격축 (호가 가격 범위)
    ///
    /// 색상 의미:
    /// - 노랑/흰색: 활성 잔량 (밝을수록 많음)
    /// - 초록:      체결로 소멸한 진짜 지지/저항
    /// - 빨강:      Spoofing 의심 (체결 없이 소멸)
    /// </summary>
    public sealed class OrderbookHeatmapSeries : ChartSeriesBase
    {
        // 🔥 Type = OrderbookHeatmap → ChartManager UpdateUI()에서 _heatmapPlot(4번째)으로 라우팅
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.OrderbookHeatmap;
        public override string Label => "Upbit - 오더북 히트맵";
        public override bool DefaultOn => true;
        // 🔥 AxisGroup.OrderbookHeatmap — ChartManager는 Type 기준으로 분기하므로
        //    TargetGroup은 실질적으로 사용되지 않지만 명시적으로 구분해 둠
        public override AxisGroup TargetGroup => AxisGroup.OrderbookHeatmap;

        private readonly OrderbookHeatmapEngine _engine;
        private readonly object _lock = new();
        private bool _isDirty = true;
        private List<HeatmapCell> _cachedCells = new();

        public OrderbookHeatmapSeries(OrderbookHeatmapEngine engine)
        {
            _engine = engine;
        }

        public override void UpdateData(object payload)
        {
            if (payload is OrderbookSnapshot snapshot)
            {
                _engine.ProcessSnapshot(snapshot);
                _isDirty = true;
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            if (!IsVisible) return;

            List<HeatmapCell> cells;

            lock (_lock)
            {
                if (_isDirty)
                {
                    // 🔥 현재 X축(시간) 범위 안의 셀만 가져와 렌더링 성능 최적화
                    var xRange = plot.Axes.Bottom.Range;
                    _cachedCells = _engine.GetCells(xRange.Min, xRange.Max);
                    _isDirty = false;
                }
                cells = _cachedCells;
            }

            if (cells.Count == 0) return;

            double maxVolume = cells.Max(c => c.Volume);
            if (maxVolume <= 0) return;

            // 🔥 시간 버킷 절반 크기 (OADate 단위) — 셀의 X 방향 너비
            double halfTimeBucket = TimeSpan.FromSeconds(
                _engine.TimeBucketSeconds / 2.0).TotalDays;

            // 🔥 가격 버킷 절반 크기 — 셀의 Y 방향 높이
            double halfPriceBucket = _engine.PriceBucketSize * 0.5;

            foreach (var cell in cells)
            {
                double alpha = Math.Clamp(cell.Volume / maxVolume, 0.05, 1.0);
                SColor color = GetCellColor(cell, alpha);

                // 🔥 UpbitOrderbookSeries는 Bar를 썼지만,
                //    히트맵은 Rectangle로 (시간 X 가격) 2D 셀을 그림
                var rect = plot.Add.Rectangle(
                    cell.TimeOA - halfTimeBucket,   // xMin
                    cell.TimeOA + halfTimeBucket,   // xMax
                    cell.Price - halfPriceBucket,  // yMin
                    cell.Price + halfPriceBucket); // yMax

                rect.FillStyle.Color = color;
                rect.LineStyle.Width = 0;           // 테두리 없음 (셀 밀도 표현에 불필요)
                rect.Axes.YAxis = targetAxis;
            }
        }

        /// <summary>
        /// 셀 상태에 따라 색상 결정
        /// alpha는 잔량 정규화 값 (0.05 ~ 1.0)
        /// </summary>
        private static SColor GetCellColor(HeatmapCell cell, double alpha)
        {
            return cell.State switch
            {
                // 체결로 소멸 → 초록 (진짜 지지/저항이었음)
                HeatmapCellState.FilledAndGone =>
                    Colors.LimeGreen.WithAlpha(alpha * 0.8),

                // 체결 없이 소멸 → 주황빨강 (Spoofing 의심)
                HeatmapCellState.SpoofingSuspect =>
                    Colors.OrangeRed.WithAlpha(alpha * 0.8),

                // 활성 잔량 → 잔량 많을수록 흰색, 적을수록 노란색
                _ => alpha > 0.7
                    ? Colors.White.WithAlpha(alpha * 0.6)
                    : Colors.Yellow.WithAlpha(alpha * 0.5)
            };
        }

        public override void Clear()
        {
            lock (_lock)
            {
                _engine.Clear();
                _cachedCells.Clear();
                _isDirty = true;
            }
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            var cells = _engine.GetCells(minOA, maxOA);
            if (cells.Count == 0) return null;

            return (cells.Min(c => c.Price), cells.Max(c => c.Price));
        }
    }
}