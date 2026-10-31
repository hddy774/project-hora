// Android 프로세스 진입점. Avalonia 앱을 만들고 내장 한글 글꼴을 등록한다.
using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Xianxia.App;

namespace Xianxia.Android
{
    /// <summary>선로만상록 프로젝트 Android 애플리케이션.</summary>
    [Application(Label = "선로만상록 프로젝트", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round")]
    public sealed class MainApplication : AvaloniaAndroidApplication<Xianxia.App.App>
    {
        /// <summary>Android 런타임이 호출하는 생성자.</summary>
        /// <param name="javaReference">자바 객체 참조.</param>
        /// <param name="transfer">참조 소유권.</param>
        public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        /// <inheritdoc />
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            // 프레임워크 초기화에서 저장을 복구하므로 Activity.OnCreate보다 먼저 연결한다.
            global::Xianxia.App.App.SetUiTraceSink(line => global::Android.Util.Log.Info("XianxiaUi", line));
            global::Xianxia.App.App.SetDeviceFontScale(Resources?.Configuration?.FontScale ?? 1);
            return base.CustomizeAppBuilder(builder).WithSeonhyeopFonts();
        }
    }
}
