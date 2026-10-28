// 하단 탭 막대(원본 .xnav). 탭마다 한자 표식과 이름을 둔다.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Xianxia.App.Controls;
using Xianxia.App.Theme;

namespace Xianxia.App.Views
{
    /// <summary>하단 탭 막대.</summary>
    internal sealed class NavBar
    {
        private readonly List<Button> _buttons = new();
        private readonly Border _root;

        /// <summary>탭 막대를 만든다.</summary>
        /// <param name="pages">탭 화면들.</param>
        /// <param name="select">탭을 골랐을 때(위치).</param>
        internal NavBar(IReadOnlyList<PageBase> pages, Action<int> select)
        {
            UniformGrid grid = new() { Rows = 1 };
            for (int index = 0; index < pages.Count; index++)
            {
                int position = index;
                TextBlock glyph = new()
                {
                    Text = pages[index].Glyph,
                    FontSize = 17,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                TextBlock name = new()
                {
                    Text = pages[index].Name,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                Button button = Ui.Button(Ui.Column(0, glyph, name), () => select(position), "nav");
                _buttons.Add(button);
                grid.Children.Add(button);
            }

            _root = new Border
            {
                Background = Palette.Brush(Palette.HeaderBack),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = grid,
            };
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _root;

        /// <summary>선택 표시를 바꾼다.</summary>
        /// <param name="selected">선택된 탭.</param>
        internal void Select(int selected)
        {
            for (int index = 0; index < _buttons.Count; index++)
            {
                _buttons[index].Classes.Set("active", index == selected);
            }
        }

        /// <summary>안전 영역(내비게이션 바) 여백을 반영한다.</summary>
        /// <param name="bottom">아래 여백.</param>
        internal void SetBottomInset(double bottom)
        {
            _root.Padding = new Thickness(0, 0, 0, bottom);
        }

        /// <summary>탭 단추(시험용).</summary>
        /// <param name="index">위치.</param>
        /// <returns>단추.</returns>
        internal Button ButtonAt(int index)
        {
            return _buttons[index];
        }
    }
}
