// 계산 단위와 다른 표현이 사용자에게 다시 노출되지 않도록 실제 화면에서 확인한다.
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>전쟁·사기·도감의 수치 의미가 화면에 드러나는지 확인한다.</summary>
    public sealed class LabelTests
    {
        /// <summary>360폭에서 전쟁 상세의 경지 하락 건수를 실제로 그린다.</summary>
        [AvaloniaTest]
        public void WarSheet_ShowsRankLossLabel()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            world.War.AtWar = true;
            world.War.WarNumber = 1;
            world.War.Casualties = 12;
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "label-saves",
                TestContext.CurrentContext.Test.Name);
            MainView view = new(new GameSession(new SaveStore(directory), world));
            Window window = new() { Width = 360, Height = 800, Content = view };
            window.Show();
            view.Open(SheetLink.ToWar(1));
            window.UpdateLayout();
            string text = string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(row => row.Text));
            Assert.That(text, Does.Contain("경지 하락").And.Contain("12"));
            Assert.That(text, Does.Not.Contain("사상자"));
            using WriteableBitmap frame = window.CaptureRenderedFrame()!;
            frame.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "labels-war-360.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }

        /// <summary>사기의 최초 기록은 개벽 이후 돌파이며 신품 예약도 도감에 명시한다.</summary>
        [AvaloniaTest]
        public void HistoryAndHelp_ShowBreakthroughAndReservedGrade()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "label-saves",
                TestContext.CurrentContext.Test.Name);
            MainView view = new(new GameSession(new SaveStore(directory), world));
            Window window = new() { Width = 360, Height = 800, Content = view };
            window.Show();
            view.SelectTab(3);
            window.UpdateLayout();
            Button records = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Content is string text && text == "세계 기록");
            records.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            view.RefreshNow(true);
            window.UpdateLayout();
            string text = string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(row => row.Text));
            Assert.That(text, Does.Contain("첫 돌파(개벽 이후)"));
            view.Open(new SheetLink(LinkKind.Help, 0));
            window.UpdateLayout();
            text = string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(row => row.Text));
            Assert.That(text, Does.Contain("현재 얻을 수 없음"));
            window.Close();
        }
    }
}
