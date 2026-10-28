// 메인 화면: 상단 막대 + 탭 화면 + 하단 탭 막대, 그 위에 알림 층과 상세 시트 층을 겹친다.
// [성능] 초당 4번만 세계를 읽어 보이는 화면만 고친다. 세계가 그대로면(정지 중) 다시 그리지 않는다.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xianxia.App.Pages;
using Xianxia.App.Services;
using Xianxia.App.Sheets;
using Xianxia.App.Theme;

namespace Xianxia.App.Views
{
    /// <summary>메인 화면.</summary>
    internal sealed class MainView : UserControl, INavigator
    {
        private readonly HeaderBar _header;
        private readonly NavBar _nav;
        private readonly PageBase[] _pages;
        private readonly ContentControl _content = new();
        private readonly SheetHost _sheets;
        private readonly ToastHost _toasts;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private long _lastVersion = -1;
        private bool _dirty = true;
        private bool _refreshQueued;
        private TopLevel? _topLevel;
        private IInsetsManager? _insets;

        /// <summary>메인 화면을 만든다.</summary>
        /// <param name="session">게임 세션.</param>
        internal MainView(GameSession session)
        {
            Session = session;
            Background = Palette.Brush(Palette.Parchment);
            Foreground = Palette.Brush(Palette.Ink);
            FontSize = 13;
            TopLevel.SetAutoSafeAreaPadding(this, false);
            _header = new HeaderBar(this);
            _pages = new PageBase[]
            {
                new WorldPage(this), new SectsPage(this), new PeoplePage(this), new HistoryPage(this),
                new LegacyPage(this), new StatsPage(this), new LogPage(this),
            };
            _nav = new NavBar(_pages, SelectTab);
            _sheets = new SheetHost(CreateSheet);
            _toasts = new ToastHost(Open);
            Grid root = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            Grid.SetRow(_header.View, 0);
            Grid.SetRow(_content, 1);
            Grid.SetRow(_nav.View, 2);
            Grid.SetRow(_toasts.View, 1);
            Grid.SetRowSpan(_sheets.View, 3);
            _toasts.View.ZIndex = 10;
            _sheets.View.ZIndex = 20;
            root.Children.AddRange(new Control[] { _header.View, _content, _nav.View, _toasts.View, _sheets.View });
            Content = root;
            _timer.Tick += (_, _) => RefreshNow(force: false);
            SelectTab(0);
            if (session.StartupNote is { } note)
            {
                _toasts.Show(note);
            }
        }

        /// <inheritdoc />
        public GameSession Session { get; }

        /// <summary>현재 탭 위치.</summary>
        internal int CurrentTab { get; private set; }

        /// <summary>탭 화면들(시험용).</summary>
        internal ReadOnlySpan<PageBase> Pages => _pages;

        /// <summary>시트 층(시험용).</summary>
        internal SheetHost Sheets => _sheets;

        /// <summary>하단 탭 막대(시험용).</summary>
        internal NavBar Navigation => _nav;

        /// <inheritdoc />
        public void Open(SheetLink link)
        {
            if (_sheets.Open(link))
            {
                RefreshNow(force: true);
            }
        }

        /// <inheritdoc />
        public void Invalidate()
        {
            _dirty = true;
            if (_refreshQueued)
            {
                return;
            }

            _refreshQueued = true;
            Dispatcher.UIThread.Post(() => RefreshNow(force: false), DispatcherPriority.Background);
        }

        /// <inheritdoc />
        public void Toast(string text)
        {
            _toasts.Show(text);
        }

        /// <summary>탭을 바꾼다.</summary>
        /// <param name="index">탭 위치.</param>
        internal void SelectTab(int index)
        {
            CurrentTab = Math.Clamp(index, 0, _pages.Length - 1);
            _content.Content = _pages[CurrentTab].View;
            _nav.Select(CurrentTab);
            RefreshNow(force: true);
        }

        /// <summary>세계를 읽어 보이는 화면을 고친다.</summary>
        /// <param name="force">세계가 그대로여도 다시 그릴지.</param>
        internal void RefreshNow(bool force)
        {
            _refreshQueued = false;
            SimulationRunner runner = Session.Runner;
            long version = runner.Version;
            if (!force && !_dirty && version == _lastVersion)
            {
                _toasts.Expire();
                return;
            }

            _lastVersion = version;
            _dirty = false;
            runner.Read(world =>
            {
                _header.Refresh(world, runner);
                _pages[CurrentTab].Refresh(world);
                _sheets.Refresh(world);
                _toasts.Refresh(world, enabled: true);
            });
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel is not null)
            {
                _topLevel.BackRequested += OnBackRequested;
                _insets = _topLevel.InsetsManager;
                if (_insets is not null)
                {
                    _insets.DisplayEdgeToEdgePreference = true;
                    _insets.SafeAreaChanged += OnSafeAreaChanged;
                    ApplyInsets(_insets.SafeAreaPadding);
                }
            }

            _timer.Start();
            RefreshNow(force: true);
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _timer.Stop();
            if (_topLevel is not null)
            {
                _topLevel.BackRequested -= OnBackRequested;
            }

            if (_insets is not null)
            {
                _insets.SafeAreaChanged -= OnSafeAreaChanged;
            }

            _topLevel = null;
            _insets = null;
            base.OnDetachedFromVisualTree(e);
        }

        private SheetBase? CreateSheet(SheetLink link)
        {
            return link.Kind switch
            {
                LinkKind.Cultivator => new CultivatorSheet(this, link),
                LinkKind.Sect => new SectSheet(this, link),
                LinkKind.Region => new RegionSheet(this, link),
                LinkKind.Tournament => new TournamentSheet(this, link),
                LinkKind.War => new WarSheet(this, link),
                LinkKind.Settings => new SettingsSheet(this, link),
                LinkKind.Help => new HelpSheet(this, link),
                _ => null,
            };
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // Android 뒤로 키: 시트가 있으면 하나 닫고, 없으면 첫 탭으로, 첫 탭이면 시스템에 맡긴다.
            if (_sheets.IsOpen)
            {
                _sheets.Back();
                e.Handled = true;
                RefreshNow(force: true);
            }
            else if (CurrentTab != 0)
            {
                SelectTab(0);
                e.Handled = true;
            }
        }

        private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
        {
            ApplyInsets(e.SafeAreaPadding);
        }

        private void ApplyInsets(Thickness padding)
        {
            _header.SetTopInset(padding.Top);
            _nav.SetBottomInset(padding.Bottom);
            _sheets.SetInsets(padding);
            Padding = new Thickness(padding.Left, 0, padding.Right, 0);
        }
    }
}
