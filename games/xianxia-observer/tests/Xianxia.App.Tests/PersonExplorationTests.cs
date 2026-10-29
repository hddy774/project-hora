// 실제 목록 조작과 상세 복귀에서 검색·쪽·스크롤을 보존하고 기존 상세 정보가 사라지지 않는지 확인한다.
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>인물 탐색과 세계 교체의 사용자 흐름 회귀.</summary>
    public sealed class PersonExplorationTests
    {
        /// <summary>검색·소속·정렬·2쪽에서 상세를 열고 뒤로 돌아와 같은 목록과 위치를 읽는다.</summary>
        [AvaloniaTest]
        public void People_BackRestoresQueryAndScroll()
        {
            MainView view = CreateView();
            using WindowScope scope = new(view);
            view.Navigate(Destinations.People);
            scope.Window.UpdateLayout();
            TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single();
            search.Text = "공통";
            Click(view, "청운종");
            Click(view, "정렬: 경지 ▾");
            Click(view, "나이");
            view.RefreshNow(true);
            Click(view, "다음 ›");
            view.RefreshNow(true);
            scope.Window.UpdateLayout();
            view.Pages[2].View.Offset = new Vector(0, 120);
            Vector offset = view.Pages[2].View.Offset;
            Assert.That(offset.Y, Is.GreaterThan(0));
            string[] before = Texts(view.Pages[2].View);
            int id = view.Session.Runner.Read(world => world.Cultivators[0].Id);
            view.Open(SheetLink.ToCultivator(id));
            scope.Window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
            scope.Window.UpdateLayout();
            Assert.That(search.Text, Is.EqualTo("공통"));
            Assert.That(Texts(view.Pages[2].View), Is.EqualTo(before));
            Assert.That(before, Does.Contain("2 / 2"));
            Assert.That(view.Pages[2].View.Offset, Is.EqualTo(offset));
        }

        /// <summary>실패한 불러오기는 검색을 보존하고 실제 교체 성공만 초기화한다.</summary>
        [AvaloniaTest]
        public void WorldReplacement_OnlySuccessResetsQuery()
        {
            MainView view = CreateView();
            using WindowScope scope = new(view);
            view.Navigate(Destinations.People);
            scope.Window.UpdateLayout();
            TextBox search = view.GetVisualDescendants().OfType<TextBox>().Single();
            search.Text = "이전 검색";
            Assert.That(view.Session.LoadSaved(out _), Is.False);
            view.RefreshNow(true);
            Assert.That(search.Text, Is.EqualTo("이전 검색"));
            view.Session.NewWorld(new WorldSettings { Seed = 20, Population = 100 });
            view.RefreshNow(true);
            Assert.That(search.Text, Is.Empty);
            Assert.That(view.Session.WorldGeneration, Is.EqualTo(1));
        }

        /// <summary>옛 전투력·소지·윤회 분류의 고유 정보가 새 네 분류에서 모두 접근 가능하다.</summary>
        [AvaloniaTest]
        public void Details_PreserveAllOriginalFields()
        {
            MainView view = CreateView();
            using WindowScope scope = new(view);
            int id = view.Session.Runner.Read(world => world.Cultivators[0].Id);
            view.Open(SheetLink.ToCultivator(id));
            Click(view.Sheets.View, "전투력 상세 ▾");
            view.RefreshNow(true);
            Assert.That(Texts(view.Sheets.View), Does.Contain("근골 / 공법 / 법보"));
            Click(view.Sheets.View, "수행");
            view.RefreshNow(true);
            Assert.That(Texts(view.Sheets.View), Does.Contain("소경지 돌파 확률"));
            Assert.That(Texts(view.Sheets.View), Does.Contain("영초 / 영철 / 내단"));
            Click(view.Sheets.View, "인연");
            view.RefreshNow(true);
            Assert.That(Texts(view.Sheets.View), Does.Contain("도반과 함께한 기간"));
            Click(view.Sheets.View, "생애");
            view.RefreshNow(true);
            Assert.That(Texts(view.Sheets.View), Does.Contain("현생 / 전생 기억"));
            Assert.That(Texts(view.Sheets.View), Does.Contain("영혼 기록"));
        }

        private static MainView CreateView()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            foreach (Cultivator person in world.Cultivators)
            {
                person.Name = "공통 이름 " + person.Id;
            }

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "people-saves",
                TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            return new MainView(new GameSession(new SaveStore(path), world));
        }

        private static string[] Texts(Control parent) => parent.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? "").ToArray();

        private static void Click(Control parent, string text)
        {
            TopLevel.GetTopLevel(parent)!.UpdateLayout();
            parent.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, text))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        /// <summary>실제 배치를 유지하고 예외 때도 창을 닫는다.</summary>
        private sealed class WindowScope : System.IDisposable
        {
            /// <summary>창을 연다.</summary>
            /// <param name="view">내용.</param>
            internal WindowScope(MainView view)
            {
                Window = new Window { Width = 360, Height = 640, Content = view };
                Window.Show();
            }

            /// <summary>실제 창.</summary>
            internal Window Window { get; }

            /// <inheritdoc />
            public void Dispose() => Window.Close();
        }
    }
}
