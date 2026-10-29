// 입력 뒤 현재 보이는 조작의 실제 위치만 남긴다. 에뮬레이터가 고정 비율 좌표에 의존하지 않게 한다.
using System;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xianxia.App.Services;

namespace Xianxia.App.Views
{
    /// <summary>개인 정보 없이 게임 조작과 전환을 연결한다.</summary>
    internal sealed class UiInputTrace
    {
        private readonly Control _root;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

        /// <summary>한 화면 수명에 연결한다.</summary>
        /// <param name="root">메인 화면.</param>
        internal UiInputTrace(Control root)
        {
            _root = root;
            _timer.Tick += Capture;
            root.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, true);
            root.AddHandler(InputElement.PointerReleasedEvent, OnInput, RoutingStrategies.Tunnel);
            root.AddHandler(ScrollViewer.ScrollChangedEvent, OnScroll, RoutingStrategies.Bubble);
            root.AddHandler(InputElement.KeyUpEvent, OnKey, RoutingStrategies.Tunnel);
            Queue();
        }

        /// <summary>여러 레이아웃 변경을 한 번의 좌표 기록으로 합친다.</summary>
        internal void Queue()
        {
            if (UiTrace.Sink is null)
            {
                return;
            }

            _timer.Stop();
            _timer.Start();
        }

        /// <summary>화면이 분리될 때 입력 연결과 대기 타이머를 해제한다.</summary>
        internal void Detach()
        {
            _timer.Stop();
            _root.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            _root.RemoveHandler(InputElement.PointerReleasedEvent, OnInput);
            _root.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScroll);
            _root.RemoveHandler(InputElement.KeyUpEvent, OnKey);
        }

        private void OnInput(object? sender, PointerReleasedEventArgs args) => Queue();

        private void OnPressed(object? sender, PointerPressedEventArgs args)
        {
            Point point = args.GetPosition(_root);
            UiTrace.Sink?.Invoke("[xo-ui] input " + JsonSerializer.Serialize(new
            {
                x = point.X,
                y = point.Y,
                target = args.Source?.GetType().Name,
            }));
        }

        private void OnScroll(object? sender, ScrollChangedEventArgs args)
        {
            if (args.OffsetDelta != Vector.Zero)
            {
                Queue();
            }
        }

        private void OnKey(object? sender, KeyEventArgs args) => Queue();

        private void Capture(object? sender, EventArgs args)
        {
            _timer.Stop();
            TopLevel.GetTopLevel(_root)?.UpdateLayout();
            UiTrace.Sink?.Invoke("[xo-ui] controls-begin");
            Control scope = _root is MainView view && view.Sheets.IsOpen ? view.Sheets.View : _root;
            CaptureViewport(scope);
            CaptureKeyboard();
            if (_root is MainView main)
            {
                main.Session.Runner.Read(world =>
                {
                    UiTrace.Context(world.Year, world.HeavenlyFavor, main.Session.Runner.IsRunning);
                    return true;
                });
            }
            foreach (Control control in scope.GetVisualDescendants().OfType<Control>())
            {
                if (control is not Button && control is not TextBox)
                {
                    continue;
                }

                if (!control.IsEffectivelyVisible || !control.IsEnabled || !control.IsHitTestVisible)
                {
                    continue;
                }

                Point center = new(control.Bounds.Width / 2, control.Bounds.Height / 2);
                if (!IsInViewport(control, center))
                {
                    continue;
                }

                string? id = AutomationProperties.GetAutomationId(control);
                if (string.IsNullOrEmpty(id) && control is Button { Content: string text })
                {
                    id = "button:" + text;
                }

                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                PixelPoint screen = control.PointToScreen(center);
                UiTrace.Control(id, screen.X, screen.Y);
            }

            UiTrace.Sink?.Invoke("[xo-ui] controls-end");
        }

        private static void CaptureViewport(Control scope)
        {
            ScrollViewer? scroll = scope.GetVisualDescendants().OfType<ScrollViewer>()
                .Where(item => item.IsEffectivelyVisible)
                .OrderByDescending(item => item.Bounds.Width * item.Bounds.Height).FirstOrDefault();
            if (scroll is null)
            {
                return;
            }

            PixelPoint first = scroll.PointToScreen(default);
            PixelPoint last = scroll.PointToScreen(new Point(scroll.Bounds.Width, scroll.Bounds.Height));
            UiTrace.Viewport(first.X, first.Y, last.X, last.Y);
        }

        private void CaptureKeyboard()
        {
            IInputPane? input = TopLevel.GetTopLevel(_root)?.InputPane;
            if (input?.State == InputPaneState.Open)
            {
                PixelPoint top = _root.PointToScreen(new Point(0, input.OccludedRect.Top));
                UiTrace.Sink?.Invoke("[xo-ui] keyboard " + JsonSerializer.Serialize(new { top = top.Y }));
            }
        }

        private bool IsInViewport(Control control, Point center)
        {
            foreach (Visual ancestor in control.GetVisualAncestors())
            {
                if (ancestor is not ScrollViewer && !ReferenceEquals(ancestor, _root))
                {
                    continue;
                }

                Point? position = control.TranslatePoint(center, ancestor);
                if (position is not { } point || !new Rect(ancestor.Bounds.Size).Contains(point))
                {
                    return false;
                }
            }

            return control.Bounds.Width > 0 && control.Bounds.Height > 0;
        }
    }
}
