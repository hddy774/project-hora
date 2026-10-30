// 큰 글자에서 모든 상세와 좁아진 키보드 본문의 실제 배치·상태 보존을 검증한다.
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>창 안에 도달 가능한 조작과 최소 본문 공간.</summary>
    public sealed class SheetAccessibilityTests
    {
        /// <summary>인물·종문·지역·전쟁·대회·설정·도감의 큰 글자 조작이 가로로 잘리지 않는다.</summary>
        /// <param name="scale">글자 배율.</param>
        [AvaloniaTest]
        [TestCase(1.0)]
        [TestCase(1.3)]
        [TestCase(2.0)]
        public void Sheets_AllKindsRemainReachable(double scale)
        {
            Typography.SetScale(scale);
            MainView view = CreateView();
            Window window = new() { Width = 360, Height = 640, Content = view };
            window.Show();
            try
            {
                int person = view.Session.Runner.Read(world => world.Cultivators[0].Id);
                SheetLink[] links =
                {
                    SheetLink.ToCultivator(person), SheetLink.ToSect(1), SheetLink.ToRegion(8),
                    SheetLink.ToWar(1), SheetLink.ToTournament(1),
                    new(LinkKind.Settings, 0), new(LinkKind.Help, 0),
                };
                foreach (SheetLink link in links)
                {
                    view.Sheets.CloseAll();
                    view.Open(link);
                    window.UpdateLayout();
                    AssertButtons(view.Sheets.View, view, 360);
                    ScrollViewer body = view.Sheets.View.GetVisualDescendants().OfType<ScrollViewer>().First();
                    Assert.That(body.Bounds.Height, Is.GreaterThan(90), link.Kind.ToString());
                    string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screens");
                    Directory.CreateDirectory(directory);
                    using WriteableBitmap frame = window.CaptureRenderedFrame()!;
                    frame.Save(Path.Combine(directory, "sheet-" + link.Kind + "-" + (int)(scale * 100) + ".png"),
                        PngBitmapEncoderOptions.Default);
                }
            }
            finally
            {
                window.Close();
                Typography.SetScale(1);
            }
        }

        /// <summary>키보드가 창을 줄여도 검색·진행 제어를 남기고 닫으면 원래 탐색을 복원한다.</summary>
        [AvaloniaTest]
        public void Keyboard_200PercentKeepsSearchAndTransport()
        {
            Typography.SetScale(2);
            MainView view = CreateView();
            Window window = new() { Width = 360, Height = 640, Content = view };
            window.Show();
            try
            {
                view.Navigate(Destinations.People);
                window.UpdateLayout();
                TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single();
                search.Text = "검색 보존";
                byte[] before = view.Session.Runner.Read(WorldSerializer.ToBytes);
                window.Height = 340;
                view.ApplyKeyboardState(true);
                window.UpdateLayout();
                Assert.That(view.Pages[2].View.Bounds.Height, Is.GreaterThan(180));
                Assert.That(view.Navigation.View.IsVisible, Is.False);
                AssertButtons(view, view, 360);
                window.Height = 640;
                view.ApplyKeyboardState(false);
                window.UpdateLayout();
                Assert.That(view.Navigation.View.IsVisible, Is.True);
                Assert.That(search.Text, Is.EqualTo("검색 보존"));
                Assert.That(view.Session.Runner.Read(WorldSerializer.ToBytes), Is.EqualTo(before));
            }
            finally
            {
                window.Close();
                Typography.SetScale(1);
            }
        }

        /// <summary>전면 배치 키보드에서도 진행 버튼이 실제 가림 영역 위에 남는다.</summary>
        [AvaloniaTest]
        public void OverlayKeyboard_ReservesOcclusionAndRestoresSafeArea()
        {
            MainView view = CreateView();
            Window window = new() { Width = 360, Height = 640, Content = view };
            window.Show();
            try
            {
                view.Navigate(Destinations.People);
                view.ApplyInsets(new Thickness(0, 24, 0, 16));
                view.ApplyKeyboardState(true, new Rect(0, 340, 360, 300));
                window.UpdateLayout();
                Button transport = view.GetVisualDescendants().OfType<Button>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "play-toggle");
                Point bottom = transport.TranslatePoint(new Point(0, transport.Bounds.Height), view)!.Value;
                Assert.That(bottom.Y, Is.LessThanOrEqualTo(340));
                Assert.That(view.Padding.Bottom, Is.EqualTo(300));
                window.Height = 340;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.That(view.Padding.Bottom, Is.EqualTo(16), "줄어든 창에는 여백을 중복하지 않음");
                view.ApplyKeyboardState(false);
                window.Height = 640;
                window.UpdateLayout();
                Assert.That(view.Padding, Is.EqualTo(new Thickness(0, 24, 0, 16)));
            }
            finally { window.Close(); }
        }

        private static MainView CreateView()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            return new MainView(new GameSession(new SaveStore(Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "sheet-layout-saves")), world));
        }

        private static void AssertButtons(Control scope, Control root, double width)
        {
            foreach (Button button in scope.GetVisualDescendants().OfType<Button>())
            {
                if (!button.IsEffectivelyVisible || button.Bounds.Height <= 0
                    || button.GetVisualAncestors().OfType<ScrollBar>().Any())
                {
                    continue;
                }

                Point? position = button.TranslatePoint(default, root);
                if (position is not { } point || point.Y < 0 || point.Y >= root.Bounds.Height)
                {
                    continue;
                }

                string? context = button.Content?.ToString();
                Assert.That(button.Bounds.Width, Is.GreaterThanOrEqualTo(47.9), context);
                Assert.That(button.Bounds.Height, Is.GreaterThanOrEqualTo(47.9), context);
                Assert.That(point.X, Is.GreaterThanOrEqualTo(-.1), context);
                Assert.That(point.X + button.Bounds.Width, Is.LessThanOrEqualTo(width + .1), context);
            }
        }
    }
}
