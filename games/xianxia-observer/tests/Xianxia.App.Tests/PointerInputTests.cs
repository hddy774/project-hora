// Click 이벤트를 직접 발생시키지 않고 실제 터치/마우스 히트 검사를 거쳐 탐색한다.
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>좌표 입력에서 실제 게임 동작까지의 회귀.</summary>
    public sealed class PointerInputTests
    {
        /// <summary>속도 선택 버튼의 실제 중심을 누르면 시트가 열린다.</summary>
        /// <param name="touch">터치이면true, 마우스이면false.</param>
        [AvaloniaTest]
        [TestCase(true)]
        [TestCase(false)]
        public void SpeedPicker_ActualPointerOpensSheet(bool touch)
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            MainView view = new(new GameSession(new SaveStore(Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "pointer-saves")), world));
            Window window = new() { Width = 360, Height = 640, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                using WriteableBitmap? frame = window.CaptureRenderedFrame();
                Button button = view.GetVisualDescendants().OfType<Button>()
                    .Single(item => AutomationProperties.GetAutomationId(item) == "speed-picker");
                Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2),
                    window)!.Value;
                if (touch)
                {
                    using IHeadlessTouchPointer pointer = window.TouchBegin(point);
                    window.TouchEnd(pointer, point);
                }
                else
                {
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                }

                Dispatcher.UIThread.RunJobs();
                Assert.That(view.Sheets.Top?.Link.Kind, Is.EqualTo(LinkKind.Speed));
            }
            finally { window.Close(); }
        }
    }
}
