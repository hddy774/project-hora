using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using Android.Views;

namespace AlphaExchange.App;

[Activity(Label = "알파 익스체인지", MainLauncher = true, Exported = true, ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : Activity
{
    GameView? game;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetStatusBarColor(Android.Graphics.Color.Rgb(11, 17, 27));
        Window?.SetNavigationBarColor(Android.Graphics.Color.Rgb(11, 17, 27));
        game = new GameView(this);
        SetContentView(game);
    }
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    { base.OnActivityResult(requestCode,resultCode,data); game?.HandleBackupResult(requestCode,resultCode,data); }
    protected override void OnPause() { game?.Pause(); base.OnPause(); }
    public override void OnBackPressed() { if (game?.GoBack() != true) base.OnBackPressed(); }
}
