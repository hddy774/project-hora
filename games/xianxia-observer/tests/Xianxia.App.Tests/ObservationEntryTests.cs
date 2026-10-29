// 새 관찰 진입과 보류 알림이 저장된 사건만 읽고 세계를 바꾸지 않는지 검증한다.
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>추천 순서와 알림 보류의 회귀.</summary>
    public sealed class ObservationEntryTests
    {
        private static readonly int[] s_expected = { 1, 3, 2 };
        /// <summary>최근100년·생존·중복 제거·동년 ID 정렬과 조회 무영향을 검사한다.</summary>
        [Test]
        public void Recommendations_UseStoredEventsAndStableTieBreak()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            world.Year = 1000;
            world.Log.Add(990, LogKind.Breakthrough, "돌파", 2);
            world.Log.Add(995, LogKind.Tournament, "대회", 3);
            world.Log.Add(995, LogKind.Breakthrough, "돌파", 1);
            world.Log.Add(995, LogKind.Breakthrough, "중복", 1);
            world.Log.Add(999, LogKind.Breakthrough, "소멸 대상", 999999);
            world.Log.Add(800, LogKind.Breakthrough, "보관 범위 밖", 4);
            world.Log.Add(1001, LogKind.Breakthrough, "미래", 5);
            byte[] before = WorldSerializer.ToBytes(world);
            Assert.That(RecommendationQuery.Select(world).Select(entry => entry.Person.Id),
                Is.EqualTo(s_expected));
            Assert.That(RecommendationQuery.Select(world).Select(entry => entry.Person.Id),
                Is.EqualTo(s_expected));
            Assert.That(WorldSerializer.ToBytes(world), Is.EqualTo(before));
        }

        /// <summary>사건 후보0이면 현재 가장 강한 인물을 보여 주고 상세로 연결할 수 있다.</summary>
        [Test]
        public void Recommendations_EmptyHistoryUsesPower()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            world.Cultivators[^1].Realm = 8;
            Assert.That(RecommendationQuery.Select(world)[0].Person.Id, Is.EqualTo(world.Cultivators[^1].Id));
            Assert.That(RecommendationQuery.Select(world)[0].Reason, Does.Contain("전투력"));
        }

        /// <summary>상세/검색 중 최신 보관 알림을 유지하고 복귀 후1건만 표시한다.</summary>
        [AvaloniaTest]
        public void Toast_DefersDuringSheetOrIme()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            ToastHost host = new(_ => { });
            Window window = new() { Width = 360, Height = 640, Content = host.View };
            window.Show();
            try
            {
                host.Refresh(world, true);
                int initialNotices = world.Chronicle.Notices.Count;
                world.Chronicle.Notify(1, "먼저 온 알림", 1, true);
                host.Refresh(world, false);
                world.Chronicle.Notify(2, "최신 보관 알림", 2, true);
                byte[] before = WorldSerializer.ToBytes(world);
                host.Refresh(world, false);
                Assert.That(host.View.IsVisible, Is.False);
                host.Refresh(world, true);
                window.UpdateLayout();
                Assert.That(host.View.GetVisualDescendants().OfType<Button>().Count(), Is.EqualTo(1));
                Assert.That(host.View.GetVisualDescendants().OfType<TextBlock>()
                    .Any(block => block.Text?.Contains("최신 보관 알림", System.StringComparison.Ordinal) == true), Is.True);
                Assert.That(world.Chronicle.Notices, Has.Count.EqualTo(initialNotices + 2));
                Assert.That(WorldSerializer.ToBytes(world), Is.EqualTo(before));
            }
            finally { window.Close(); }
        }
    }
}
