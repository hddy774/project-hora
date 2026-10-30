namespace Chronicles.Game;

public sealed partial class GameState
{
    public Ecology.EcologyState Ecology { get; set; } = new();
}
