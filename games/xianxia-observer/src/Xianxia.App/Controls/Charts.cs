// 통계 그래프: 여러 계열 꺾은선(종문 세력 추이)과 누적 띠(경지 구성 추이). 원본에는 시간 추이 그래프가 없었다.
// [성능] 표본은 최대 수백 개라 매번 전부 그려도 가볍다. 데이터가 바뀔 때만 다시 그린다.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>꺾은선 계열.</summary>
    /// <param name="Name">이름(범례).</param>
    /// <param name="Color">색.</param>
    /// <param name="Values">값(가로축 표본 순서).</param>
    internal sealed record ChartSeries(string Name, Color Color, IReadOnlyList<double> Values);

    /// <summary>그래프 공통: 축 글자·범례.</summary>
    internal abstract class ChartBase : Control
    {
        private static double LabelSize => 13 * Typography.Scale;
        private static readonly Typeface s_typeface = new(AppBuilderExtensions.DefaultFontFamily);

        /// <summary>가로축 처음·끝 연도.</summary>
        protected (int First, int Last) Years { get; set; }

        /// <summary>그래프 높이.</summary>
        protected double PlotHeight { get; init; } = 120;

        /// <summary>왼쪽 축 글자 칸 너비.</summary>
        protected static double AxisWidth => 60 * Typography.Scale;

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, PlotHeight + (LabelSize * (Typography.Scale >= 1.5 ? 3 : 1.6)));
        }

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Typography.Changed += Reflow;
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            Typography.Changed -= Reflow;
            base.OnDetachedFromVisualTree(e);
        }

        private void Reflow()
        {
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>글자를 그린다.</summary>
        /// <param name="context">그리기 문맥.</param>
        /// <param name="text">글자.</param>
        /// <param name="origin">왼쪽 위.</param>
        /// <param name="color">색.</param>
        /// <param name="alignRight">오른쪽 맞춤이면 origin.X가 오른쪽 끝.</param>
        /// <param name="maximumWidth">빈 상태 문장의 줄바꿈 너비.</param>
        protected static void Label(DrawingContext context, string text, Point origin, Color color,
            bool alignRight = false, double maximumWidth = double.PositiveInfinity)
        {
            FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, s_typeface,
                LabelSize, Palette.Brush(color));
            if (double.IsFinite(maximumWidth))
            {
                formatted.MaxTextWidth = maximumWidth;
            }

            double x = alignRight ? origin.X - formatted.Width : origin.X;
            context.DrawText(formatted, new Point(x, origin.Y));
        }

        /// <summary>가로축 연도와 바닥선을 그린다.</summary>
        /// <param name="context">그리기 문맥.</param>
        /// <param name="plot">그래프 영역.</param>
        protected void DrawYearAxis(DrawingContext context, Rect plot)
        {
            Pen axis = new(Palette.Tint(Palette.Ink, 0x40), 1);
            context.DrawLine(axis, plot.BottomLeft, plot.BottomRight);
            Label(context, Years.First.ToString("#,0", CultureInfo.InvariantCulture) + "년",
                new Point(plot.Left, plot.Bottom + 2), Palette.Muted);
            Label(context, Years.Last.ToString("#,0", CultureInfo.InvariantCulture) + "년",
                new Point(plot.Right, plot.Bottom + 2 + (Typography.Scale >= 1.5 ? LabelSize * 1.4 : 0)),
                Palette.Muted, alignRight: true);
        }
    }

    /// <summary>여러 계열 꺾은선 그래프.</summary>
    internal sealed class LineChart : ChartBase
    {
        private IReadOnlyList<ChartSeries> _series = Array.Empty<ChartSeries>();
        private Func<double, string> _format = value => value.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>그래프를 만든다.</summary>
        /// <param name="height">그래프 높이.</param>
        internal LineChart(double height = 110)
        {
            PlotHeight = height;
        }

        /// <summary>데이터를 바꾼다.</summary>
        /// <param name="series">계열.</param>
        /// <param name="firstYear">첫 표본 연도.</param>
        /// <param name="lastYear">마지막 표본 연도.</param>
        /// <param name="format">세로축 값 표시.</param>
        internal void Set(IReadOnlyList<ChartSeries> series, int firstYear, int lastYear, Func<double, string> format)
        {
            _series = series;
            _format = format;
            Years = (firstYear, lastYear);
            InvalidateVisual();
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            Rect plot = new(AxisWidth, 4, Math.Max(10, Bounds.Width - AxisWidth - 4), PlotHeight - 4);
            double max = 0;
            int count = 0;
            foreach (ChartSeries series in _series)
            {
                count = Math.Max(count, series.Values.Count);
                foreach (double value in series.Values)
                {
                    max = Math.Max(max, value);
                }
            }

            max = max <= 0 ? 1 : max * 1.08;
            Pen grid = new(Palette.Tint(Palette.Ink, 0x18), 1);
            for (int line = 0; line <= 2; line++)
            {
                double y = plot.Bottom - (plot.Height * line / 2);
                context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
                Label(context, _format(max * line / 2), new Point(plot.Left - 3, y - 6), Palette.Muted, true);
            }

            DrawYearAxis(context, plot);
            if (count < 2)
            {
                Label(context, "표본이 쌓이면 그려집니다.", new Point(plot.Left + 6, plot.Top + 6), Palette.Muted,
                    maximumWidth: Math.Max(10, plot.Width - 12));
                return;
            }

            foreach (ChartSeries series in _series)
            {
                DrawSeries(context, plot, series, count, max);
            }
        }

        private static void DrawSeries(DrawingContext context, Rect plot, ChartSeries series, int count, double max)
        {
            StreamGeometry geometry = new();
            using (StreamGeometryContext stream = geometry.Open())
            {
                for (int index = 0; index < series.Values.Count; index++)
                {
                    Point point = new(plot.Left + (plot.Width * index / (count - 1)),
                        plot.Bottom - (plot.Height * series.Values[index] / max));
                    if (index == 0)
                    {
                        stream.BeginFigure(point, false);
                    }
                    else
                    {
                        stream.LineTo(point);
                    }
                }

                stream.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(Palette.Brush(series.Color), 1.8), geometry);
        }
    }

    /// <summary>누적 띠 그래프: 표본마다 각 층의 비율을 쌓는다(경지 구성 추이).</summary>
    internal sealed class StackChart : ChartBase
    {
        private IReadOnlyList<IReadOnlyList<int>> _samples = Array.Empty<IReadOnlyList<int>>();
        private IReadOnlyList<Color> _colors = Array.Empty<Color>();

        /// <summary>그래프를 만든다.</summary>
        /// <param name="height">그래프 높이.</param>
        internal StackChart(double height = 120)
        {
            PlotHeight = height;
        }

        /// <summary>데이터를 바꾼다.</summary>
        /// <param name="samples">표본마다 층별 인원.</param>
        /// <param name="colors">층 색(아래층부터).</param>
        /// <param name="firstYear">첫 표본 연도.</param>
        /// <param name="lastYear">마지막 표본 연도.</param>
        internal void Set(IReadOnlyList<IReadOnlyList<int>> samples, IReadOnlyList<Color> colors, int firstYear,
            int lastYear)
        {
            _samples = samples;
            _colors = colors;
            Years = (firstYear, lastYear);
            InvalidateVisual();
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            Rect plot = new(AxisWidth, 4, Math.Max(10, Bounds.Width - AxisWidth - 4), PlotHeight - 4);
            Label(context, "100%", new Point(plot.Left - 3, plot.Top - 2), Palette.Muted, true);
            Label(context, "0%", new Point(plot.Left - 3, plot.Bottom - 10), Palette.Muted, true);
            DrawYearAxis(context, plot);
            int count = _samples.Count;
            if (count < 2)
            {
                Label(context, "표본이 쌓이면 그려집니다.", new Point(plot.Left + 6, plot.Top + 6), Palette.Muted,
                    maximumWidth: Math.Max(10, plot.Width - 12));
                return;
            }

            double columnWidth = plot.Width / count;
            for (int index = 0; index < count; index++)
            {
                IReadOnlyList<int> layers = _samples[index];
                double total = 0;
                foreach (int value in layers)
                {
                    total += value;
                }

                if (total <= 0)
                {
                    continue;
                }

                double bottom = plot.Bottom;
                double left = plot.Left + (columnWidth * index);
                for (int layer = 0; layer < layers.Count && layer < _colors.Count; layer++)
                {
                    double height = plot.Height * layers[layer] / total;
                    if (height <= 0)
                    {
                        continue;
                    }

                    context.FillRectangle(Palette.Brush(_colors[layer]),
                        new Rect(left, bottom - height, columnWidth + 0.6, height));
                    bottom -= height;
                }
            }
        }
    }
}
