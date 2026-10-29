// 키-값 줄 목록(원본 KV). 줄을 위치로 갱신하고 남는 줄은 숨긴다.
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>키-값 목록.</summary>
    internal sealed class KeyValueList
    {
        private readonly StackPanel _panel = new();
        private readonly List<(Border Row, TextBlock Label, TextBlock Value)> _rows = new();
        private int _used;

        /// <summary>화면 요소.</summary>
        internal Control View => _panel;

        /// <summary>갱신을 시작한다. 이후 <see cref="Add"/>로 채운 줄만 보인다.</summary>
        internal void Begin()
        {
            _used = 0;
        }

        /// <summary>다음 줄을 채운다.</summary>
        /// <param name="label">이름.</param>
        /// <param name="value">값.</param>
        /// <param name="color">값 색(없으면 먹색).</param>
        internal void Add(string label, string value, Color? color = null)
        {
            if (_used == _rows.Count)
            {
                TextBlock name = Ui.Muted(string.Empty, 11.5);
                name.VerticalAlignment = VerticalAlignment.Center;
                TextBlock text = Ui.Text(string.Empty, 12, bold: true);
                text.TextAlignment = TextAlignment.Right;
                text.Margin = new Thickness(10, 0, 0, 0);
                Border row = new()
                {
                    BorderBrush = Palette.Tint(Palette.Accent, 0x1A),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 4),
                    Child = Ui.Columns("Auto,*", name, text),
                };
                _rows.Add((row, name, text));
                _panel.Children.Add(row);
            }

            (Border border, TextBlock labelBlock, TextBlock valueBlock) = _rows[_used];
            border.IsVisible = true;
            labelBlock.Text = label;
            valueBlock.Text = value;
            valueBlock.Foreground = Palette.Brush(color ?? Palette.Ink);
            _used++;
        }

        /// <summary>갱신을 마치고 쓰지 않은 줄을 숨긴다.</summary>
        internal void End()
        {
            for (int index = _used; index < _rows.Count; index++)
            {
                _rows[index].Row.IsVisible = false;
            }
        }
    }
}
