using Chronicles.Game.Exploration;

namespace Chronicles.Game;

public sealed partial class GameState
{
    public ExplorationState Exploration { get; set; } = new();
}
