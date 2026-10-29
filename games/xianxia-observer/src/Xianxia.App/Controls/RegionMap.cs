// 13개 지역 약도. 서쪽은 청운종, 동쪽은 천마문, 가운데는 접경지다(원본은 지역을 글자 목록으로만 보여 줬다).
// 지역을 누르면 지역 시트를 연다. 원 안의 수는 거주자 수, 색은 지배 종문, 작은 표식은 비경(秘)·수조(獸).
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Xianxia.App.Theme;
using Xianxia.Simulation.Data;

namespace Xianxia.App.Controls
{
    /// <summary>약도에 그릴 지역 상태.</summary>
    /// <param name="Id">지역 번호.</param>
    /// <param name="ControllerSectId">지배 종문(0이면 중립).</param>
    /// <param name="Residents">거주자 수.</param>
    /// <param name="SecretRealm">비경이 열려 있는가.</param>
    /// <param name="BeastTide">수조가 일고 있는가.</param>
    internal readonly record struct MapRegion(int Id, int ControllerSectId, int Residents, bool SecretRealm,
        bool BeastTide);

    /// <summary>지역 약도.</summary>
    internal sealed class RegionMap : Control
    {
        private const double MapHeight = 250;
        private static readonly Typeface s_typeface = new(AppBuilderExtensions.DefaultFontFamily);
        private static readonly Typeface s_bold = new(AppBuilderExtensions.DefaultFontFamily, FontStyle.Normal,
            FontWeight.Bold);

        // 지역 번호 순서의 정규화 좌표(가로 0~1, 세로 0~1).
        private static readonly Point[] s_positions =
        {
            new(0.11, 0.42), new(0.29, 0.42), new(0.14, 0.76), new(0.14, 0.11), new(0.89, 0.42),
            new(0.71, 0.42), new(0.86, 0.76), new(0.86, 0.11), new(0.50, 0.42), new(0.62, 0.11),
            new(0.38, 0.11), new(0.62, 0.76), new(0.38, 0.76),
        };

        // 길(이웃 지역) 목록.
        private static readonly (int From, int To)[] s_roads =
        {
            (0, 1), (0, 3), (0, 2), (1, 10), (1, 8), (1, 12), (3, 10), (2, 12), (10, 8), (12, 8), (8, 9),
            (8, 11), (9, 5), (11, 5), (9, 7), (11, 6), (5, 4), (4, 7), (4, 6), (10, 9), (12, 11),
        };

        private readonly StackPanel _list = new() { Spacing = 6 };
        private readonly Button[] _regionButtons = new Button[RegionCatalog.All.Count];

        private IReadOnlyList<MapRegion> _regions = Array.Empty<MapRegion>();

        /// <summary>약도를 만든다.</summary>
        internal RegionMap()
        {
            _list.IsVisible = Typography.Scale >= 1.2;
            for (int id = 0; id < _regionButtons.Length; id++)
            {
                int selected = id;
                Button button = Ui.Button(RegionCatalog.All[id].Name, () => RegionTapped?.Invoke(selected), "card");
                AutomationProperties.SetAutomationId(button, "region-list-" + id);
                _regionButtons[id] = button;
                _list.Children.Add(button);
            }

            Tapped += OnTapped;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        /// <summary>지도와 동등한 연결을 제공하는 확대 가능한 지역 목록.</summary>
        internal Control ListView => _list;

        /// <summary>지역 목록을 접거나 펼친다.</summary>
        internal void ToggleList() => _list.IsVisible = !_list.IsVisible;

        /// <summary>지역을 눌렀을 때(지역 번호).</summary>
        internal event Action<int>? RegionTapped;

        /// <summary>지역 상태를 바꾼다.</summary>
        /// <param name="regions">지역 번호 순서의 상태.</param>
        internal void Set(IReadOnlyList<MapRegion> regions)
        {
            _regions = regions;
            foreach (MapRegion region in regions)
            {
                _regionButtons[region.Id].Content = (region.Id + 1) + " · " + RegionCatalog.All[region.Id].Name
                    + " · " + region.Residents + "명" + (region.SecretRealm ? " · 비경" : "")
                    + (region.BeastTide ? " · 수조" : "");
            }
            InvalidateVisual();
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            Pen road = new(Palette.Tint(Palette.Accent, 0x30), 2, new DashStyle(new double[] { 2, 2 }, 0));
            foreach ((int from, int to) in s_roads)
            {
                context.DrawLine(road, Center(from), Center(to));
            }

            DrawSideLabel(context, "청운종 영지", 0.02, Palette.Orthodox, alignRight: false);
            DrawSideLabel(context, "천마문 영지", 0.98, Palette.Demonic, alignRight: true);
            foreach (MapRegion region in _regions)
            {
                DrawRegion(context, region);
            }
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, MapHeight + (20 * Typography.Scale));
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
            if (Typography.Scale >= 1.2)
            {
                _list.IsVisible = true;
            }

            InvalidateVisual();
        }

        private static double Radius(int id)
        {
            return RegionCatalog.All[id].Kind == RegionKind.Center ? 21 : 17;
        }

        private static void Text(DrawingContext context, string text, Point center, double size, Color color,
            bool bold)
        {
            FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                bold ? s_bold : s_typeface, Math.Max(13, size) * Typography.Scale, Palette.Brush(color));
            context.DrawText(formatted, new Point(center.X - (formatted.Width / 2),
                center.Y - (formatted.Height / 2)));
        }

        private Point Center(int id)
        {
            Point position = s_positions[id];
            double width = Math.Max(100, Bounds.Width);
            return new Point(position.X * width, 10 + (position.Y * (MapHeight - 40)));
        }

        private void DrawSideLabel(DrawingContext context, string text, double x, Color color, bool alignRight)
        {
            double fontSize = 13 * Typography.Scale;
            FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                s_typeface, fontSize, Palette.Brush(color));
            double left = (x * Bounds.Width) - (alignRight ? formatted.Width : 0);
            context.DrawText(formatted, new Point(left, MapHeight - 14));
        }

        private void DrawRegion(DrawingContext context, MapRegion region)
        {
            Point center = Center(region.Id);
            double radius = Radius(region.Id);
            Color color = region.ControllerSectId == 0 ? Palette.Rogue : Palette.Sect(region.ControllerSectId);
            RegionDefinition definition = RegionCatalog.All[region.Id];
            context.DrawEllipse(Palette.Brush(Palette.PanelBack), null, center, radius, radius);
            Pen ring = new(Palette.Brush(color), definition.Kind == RegionKind.Border ? 2.5 : 1.5,
                definition.Kind == RegionKind.Border ? new DashStyle(new double[] { 3, 1.5 }, 0) : null);
            context.DrawEllipse(Palette.Tint(color, 0x38), ring, center, radius, radius);
            Text(context, (Typography.Scale >= 1.2 ? region.Id + 1 : region.Residents)
                .ToString(CultureInfo.InvariantCulture), center, 13, Palette.Ink, true);
            if (Typography.Scale < 1.2)
            {
                Text(context, definition.Name, new Point(center.X, center.Y + radius + 10), 13, Palette.Ink, false);
            }
            if (region.SecretRealm)
            {
                DrawMarker(context, "秘", new Point(center.X + radius - 2, center.Y - radius + 2), Palette.Of(
                    LogKind.SecretRealm));
            }

            if (region.BeastTide)
            {
                DrawMarker(context, "獸", new Point(center.X - radius + 2, center.Y - radius + 2), Palette.Danger);
            }
        }

        private static void DrawMarker(DrawingContext context, string glyph, Point center, Color color)
        {
            if (Typography.Scale >= 1.2)
            {
                context.DrawEllipse(Palette.Brush(color), null, center, 5, 5);
                return;
            }

            context.DrawEllipse(Palette.Brush(color), new Pen(Palette.Brush(Palette.PanelBack), 1.5), center, 8, 8);
            Text(context, glyph, center, 9, Palette.OnAccent, true);
        }

        private void OnTapped(object? sender, TappedEventArgs e)
        {
            Point point = e.GetPosition(this);
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int id = 0; id < s_positions.Length; id++)
            {
                Vector offset = point - Center(id);
                double distance = Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = id;
                }
            }

            if (best >= 0 && bestDistance <= Radius(best) + 16)
            {
                RegionTapped?.Invoke(best);
            }
        }
    }
}
