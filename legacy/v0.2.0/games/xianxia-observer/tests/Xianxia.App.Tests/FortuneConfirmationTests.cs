// 실제 확인 UI에서 취소·중복·천기 부족·윤회·비승의 잘못된 소비를 방지한다.
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>천기 소비 전후의 사용자 흐름.</summary>
    public sealed class FortuneConfirmationTests
    {
        /// <summary>취소는 무소비, 정상 확인 뒤 같은 버튼을 다시 눌러도 한 번만 소비한다.</summary>
        [AvaloniaTest]
        public void Fortune_CancelAndDuplicateConfirmation_DoNotSpendAgain()
        {
            MainView view = CreateView();
            Window window = Show(view);
            try
            {
                int initial = view.Session.Runner.Read(world => world.HeavenlyFavor);
                Click(window, "fortune-request");
                Click(window, "fortune-cancel");
                Assert.That(view.Session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(initial));
                Click(window, "fortune-request");
                Button confirm = Find(window, "fortune-confirm");
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.That(view.Session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(initial - 2));
            }
            finally { window.Close(); }
        }

        /// <summary>미리보기 이후 실제 상태가 바뀌었으면 소비하지 않는다.</summary>
        /// <param name="change">부족/윤회/비승/세계 교체.</param>
        [AvaloniaTest]
        [TestCase("balance")]
        [TestCase("rebirth")]
        [TestCase("ascension")]
        [TestCase("world")]
        public void Fortune_RechecksTargetBeforeSpend(string change)
        {
            MainView view = CreateView();
            Window window = Show(view);
            try
            {
                Click(window, "fortune-request");
                Button confirm = Find(window, "fortune-confirm");
                view.Session.Runner.Write(world =>
                {
                    Cultivator person = world.Cultivators[0];
                    if (change == "balance")
                    {
                        world.HeavenlyFavor = 1;
                    }

                    if (change == "rebirth")
                    {
                        LifeCycleSystem.Die(world, person, DeathCause.Lifespan);
                    }

                    if (change == "ascension")
                    {
                        LifeCycleSystem.Ascend(world, person);
                    }

                    return true;
                });
                if (change == "world")
                {
                    view.Session.NewWorld(new WorldSettings { Seed = 20, Population = 100 });
                }

                int before = view.Session.Runner.Read(world => world.HeavenlyFavor);
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.That(view.Session.Runner.Read(world => world.HeavenlyFavor), Is.EqualTo(before));
            }
            finally { window.Close(); }
        }

        private static MainView CreateView()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            return new MainView(new GameSession(new SaveStore(Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "fortune-saves", TestContext.CurrentContext.Test.Name)), world));
        }

        private static Window Show(MainView view)
        {
            Window window = new() { Width = 360, Height = 640, Content = view };
            window.Show();
            int id = view.Session.Runner.Read(world => world.Cultivators[0].Id);
            view.Open(SheetLink.ToCultivator(id));
            window.UpdateLayout();
            return window;
        }

        private static Button Find(Window window, string id)
        {
            window.UpdateLayout();
            return window.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == id);
        }

        private static void Click(Window window, string id)
            => Find(window, id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
