// 헤드리스 테스트용 Avalonia 애플리케이션 설정. 실제 Skia 렌더러로 화면을 그려 PNG로 확인한다.
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Xianxia.App.Tests.TestAppBuilder))]

namespace Xianxia.App.Tests
{
    /// <summary>헤드리스 플랫폼으로 앱을 띄우는 빌더.</summary>
    public static class TestAppBuilder
    {
        /// <summary>Avalonia.Headless가 호출하는 진입점.</summary>
        /// <returns>헤드리스 AppBuilder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<Xianxia.App.App>()
                .UseSkia()
                .UseHarfBuzz()
                .WithSeonhyeopFonts()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
    }
}
