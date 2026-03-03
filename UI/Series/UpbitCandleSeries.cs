using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using Upbit_Manager.Core;
using Upbit_Manager.Core.Alarms;
using Upbit_Manager.Core.Collections;
using Upbit_Manager.Interfaces;
using Upbit_Manager.Models.Common;
using Upbit_Manager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    /// <summary>
    /// 업비트 캔들 데이터를 관리하고 ScottPlot에 최적화된 방식으로 렌더링하는 클래스입니다.
    /// 배열 캐싱, Dirty 플래그, Plottable 객체 재사용을 통해 GC 부하를 최소화합니다.
    /// </summary>
    public class UpbitCandleSeries : ChartSeriesBase, IVolumeDataProvider
    {
        // ─── 속성 정의 (IChartSeries 구현) ──────────────────────────────────────────

        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.Candle;
        public override string Label => "Upbit - 캔들";
        public override bool DefaultOn => true;
        public override AxisGroup TargetGroup => AxisGroup.Price;

        // ─── 데이터 버퍼 및 캐싱 상태 ──────────────────────────────────────────

        private const int MAX_BUFFER = 3000;

        // 실시간 데이터를 저장하는 순환 버퍼 (Core/RingBuffer.cs 기반)
        private readonly RingBuffer<OHLC> _ohlcBuffer = new(MAX_BUFFER);
        private readonly RingBuffer<double> _volumeBuffer = new(MAX_BUFFER);

        // ⭐ 렌더링 최적화를 위한 배열 캐시
        private readonly OHLC[] _cachedOhlcArray = new OHLC[MAX_BUFFER];

        // ⭐ ScottPlot 5 전달용 고정 리스트 (매번 new List를 하지 않음)
        private readonly List<OHLC> _renderList = new(MAX_BUFFER);

        // 데이터 변경 여부를 확인하여 불필요한 복사 연산을 방지하는 플래그
        private bool _arrayDirty = true;

        // ⭐ ScottPlot 플로팅 객체 캐싱 (객체 재사용)
        private CandlestickPlot? _candlePlotObject;
        private HorizontalLine? _priceLineObject;

        private DateTime _lastCandleTime = DateTime.MinValue;
        public double LastPrice { get; private set; }
        public double PrevPrice { get; private set; }

        // ─── 데이터 업데이트 로직 ──────────────────────────────────────────

        /// <summary>
        /// 초기 데이터 로드 또는 실시간 틱 데이터를 수신하여 버퍼를 갱신합니다.
        /// </summary>
        public override void UpdateData(object payload)
        {
            lock (_ohlcBuffer)
            {
                if (payload is List<CommonCandle> candles)
                {
                    // 1. 과거 캔들 데이터 초기화 및 로드
                    _ohlcBuffer.Clear();
                    _volumeBuffer.Clear();

                    foreach (var c in candles)
                    {
                        _ohlcBuffer.Add(new OHLC(c.Open, c.High, c.Low, c.Close, c.Time, TimeSpan.FromMinutes(1)));
                        _volumeBuffer.Add(c.Volume);
                    }

                    if (candles.Count > 0)
                    {
                        var last = candles[^1];
                        _lastCandleTime = last.Time;
                        LastPrice = last.Close;
                        PrevPrice = LastPrice;
                    }
                }
                else if (payload is UpbitRealtimePayload rt)
                {
                    // 2. 실시간 체결 데이터 처리
                    PrevPrice = LastPrice;
                    LastPrice = rt.Price;

                    DateTime now = DateTime.Now;
                    DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

                    if (_ohlcBuffer.Count > 0 && _lastCandleTime == currentMinute)
                    {
                        // 기존 현재분 캔들 업데이트
                        var last = _ohlcBuffer.Last;
                        var updated = new OHLC(
                            last.Open,
                            Math.Max(last.High, rt.Price),
                            Math.Min(last.Low, rt.Price),
                            rt.Price,
                            currentMinute,
                            TimeSpan.FromMinutes(1)
                        );
                        _ohlcBuffer.UpdateLast(updated);
                        _volumeBuffer.UpdateLast(_volumeBuffer.Last + rt.Volume);
                    }
                    else
                    {
                        // 새로운 분봉 시작
                        _ohlcBuffer.Add(new OHLC(rt.Price, rt.Price, rt.Price, rt.Price, currentMinute, TimeSpan.FromMinutes(1)));
                        _volumeBuffer.Add(rt.Volume);
                        _lastCandleTime = currentMinute;
                    }
                }

                // ⭐ 데이터가 변경되었으므로 다음 Render 시점에 복사가 필요함을 표시
                _arrayDirty = true;
            }
        }

        // ─── 렌더링 로직 ──────────────────────────────────────────

        /// <summary>
        /// ScottPlot에 캔들과 현재가선을 렌더링합니다. 
        /// Dirty 플래그를 사용하여 CPU와 메모리 사용량을 최적화합니다.
        /// </summary>
        public override void Render(Plot plot, IYAxis targetAxis)
        {
            lock (_ohlcBuffer)
            {
                if (_ohlcBuffer.Count == 0) return;

                // 3. ⭐ 데이터가 Dirty인 경우에만 캐시 배열 업데이트 및 리스트 재구성
                if (_arrayDirty)
                {
                    // RingBuffer 내용을 배열로 고속 복사 (Linq 사용 안 함)
                    int count = 0;
                    foreach (var item in _ohlcBuffer)
                    {
                        if (count >= MAX_BUFFER) break;
                        _cachedOhlcArray[count++] = item;
                    }

                    // ScottPlot 5의 Candlestick이 참조하는 고정 리스트의 데이터 갱신
                    _renderList.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        _renderList.Add(_cachedOhlcArray[i]);
                    }

                    _arrayDirty = false;
                }

                // 4. ⭐ 캔들스틱 객체 재사용 (매번 plot.Add 하지 않음)
                // 수정됨: ScottPlot 5에서는 plot.GetPlottables()를 통해 열거하거나 
                // 단순히 null 체크와 함께 plot에 현재 등록되어 있는지 확인해야 합니다.
                if (_candlePlotObject == null || !plot.GetPlottables().Contains(_candlePlotObject))
                {
                    // 최초 1회 생성 시 _renderList 참조를 전달 (이후 리스트 내용만 바뀌면 자동 반영됨)
                    _candlePlotObject = plot.Add.Candlestick(_renderList);
                    _candlePlotObject.RisingColor = Colors.Red;
                    _candlePlotObject.FallingColor = Colors.Blue;
                    _candlePlotObject.Sequential = false;
                    _candlePlotObject.Axes.YAxis = targetAxis;
                }

                // 5. ⭐ 현재가 라인 객체 재사용
                if (LastPrice > 0)
                {
                    // 수정됨: plot.Plottables 직접 접근 대신 GetPlottables() 사용
                    if (_priceLineObject == null || !plot.GetPlottables().Contains(_priceLineObject))
                    {
                        _priceLineObject = plot.Add.HorizontalLine(LastPrice);
                        _priceLineObject.Axes.YAxis = targetAxis;
                        _priceLineObject.LineStyle.Width = 1;
                        _priceLineObject.LinePattern = LinePattern.Dashed;
                        //_priceLineObject.LabelOppositeAxis = true;
                        _priceLineObject.LabelFontColor = Colors.White;
                    }

                    // 값 및 스타일 업데이트 (객체는 유지)
                    _priceLineObject.Y = LastPrice;
                    _priceLineObject.Text = LastPrice.ToString("N0");
                    _priceLineObject.LineStyle.Color = Colors.Yellow.WithAlpha(0.6);
                    _priceLineObject.LabelBackgroundColor = LastPrice >= PrevPrice ? Colors.Red : Colors.Blue;
                    _priceLineObject.TextAlignment = Alignment.MiddleLeft;
                    _priceLineObject.TextRotation = 0;
                    _priceLineObject.LabelOppositeAxis = true;
                    _priceLineObject.LabelStyle.OffsetX = 2;
                }
            }
        }

        /// <summary>
        /// 데이터를 초기화하고 렌더링 객체들을 해제합니다.
        /// </summary>
        public override void Clear()
        {
            lock (_ohlcBuffer)
            {
                _ohlcBuffer.Clear();
                _volumeBuffer.Clear();
                _renderList.Clear();
                _lastCandleTime = DateTime.MinValue;
                LastPrice = PrevPrice = 0;

                // 캐싱된 플로팅 객체 참조 해제
                _candlePlotObject = null;
                _priceLineObject = null;
                _arrayDirty = true;
            }
        }

        /// <summary>
        /// 화면에 보이는 X축 범위 내에서 Y축 스케일을 계산합니다.
        /// </summary>
        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            lock (_ohlcBuffer)
            {
                double high = double.MinValue;
                double low = double.MaxValue;
                bool found = false;

                // 버퍼 직접 순회 (할당 없음)
                foreach (var o in _ohlcBuffer)
                {
                    double oa = o.DateTime.ToOADate();
                    if (oa >= minOA && oa <= maxOA)
                    {
                        if (o.High > high) high = o.High;
                        if (o.Low < low) low = o.Low;
                        found = true;
                    }
                }

                return found ? (low, high) : null;
            }
        }

        // ─── IVolumeDataProvider 구현 (알람 엔진 연동) ──────────────────────────────────

        /// <summary>
        /// 인덱서를 사용하여 힙 할당 없이 평균 거래량을 계산합니다.
        /// </summary>
        public double GetAverageVolume(int lookbackCount)
        {
            lock (_ohlcBuffer)
            {
                int totalCount = _volumeBuffer.Count;
                if (totalCount < 2) return 0;

                // 마지막 봉(진행중인 봉)은 제외하고 계산
                int actualLookback = Math.Min(lookbackCount, totalCount - 1);
                double sum = 0;

                // RingBuffer 인덱서를 사용하여 직접 접근 (ToList() 제거)
                for (int i = 0; i < actualLookback; i++)
                {
                    // 끝에서 두 번째부터 거꾸로 lookbackCount 만큼 합산
                    sum += _volumeBuffer[totalCount - 2 - i];
                }

                return sum / actualLookback;
            }
        }

        /// <summary>
        /// 현재 실시간으로 쌓이고 있는 캔들의 거래량을 반환합니다.
        /// </summary>
        public double GetCurrentCandleVolume()
        {
            lock (_ohlcBuffer)
            {
                return _volumeBuffer.Count > 0 ? _volumeBuffer.Last : 0;
            }
        }
    }
}