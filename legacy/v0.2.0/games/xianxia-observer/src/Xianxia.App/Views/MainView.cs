// 메인 화면: 상단 막대 + 탭 화면 + 하단 탭 막대, 그 위에 알림 층과 상세 시트 층을 겹친다.
// [성능] 초당 4번만 세계를 읽어 보이는 화면만 고친다. 세계가 그대로면(정지 중) 다시 그리지 않는다.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
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
        private readonly TransportBar _transport;
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
        private IInputPane? _inputPane;
        private UiInputTrace? _inputTrace;
        private int _worldGeneration;
        private Thickness _safeArea;
        private Rect _keyboardBounds;
        private bool _keyboardOpen;
        private bool _autoStartPending;

        /// <summary>메인 화면을 만든다.</summary>
        /// <param name="session">게임 세션.</param>
        /// <param name="startWhenReady">입력 영역이 준비되면 자동 진행을 시작할지.</param>
        internal MainView(GameSession session, bool startWhenReady = false)
        {
            Session = session;
            _autoStartPending = startWhenReady;
            AddHandler(PointerPressedEvent, OnStartupInteraction, RoutingStrategies.Tunnel, true);
            _worldGeneration = session.WorldGeneration;
            Background = Palette.Brush(Palette.Parchment);
            Foreground = Palette.Brush(Palette.Ink);
            FontSize = 15;
            TopLevel.SetAutoSafeAreaPadding(this, false);
            _header = new HeaderBar(this);
            _transport = new TransportBar(this);
            _pages = new PageBase[]
            {
                new WorldPage(this), new SectsPage(this), new PeoplePage(this), new HistoryPage(this),
                new LegacyPage(this), new StatsPage(this), new LogPage(this), new MorePage(this),
            };
            _nav = new NavBar(_pages, SelectTab);
            _sheets = new SheetHost(this, CreateSheet);
            _toasts = new ToastHost(Open);
            Content = CreateLayout();
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

        /// <summary>현재 화면의 안정된 목적지 ID.</summary>
        internal string CurrentDestination => Destinations.FromPage(CurrentTab);

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

        /// <inheritdoc/>
        public void Navigate(string destination) => SelectTab(Destinations.ToPage(destination));

        /// <inheritdoc/>
        public void CloseSheet()
        {
            _sheets.Back();
            RefreshNow(force: true);
        }

        /// <summary>탭을 바꾼다.</summary>
        /// <param name="index">탭 위치.</param>
        internal void SelectTab(int index)
        {
            CurrentTab = Math.Clamp(index, 0, _pages.Length - 1);
            _content.Content = _pages[CurrentTab].View;
            _nav.Select(CurrentTab);
            RefreshNow(force: true);
            UiTrace.Tab(CurrentTab, _pages[CurrentTab].Name);
            _inputTrace?.Queue();
        }

        /// <summary>세계를 읽어 보이는 화면을 고친다.</summary>
        /// <param name="force">세계가 그대로여도 다시 그릴지.</param>
        internal void RefreshNow(bool force)
        {
            _refreshQueued = false;
            SimulationRunner runner = Session.Runner;
            bool showNotices = !_sheets.IsOpen && _topLevel?.FocusManager?.GetFocusedElement() is not TextBox;
            _toasts.SetDeferred(!showNotices);
            if (_worldGeneration != Session.WorldGeneration)
            {
                _worldGeneration = Session.WorldGeneration;
                foreach (PageBase page in _pages)
                {
                    page.ResetState();
                }

                _sheets.CloseAll();
                _toasts.Show("새로 열린 세계입니다. 인물 검색과 선택을 초기화했습니다.");
                _dirty = true;
            }
            StartWhenInputReady();
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
                _transport.Refresh(runner);
                _pages[CurrentTab].Refresh(world);
                _sheets.Refresh(world);
                _toasts.Refresh(world, enabled: showNotices);
            });
            if (force)
            {
                _inputTrace?.Queue();
            }
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel is not null)
            {
                _topLevel.BackRequested += OnBackRequested;
                _topLevel.ScalingChanged += OnScalingChanged;
                _insets = _topLevel.InsetsManager;
                _inputPane = _topLevel.InputPane;
                if (_inputPane is not null)
                {
                    _inputPane.StateChanged += OnInputPaneChanged;
                    ApplyKeyboardState(_inputPane.State == InputPaneState.Open, _inputPane.OccludedRect);
                }
                if (_insets is not null)
                {
                    _insets.DisplayEdgeToEdgePreference = true;
                    _insets.SafeAreaChanged += OnSafeAreaChanged;
                    ApplyInsets(_insets.SafeAreaPadding);
                }
            }

            _timer.Start();
            Typography.Changed += OnTypographyChanged;
            _inputTrace = new UiInputTrace(this);
            RefreshNow(force: true);
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _inputTrace?.Detach();
            _inputTrace = null;
            _timer.Stop();
            Typography.Changed -= OnTypographyChanged;
            if (_topLevel is not null)
            {
                _topLevel.BackRequested -= OnBackRequested;
                _topLevel.ScalingChanged -= OnScalingChanged;
            }

            if (_insets is not null)
            {
                _insets.SafeAreaChanged -= OnSafeAreaChanged;
            }

            if (_inputPane is not null)
            {
                _inputPane.StateChanged -= OnInputPaneChanged;
            }

            _topLevel = null;
            _insets = null;
            _inputPane = null;
            base.OnDetachedFromVisualTree(e);
        }

        /// <summary>본문은 스크롤하고 진행·주 탐색은 화면 아래에 유지한다.</summary>
        private Grid CreateLayout()
        {
            Grid root = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
            Grid.SetRow(_header.View, 0);
            Grid.SetRow(_content, 1);
            Grid.SetRow(_transport.View, 2);
            Grid.SetRow(_nav.View, 3);
            Grid.SetRow(_toasts.View, 1);
            Grid.SetRowSpan(_sheets.View, 4);
            _toasts.View.ZIndex = 10;
            _sheets.View.ZIndex = 20;
            root.Children.AddRange(new Control[]
            {
            _header.View, _content, _transport.View, _nav.View, _toasts.View, _sheets.View,
            });
            return root;
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
                LinkKind.Speed => new SpeedSheet(this, link),
                _ => null,
            };
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // 상세 → 더보기 하위 → 천하 → 플랫폼 순으로 복귀한다.
            if (_sheets.IsOpen)
            {
                _sheets.Back();
                e.Handled = true;
                RefreshNow(force: true);
            }
            else if (CurrentTab != 0)
            {
                SelectTab(CurrentTab is >= 4 and <= 6 ? 7 : 0);
                e.Handled = true;
            }
        }

        private void StartWhenInputReady()
        {
            if (!_autoStartPending || _topLevel is null)
            {
                return;
            }

            Control transport = _transport.View;
            Point? point = transport.TranslatePoint(new Point(transport.Bounds.Width / 4,
                transport.Bounds.Height / 2), _topLevel);
            if (point is { } position && _topLevel.InputHitTest(position) is not null)
            {
                _autoStartPending = false;
                Session.Runner.SetRunning(true);
            }
        }

        private void OnStartupInteraction(object? sender, PointerPressedEventArgs args)
            => _autoStartPending = false;

        private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
        {
            ApplyInsets(e.SafeAreaPadding);
        }

        private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
            => ApplyKeyboardState(e.NewState == InputPaneState.Open, e.EndRect);

        /// <summary>키보드 중에는 제목·주 탐색을 접어 검색 본문을 확보하고 진행 제어는 유지한다.</summary>
        /// <param name="open">플랫폼 키보드가 열렸는가.</param>
        /// <param name="occluded">클라이언트 좌표에서 키보드가 가리는 영역.</param>
        internal void ApplyKeyboardState(bool open, Rect occluded = default)
        {
            _keyboardOpen = open;
            _keyboardBounds = occluded;
            _header.View.IsVisible = !open;
            _nav.View.IsVisible = !open;
            ApplyViewportPadding();
            _inputTrace?.Queue();
        }

        private void OnScalingChanged(object? sender, EventArgs e)
        {
            // Android는 첫 배치 뒤 실제 밀도를 확정할 수 있다. 초기 밀도1의 여백을 그대로 두지 않는다.
            if (_insets is not null)
            {
                ApplyInsets(_insets.SafeAreaPadding);
            }
        }

        /// <inheritdoc />
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyViewportPadding();
        }

        private void ApplyViewportPadding()
        {
            double overlap = _keyboardOpen && _keyboardBounds.Height > 0
                ? Math.Max(0, Bounds.Height - _keyboardBounds.Top) : 0;
            Padding = new Thickness(_safeArea.Left, _safeArea.Top, _safeArea.Right,
                Math.Max(_safeArea.Bottom, overlap));
        }

        private void OnTypographyChanged() => RefreshNow(force: true);

        /// <summary>루트에서만 안전 영역을 적용해 헤더·본문·시트의 이중 여백을 피한다.</summary>
        /// <param name="padding">플랫폼 안전 영역.</param>
        internal void ApplyInsets(Thickness padding)
        {
            _safeArea = padding;
            ApplyViewportPadding();
            _inputTrace?.Queue();
        }
    }
}
