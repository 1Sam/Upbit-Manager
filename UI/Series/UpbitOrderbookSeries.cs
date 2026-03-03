using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Upbit;
using Upbit_Manager.Models.Common;
using System.Diagnostics;
//using Upbit_Manager.Core.Orderbook;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 호가창 데이터를 가로형 바 차트로 렌더링합니다.
    /// </summary>
    public class UpbitOrderbookSeries : ChartSeriesBase
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Orderbook;
        public override string Label => "Upbit - 호가창";
        public override bool DefaultOn => true;
        // [수정] ChartManager에서 분기 처리를 위해 명확히 Orderbook 지정
        public override AxisGroup TargetGroup => AxisGroup.Orderbook;

        private UpbitOrderbookPayload? _lastPayload;
        private readonly List<Bar> _bars = new(30);
        private bool _isDirty = true;
        private ScottPlot.Plottables.BarPlot? _barPlotObject;
        private readonly object _lock = new();

        // 캐시 엔진 참조 (옵션)
        //private OrderbookCacheEngine? _cacheEngine;


        public override void UpdateData(object payload)
        {
            if (payload is UpbitOrderbookPayload ob)  // 튜플 제거, 원래대로
            {
                lock (_lock)
                {
                    // UpbitOrderbookPayload로 변환하거나
                    _lastPayload = ob; 
                    _isDirty = true;
                    // 컬렉터로부터 데이터가 오는지 확인하는 생명선 로그
                    //Debug.WriteLine($"[Orderbook] Sync: Units={ob.Units.Length} TotalAsk={ob.TotalAskSize}");


                }
            }
        }


        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_lock)
            {
                if (_lastPayload == null || _lastPayload.Units.Length == 0)
                {
                    if (_barPlotObject != null)
                    {
                        plot.Remove(_barPlotObject);
                        _barPlotObject = null;
                    }
                    return;
                }

                if (_isDirty)
                {
                    UpdateBarList();
                    _isDirty = false;
                }

                // 플롯 객체 관리
                if (_barPlotObject == null || !plot.GetPlottables().Contains(_barPlotObject))
                {
                    _barPlotObject = plot.Add.Bars(_bars);
                    _barPlotObject.Horizontal = true; // 가로형 바 (X: 잔량, Y: 가격)
                    _barPlotObject.Axes.YAxis = targetAxis;
                }

                // [중요] 호가창 전용 축 설정
                // 매번 렌더링 시점에 가격(Y축) 범위를 데이터에 맞게 강제 조정합니다.
                if (_bars.Count > 0)
                {
                    double minPrice = _bars.Min(b => b.Position);
                    double maxPrice = _bars.Max(b => b.Position);
                    double priceRange = maxPrice - minPrice;

                    // 위아래로 5% 여유를 두어 가격이 잘리지 않게 함
                    if (priceRange > 0)
                    {
                        targetAxis.Range.Set(minPrice - (priceRange * 0.05), maxPrice + (priceRange * 0.05));
                    }

                    // X축(잔량)은 항상 0부터 최대 잔량의 부호 반전까지 자동 조절
                    plot.Axes.AutoScaleX();
                }
            }

        }

        private void UpdateBarList()
        {
            if (_lastPayload == null) return;

            _bars.Clear();

            // 호가 데이터를 정렬하여 리스트 생성
            foreach (var unit in _lastPayload.Units)
            {

                // 데이터 확인
                //Debug.WriteLine($"Ask: {unit.AskPrice} / {unit.AskSize} | Bid: {unit.BidPrice} / {unit.BidSize}");

                // 매도 호가 (양수 방향)
                _bars.Add(new Bar
                {
                    Position = unit.AskPrice,
                    Value = unit.AskSize,
                    FillColor = Colors.Blue.WithAlpha(0.6),
                    LineColor = Colors.Blue,
                    LineWidth = 1
                });

                // 매수 호가 (음수 방향)
                _bars.Add(new Bar
                {
                    Position = unit.BidPrice,
                    Value = -unit.BidSize,
                    FillColor = Colors.Red.WithAlpha(0.6),
                    LineColor = Colors.Red,
                    LineWidth = 1
                });
            }
        }

        public override void Clear()
        {
            lock (_lock)
            {
                _lastPayload = null;
                _bars.Clear();
                _barPlotObject = null;
                _isDirty = true;
            }
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            // 호가창은 시간축(OA)에 영향을 받지 않으므로 전체 범위 반환
            lock (_lock)
            {
                if (_lastPayload == null || !_lastPayload.Units.Any()) return null;
                var p = _lastPayload.Units;
                return (p.Min(x => x.BidPrice), p.Max(x => x.AskPrice));
            }
        }
    }
}