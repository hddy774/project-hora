using AlphaExchange.Core;
using System.Text.Json.Nodes;

static class V18OptimizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "hora-utf8-" + Guid.NewGuid().ToString("N"));
        try
        {
            var game = TestFixtures.SmallMarket(181);
            var store = new GameStore(folder); store.Attach(game);
            for (int i = 0; i < 77; i++) game.AdvanceMinute();
            string expected = game.Serialize();
            var snapshot = store.PrepareSave(game);
            // Advance the live market before either lazy representation is read.
            for (int i = 0; i < 31; i++) game.AdvanceMinute();
            var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() => snapshot.Json)).ToArray();
            store.WriteSnapshot(snapshot);
            Task.WaitAll(readers);
            check(readers.All(t => t.Result == expected), "Concurrent JSON readers and UTF-8 writer retain the isolated partial-minute checkpoint");
            var restored = new GameStore(folder).Load(out var message)!;
            check(message.Length == 0 && restored.Serialize() == expected, "Direct UTF-8 checkpoint restores every ledger and accepted minute exactly");

            var replacement = JsonNode.Parse(expected)!; replacement["FollowedId"] = 2;
            var replaced = snapshot with { Json = replacement.ToJsonString() };
            store.WriteSnapshot(replaced);
            restored = new GameStore(folder).Load(out message)!;
            check(message.Length == 0 && restored.State.FollowedId == 2 && snapshot.Json == expected,
                "Replacing record JSON invalidates its encoded payload without mutating the original checkpoint");
            store.WriteSnapshot(snapshot);
            restored = new GameStore(folder).Load(out message)!;
            check(message.Length == 0 && restored.Serialize() == expected, "Reusing the original checkpoint preserves its bytes after a record copy");
        }
        finally { Directory.Delete(folder, true); }
    }
}
