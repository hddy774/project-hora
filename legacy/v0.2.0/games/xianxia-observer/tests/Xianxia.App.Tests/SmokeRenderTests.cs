// 메인 화면이 실제 글꼴로 그려지고 시트·뒤로 가기가 동작하는지 확인하는 기본 검사. 결과 PNG는 bin/.../screens에 남는다.
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>메인 화면 렌더링 연기 검사.</summary>
    public sealed class SmokeRenderTests
    {
        [AvaloniaTest]
        public void MainView_AfterSixHundredYears_RendersWorldTab()
        {
            GameSession session = CreateSession(600);
            MainView view = new(session);
            Window window = Show(view);

            Save(window, "main-world.png");
            Assert.That(view.CurrentTab, Is.Zero);
            Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(600));
        }

        [AvaloniaTest]
        public void BackRequest_WithOpenSheet_ClosesSheetBeforeLeavingTab()
        {
            GameSession session = CreateSession(100);
            MainView view = new(session);
            Window window = Show(view);
            view.SelectTab(2);
            view.Open(new SheetLink(LinkKind.Settings, 0));
            Assert.That(view.Sheets.IsOpen, Is.True);

            window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
            Assert.That(view.Sheets.IsOpen, Is.False);
            Assert.That(view.CurrentTab, Is.EqualTo(2));

            window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
            Assert.That(view.CurrentTab, Is.Zero);
        }

        [AvaloniaTest]
        public void Session_SaveThenLoad_RestoresYear()
        {
            GameSession session = CreateSession(250);

            Assert.That(session.Save(), Is.True);
            session.Runner.Advance(50);
            Assert.That(session.LoadSaved(out string message), Is.True, message);
            Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(250));
        }

        private static GameSession CreateSession(int years)
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "saves",
                TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 300 });
            GameSession session = new(new SaveStore(directory), world);
            session.Runner.Advance(years);
            return session;
        }

        private static Window Show(MainView view)
        {
            Window window = new() { Width = 412, Height = 860, Content = view };
            window.Show();
            view.RefreshNow(force: true);
            return window;
        }

        private static void Save(Window window, string name)
        {
            WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screens");
            Directory.CreateDirectory(directory);
            frame!.Save(Path.Combine(directory, name), PngBitmapEncoderOptions.Default);
        }
    }
}
