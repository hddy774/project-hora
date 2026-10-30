// 다섯 주 탭과 더보기의 세 기존 화면, 속도 선택·안전 영역을 실제 입력으로 검증한다.
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>새 프레임에서 기존 데이터 경로와 실행 상태를 보존한다.</summary>
    public sealed class NavigationTests
    {
        /// <summary>모든 목적지가 실제 버튼으로 열리며 세계와 난수를 바꾸지 않는다.</summary>
        [AvaloniaTest]
        public void Navigation_PreservesAllDestinationsAndWorld()
        {
            MainView view = CreateView();
            Window window = Show(view);
            byte[] before = view.Session.Runner.Read(WorldSerializer.ToBytes);
            for (int index = 0; index < Destinations.Primary.Length; index++)
            {
                view.Navigation.ButtonAt(index).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
                Assert.That(view.CurrentDestination, Is.EqualTo(Destinations.FromPage(Destinations.Primary[index])));
            }

            for (int page = 4; page <= 6; page++)
            {
                string destination = Destinations.FromPage(page);
                Click(window, "more-" + destination);
                window.UpdateLayout();
                Assert.That(view.CurrentDestination, Is.EqualTo(destination));
                window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
                window.UpdateLayout();
                Assert.That(view.CurrentDestination, Is.EqualTo(Destinations.More));
            }

            window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
            Assert.That(view.CurrentDestination, Is.EqualTo(Destinations.World));
            Assert.That(view.Session.Runner.Read(WorldSerializer.ToBytes), Is.EqualTo(before));
            window.Close();
        }

        /// <summary>여섯 선택이 실제 실행기와 진행 바에 반영되고 선택 시 시트를 닫는다.</summary>
        [AvaloniaTest]
        public void SpeedSheet_UsesRunnerSpeeds()
        {
            MainView view = CreateView();
            Window window = Show(view);
            for (int index = 0; index < SimulationRunner.Speeds.Length; index++)
            {
                Click(window, "speed-picker");
                window.UpdateLayout();
                Click(window, "speed-" + index);
                window.UpdateLayout();
                Assert.That(view.Session.Runner.SpeedIndex, Is.EqualTo(index));
                Assert.That(view.Sheets.IsOpen, Is.False);
                Button picker = Find(window, "speed-picker");
                Assert.That(picker.Content, Is.EqualTo(TransportBar.SpeedText(index) + " ▾"));
            }

            Click(window, "play-toggle");
            Assert.That(view.Session.Runner.IsRunning, Is.True);
            Click(window, "play-toggle");
            Assert.That(view.Session.Runner.IsRunning, Is.False);
            int id = view.Session.Runner.Read(world => world.Cultivators[0].Id);
            view.Open(SheetLink.ToCultivator(id));
            window.UpdateLayout();
            Click(window, "sheet-play-toggle");
            Assert.That(view.Session.Runner.IsRunning, Is.True);
            Click(window, "sheet-play-toggle");
            Assert.That(view.Session.Runner.IsRunning, Is.False);
            window.Close();
        }

        /// <summary>상하·좌우 안전 영역을 루트 한 번만 적용한다.</summary>
        [AvaloniaTest]
        public void SafeArea_AppliesAtRootOnce()
        {
            MainView view = CreateView();
            Window window = Show(view);
            Thickness safe = new(10, 24, 10, 24);
            view.ApplyInsets(safe);
            window.UpdateLayout();
            Assert.That(view.Padding, Is.EqualTo(safe));
            Assert.That(((Border)view.Navigation.View).Padding.Bottom, Is.Zero);
            window.Close();
        }

        private static MainView CreateView()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "navigation-saves",
                TestContext.CurrentContext.Test.Name);
            return new MainView(new GameSession(new SaveStore(path),
                WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 })));
        }

        private static Window Show(MainView view)
        {
            Window window = new() { Width = 360, Height = 800, Content = view };
            window.Show();
            window.UpdateLayout();
            return window;
        }

        private static Button Find(Window window, string id) => window.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == id);

        private static void Click(Window window, string id) => Find(window, id)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
