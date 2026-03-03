using ScottPlot;

namespace Upbit_Manager.UI
{
    /// <summary>
    /// 좌측 2단 + 우측 3단 5분할 레이아웃
    /// </summary>
    public sealed class FivePanelLayout : IMultiplotLayout
    {
        public PixelRect[] GetSubplotRectangles(SubplotCollection subplots, PixelRect figureRect)
        {
            PixelRect[] rects = new PixelRect[5];

            float leftWidth = figureRect.Width * 0.75f;
            float rightWidth = figureRect.Width * 0.25f;
            float leftTopHeight = figureRect.Height * 0.75f;


            rects[0] = new PixelRect(leftWidth, leftTopHeight)
                .WithDelta(figureRect.Left, figureRect.Top);

            rects[1] = new PixelRect(leftWidth, figureRect.Height - leftTopHeight)
                .WithDelta(figureRect.Left, figureRect.Top + leftTopHeight);

            float rightPanelHeight = figureRect.Height / 3f;

            rects[2] = new PixelRect(rightWidth, rightPanelHeight)
                .WithDelta(figureRect.Left + leftWidth, figureRect.Top);

            rects[3] = new PixelRect(rightWidth, rightPanelHeight)
                .WithDelta(figureRect.Left + leftWidth, figureRect.Top + rightPanelHeight);

            rects[4] = new PixelRect(rightWidth, figureRect.Height - (rightPanelHeight * 2))
                .WithDelta(figureRect.Left + leftWidth, figureRect.Top + (rightPanelHeight * 2));

            return rects;
        }
    }
}