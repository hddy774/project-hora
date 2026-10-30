using Chronicles.Game.Combat;

namespace Chronicles.Game;

public sealed partial class GameState
{
    public CombatLedger Combat { get; set; } = new();
}
