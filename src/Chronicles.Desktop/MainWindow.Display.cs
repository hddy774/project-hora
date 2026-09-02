using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Chronicles.Application;

namespace Chronicles.Desktop;

internal enum WindowDisplayMode : byte
{
    Windowed,
    FullScreen,
}

public partial class MainWindow
{
    private double _effectiveUiScale = 1d;
    private WindowDisplayMode _windowDisplayMode;
    private WindowState _windowedState = WindowState.Maximized;
    private Rect _windowedBounds = Rect.Empty;

    private void ApplyAdaptiveUiScale()
    {
        var effective = AdaptiveUiScalePolicy.CalculateEffectiveScale(
            _uiScale,
            ActualWidth,
            ActualHeight);
        if (Math.Abs(_effectiveUiScale - effective) < 0.0001d
            && RootLayoutGrid.LayoutTransform is ScaleTransform)
        {
            SettingsWorkspacePanel.SetUiScale(_uiScale, _effectiveUiScale);
            return;
        }

        _effectiveUiScale = effective;
        RootLayoutGrid.LayoutTransform = new ScaleTransform(effective, effective);
        SettingsWorkspacePanel.SetUiScale(_uiScale, effective);
    }

    private void ApplyRestoredWindowMode()
    {
        ApplyAdaptiveUiScale();
        if (_windowDisplayMode == WindowDisplayMode.FullScreen)
        {
            _windowDisplayMode = WindowDisplayMode.Windowed;
            EnterFullScreen();
        }
        else
        {
            UpdateWindowModeControls();
        }
    }

    private void WindowModeButton_Click(object sender, RoutedEventArgs e) => ExitFullScreen();

    private void FullScreenButton_Click(object sender, RoutedEventArgs e) => EnterFullScreen();

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "선로만상록을 종료할까요?\n저장하지 않은 세계 진행은 사라집니다.",
            "게임 종료",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _windowDisplayMode == WindowDisplayMode.FullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
        }
    }

    private void ToggleFullScreen()
    {
        if (_windowDisplayMode == WindowDisplayMode.FullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        if (_windowDisplayMode == WindowDisplayMode.FullScreen)
        {
            return;
        }

        _windowedState = WindowState == WindowState.Minimized
            ? WindowState.Normal
            : WindowState;
        _windowedBounds = RestoreBounds;
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        _windowDisplayMode = WindowDisplayMode.FullScreen;
        UpdateWindowModeControls();
        ApplyAdaptiveUiScale();
        ConfigureWorkspaceLayout(_workspaceRoute);
        _logService.AddDebug(DebugLogLevel.Information, "화면 모드 변경 · 전체화면");
    }

    private void ExitFullScreen()
    {
        if (_windowDisplayMode != WindowDisplayMode.FullScreen)
        {
            WindowState = WindowState.Maximized;
            UpdateWindowModeControls();
            return;
        }

        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        if (!_windowedBounds.IsEmpty)
        {
            Left = _windowedBounds.Left;
            Top = _windowedBounds.Top;
            Width = Math.Max(MinWidth, _windowedBounds.Width);
            Height = Math.Max(MinHeight, _windowedBounds.Height);
        }
        WindowState = _windowedState == WindowState.Minimized
            ? WindowState.Normal
            : _windowedState;
        _windowDisplayMode = WindowDisplayMode.Windowed;
        UpdateWindowModeControls();
        ApplyAdaptiveUiScale();
        ConfigureWorkspaceLayout(_workspaceRoute);
        _logService.AddDebug(DebugLogLevel.Information, "화면 모드 변경 · 창 모드");
    }

    private void UpdateWindowModeControls()
    {
        var fullScreen = _windowDisplayMode == WindowDisplayMode.FullScreen;
        WindowModeButton.IsEnabled = fullScreen;
        FullScreenButton.IsEnabled = !fullScreen;
        SettingsWorkspacePanel.SetWindowDisplayMode(fullScreen);
    }
}
