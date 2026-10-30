using Android.App;
using Android.OS;
using Android.Webkit;
using Chronicles.Game;
using Java.Interop;
using System.Text.Json;

namespace Chronicles.Android;

[Activity(Label = "선로만상록", MainLauncher = true, Exported = true, ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Portrait,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private GameSession? session;
    private WebView? web;
    private NativeBridge? bridge;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        session = new GameSession(Path.Combine(FilesDir!.AbsolutePath, "worlds"));
        web = new WebView(this);
        web.Settings.JavaScriptEnabled = true;
        web.Settings.DomStorageEnabled = false;
        web.Settings.AllowFileAccess = false;
        web.Settings.AllowContentAccess = false;
        web.Settings.SetSupportZoom(true);
        web.Settings.BuiltInZoomControls = true;
        web.Settings.DisplayZoomControls = false;
        web.SetWebViewClient(new LocalOnlyClient(this));
        web.SetWebChromeClient(new NativeConsole());
        bridge = new NativeBridge(session);
        web.AddJavascriptInterface(bridge, "seonroNative");
        SetContentView(web);
        using var reader = new StreamReader(Assets!.Open("index.html"));
        web.LoadDataWithBaseURL("https://seonro.local/", reader.ReadToEnd(), "text/html", "UTF-8", null);
    }

    protected override void OnPause()
    {
        web?.EvaluateJavascript("window.pauseWorld && window.pauseWorld()", null);
        try { session?.Suspend(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        if (web is not null)
        {
            web.RemoveJavascriptInterface("seonroNative"); web.Destroy(); web.Dispose();
        }
        bridge?.Dispose(); base.OnDestroy();
    }

    private void CheckRender(int attempt)
    {
        if (web is null || IsFinishing || IsDestroyed) return;
        const string script = "(function(){var h=document.querySelector('#content h2');var c=document.querySelector('#connection');var v=typeof view!=='undefined'&&view?view:null;return JSON.stringify({rendered:!!(v&&h&&h.getBoundingClientRect().height>0),heading:h?h.textContent:null,title:v?v.title:null,version:v?v.version:null,error:window.seonroLastHostError||null,connection:c?c.textContent:null});})()";
        web.EvaluateJavascript(script, new RenderCallback(this, attempt));
    }

    private sealed class RenderCallback(MainActivity activity, int attempt) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value)
        {
            var result = JsonSerializer.Deserialize<string>(value?.ToString() ?? "null") ?? "{}";
            using var report = JsonDocument.Parse(result);
            if (report.RootElement.TryGetProperty("rendered", out var rendered) && rendered.ValueKind == JsonValueKind.True)
                global::Android.Util.Log.Info("SeonroRender", result);
            else if (attempt < 60) activity.web?.PostDelayed(() => activity.CheckRender(attempt + 1), 250);
            else global::Android.Util.Log.Error("SeonroRender", result);
        }
    }

    private sealed class NativeConsole : WebChromeClient
    {
        public override bool OnConsoleMessage(ConsoleMessage? message)
        {
            global::Android.Util.Log.Warn("SeonroWeb", message?.Message() ?? "WebView message");
            return true;
        }
    }

    private sealed class LocalOnlyClient(MainActivity activity) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;
        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            activity.CheckRender(0);
        }
    }
}

public sealed class NativeBridge(GameSession session) : Java.Lang.Object
{
    [JavascriptInterface, Export("request")]
    public string Request(string json) => session.HandleJson(json);
}
