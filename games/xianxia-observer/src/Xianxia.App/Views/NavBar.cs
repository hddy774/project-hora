// 하단 탭 막대(원본 .xnav). 탭마다 한자 표식과 이름을 둔다.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
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
            for (int index = 0; index < Destinations.Primary.Length; index++)
            {
                int position = Destinations.Primary[index];
                Button button = CreateButton(pages[position], position, select);
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

        /// <summary>한 목적지의 표식과 읽을 수 있는 이름을 함께 둔다.</summary>
        private static Button CreateButton(PageBase page, int position, Action<int> select)
        {
            TextBlock glyph = new()
            {
                Text = page.Glyph,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            TextBlock name = new()
            {
                Text = page.Name,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Typography.Bind(glyph, TextBlock.FontSizeProperty, 17);
            Typography.Bind(name, TextBlock.FontSizeProperty, 13);
            name.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            Button button = Ui.Button(Ui.Column(0, glyph, name), () => select(position), "nav");
            AutomationProperties.SetAutomationId(button, "nav-" + Destinations.FromPage(position));
            AutomationProperties.SetName(button, page.Name);
            return button;
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _root;

        /// <summary>선택 표시를 바꾼다.</summary>
        /// <param name="selected">선택된 탭.</param>
        internal void Select(int selected)
        {
            for (int index = 0; index < _buttons.Count; index++)
            {
                _buttons[index].Classes.Set("active", Destinations.Primary[index] == (selected >= 4 ? 7 : selected));
            }
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
