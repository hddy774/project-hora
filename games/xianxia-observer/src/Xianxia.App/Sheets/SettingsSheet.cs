// 설정: 명시적 저장·불러오기와 시드·인구를 입력한 새 세계. 파일 작업과 세계 생성은 UI 밖에서 수행한다.
using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>오프라인 저장과 세계 교체 설정. 파괴적인 교체는 화면 내 확인을 거친다.</summary>
    internal sealed class SettingsSheet : SheetBase
    {
        private readonly TextBlock _status = Ui.Muted();
        private readonly TextBlock _world = Ui.Muted();
        private readonly TextBox _seed = new() { PlaceholderText = "0 이상의 정수 시드" };
        private readonly TextBox _population = new() { Text = "1000", PlaceholderText = "인구 100~3000" };
        private readonly CheckBox _confirm = new() { Content = "불러오기·새 세계로 현재 진행을 교체합니다" };
        private readonly Button _save;
        private readonly Button _load;
        private readonly Button _create;
        private bool _busy;

        /// <summary>설정 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">설정 연결.</param>
        internal SettingsSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            Title = "설정 · 저장";
            _seed.Text = GameSession.ClockSeed().ToString(CultureInfo.InvariantCulture);
            _save = Ui.Button("지금 저장", () => RunOperation(() =>
            {
                navigator.Session.Save();
                return navigator.Session.LastSaveMessage;
            }), "small");
            _load = Ui.Button("저장 불러오기", Load, "small");
            _create = Ui.Button("새 세계 시작", CreateWorld, "primary");
            Body.Children.Add(Ui.Section("현재 세계", _world, _status, _save,
                Ui.Muted("90초마다 자동 저장합니다. 앱이 뒤로 가면 멈추고 저장하며, 돌아오면 이어갑니다.")));
            Body.Children.Add(Ui.Section("세계 교체", _confirm, _load,
                Ui.Title("새 세계 시드"), _seed, Ui.Title("인구 (100~3000)"), _population, _create,
                Ui.Muted("새 세계는 기존 저장을 교체합니다. 같은 시드와 인구는 같은 시작 세계를 만듭니다.")));
            Body.Children.Add(Ui.Section("선협 관찰 0.1.0", Ui.Muted("오프라인 · Android 7.0 이상 · 한국어"),
                Ui.Button("게임 도감 ›", () => navigator.Open(new SheetLink(LinkKind.Help, 0)), "small")));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            _world.Text = world.Settings.Name + " · " + Fmt.Years(world.Year) + " · 인구 "
                + world.Cultivators.Count + " · 시드 " + world.Settings.Seed;
            _save.IsEnabled = !_busy;
            _load.IsEnabled = !_busy && Navigator.Session.Store.Exists;
            _create.IsEnabled = !_busy;
            _seed.IsEnabled = !_busy;
            _population.IsEnabled = !_busy;
            _confirm.IsEnabled = !_busy;
            if (!_busy && string.IsNullOrEmpty(_status.Text))
            {
                _status.Text = Navigator.Session.LastSaveMessage;
            }
        }

        private void Load()
        {
            if (_confirm.IsChecked != true)
            {
                _status.Text = "세계 교체 확인란을 먼저 선택해 주세요.";
                return;
            }

            RunOperation(() =>
            {
                Navigator.Session.LoadSaved(out string message);
                return message;
            });
        }

        private void CreateWorld()
        {
            if (!ulong.TryParse(_seed.Text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed)
                || !int.TryParse(_population.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int population)
                || population is < 100 or > 3000)
            {
                _status.Text = "시드는 0 이상의 정수, 인구는 100~3000으로 입력해 주세요.";
                return;
            }

            if (_confirm.IsChecked != true)
            {
                _status.Text = "세계 교체 확인란을 먼저 선택해 주세요.";
                return;
            }

            RunOperation(() =>
            {
                Navigator.Session.NewWorld(new WorldSettings { Seed = seed, Population = population });
                return "새 세계를 열었습니다. " + Navigator.Session.LastSaveMessage;
            });
        }

        private async void RunOperation(Func<string> operation)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            Navigator.Session.Runner.SetRunning(false);
            _status.Text = "처리 중… 잠시 기다려 주세요.";
            Navigator.Invalidate();
            try
            {
                _status.Text = await Task.Run(operation);
                _confirm.IsChecked = false;
            }
            finally
            {
                _busy = false;
                Navigator.Invalidate();
            }
        }
    }
}
