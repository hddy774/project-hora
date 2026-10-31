// 목록 한 줄(원본 AgCard·AchCard·로그 카드). 순위 원·제목·부제·배지·오른쪽 수치를 한 모양으로 통일했다.
// [성능] 줄 객체를 재사용하고 갱신 때는 글자·색만 바꾼다. 누르면 연결된 상세 시트를 연다.
using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Theme;
using Xianxia.App.Views;

namespace Xianxia.App.Controls
{
    /// <summary>목록 한 줄의 내용.</summary>
    internal readonly record struct RowModel
    {
        /// <summary>제목.</summary>
        internal string Title { get; init; }

        /// <summary>제목 색(없으면 먹색).</summary>
        internal Color? TitleColor { get; init; }

        /// <summary>부제(흐린 글자).</summary>
        internal string? Subtitle { get; init; }

        /// <summary>왼쪽 원 안의 글자(순위 등).</summary>
        internal string? Lead { get; init; }

        /// <summary>왼쪽 원 색.</summary>
        internal Color LeadColor { get; init; }

        /// <summary>오른쪽 굵은 값.</summary>
        internal string? Value { get; init; }

        /// <summary>오른쪽 값 색.</summary>
        internal Color? ValueColor { get; init; }

        /// <summary>오른쪽 값 아래 설명.</summary>
        internal string? Caption { get; init; }

        /// <summary>왼쪽 막대 색(없으면 막대 없음).</summary>
        internal Color? Accent { get; init; }

        /// <summary>배지.</summary>
        internal BadgeSpec[]? Badges { get; init; }

        /// <summary>눌렀을 때 열 시트.</summary>
        internal SheetLink Link { get; init; }
    }

    /// <summary>재사용하는 목록 줄.</summary>
    internal sealed class InfoRow
    {
        private readonly Border _accent;
        private readonly Border _lead;
        private readonly TextBlock _leadText;
        private readonly TextBlock _title;
        private readonly TextBlock _subtitle;
        private readonly BadgeStrip _badges = new();
        private readonly TextBlock _value;
        private readonly TextBlock _caption;
        private readonly StackPanel _right;
        private SheetLink _link;

        /// <summary>줄을 만든다.</summary>
        /// <param name="open">연결을 여는 동작.</param>
        internal InfoRow(Action<SheetLink> open)
        {
            _accent = new Border { Width = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 7, 0) };
            _leadText = new TextBlock
            {
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Typography.Bind(_leadText, TextBlock.FontSizeProperty, 13);
            _lead = new Border
            {
                MinWidth = 28,
                MinHeight = 28,
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _leadText,
            };
            _title = Ui.Text(string.Empty, 12.5, bold: true);
            _subtitle = Ui.Muted(string.Empty, 10.5);
            StackPanel middle = Ui.Column(2, _title, _subtitle, _badges.View);
            middle.VerticalAlignment = VerticalAlignment.Center;
            _value = Ui.Text(string.Empty, 12, Palette.Accent, bold: true);
            _value.HorizontalAlignment = HorizontalAlignment.Right;
            _value.TextWrapping = TextWrapping.NoWrap;
            _caption = Ui.Muted(string.Empty, 9.5);
            _caption.HorizontalAlignment = HorizontalAlignment.Right;
            _caption.TextWrapping = TextWrapping.NoWrap;
            _right = Ui.Column(1, _value, _caption);
            _right.Margin = new Thickness(6, 0, 0, 0);
            _right.VerticalAlignment = VerticalAlignment.Center;
            Grid grid = Ui.Columns("Auto,Auto,*,Auto", _accent, _lead, middle, _right);
            View = Ui.Button(grid, () => open(_link), "card");
        }

        /// <summary>화면 요소.</summary>
        internal Button View { get; }

        /// <summary>내용을 바꾼다.</summary>
        /// <param name="model">내용.</param>
        internal void Update(in RowModel model)
        {
            _link = model.Link;
            AutomationProperties.SetAutomationId(View, "link-" + model.Link.Kind + "-" + model.Link.Id);
            View.IsHitTestVisible = model.Link.IsSet;
            _accent.IsVisible = model.Accent is not null;
            if (model.Accent is { } accent)
            {
                _accent.Background = Palette.Brush(accent);
            }

            _lead.IsVisible = model.Lead is not null;
            if (model.Lead is not null)
            {
                _leadText.Text = model.Lead;
                _leadText.Foreground = Palette.Brush(model.LeadColor == default ? Palette.Muted : model.LeadColor);
                _lead.Background = Palette.Tint(model.LeadColor == default ? Palette.Muted : model.LeadColor, 0x26);
            }

            _title.Text = model.Title;
            _title.Foreground = Palette.Brush(model.TitleColor ?? Palette.Ink);
            _subtitle.Text = model.Subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(model.Subtitle);
            _badges.Set(model.Badges);
            _value.Text = model.Value;
            _value.Foreground = Palette.Brush(model.ValueColor ?? Palette.Accent);
            _caption.Text = model.Caption;
            _caption.IsVisible = !string.IsNullOrEmpty(model.Caption);
            _right.IsVisible = model.Value is not null || model.Caption is not null;
        }
    }
}
