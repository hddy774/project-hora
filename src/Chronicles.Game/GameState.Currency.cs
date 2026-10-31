namespace Chronicles.Game;

public sealed partial class GameState
{
    public Currency.CurrencyLedger Currency { get; set; } = new();
}
