// Android 프로세스 진입점. Avalonia 앱을 만들고 내장 한글 글꼴을 등록한다.
using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Xianxia.App;

namespace Xianxia.Android
{
    /// <summary>선협 관찰 Android 애플리케이션.</summary>
    [Application(Label = "선협 관찰", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round")]
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
            return base.CustomizeAppBuilder(builder).WithSeonhyeopFonts();
        }
    }
}
