using ScottPlot;
using Upbit_Manager.Models.Common;
using UpbitManager.Models.Upbit;

namespace Upbit_Manager.UI.Series
{
    public class VolumeLimitSeries : ChartSeriesBase
    {
        public override ExchangeSource Source => ExchangeSource.Upbit;
        public override SeriesType Type => SeriesType.VolumeLimit;
        public override AxisGroup TargetGroup => AxisGroup.Volume;
        public override string Label => "거래량 알람 기준선";
        public override bool DefaultOn => true;

        private double _currentThreshold = 0;

        public override void UpdateData(object payload)
        {
            // 1. [객체지향 방식] payload가 어떤 타입인지 스스로 확인합니다.
            // 케이스 A: 실시간 틱 데이터가 들어오는 경우
            // { Price = 403, Volume = 100, ... } 같은 데이터는 이 시리즈의 관심사가 아닙니다.
            if (payload is UpbitRealtimePayload)
            {
                return;
            }

            // 케이스 B: MainController 등에서 계산되어 넘어온 '기준 수치' (double)
            // 이 시리즈는 오직 'double' 타입의 데이터만 자신의 'Threshold'로 취급합니다.
            if (payload is double threshold)
            {
                _currentThreshold = threshold;
            }
        }

        public override void Render(Plot plot, IYAxis targetAxis)
        {
            // 1. 표시 조건 확인
            if (!IsVisible || _currentThreshold <= 0) return;

            // 2. ⭐ ScottPlot 5 스타일: HorizontalLine 직접 생성 및 추가
            var line = plot.Add.HorizontalLine(_currentThreshold);

            // 3. 축 설정 (보내주신 LeftAxis 범위에 맞게 설정)
            line.Axes.YAxis = targetAxis;

            // 4. 스타일 설정 (바이낸스 시리즈 참고)
            line.Color = Colors.Orange.WithAlpha(0.8);
            line.LinePattern = LinePattern.Dashed; 
            line.LineWidth = 2;

            // 5. 우측 레이블 표시 설정
            line.Text = $"기준: {_currentThreshold:N0}";
            line.LabelStyle.FontName = "Malgun Gothic";
            line.LabelStyle.FontSize = 10;
            line.LabelStyle.BackgroundColor = Colors.Orange;
            line.LabelStyle.ForeColor = Colors.White;
            line.LabelStyle.Padding = 2;

            // 레이블을 차트 우측 끝에 붙이기 (선택 사항)
            line.LabelAlignment = Alignment.MiddleLeft;
        }

        public override (double Min, double Max)? GetPriceRange(double minOA, double maxOA)
        {
            // 기준선이 너무 높아서 차트가 작게 보이지 않도록 null을 반환하거나,
            // 선이 항상 화면 안에 보길 원하면 아래 주석을 해제하세요.
            // if (!IsVisible || _currentThreshold <= 0) return null;
            // return (_currentThreshold, _currentThreshold);
            return null;
        }

        public override void Clear() => _currentThreshold = 0;
    }
}