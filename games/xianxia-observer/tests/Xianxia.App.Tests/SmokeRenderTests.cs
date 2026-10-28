// 화면이 실제 글꼴로 렌더링되는지 확인하는 기본 검사.
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using Xianxia.App.Views;

namespace Xianxia.App.Tests
{
    /// <summary>메인 화면 렌더링 연기 검사.</summary>
    public sealed class SmokeRenderTests
    {
        /// <summary>메인 화면을 그려 PNG로 저장한다.</summary>
        [AvaloniaTest]
        public void MainView_WhenRendered_ProducesImage()
        {
            Window window = new() { Width = 412, Height = 400, Content = new MainView() };
            window.Show();
            WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screens");
            Directory.CreateDirectory(directory);
            frame!.Save(Path.Combine(directory, "smoke.png"), PngBitmapEncoderOptions.Default);
        }
    }
}
