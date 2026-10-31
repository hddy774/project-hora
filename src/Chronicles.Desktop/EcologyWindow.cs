using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Chronicles.Application;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Chronicles.Desktop;

public sealed class EcologyWindow : Window, IDisposable
{
    private readonly EcologyApplicationSession session = new();
    private readonly WebView2 browser = new();
    private static readonly JsonSerializerOptions JsonOptions = new();
    private bool disposed;
    private readonly bool smokeTest;
    private const string LocalPage = "https://seonro.local/index.html";

    public EcologyWindow(bool smokeTest = false)
    {
        this.smokeTest = smokeTest;
        Title = "선로만상록"; Width = 1440; Height = 960; MinWidth = 390; MinHeight = 600;
        Content = browser; Loaded += OnLoaded; Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        try
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeonroMansangrok", "browser");
            var environment = await CoreWebView2Environment.CreateAsync(null, data);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.NavigationStarting += (_, e) => e.Cancel = e.Uri != LocalPage;
            browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            browser.CoreWebView2.WebMessageReceived += OnMessage;
            if (smokeTest) browser.CoreWebView2.NavigationCompleted += OnSmokeNavigation;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameShell.html")!;
            using var reader = new StreamReader(stream);
            var assets = Path.Combine(data, "assets");
            Directory.CreateDirectory(assets);
            await File.WriteAllTextAsync(Path.Combine(assets, "index.html"), await reader.ReadToEndAsync());
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("seonro.local", assets,
                CoreWebView2HostResourceAccessKind.DenyCors);
            browser.CoreWebView2.Navigate(LocalPage);
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or IOException)
        {
            MessageBox.Show("Microsoft Edge WebView2 Runtime을 확인하세요.\n" + e.Message,
                "선로만상록 실행", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnSmokeNavigation(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (browser.Source?.AbsoluteUri != LocalPage) return;
        var passed = false;
        for (var attempt = 0; attempt < 40 && !disposed; attempt++)
        {
            var versionLiteral = JsonSerializer.Serialize(EcologyApplicationSession.Version, JsonOptions);
            var value = await browser.CoreWebView2.ExecuteScriptAsync("typeof view !== 'undefined' && view !== null && view.title === '선로만상록' && view.version === " + versionLiteral + " && document.querySelector('#content h2') !== null");
            if (value == "true") { passed = true; break; }
            await Task.Delay(250);
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "seonro-smoke-result.json"),
            JsonSerializer.Serialize(new { passed, version = EcologyApplicationSession.Version, host = "WPF WebView2", actualRenderAndBridge = passed,
                diagnostic = await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({hasView:typeof view!=='undefined'&&view!==null,error:window.seonroLastHostError||null,connection:document.querySelector('#connection')?.textContent,heading:document.querySelector('#content h2')?.textContent,source:location.href})") }, JsonOptions));
        System.Windows.Application.Current.Shutdown(passed ? 0 : 1);
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (args.Source != LocalPage) return;
        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var root = document.RootElement;
            var sequence = root.GetProperty("sequence").GetInt64();
            var request = root.GetProperty("request").GetRawText();
            var response = await Task.Run(() => session.HandleJson(request));
            if (disposed) return;
            browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { sequence, payload = response }, JsonOptions));
        }
        catch (JsonException) { }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        try { session.Suspend(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; browser.Dispose(); GC.SuppressFinalize(this);
    }
}
