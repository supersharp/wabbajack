using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated;

/// <summary>
///     The user's own answers to "where is this game installed", kept in the saved-settings folder beside the
///     install history so the app and the CLI see the same folders.
///     <para>
///         Reading is synchronous because <see cref="IGameLocator" /> is: a check asks it in the middle of a
///         run and cannot wait on a file. The file is loaded once, and a write replaces the in-memory copy
///         only after the new one has reached disk.
///     </para>
/// </summary>
public sealed class ManualGameLocationStore : IManualGameLocationStore
{
    private const string SettingsKey = "game-locations";

    private readonly SettingsManager _settings;
    private readonly ILogger<ManualGameLocationStore> _logger;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly object _sync = new();
    private Dictionary<Game, AbsolutePath>? _locations;

    public ManualGameLocationStore(SettingsManager settings, ILogger<ManualGameLocationStore> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool TryGet(Game game, out AbsolutePath path)
    {
        return Snapshot().TryGetValue(game, out path);
    }

    public IReadOnlyDictionary<Game, AbsolutePath> All => new Dictionary<Game, AbsolutePath>(Snapshot());

    public async Task Set(Game game, AbsolutePath path, CancellationToken token = default)
    {
        if (!path.DirectoryExists())
            throw new ArgumentException($"The game folder {path} does not exist", nameof(path));

        await _write.WaitAsync(token);
        try
        {
            var next = new Dictionary<Game, AbsolutePath>(Snapshot()) {[game] = path};
            await _settings.Save(SettingsKey, new ManualGameLocations {Locations = next});
            lock (_sync)
                _locations = next;

            _logger.LogInformation("Using the folder {Folder} for {Game}", path, game);
        }
        finally
        {
            _write.Release();
        }
    }

    public async Task Clear(Game game, CancellationToken token = default)
    {
        await _write.WaitAsync(token);
        try
        {
            var next = new Dictionary<Game, AbsolutePath>(Snapshot());
            if (!next.Remove(game)) return;

            await _settings.Save(SettingsKey, new ManualGameLocations {Locations = next});
            lock (_sync)
                _locations = next;

            _logger.LogInformation("Cleared the manually set folder for {Game}", game);
        }
        finally
        {
            _write.Release();
        }
    }

    private Dictionary<Game, AbsolutePath> Snapshot()
    {
        lock (_sync)
        {
            return _locations ??= Load();
        }
    }

    private Dictionary<Game, AbsolutePath> Load()
    {
        try
        {
            // SettingsManager only reads asynchronously; the wait is on a worker so a UI thread with a
            // synchronization context can never be the one that continues the read.
            var saved = Task.Run(() => _settings.Load<ManualGameLocations>(SettingsKey))
                .GetAwaiter().GetResult();
            return saved.Locations ?? new Dictionary<Game, AbsolutePath>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the manually set game folders");
            return new Dictionary<Game, AbsolutePath>();
        }
    }
}

/// <summary>The on-disk shape of the manual folders.</summary>
public sealed class ManualGameLocations
{
    public Dictionary<Game, AbsolutePath> Locations { get; set; } = new();
}
