// 상세 시트 층(원본 Modal·SectModal). 시트를 쌓아 두고 뒤로 가기(단추·Android 뒤로 키)로 하나씩 닫는다.
// 시트 바깥의 어두운 바탕을 누르면 맨 위 시트를 닫는다.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Xianxia.App.Controls;
using Xianxia.App.Theme;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>상세 시트 층.</summary>
    internal sealed class SheetHost
    {
        private readonly List<SheetBase> _stack = new();
        private readonly Func<SheetLink, SheetBase?> _factory;
        private readonly Grid _root;
        private readonly Border _card;
        private readonly TextBlock _title;
        private readonly Button _back;
        private readonly ContentControl _tabs = new();
        private readonly ScrollViewer _scroll;
        private readonly Dictionary<SheetBase, Vector> _offsets = new();

        /// <summary>시트 층을 만든다.</summary>
        /// <param name="factory">연결에 맞는 시트를 만드는 함수.</param>
        internal SheetHost(Func<SheetLink, SheetBase?> factory)
        {
            _factory = factory;
            _title = Ui.Text(string.Empty, 15, Palette.Accent, bold: true);
            _title.VerticalAlignment = VerticalAlignment.Center;
            _title.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
            _title.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            _back = Ui.Button("‹ 뒤로", Back, "ghost");
            Button close = Ui.Button("닫기 ×", CloseAll, "ghost");
            Grid titleBar = Ui.Columns("Auto,*,Auto", _back, _title, close);
            _tabs.Margin = new Thickness(0, 6, 0, 2);
            _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            DockPanel layout = new() { LastChildFill = true };
            DockPanel.SetDock(titleBar, Dock.Top);
            DockPanel.SetDock(_tabs, Dock.Top);
            layout.Children.Add(titleBar);
            layout.Children.Add(_tabs);
            layout.Children.Add(_scroll);
            _card = new Border
            {
                Background = Palette.Brush(Palette.PanelBack),
                BorderBrush = Palette.Brush(Palette.Accent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 12),
                Margin = new Thickness(8, 12, 8, 12),
                Child = layout,
            };
            _root = new Grid { Background = Palette.Brush(Palette.Backdrop), IsVisible = false };
            _root.Children.Add(_card);
            _root.PointerPressed += OnBackdropPressed;
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _root;

        /// <summary>열린 시트가 있는가.</summary>
        internal bool IsOpen => _stack.Count > 0;

        /// <summary>맨 위 시트(없으면 null).</summary>
        internal SheetBase? Top => _stack.Count == 0 ? null : _stack[^1];

        /// <summary>안전 영역 여백을 반영한다.</summary>
        /// <param name="insets">여백.</param>
        internal void SetInsets(Thickness insets)
        {
            _card.Margin = new Thickness(8, 12 + insets.Top, 8, 12 + insets.Bottom);
        }

        /// <summary>시트를 연다. 맨 위 시트와 같은 연결이면 무시한다.</summary>
        /// <param name="link">연결.</param>
        /// <returns>열었으면 true.</returns>
        internal bool Open(SheetLink link)
        {
            if (!link.IsSet || (Top is { } top && top.Link == link))
            {
                return false;
            }

            SheetBase? sheet = _factory(link);
            if (sheet is null)
            {
                return false;
            }

            if (Top is { } previous)
            {
                _offsets[previous] = _scroll.Offset;
            }

            _stack.Add(sheet);
            Show(sheet, Vector.Zero);
            return true;
        }

        /// <summary>맨 위 시트를 닫는다.</summary>
        internal void Back()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            _stack.RemoveAt(_stack.Count - 1);
            if (Top is { } top)
            {
                Vector offset = _offsets.TryGetValue(top, out Vector saved) ? saved : Vector.Zero;
                _offsets.Remove(top);
                Show(top, offset);
                return;
            }

            _root.IsVisible = false;
            _scroll.Content = null;
            _tabs.Content = null;
        }

        /// <summary>모든 시트를 닫는다.</summary>
        internal void CloseAll()
        {
            _stack.Clear();
            _offsets.Clear();
            _root.IsVisible = false;
            _scroll.Content = null;
            _tabs.Content = null;
        }

        /// <summary>맨 위 시트를 다시 그린다.</summary>
        /// <param name="world">세계.</param>
        internal void Refresh(World world)
        {
            if (Top is not { } top)
            {
                return;
            }

            top.Refresh(world);
            _title.Text = top.Title;
        }

        private void Show(SheetBase sheet, Vector offset)
        {
            _root.IsVisible = true;
            _back.IsVisible = _stack.Count > 1;
            _tabs.Content = sheet.Tabs;
            _tabs.IsVisible = sheet.Tabs is not null;
            _scroll.Content = sheet.Body;
            _title.Text = sheet.Title;
            _scroll.Offset = offset;
        }

        private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ReferenceEquals(e.Source, _root))
            {
                Back();
                e.Handled = true;
            }
        }
    }
}
