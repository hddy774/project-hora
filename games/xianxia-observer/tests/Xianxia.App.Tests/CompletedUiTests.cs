// 완성된 탭·시트의 실제 입력·렌더링, 세계 교체 확인과 천기 차감·중복 개입 방지를 검사한다.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>연결된 화면과 저장·개입 흐름의 통합 검사.</summary>
    public sealed class CompletedUiTests
    {
        /// <summary>일곱 탭과 모든 상세 하위 탭은 실제 세계 데이터를 렌더링한다.</summary>
        [AvaloniaTest]
        public void EveryPageAndSheet_AfterWorldEvents_RendersWithoutPlaceholders()
        {
            GameSession session = CreateSession(Balance.FirstWarYear + Balance.WarDuration + 200);
            MainView view = new(session);
            Window window = Show(view);
            try
            {
                for (int tab = 0; tab < view.Pages.Length; tab++)
                {
                    view.SelectTab(tab);
                    AssertReady(window);
                    Save(window, "tab-" + tab + ".png");
                }

                World world = session.Runner.Read(value => value);
                Assert.That(world.Tournaments, Is.Not.Empty);
                Assert.That(world.Wars, Is.Not.Empty);
                SheetLink[] links =
                {
                    SheetLink.ToCultivator(world.Cultivators[0].Id), SheetLink.ToSect(1),
                    SheetLink.ToRegion(0), SheetLink.ToTournament(world.Tournaments[0].Number),
                    SheetLink.ToWar(world.Wars[0].Number), new(LinkKind.Settings, 0), new(LinkKind.Help, 0),
                };
                foreach (SheetLink link in links)
                {
                    RenderSheet(view, window, link);
                }
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>검색의 빈 결과와 관심 표시가 저장 후에도 유지되는지 확인한다.</summary>
        [AvaloniaTest]
        public void People_SearchAndFollow_UpdatesListAndPersistsAcrossReload()
        {
            GameSession session = CreateSession(0);
            MainView view = new(session);
            Window window = Show(view);
            try
            {
                view.SelectTab(2);
                window.UpdateLayout();
                TextBox search = window.GetVisualDescendants().OfType<TextBox>().Single();
                search.Text = "存在しない名前";
                view.RefreshNow(true);
                Assert.That(Texts(window), Does.Contain("조건에 맞는 인물이 없습니다."));
                search.Text = "";
                view.RefreshNow(true);
                int id = session.Runner.Read(world => world.Cultivators[0].Id);
                view.Open(SheetLink.ToCultivator(id));
                Click(window, "☆ 관심 표시");
                view.RefreshNow(true);
                Assert.That(session.Runner.Read(world => world.IsFollowed(id)), Is.True);
                Assert.That(session.Save(), Is.True);
                GameSession loaded = GameSession.LoadOrCreate(session.Store);
                Assert.That(loaded.Runner.Read(world => world.IsFollowed(id)), Is.True);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>기연은 정확히 천기 2점을 쓰며 지역의 중복 사건은 추가 차감하지 않는다.</summary>
        [AvaloniaTest]
        public void InterventionButtons_InsufficientFavorOrDuplicateEvent_DoNotSpendAgain()
        {
            GameSession session = CreateSession(0);
            session.Runner.Write(world => world.HeavenlyFavor = 10);
            MainView view = new(session);
            Window window = Show(view);
            try
            {
                int id = session.Runner.Read(world => world.Cultivators[0].Id);
                view.Open(SheetLink.ToCultivator(id));
                Click(window, "기연 하사 · 천기 2");
                view.RefreshNow(true);
                Assert.That(session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(8));
                Assert.That(session.Runner.Read(world => world.Find(id)!.HasTitle(TitleId.Chosen)), Is.True);
                view.Sheets.CloseAll();
                view.Open(SheetLink.ToRegion(8));
                Click(window, "비경 개방 · 천기 3");
                Click(window, "비경 개방 · 천기 3");
                view.RefreshNow(true);
                Assert.That(session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(5));
                Assert.That(session.Runner.Read(world => world.SecretRealms.Count), Is.EqualTo(1));
                Click(window, "수조 소환 · 천기 3");
                view.RefreshNow(true);
                Assert.That(session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(2));
                Assert.That(session.Runner.Read(world => world.BeastTides.Count), Is.EqualTo(1));
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>잘못된 입력·확인 누락은 진행을 보존하고, 확인한 새 세계만 저장까지 교체한다.</summary>
        [AvaloniaTest]
        public async Task Settings_CreateWorld_RequiresValidInputAndExplicitConfirmation()
        {
            GameSession session = CreateSession(20);
            MainView view = new(session);
            Window window = Show(view);
            try
            {
                view.Open(new SheetLink(LinkKind.Settings, 0));
                window.UpdateLayout();
                TextBox[] fields = window.GetVisualDescendants().OfType<TextBox>().ToArray();
                fields[0].Text = "77";
                fields[1].Text = "99";
                Click(window, "새 세계 시작");
                Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(20));
                fields[1].Text = "100";
                Click(window, "새 세계 시작");
                Assert.That(Texts(window), Does.Contain("세계 교체 확인란"));
                Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(20));
                window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
                Click(window, "새 세계 시작");
                for (int attempt = 0; attempt < 500 && !session.Store.Exists; attempt++)
                {
                    await Task.Delay(10);
                }

                Assert.That(session.Store.Exists, Is.True);
                GameSession loaded = GameSession.LoadOrCreate(session.Store);
                Assert.That(loaded.Runner.Read(world => world.Settings.Seed), Is.EqualTo(77));
                Assert.That(loaded.Runner.Read(world => world.Cultivators.Count), Is.EqualTo(100));
                Assert.That(loaded.Runner.Read(world => world.Year), Is.Zero);
                Assert.That(session.Runner.IsRunning, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>수동 저장과 확인한 불러오기가 실제 버튼 입력으로 같은 세계를 복구한다.</summary>
        [AvaloniaTest]
        public async Task Settings_SaveAndLoad_RestoresSavedYearOnlyAfterConfirmation()
        {
            GameSession session = CreateSession(20);
            MainView view = new(session);
            Window window = Show(view);
            try
            {
                view.Open(new SheetLink(LinkKind.Settings, 0));
                Click(window, "지금 저장");
                for (int attempt = 0; attempt < 500 && !Texts(window).Contains("저장했습니다", StringComparison.Ordinal);
                    attempt++)
                {
                    await Task.Delay(10);
                }

                Assert.That(session.Store.Exists, Is.True);
                session.Runner.Advance(10);
                view.RefreshNow(true);
                Click(window, "저장 불러오기");
                Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(30));
                window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
                Click(window, "저장 불러오기");
                for (int attempt = 0; attempt < 500 && session.Runner.Read(world => world.Year) != 20; attempt++)
                {
                    await Task.Delay(10);
                }

                Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(20));
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>새 세계와 존재하지 않는 과거 사건·영혼에서도 오류 없이 빈 상태를 표시한다.</summary>
        [AvaloniaTest]
        public void MissingTargetsAndEmptyHistory_RenderGracefulEmptyStates()
        {
            MainView view = new(CreateSession(0));
            Window window = Show(view, 360);
            try
            {
                foreach (LinkKind kind in Enum.GetValues<LinkKind>().Where(kind => kind != LinkKind.None))
                {
                    view.Open(new SheetLink(kind, 999999));
                    AssertReady(window);
                    view.Sheets.CloseAll();
                }
            }
            finally
            {
                window.Close();
            }
        }

        private static void RenderSheet(MainView view, Window window, SheetLink link)
        {
            view.Open(link);
            SheetBase sheet = view.Sheets.Top!;
            Assert.That(sheet.Title, Is.Not.Empty);
            AssertReady(window);
            if (sheet.Tabs is Panel tabs)
            {
                foreach (Button button in tabs.Children.OfType<Button>())
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    view.RefreshNow(true);
                    AssertReady(window);
                }
            }

            Save(window, "sheet-" + link.Kind + ".png");
            view.Sheets.CloseAll();
        }

        private static GameSession CreateSession(int years)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "saves",
                TestContext.CurrentContext.Test.Name);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            GameSession session = new(new SaveStore(path), WorldFactory.Create(new WorldSettings
            {
                Seed = 7,
                Population = 100,
            }));
            session.Runner.Advance(years);
            return session;
        }

        private static Window Show(MainView view, int width = 412)
        {
            Window window = new() { Width = width, Height = 860, Content = view };
            window.Show();
            view.RefreshNow(true);
            return window;
        }

        private static string Texts(Window window)
        {
            return string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));
        }

        private static void Click(Window window, string content)
        {
            window.UpdateLayout();
            window.GetVisualDescendants().OfType<Button>().Single(button => button.Content?.ToString() == content)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private static void AssertReady(Window window)
        {
            window.UpdateLayout();
            Assert.That(Texts(window), Does.Not.Contain("준비 중"));
            Assert.That(Texts(window), Does.Not.Contain("구현 예정"));
            Assert.That(window.CaptureRenderedFrame(), Is.Not.Null);
        }

        private static void Save(Window window, string name)
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screens");
            Directory.CreateDirectory(directory);
            using WriteableBitmap frame = window.CaptureRenderedFrame()!;
            frame.Save(Path.Combine(directory, name), PngBitmapEncoderOptions.Default);
        }
    }
}
