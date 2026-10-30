using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    /// <summary>The play token changes with authorized facts, without counting unseen world activity.</summary>
    internal static long PerceptionRevision(GameView visible)
    {
        var canonical = visible with { Revision = 0, Generation = 0, Hash = "", Detail = null };
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical));
        return (long)(BinaryPrimitives.ReadUInt64LittleEndian(digest) & 0x001fffffffffffffUL);
    }

    private long CommandContextRevision => state.Controlled == 0 ? Revision : View().Revision;
    private CommandResult ProjectCommandResult(CommandResult result) => state.Controlled == 0
        ? result : result with { Revision = CommandContextRevision };
}
