// 천기 소비 전에 대상·현생·경지·효과·잔액을 확인하고 같은 요청은 한 번만 실행한다.
using Avalonia.Automation;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Sheets
{
    /// <summary>단일 사용 기연 확인. 확인 대기 중 윤회·비승·세계 교체를 거부한다.</summary>
    internal sealed class FortuneConfirmation
    {
        private readonly INavigator _navigator;
        private readonly int _id;
        private readonly TextBlock _preview = Ui.Text();
        private Request? _pending;

        /// <summary>취소 가능한 확인 영역을 만든다.</summary>
        /// <param name="navigator">실행기와 안내.</param>
        /// <param name="id">영혼 번호.</param>
        internal FortuneConfirmation(INavigator navigator, int id)
        {
            _navigator = navigator;
            _id = id;
            Button confirm = Ui.Button("천기 2 사용", Confirm, "primary");
            Button cancel = Ui.Button("취소", Cancel);
            AutomationProperties.SetAutomationId(confirm, "fortune-confirm");
            AutomationProperties.SetAutomationId(cancel, "fortune-cancel");
            View = Ui.Section("기연 하사 확인", _preview, Ui.Cells(2, 8, cancel, confirm));
            View.IsVisible = false;
        }

        /// <summary>확인 영역.</summary>
        internal Control View { get; }

        /// <summary>읽기 잠금 안에서 미리보기를 만들며 아직 소비하지 않는다.</summary>
        internal void Begin()
        {
            _pending = _navigator.Session.Runner.Read(Capture);
            if (_pending is not { } request)
            {
                _navigator.Toast("대상이 없거나 천기가 부족합니다. 현재 상태를 다시 확인해 주세요.");
                return;
            }

            _preview.Text = request.Preview;
            View.IsVisible = true;
        }

        private Request? Capture(World world)
        {
            if (world.Find(_id) is not { } person || !InterventionSystem.CanBestowFortune(world, person)
                || _navigator.Session.Runner.IsFaulted)
            {
                return null;
            }

            int cost = InterventionSystem.Cost(InterventionKind.BestowFortune);
            string preview = person.DisplayName + " · " + person.RealmText + " · " + person.LifeNumber
                + "번째 생\n깨달음 +400 · 현재 단계 영기 50% · 부상 완치 · 도심 +20\n비용 천기 " + cost
                + " · 현재 " + world.HeavenlyFavor + " → 사용 후 " + (world.HeavenlyFavor - cost)
                + "\n진행 중 대상 상태가 바뀌면 다시 확인해야 합니다.";
            return new Request(world, person, person.LifeNumber, person.Realm, person.Stage, preview);
        }

        private void Cancel()
        {
            _pending = null;
            View.IsVisible = false;
        }

        private void Confirm()
        {
            Request? request = _pending;
            Cancel(); // 쓰기와 재진입 전에 비운다. 중복 클릭은 같은 요청을 재사용할 수 없다.
            if (request is null)
            {
                return;
            }

            bool success = _navigator.Session.Runner.Write(world => ReferenceEquals(world, request.World)
                && ReferenceEquals(world.Find(_id), request.Person) && request.Person.LifeNumber == request.Life
                && request.Person.Realm == request.Realm && request.Person.Stage == request.Stage
                && !_navigator.Session.Runner.IsFaulted && InterventionSystem.BestowFortune(world, request.Person));
            _navigator.Toast(success ? "기연을 내렸습니다. 천기 2점을 사용했습니다."
                : "상태가 바뀌었거나 천기가 부족합니다. 소비하지 않았습니다. 다시 확인해 주세요.");
            _navigator.Invalidate();
        }

        /// <summary>다른 세계·다른 현생에 확인을 적용하지 않기 위한 캡처.</summary>
        /// <param name="World">대상 세계.</param>
        /// <param name="Person">대상 객체.</param>
        /// <param name="Life">현생 번호.</param>
        /// <param name="Realm">경지.</param>
        /// <param name="Stage">단계.</param>
        /// <param name="Preview">잠금 안에서 캡처한 설명.</param>
        private sealed record Request(World World, Cultivator Person, int Life, int Realm, int Stage, string Preview);
    }
}
