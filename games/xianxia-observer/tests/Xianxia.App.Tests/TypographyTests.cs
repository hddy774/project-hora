// 실제 시스템 글자 배율에서 탐색·터치·가로 배치가 유지되는지 렌더링으로 확인한다.
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
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
    /// <summary>화면을 축소하지 않는 큰 글자와 상태 보존 검증.</summary>
    public sealed class TypographyTests
    {
        /// <summary>두 화면 크기와 세 배율에서 모든 기존 목적지를 그린다.</summary>
        /// <param name="width">화면 너비.</param>
        /// <param name="height">화면 높이.</param>
        /// <param name="scale">시스템 글자 배율.</param>
        [AvaloniaTest]
        [TestCase(360, 640, 1.0)]
        [TestCase(360, 640, 1.3)]
        [TestCase(360, 640, 2.0)]
        [TestCase(412, 860, 1.0)]
        [TestCase(412, 860, 1.3)]
        [TestCase(412, 860, 2.0)]
        public void Layout_LargeTextKeepsControlsReachable(int width, int height, double scale)
        {
            Typography.SetScale(scale);
            MainView view = CreateView();
            Window window = new() { Width = width, Height = height, Content = view };
            try
            {
                window.Show();
                view.ApplyInsets(new Thickness(0, 24, 0, 24));
                for (int page = 0; page < view.Pages.Length; page++)
                {
                    view.SelectTab(page);
                    window.UpdateLayout();
                    AssertButtonsFit(view, width);
                }

                view.SelectTab(0);
                window.UpdateLayout();
                if (width == 360 && scale <= 1.3)
                {
                    Assert.That(view.Pages[0].View.Bounds.Height,
                        Is.GreaterThanOrEqualTo(scale == 1 ? 360 : 280));
                }

                using WriteableBitmap frame = window.CaptureRenderedFrame()!;
                string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screens");
                Directory.CreateDirectory(directory);
                frame.Save(Path.Combine(directory,
                    "layout-" + width + "-" + (int)(scale * 100) + ".png"), PngBitmapEncoderOptions.Default);
            }
            finally
            {
                window.Close();
                Typography.SetScale(1);
            }
        }

        /// <summary>열린 인물 목록의 검색과 세계를 보존하면서 기존 글자를 실제로 키운다.</summary>
        [AvaloniaTest]
        public void SystemScale_UpdatesExistingTextAndPreservesSearchAndWorld()
        {
            MainView view = CreateView();
            Window window = new() { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                view.Navigate(Destinations.People);
                window.UpdateLayout();
                TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single();
                search.Text = "없는 인물";
                byte[] before = view.Session.Runner.Read(WorldSerializer.ToBytes);
                TextBlock title = view.GetVisualDescendants().OfType<TextBlock>()
                    .Single(text => text.Text == "선로만상록 프로젝트");
                double original = title.FontSize;
                App.SetDeviceFontScale(2);
                window.UpdateLayout();
                Assert.That(title.FontSize, Is.EqualTo(original * 2));
                Assert.That(search.FontSize, Is.EqualTo(30));
                Assert.That(search.Text, Is.EqualTo("없는 인물"));
                Assert.That(view.Session.Runner.Read(WorldSerializer.ToBytes), Is.EqualTo(before));
            }
            finally
            {
                window.Close();
                Typography.SetScale(1);
            }
        }

        private static MainView CreateView()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "typography-saves",
                TestContext.CurrentContext.Test.Name);
            return new MainView(new GameSession(new SaveStore(path),
                WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 })));
        }

        private static void AssertButtonsFit(MainView view, double width)
        {
            foreach (Button button in view.GetVisualDescendants().OfType<Button>())
            {
                // 네이티브 스크롤바 내부 화살표는 본문 스크롤 제스처의 부품이다.
                // 게임이 제공하는 모든 동작 버튼은 아래에서 실제 배치 크기로 검사한다.
                if (button.GetVisualAncestors().OfType<ScrollBar>().Any())
                {
                    continue;
                }

                if (!button.IsEffectivelyVisible || button.Bounds.Height <= 0)
                {
                    continue;
                }

                Point? position = button.TranslatePoint(default, view);
                if (position is not { } point || point.Y < 0 || point.Y >= view.Bounds.Height)
                {
                    continue;
                }

                string context = view.CurrentDestination + ": " + button.Content;
                Assert.That(button.Bounds.Width, Is.GreaterThanOrEqualTo(47.9), context);
                Assert.That(button.Bounds.Height, Is.GreaterThanOrEqualTo(47.9), context);
                Assert.That(point.X, Is.GreaterThanOrEqualTo(-0.1), context);
                Assert.That(point.X + button.Bounds.Width, Is.LessThanOrEqualTo(width + 0.1), context);
            }
        }
    }
}
