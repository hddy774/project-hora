// Android 첫 화면(Activity). 세로 화면 고정, 화면 회전·크기 변경 시 Activity를 다시 만들지 않는다.
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Avalonia.Android;

namespace Xianxia.Android
{
    /// <summary>선로만상록 프로젝트의 유일한 Activity.</summary>
    [Activity(
        Name = "org.hora.xianxiaobserver.MainActivity",
        Label = "선로만상록 프로젝트",
        Theme = "@style/SeonhyeopTheme",
        Icon = "@mipmap/ic_launcher",
        RoundIcon = "@mipmap/ic_launcher_round",
        MainLauncher = true,
        Exported = true,
        ScreenOrientation = ScreenOrientation.Portrait,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
            | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density
            | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.FontScale)]
    public sealed class MainActivity : AvaloniaMainActivity
    {
        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            global::Xianxia.App.App.SetDeviceFontScale(Resources?.Configuration?.FontScale ?? 1);
            base.OnCreate(savedInstanceState);
        }

        /// <inheritdoc/>
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            global::Xianxia.App.App.SetDeviceFontScale(newConfig.FontScale);
        }
    }
}
