// 코드로 화면을 짜는 작은 도우미. 원본의 패널(.xp)·카드(.xc)·제목·키-값(KV) 모양을 같은 크기와 색으로 만든다.
// [성능] 화면은 한 번 만들고 갱신 때는 글자·색만 바꾼다. 여기서 만든 컨트롤은 대부분 처음 한 번만 생성된다.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>컨트롤 생성 도우미.</summary>
    internal static class Ui
    {
        /// <summary>화면 가장자리 여백.</summary>
        internal const double Gutter = 16;

        private const string ButtonThemeKey = "SeonhyeopButton";
        private static ControlTheme? s_buttonTheme;

        /// <summary>단추 테마(Theme/Controls.axaml).</summary>
        internal static ControlTheme? ButtonTheme
        {
            get
            {
                if (s_buttonTheme is null && Application.Current is { } application
                    && application.TryGetResource(ButtonThemeKey, null, out object? value))
                {
                    s_buttonTheme = value as ControlTheme;
                }

                return s_buttonTheme;
            }
        }

        /// <summary>글자.</summary>
        /// <param name="text">내용.</param>
        /// <param name="size">크기.</param>
        /// <param name="color">색(없으면 상속).</param>
        /// <param name="bold">굵게.</param>
        /// <returns>글자 블록.</returns>
        internal static TextBlock Text(string text = "", double size = 15, Color? color = null, bool bold = false)
        {
            TextBlock block = new()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
            };
            Typography.Bind(block, TextBlock.FontSizeProperty, size);
            if (color is { } value)
            {
                block.Foreground = Palette.Brush(value);
            }

            if (bold)
            {
                block.FontWeight = FontWeight.Bold;
            }

            return block;
        }

        /// <summary>흐린 보조 글자.</summary>
        /// <param name="text">내용.</param>
        /// <param name="size">크기.</param>
        /// <returns>글자 블록.</returns>
        internal static TextBlock Muted(string text = "", double size = 13)
        {
            return Text(text, size, Palette.Muted);
        }

        /// <summary>패널 제목(굵은 갈색).</summary>
        /// <param name="text">제목.</param>
        /// <returns>글자 블록.</returns>
        internal static TextBlock Title(string text)
        {
            return Text(text, 20, Palette.Ink, bold: true);
        }

        /// <summary>패널(원본 .xp): 연한 양피지 바탕, 둥근 테두리.</summary>
        /// <param name="child">내용.</param>
        /// <returns>테두리.</returns>
        internal static Border Panel(Control child)
        {
            return new Border
            {
                Background = Palette.Brush(Palette.PanelBack),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(12),
                Child = child,
            };
        }

        /// <summary>제목이 있는 패널.</summary>
        /// <param name="title">제목.</param>
        /// <param name="children">내용.</param>
        /// <returns>패널.</returns>
        internal static Border Section(string title, params Control[] children)
        {
            StackPanel body = Column(6, Title(title));
            foreach (Control child in children)
            {
                body.Children.Add(child);
            }

            return Panel(body);
        }

        /// <summary>카드(원본 .xc): 조금 진한 바탕.</summary>
        /// <param name="child">내용.</param>
        /// <param name="accent">왼쪽 굵은 테두리 색(없으면 일반 테두리).</param>
        /// <returns>테두리.</returns>
        internal static Border Card(Control child, Color? accent = null)
        {
            Border card = new()
            {
                Background = Palette.Brush(Palette.CardBack),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(12, 10),
                Child = child,
            };
            if (accent is { } color)
            {
                card.BorderBrush = Palette.Tint(color, 0x80);
                card.BorderThickness = new Thickness(3, 1, 1, 1);
            }

            return card;
        }

        /// <summary>세로 묶음.</summary>
        /// <param name="spacing">간격.</param>
        /// <param name="children">내용.</param>
        /// <returns>묶음.</returns>
        internal static StackPanel Column(double spacing, params Control[] children)
        {
            StackPanel panel = new() { Spacing = spacing };
            panel.Children.AddRange(children);
            return panel;
        }

        /// <summary>가로 묶음.</summary>
        /// <param name="spacing">간격.</param>
        /// <param name="children">내용.</param>
        /// <returns>묶음.</returns>
        internal static StackPanel Row(double spacing, params Control[] children)
        {
            StackPanel panel = new() { Spacing = spacing, Orientation = Orientation.Horizontal };
            panel.Children.AddRange(children);
            return panel;
        }

        /// <summary>열 정의대로 자식을 차례로 배치한 격자.</summary>
        /// <param name="columns">열 정의(예: "*,Auto").</param>
        /// <param name="children">열마다 하나씩.</param>
        /// <returns>격자.</returns>
        internal static Grid Columns(string columns, params Control[] children)
        {
            Grid grid = new() { ColumnDefinitions = new ColumnDefinitions(columns) };
            for (int index = 0; index < children.Length; index++)
            {
                Grid.SetColumn(children[index], index);
                grid.Children.Add(children[index]);
            }

            return grid;
        }

        /// <summary>같은 너비 칸으로 나눈 격자(원본 grid repeat(n,1fr)).</summary>
        /// <param name="count">칸 수.</param>
        /// <param name="spacing">칸 사이 간격.</param>
        /// <param name="children">내용.</param>
        /// <returns>격자.</returns>
        internal static Grid Cells(int count, double spacing, params Control[] children)
        {
            Grid grid = new() { ColumnSpacing = spacing, RowSpacing = spacing };
            for (int column = 0; column < count; column++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            int rows = (children.Length + count - 1) / Math.Max(1, count);
            for (int row = 0; row < rows; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (int index = 0; index < children.Length; index++)
            {
                Grid.SetColumn(children[index], index % count);
                Grid.SetRow(children[index], index / count);
                grid.Children.Add(children[index]);
            }

            return grid;
        }

        /// <summary>단추.</summary>
        /// <param name="content">글자.</param>
        /// <param name="onClick">눌렀을 때.</param>
        /// <param name="classes">테마 분류(small·card·nav·ghost·primary·active).</param>
        /// <returns>단추.</returns>
        internal static Button Button(string content, Action onClick, params string[] classes)
        {
            Button button = new() { Content = content, Theme = ButtonTheme };
            Typography.Bind(button, TemplatedControl.FontSizeProperty, Array.IndexOf(classes, "small") >= 0 ? 13 : 15);
            button.Classes.AddRange(classes);
            button.Click += (_, _) => onClick();
            return button;
        }

        /// <summary>내용이 컨트롤인 단추(카드형 항목).</summary>
        /// <param name="content">내용.</param>
        /// <param name="onClick">눌렀을 때.</param>
        /// <param name="classes">테마 분류.</param>
        /// <returns>단추.</returns>
        internal static Button Button(Control content, Action onClick, params string[] classes)
        {
            Button button = new() { Content = content, Theme = ButtonTheme };
            Typography.Bind(button, TemplatedControl.FontSizeProperty, Array.IndexOf(classes, "small") >= 0 ? 13 : 15);
            button.Classes.AddRange(classes);
            button.Click += (_, _) => onClick();
            return button;
        }

        /// <summary>얇은 구분선.</summary>
        /// <returns>선.</returns>
        internal static Border Divider()
        {
            return new Border
            {
                Height = 1,
                Background = Palette.Brush(Palette.Line),
                Margin = new Thickness(0, 2),
            };
        }

        /// <summary>작은 수치 칸(원본 헤더 통계·종문 3칸): 위에 이름, 아래에 굵은 값.</summary>
        /// <param name="label">이름.</param>
        /// <param name="value">값 글자 블록(갱신용으로 바깥에서 보관).</param>
        /// <returns>칸.</returns>
        internal static Border Stat(string label, TextBlock value)
        {
            value.FontWeight = FontWeight.Bold;
            Typography.Bind(value, TextBlock.FontSizeProperty, 15);
            value.HorizontalAlignment = HorizontalAlignment.Center;
            value.TextWrapping = TextWrapping.Wrap;
            TextBlock name = Muted(label, 10);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextWrapping = TextWrapping.Wrap;
            return new Border
            {
                Background = Palette.Tint(Palette.Accent, 0x14),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(4, 4),
                Child = Column(0, name, value),
            };
        }
    }
}
