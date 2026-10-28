// 데스크톱 개발 실행 진입점.
using System;
using Avalonia;
using Xianxia.App;

namespace Xianxia.Desktop
{
    /// <summary>데스크톱에서 앱을 실행한다.</summary>
    public static class Program
    {
        /// <summary>프로그램 진입점.</summary>
        /// <param name="args">명령줄 인자.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>디자이너와 실행이 함께 쓰는 Avalonia 설정.</summary>
        /// <returns>설정된 AppBuilder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<Xianxia.App.App>()
                .UsePlatformDetect()
                .WithSeonhyeopFonts()
                .LogToTrace();
        }
    }
}
