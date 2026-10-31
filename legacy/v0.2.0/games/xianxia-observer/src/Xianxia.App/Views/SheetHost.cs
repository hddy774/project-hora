// 상세 시트 층(원본 Modal·SectModal). 시트를 쌓아 두고 뒤로 가기(단추·Android 뒤로 키)로 하나씩 닫는다.
// 시트 바깥의 어두운 바탕을 누르면 맨 위 시트를 닫는다.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Xianxia.App.Controls;
using Xianxia.App.Services;
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
        private readonly INavigator _navigator;
        private readonly Button _play;
        private readonly TextBlock _state = Ui.Muted();

        /// <summary>시트 층을 만든다.</summary>
        /// <param name="factory">연결에 맞는 시트를 만드는 함수.</param>
        /// <param name="navigator">시트 안에서 현재 진행을 확인·정지하는 연결.</param>
        internal SheetHost(INavigator navigator, Func<SheetLink, SheetBase?> factory)
        {
            _navigator = navigator;
            _factory = factory;
            _title = Ui.Text(string.Empty, 20, Palette.Ink, bold: true);
            _title.VerticalAlignment = VerticalAlignment.Center;
            _back = Ui.Button("‹ 뒤로", Back, "ghost");
            Button close = Ui.Button("닫기 ×", CloseAll, "ghost");
            AutomationProperties.SetAutomationId(close, "sheet-close");
            AutomationProperties.SetAutomationId(_back, "sheet-back");
            Grid titleBar = Ui.Columns("Auto,*,Auto", _back, _title, close);
            _tabs.Margin = new Thickness(0, 6, 0, 2);
            _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _play = Ui.Button("▶ 진행", TogglePlay);
            AutomationProperties.SetAutomationId(_play, "sheet-play-toggle");
            (_root, _card) = CreateSurface(titleBar);
            _root.PointerPressed += OnBackdropPressed;
        }

        /// <summary>제목·탭·진행 상태를 고정하고 상세 본문만 스크롤한다.</summary>
        private (Grid Root, Border Card) CreateSurface(Control titleBar)
        {
            DockPanel layout = new() { LastChildFill = true };
            DockPanel.SetDock(titleBar, Dock.Top);
            DockPanel.SetDock(_tabs, Dock.Top);
            Grid transport = Ui.Columns("*,Auto", _state, _play);
            DockPanel.SetDock(transport, Dock.Bottom);
            layout.Children.Add(titleBar);
            layout.Children.Add(_tabs);
            layout.Children.Add(transport);
            layout.Children.Add(_scroll);
            Border card = new()
            {
                Background = Palette.Brush(Palette.PanelBack),
                BorderBrush = Palette.Brush(Palette.Accent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(12, 8, 12, 12),
                Margin = new Thickness(8, 12, 8, 12),
                Child = layout,
            };
            Grid root = new() { Background = Palette.Brush(Palette.Backdrop), IsVisible = false };
            root.Children.Add(card);
            return (root, card);
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
            UiTrace.Sheet(link.Kind.ToString(), link.Id);
            return true;
        }

        /// <summary>맨 위 시트를 닫는다.</summary>
        internal void Back()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            UiTrace.SheetClosed(_stack[^1].Link.Kind.ToString());
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
            foreach (SheetBase sheet in _stack)
            {
                UiTrace.SheetClosed(sheet.Link.Kind.ToString());
            }

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
            bool running = _navigator.Session.Runner.IsRunning;
            _play.Content = running ? "■ 정지" : "▶ 진행";
            _play.IsEnabled = !_navigator.Session.Runner.IsFaulted;
            _state.Text = _navigator.Session.Runner.IsFaulted ? "오류로 정지 · 복구 필요"
                : (running ? "관찰 중 · " : "정지 · ")
                    + TransportBar.SpeedText(_navigator.Session.Runner.SpeedIndex);
        }

        private void TogglePlay()
        {
            _navigator.Session.Runner.SetRunning(!_navigator.Session.Runner.IsRunning);
            _navigator.Invalidate();
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
