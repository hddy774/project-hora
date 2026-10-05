using Android.App;
using Android.Content;
using Android.Widget;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    const int ExportRequest = 420, ImportRequest = 421;
    async void PickBackup(bool export)
    {
        if (fileBusy || Context is not Activity activity) return;
        if (export && game is null) { Notify("먼저 시장을 시작하세요."); return; }
        auto = false; Save(); fileBusy = true; Invalidate();
        await saveTask;
        if (pendingSave is not null) { fileBusy = false; Notify("저장 실패를 해결한 뒤 파일을 처리하세요."); return; }
        fileBusy = true; Invalidate();
        try
        {
            var intent = new Intent(export ? Intent.ActionCreateDocument : Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable); intent.SetType("application/zip");
            if (export) intent.PutExtra(Intent.ExtraTitle,"AlphaExchange-v1.7.0-history.zip");
            activity.StartActivityForResult(intent,export ? ExportRequest : ImportRequest);
        }
        catch (ActivityNotFoundException) { fileBusy = false; Notify("기기의 파일 선택 앱을 사용할 수 없습니다."); }
    }
    public async void HandleBackupResult(int requestCode, Result resultCode, Intent? intent)
    {
        if (requestCode is not (ExportRequest or ImportRequest)) return;
        if (resultCode != Result.Ok || intent?.Data is null) { fileBusy = false; Invalidate(); return; }
        var uri = intent.Data;
        try
        {
            if (requestCode == ExportRequest)
            {
                await Task.Run(() => { using var output = Context!.ContentResolver!.OpenOutputStream(uri,"wt") ?? throw new IOException(); store.Export(output); });
                Notify("게임과 전체 통계를 파일로 내보냈습니다.");
            }
            else
            {
                var imported = await Task.Run(() =>
                {
                    string temporary = Path.Combine(Context!.CacheDir!.AbsolutePath,"selected-backup.zip");
                    try
                    {
                        using (var input = Context.ContentResolver!.OpenInputStream(uri) ?? throw new IOException())
                        using (var output = File.Create(temporary))
                        {
                            byte[] buffer = new byte[65536]; long copied = 0; int count;
                            while ((count = input.Read(buffer)) > 0) { copied += count; if (copied > 3L*1024*1024*1024) throw new InvalidDataException(); output.Write(buffer,0,count); }
                        }
                        using var selected = File.OpenRead(temporary); return store.Import(selected);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                });
                game = imported; lobby = false; page = 3; statsTab = 0; scroll = 0;
                selectedStock = selectedTrader = -1; showResult = confirmNew = false;
                companyStock = companyTab = 0; companySeason = selectedSeason = historyPage = 0;
                ownershipStock=ownershipPage=0;
                corporateBefore = long.MaxValue; companyReportCache.Clear(); seasonCache.Clear();
                comparisonPeriod = ComparisonPeriod.All; lastCommitTime = Now;
                Notify("게임과 통계를 복원했습니다. 재생하면 이어집니다.");
            }
        }
        catch (Exception e) when (GameStore.StorageException(e) || e is Java.Lang.SecurityException or Java.IO.IOException)
        { Notify("파일을 처리하지 못했습니다. 기존 기록은 보존합니다."); }
        finally { fileBusy = false; Invalidate(); }
    }
    void CustomPeriod()
    {
        if (Context is not Activity activity || game is null) return;
        var layout = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        layout.SetPadding(30,10,30,10);
        var from = new EditText(activity) { Hint = "시작: 시장 시작 후 시간 (0부터)", InputType = Android.Text.InputTypes.ClassNumber,
            Text = S.ComparisonFrom.ToString() };
        var to = new EditText(activity) { Hint = "종료: 시장 시작 후 시간", InputType = Android.Text.InputTypes.ClassNumber,
            Text = (S.ComparisonTo <= 0 ? S.CompletedHours : S.ComparisonTo).ToString() };
        layout.AddView(new TextView(activity) { Text = "24시간 = 1일 · 720시간 = 1시즌\n시장 시작을 0으로 계산합니다." });
        layout.AddView(from); layout.AddView(to);
        new AlertDialog.Builder(activity)!.SetTitle("비교 기간 지정")!.SetView(layout)!
            .SetNegativeButton("취소",(_,_) => {})!.SetPositiveButton("적용",(_,_) =>
            {
                if (!long.TryParse(from.Text,out long a) || !long.TryParse(to.Text,out long b) || a < 0 || b < a || b > S.CompletedHours)
                { Notify("시작 ≤ 종료 ≤ 현재 시간으로 입력하세요."); return; }
                S.ComparisonFrom = a; S.ComparisonTo = b; comparisonPeriod = ComparisonPeriod.Custom; scroll = 0; Invalidate();
            })!.Show();
    }
    float DrawStorage(float y)
    {
        y = Statement("기록 보관",y,[("진행 / 저장 시간",$"{S.CompletedHours:N0} / {store.LastCommittedHour:N0}"),
            ("저장을 기다리는 기록",$"{store.PendingCount:N0}개")]);
        Text("모든 배속의 시간별 기록을 이어서 보관합니다.",21,y+6,10,Muted); y += 24;
        Button("전체 기록 내보내기 · 게임 + 통계 CSV",20,y,360,42,()=>PickBackup(true),false); y+=54;
        Button("기록 파일 가져오기 · 게임 복원",20,y,360,42,()=>PickBackup(false),false); y+=54;
        return Wrap("가져오기 전 현재 기록도 보존됩니다. 앱 삭제 전에는 기기 파일로 내보내세요.",21,y+8,355,11,Muted,20)+15;
    }
}
