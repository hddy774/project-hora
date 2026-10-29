// 실제 탐색 동작에서 기록되는 전환과 관찰 무영향을 검증한다.
using System.Collections.Generic;
using System.IO;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>로그는 실제 화면 전환 때만 발생한다.</summary>
    public sealed class UiTraceTests
    {
        /// <summary>탭·시트 전환을 실제 실행하고 정상 동작과 로그를 함께 확인한다.</summary>
        [AvaloniaTest]
        public void UiTrace_WritesActualTabAndSheetOpenClose()
        {
            List<string> lines = new();
            UiTrace.Sink = lines.Add;
            try
            {
                World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
                byte[] before = WorldSerializer.ToBytes(world);
                GameSession session = new(new SaveStore(Path.Combine(TestContext.CurrentContext.WorkDirectory,
                    "trace-saves")), world);
                MainView view = new(session);
                view.Navigate(Destinations.People);
                view.Open(SheetLink.ToCultivator(world.Cultivators[0].Id));
                view.CloseSheet();
                Assert.That(lines, Does.Contain("[xo-ui] tab 2 인물"));
                Assert.That(lines, Does.Contain("[xo-ui] sheet Cultivator " + world.Cultivators[0].Id));
                Assert.That(lines, Does.Contain("[xo-ui] close Cultivator"));
                Assert.That(WorldSerializer.ToBytes(world), Is.EqualTo(before));
                lines.Clear();
                WorldSimulator.Run(world, 100);
                view.RefreshNow(false);
                Assert.That(lines, Is.Empty, "연간 진행이나 일반 렌더가 전환 로그를 반복하면 안 된다.");
            }
            finally
            {
                UiTrace.Sink = null;
            }
        }
    }
}
