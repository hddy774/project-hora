using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    void RequestSave()
    {
        saveRequested = forcedSaveRequested = true;
        StartSimulationWorker();
    }

    bool Save(bool force = true, bool duringTransition = false)
    {
        if (game is null || (fileBusy && !duringTransition)) return false;
        lastSave = Now;
        if (!force && preparedRun == S.RunId && preparedMinute == S.CompletedMinutes &&
            preparedTransaction == S.NextTransactionId && preparedEvent == S.NextCorporateEventId) return true;
        try
        {
            var snapshot = store.PrepareSave(game);
            preparedRun = S.RunId;
            preparedMinute = S.CompletedMinutes;
            preparedTransaction = S.NextTransactionId;
            preparedEvent = S.NextCorporateEventId;
            checkpointWriter.Request(snapshot);
            return true;
        }
        catch (Exception error) when (GameStore.StorageException(error))
        {
            Post(() => Notify("저장 데이터를 준비하지 못했습니다."));
            return false;
        }
    }
}
