using Android.App;
using Android.OS;
using Android.Webkit;
using Chronicles.Game;
using Java.Interop;

namespace Chronicles.Android;

[Activity(Label = "선로만상록", MainLauncher = true, Exported = true, ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Portrait,
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
        web.SetWebViewClient(new LocalOnlyClient());
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

    private sealed class LocalOnlyClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;
    }
}

public sealed class NativeBridge(GameSession session) : Java.Lang.Object
{
    [JavascriptInterface, Export("request")]
    public string Request(string json) => session.HandleJson(json);
}
