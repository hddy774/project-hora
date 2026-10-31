// 부분 처리된 해가 마지막 정상 저장을 덮지 않는지, 정지 중 관찰도 자동 저장되는지 확인한다.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>실제 실행 스레드·파일·화면을 통한 오류 격리 회귀.</summary>
    public sealed class RunnerFaultTests
    {
        private string _directory = "";
        private SimulationRunner? _runner;

        /// <summary>검사마다 저장 폴더를 격리한다.</summary>
        [SetUp]
        public void SetUp() => _directory = Directory.CreateTempSubdirectory("xianxia-runner-").FullName;

        /// <summary>검사 실패 시에도 작업 스레드와 저장 폴더를 정리한다.</summary>
        [TearDown]
        public void TearDown()
        {
            _runner?.Stop();
            Directory.Delete(_directory, true);
        }

        /// <summary>다섯 번째 해의 일부 변경 뒤 실패해도 재개·수동·백그라운드·종료 저장을 막는다.</summary>
        [Test]
        public void Fault_StopsAndBlocksResumeAndAllSaves()
        {
            GameSession session = CreateSession();
            Assert.That(session.Save(), Is.True);
            byte[] original = File.ReadAllBytes(session.Store.FilePath);
            FailOnFifthStep(session.Runner);
            int year = session.Runner.Read(world => world.Year);
            Assert.That(year, Is.EqualTo(4));
            session.Runner.SetRunning(true);
            Thread.Sleep(1100);
            Assert.That(session.Runner.IsRunning, Is.False);
            Assert.That(session.Runner.Read(world => world.Year), Is.EqualTo(year));
            Assert.That(session.Save(), Is.False);
            Assert.That(session.LastSaveMessage, Is.EqualTo(GameSession.FaultMessage));
            session.OnBackground();
            session.OnForeground();
            session.Shutdown();
            Assert.That(File.ReadAllBytes(session.Store.FilePath), Is.EqualTo(original));
        }

        /// <summary>마지막 정상 저장으로 교체하면 오류를 해제하고 진행할 수 있다.</summary>
        [Test]
        public void LoadSaved_ClearsFaultAndResumes()
        {
            GameSession session = CreateSession();
            Assert.That(session.Save(), Is.True);
            FailOnFifthStep(session.Runner);
            session.Runner.BeforeStepForTest = null;
            Assert.That(session.LoadSaved(out string message), Is.True, message);
            Assert.That(session.Runner.IsFaulted, Is.False);
            Assert.That(session.Runner.Error, Is.Null);
            session.Runner.SetRunning(true);
            Assert.That(SpinWait.SpinUntil(() => session.Runner.Read(world => world.Year) > 0,
                TimeSpan.FromSeconds(3)), Is.True);
        }

        /// <summary>정지 중 변경이 있을 때만 자동 저장하고 동일 버전을 반복 저장하지 않는다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Paused_AutosavesOnlyAfterChange(bool change)
        {
            int saves = 0;
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            _runner = new SimulationRunner(world, () => Interlocked.Increment(ref saves),
                TimeSpan.FromMilliseconds(200));
            _runner.Start();
            if (change)
            {
                _runner.Write(value =>
                {
                    value.SetFollowed(value.Cultivators[0].Id, true);
                    return true;
                });
                Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref saves) == 1,
                    TimeSpan.FromMilliseconds(1700)), Is.True);
            }

            Thread.Sleep(1100);
            Assert.That(Volatile.Read(ref saves), Is.EqualTo(change ? 1 : 0));
        }

        /// <summary>스레드 객체의 일회 수명 계약을 명시적으로 강제한다.</summary>
        [Test]
        public void Start_AfterStop_Throws()
        {
            GameSession session = CreateSession();
            session.Runner.Start();
            session.Runner.Stop();
            Assert.Throws<InvalidOperationException>(() => session.Runner.Start());
        }

        /// <summary>오류 세계에서 설정 저장·상단 진행을 실제로 비활성화한다.</summary>
        [AvaloniaTest]
        public void SettingsSheet_DisablesSaveWhenFaulted()
        {
            GameSession session = CreateSession();
            FailOnFifthStep(session.Runner);
            MainView view = new(session);
            Window window = new() { Width = 360, Height = 800, Content = view };
            window.Show();
            view.Open(new SheetLink(LinkKind.Settings, 0));
            view.RefreshNow(true);
            window.UpdateLayout();
            Button save = window.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Content is string text && text == "지금 저장");
            Assert.That(save.IsEnabled, Is.False);
            string text = string.Join("\n", window.GetVisualDescendants().OfType<TextBlock>().Select(row => row.Text));
            Assert.That(text, Does.Contain(GameSession.FaultMessage));
            window.Close();
        }

        private GameSession CreateSession()
        {
            GameSession session = new(new SaveStore(_directory),
                WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 }));
            _runner = session.Runner;
            return session;
        }

        private static void FailOnFifthStep(SimulationRunner runner)
        {
            int steps = 0;
            runner.BeforeStepForTest = world =>
            {
                if (++steps == 5)
                {
                    // throw 이전의 실제 부분 변경이 디스크로 새면 검사에서 발견된다.
                    world.Cultivators[0].SpiritStones = -123;
                    throw new InvalidOperationException("고장 주입");
                }
            };
            runner.SetSpeed(5);
            runner.Start();
            runner.SetRunning(true);
            Assert.That(SpinWait.SpinUntil(() => runner.IsFaulted, TimeSpan.FromSeconds(3)), Is.True);
        }
    }
}
