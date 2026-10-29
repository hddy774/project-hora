// 알림(원본에 없던 기능): 연대기급 사건과 관심 인물의 주요 사건을 화면 위쪽에 잠깐 띄운다.
// 빠른 속도에서는 한 번에 많은 알림이 오므로 최신 3건만 보이고 나머지는 "외 n건"으로 줄인다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Xianxia.App.Controls;
using Xianxia.App.Theme;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>알림 층.</summary>
    internal sealed class ToastHost
    {
        private const int MaxVisible = 3;
        private static readonly TimeSpan s_lifetime = TimeSpan.FromSeconds(5);
        private readonly StackPanel _panel = new()
        {
            Spacing = 4,
            Margin = new Thickness(Ui.Gutter, 6, Ui.Gutter, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };

        private readonly List<(Button View, TextBlock Text, long Expires, SheetLink Link)> _toasts = new();
        private readonly Action<SheetLink> _open;
        private World? _world;
        private long _lastSequence;

        /// <summary>알림 층을 만든다.</summary>
        /// <param name="open">알림을 눌렀을 때 시트 열기.</param>
        internal ToastHost(Action<SheetLink> open)
        {
            _open = open;
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _panel;

        /// <summary>새 알림을 가져오고 오래된 알림을 지운다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="enabled">알림을 띄울지(시트가 열려 있으면 띄우지 않고 건너뛴다).</param>
        internal void Refresh(World world, bool enabled)
        {
            IReadOnlyList<Notice> notices = world.Chronicle.Notices;
            long newest = notices.Count == 0 ? 0 : notices[^1].Sequence;
            if (!ReferenceEquals(world, _world))
            {
                // 새 세계·불러오기: 이전 알림은 띄우지 않는다.
                _world = world;
                _lastSequence = newest;
                return;
            }

            List<Notice> fresh = new();
            foreach (Notice notice in notices)
            {
                if (notice.Sequence > _lastSequence)
                {
                    fresh.Add(notice);
                }
            }

            _lastSequence = Math.Max(_lastSequence, newest);
            if (enabled && fresh.Count > 0)
            {
                int skipped = Math.Max(0, fresh.Count - MaxVisible);
                for (int index = skipped; index < fresh.Count; index++)
                {
                    Notice notice = fresh[index];
                    string extra = index == fresh.Count - 1 && skipped > 0 ? " (외 " + skipped + "건)" : string.Empty;
                    string text = "[" + Services.Fmt.Years(notice.Year) + "] " + notice.Text + extra;
                    Show(text, notice.Major ? Palette.Accent : Palette.Gold, SheetLink.ToCultivator(notice.SubjectId));
                }
            }

            Expire();
        }

        /// <summary>짧은 안내를 띄운다.</summary>
        /// <param name="text">내용.</param>
        internal void Show(string text)
        {
            Show(text, Palette.Favor, SheetLink.None);
        }

        /// <summary>시간이 지난 알림을 지운다.</summary>
        internal void Expire()
        {
            long now = Stopwatch.GetTimestamp();
            for (int index = _toasts.Count - 1; index >= 0; index--)
            {
                if (_toasts[index].Expires <= now)
                {
                    _panel.Children.Remove(_toasts[index].View);
                    _toasts.RemoveAt(index);
                }
            }
        }

        private void Show(string text, Avalonia.Media.Color color, SheetLink link)
        {
            while (_toasts.Count >= MaxVisible)
            {
                _panel.Children.Remove(_toasts[0].View);
                _toasts.RemoveAt(0);
            }

            TextBlock block = Ui.Text(text, 11.5);
            Button view = Ui.Button(block, () => Dismiss(block, link), "card");
            view.Background = Palette.Brush(Palette.PanelBack);
            view.BorderBrush = Palette.Brush(color);
            view.BorderThickness = new Thickness(3, 1, 1, 1);
            long expires = Stopwatch.GetTimestamp() + (long)(s_lifetime.TotalSeconds * Stopwatch.Frequency);
            _toasts.Add((view, block, expires, link));
            _panel.Children.Add(view);
        }

        private void Dismiss(TextBlock block, SheetLink link)
        {
            for (int index = 0; index < _toasts.Count; index++)
            {
                if (ReferenceEquals(_toasts[index].Text, block))
                {
                    _panel.Children.Remove(_toasts[index].View);
                    _toasts.RemoveAt(index);
                    break;
                }
            }

            if (link.IsSet)
            {
                _open(link);
            }
        }
    }
}
