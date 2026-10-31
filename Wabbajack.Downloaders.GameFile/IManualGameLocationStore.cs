using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

/// <summary>
///     Folders the user named for a game, used before anything the machine can discover for itself.
///     <para>
///         The stores answer "where did Skyrim come from"; they cannot answer "where did I put Skyrim". A
///         game installed outside the store's library - a GOG backup unpacked by hand, a Steam library Steam
///         no longer remembers, a copy on a drive that is not attached all the time - is invisible to every
///         handler <see cref="GameLocator" /> has, so this is what turns "Wabbajack cannot find your game"
///         into a folder picker that is remembered.
///     </para>
///     <para>
///         Implementations live with the host that can persist them; this project only declares the seam so
///         <see cref="GameLocator" /> never learns where settings are stored.
///     </para>
/// </summary>
public interface IManualGameLocationStore
{
    /// <summary>The folder the user named for this game, if they named one.</summary>
    bool TryGet(Game game, out AbsolutePath path);

    /// <summary>Every game the user has named a folder for. A copy, safe to enumerate while another writes.</summary>
    IReadOnlyDictionary<Game, AbsolutePath> All { get; }

    /// <summary>Remembers <paramref name="path" /> for <paramref name="game" />, replacing any earlier answer.</summary>
    Task Set(Game game, AbsolutePath path, CancellationToken token = default);

    /// <summary>Forgets the manual folder for <paramref name="game" />, so detection is used again.</summary>
    Task Clear(Game game, CancellationToken token = default);
}
