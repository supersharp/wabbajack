using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

/// <summary>
///     A host that has nowhere to keep a manual folder. Registered by hosts that want a
///     <see cref="GameLocator" /> but no persistence - the test containers in particular - so the locator
///     always has a store to ask and no host has to special-case its absence.
/// </summary>
public sealed class NullManualGameLocationStore : IManualGameLocationStore
{
    public static readonly NullManualGameLocationStore Instance = new();

    public bool TryGet(Game game, out AbsolutePath path)
    {
        path = default;
        return false;
    }

    public IReadOnlyDictionary<Game, AbsolutePath> All { get; } =
        new Dictionary<Game, AbsolutePath>();

    public Task Set(Game game, AbsolutePath path, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public Task Clear(Game game, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
}
