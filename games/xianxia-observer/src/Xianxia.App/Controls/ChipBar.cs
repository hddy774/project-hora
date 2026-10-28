// 칩 선택 막대(원본 정렬·필터·하위 탭 단추 줄). 하나만 선택되며, 분류별 색이 있으면 선택된 칩을 그 색으로 칠한다.
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>칩 선택 막대.</summary>
    internal sealed class ChipBar
    {
        private readonly List<Button> _buttons = new();
        private readonly IReadOnlyList<Color>? _colors;
        private readonly Action<int> _changed;

        /// <summary>칩 막대를 만든다.</summary>
        /// <param name="labels">칩 이름.</param>
        /// <param name="changed">선택이 바뀌었을 때(새 위치).</param>
        /// <param name="colors">선택 시 칠할 색(칩마다, 없으면 갈색).</param>
        /// <param name="stretch">칸을 같은 너비로 채울지(하위 탭).</param>
        internal ChipBar(IReadOnlyList<string> labels, Action<int> changed, IReadOnlyList<Color>? colors = null,
            bool stretch = false)
        {
            _changed = changed;
            _colors = colors;
            Panel panel = stretch ? new UniformGrid { Rows = 1 } : new WrapPanel();
            for (int index = 0; index < labels.Count; index++)
            {
                int position = index;
                Button button = Ui.Button(labels[index], () => Select(position, notify: true), "small");
                button.Margin = stretch ? new Avalonia.Thickness(1.5, 0) : new Avalonia.Thickness(0, 0, 4, 4);
                if (stretch)
                {
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.Padding = new Avalonia.Thickness(0, 5);
                }

                _buttons.Add(button);
                panel.Children.Add(button);
            }

            View = panel;
            Select(0, notify: false);
        }

        /// <summary>화면 요소.</summary>
        internal Control View { get; }

        /// <summary>선택된 위치.</summary>
        internal int Selected { get; private set; }

        /// <summary>칩을 선택한다.</summary>
        /// <param name="index">위치.</param>
        /// <param name="notify">변경 알림을 보낼지.</param>
        internal void Select(int index, bool notify)
        {
            Selected = Math.Clamp(index, 0, _buttons.Count - 1);
            for (int position = 0; position < _buttons.Count; position++)
            {
                Button button = _buttons[position];
                bool active = position == Selected;
                if (_colors is not null && position < _colors.Count)
                {
                    Paint(button, active, _colors[position]);
                }
                else
                {
                    button.Classes.Set("active", active);
                }
            }

            if (notify)
            {
                _changed(Selected);
            }
        }

        /// <summary>칩 이름을 바꾼다(건수 표시 등).</summary>
        /// <param name="index">위치.</param>
        /// <param name="label">이름.</param>
        internal void Rename(int index, string label)
        {
            _buttons[index].Content = label;
        }

        private static void Paint(Button button, bool active, Color color)
        {
            if (active)
            {
                button.Background = Palette.Brush(color);
                button.BorderBrush = Palette.Brush(color);
                button.Foreground = Palette.Brush(Palette.OnAccent);
                button.FontWeight = FontWeight.Bold;
                return;
            }

            button.Background = Brushes.Transparent;
            button.BorderBrush = Palette.Tint(color, 0x55);
            button.Foreground = Palette.Brush(color);
            button.FontWeight = FontWeight.Normal;
        }
    }
}
