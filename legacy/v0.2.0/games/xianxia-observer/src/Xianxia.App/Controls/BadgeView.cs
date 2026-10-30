// 둥근 배지(원본 Bdg). 같은 배지를 재사용하며 글자와 색만 바꾼다.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>배지 내용.</summary>
    /// <param name="Text">글자.</param>
    /// <param name="Color">색.</param>
    internal readonly record struct BadgeSpec(string Text, Color Color);

    /// <summary>둥근 배지.</summary>
    internal sealed class BadgeView : Border
    {
        private readonly TextBlock _text;

        /// <summary>빈 배지를 만든다.</summary>
        internal BadgeView()
        {
            CornerRadius = new CornerRadius(3);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(8, 3);
            Margin = new Thickness(0, 0, 3, 3);
            _text = Ui.Text("", 13, bold: true);
            Child = _text;
        }

        /// <summary>글자와 색을 정한 배지를 만든다.</summary>
        /// <param name="spec">내용.</param>
        internal BadgeView(BadgeSpec spec)
            : this()
        {
            Set(spec);
        }

        /// <summary>글자와 색을 바꾼다.</summary>
        /// <param name="spec">내용.</param>
        internal void Set(BadgeSpec spec)
        {
            _text.Text = spec.Text;
            _text.Foreground = Palette.Brush(spec.Color);
            Background = Palette.Tint(spec.Color, 0x1C);
            BorderBrush = Palette.Tint(spec.Color, 0x5A);
        }
    }

    /// <summary>배지 여러 개를 줄바꿈해 늘어놓는 묶음. 배지 객체를 재사용한다.</summary>
    internal sealed class BadgeStrip
    {
        private readonly WrapPanel _panel = new();

        /// <summary>화면 요소.</summary>
        internal Control View => _panel;

        /// <summary>배지 목록을 바꾼다.</summary>
        /// <param name="specs">배지들(없으면 숨김).</param>
        internal void Set(BadgeSpec[]? specs)
        {
            int count = specs?.Length ?? 0;
            while (_panel.Children.Count < count)
            {
                _panel.Children.Add(new BadgeView());
            }

            for (int index = 0; index < _panel.Children.Count; index++)
            {
                BadgeView badge = (BadgeView)_panel.Children[index];
                badge.IsVisible = index < count;
                if (index < count)
                {
                    badge.Set(specs![index]);
                }
            }

            _panel.IsVisible = count > 0;
        }
    }
}
