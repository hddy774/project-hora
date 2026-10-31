// 직접 그리는 막대 두 종류: 진행 막대(원본 B)와 접경지 세력 줄다리기 막대(원본 세력도).
// [성능] 값이 바뀔 때만 다시 그린다. 그리기는 사각형 두세 개라 수백 개를 띄워도 가볍다.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>가로 진행 막대.</summary>
    internal sealed class Meter : Control
    {
        private readonly double _thickness;
        private double _ratio;
        private Color _color = Palette.Accent;

        /// <summary>막대를 만든다.</summary>
        /// <param name="thickness">두께.</param>
        internal Meter(double thickness = 5)
        {
            _thickness = thickness;
        }

        /// <summary>비율과 색을 바꾼다.</summary>
        /// <param name="ratio">0~1.</param>
        /// <param name="color">채움 색.</param>
        internal void Set(double ratio, Color color)
        {
            double clamped = double.IsFinite(ratio) ? Math.Clamp(ratio, 0, 1) : 0;
            if (Math.Abs(clamped - _ratio) < 0.001 && color == _color)
            {
                return;
            }

            _ratio = clamped;
            _color = color;
            InvalidateVisual();
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            double width = Bounds.Width;
            double radius = _thickness / 2;
            Rect track = new(0, 0, width, _thickness);
            context.DrawRectangle(Palette.Brush(Palette.Track), null, track, radius, radius);
            if (_ratio > 0)
            {
                double fill = Math.Max(_thickness, width * _ratio);
                context.DrawRectangle(Palette.Brush(_color), null, new Rect(0, 0, fill, _thickness), radius, radius);
            }
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, _thickness);
        }
    }

    /// <summary>
    /// 줄다리기 막대: 가운데에서 양쪽으로 뻗는다. 대결 카드와 같은 쪽에 두려고 양수(청운종)는 왼쪽,
    /// 음수(천마문)는 오른쪽으로 그린다.
    /// 점령 문턱(±4)에 눈금을 그린다.
    /// </summary>
    internal sealed class TugBar : Control
    {
        private const double Thickness = 8;
        private int _value;
        private int _limit = 10;
        private int _threshold = 4;

        /// <summary>값을 바꾼다.</summary>
        /// <param name="value">현재 값.</param>
        /// <param name="limit">양쪽 끝 값.</param>
        /// <param name="threshold">점령 문턱.</param>
        internal void Set(int value, int limit, int threshold)
        {
            if (value == _value && limit == _limit && threshold == _threshold)
            {
                return;
            }

            _value = value;
            _limit = Math.Max(1, limit);
            _threshold = threshold;
            InvalidateVisual();
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            double width = Bounds.Width;
            double center = width / 2;
            context.DrawRectangle(Palette.Brush(Palette.Track), null, new Rect(0, 0, width, Thickness), 4, 4);
            double half = width / 2;
            double extent = half * Math.Min(1, Math.Abs(_value) / (double)_limit);
            if (_value > 0)
            {
                context.DrawRectangle(Palette.Brush(Palette.Orthodox), null,
                    new Rect(center - extent, 0, extent, Thickness));
            }
            else if (_value < 0)
            {
                context.DrawRectangle(Palette.Brush(Palette.Demonic), null, new Rect(center, 0, extent, Thickness));
            }

            Pen tick = new(Palette.Tint(Palette.Ink, 0x60), 1);
            double offset = half * _threshold / _limit;
            context.DrawLine(tick, new Point(center - offset, -1), new Point(center - offset, Thickness + 1));
            context.DrawLine(tick, new Point(center + offset, -1), new Point(center + offset, Thickness + 1));
            Pen middle = new(Palette.Tint(Palette.Ink, 0x80), 2);
            context.DrawLine(middle, new Point(center, -1), new Point(center, Thickness + 1));
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, Thickness + 2);
        }
    }
}
