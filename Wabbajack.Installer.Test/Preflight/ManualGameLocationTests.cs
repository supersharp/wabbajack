#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class ManualGameLocationTests : IDisposable
{
    private readonly AbsolutePath _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "wj-manual-location-" + Guid.NewGuid().ToString("N")).ToAbsolutePath();

    public ManualGameLocationTests()
    {
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        if (_root.DirectoryExists()) _root.DeleteDirectory();
    }

    [Fact]
    public void TheLocatorPrefersTheFolderTheUserNamed()
    {
        var first = Directory();
        var second = Directory();
        var store = new FakeStore {[Game.SkyrimSpecialEdition] = first};

        var locator = new GameLocator(NullLogger<GameLocator>.Instance, store);

        Assert.True(locator.TryFindLocation(Game.SkyrimSpecialEdition, out var path));
        Assert.Equal(first, path);

        // Not cached: a later change in Settings has to take effect in the same process.
        store[Game.SkyrimSpecialEdition] = second;
        Assert.True(locator.TryFindLocation(Game.SkyrimSpecialEdition, out path));
        Assert.Equal(second, path);
    }

    [Fact]
    public async Task TheStoredFolderSurvivesANewStoreInstance()
    {
        var saved = Directory();
        var game = Directory();

        using var provider = BuildProvider(saved);
        var store = provider.GetRequiredService<ManualGameLocationStore>();
        await store.Set(Game.SkyrimSpecialEdition, game);

        Assert.True(store.TryGet(Game.SkyrimSpecialEdition, out var remembered));
        Assert.Equal(game, remembered);

        // A fresh instance is what the next launch gets: it must read what this one wrote.
        var reloaded = new ManualGameLocationStore(provider.GetRequiredService<SettingsManager>(),
            NullLogger<ManualGameLocationStore>.Instance);
        Assert.True(reloaded.TryGet(Game.SkyrimSpecialEdition, out remembered));
        Assert.Equal(game, remembered);

        await reloaded.Clear(Game.SkyrimSpecialEdition);
        Assert.False(reloaded.TryGet(Game.SkyrimSpecialEdition, out _));
    }

    [Fact]
    public async Task AFolderThatIsNotThereCannotBeStored()
    {
        var saved = Directory();
        using var provider = BuildProvider(saved);
        var store = provider.GetRequiredService<ManualGameLocationStore>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.Set(Game.SkyrimSpecialEdition, _root.Combine("missing")));
        Assert.False(store.TryGet(Game.SkyrimSpecialEdition, out _));
    }

    private ServiceProvider BuildProvider(AbsolutePath savedSettings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDTOConverters();
        services.AddDTOSerializer();
        services.AddSingleton(new Wabbajack.Services.OSIntegrated.Configuration
        {
            SavedSettingsLocation = savedSettings,
            ModListsDownloadLocation = Directory(),
            EncryptedDataLocation = Directory(),
            LogLocation = Directory(),
            ImageCacheLocation = Directory()
        });
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<ManualGameLocationStore>();
        return services.BuildServiceProvider();
    }

    private AbsolutePath Directory()
    {
        var path = _root.Combine(Guid.NewGuid().ToString("N"));
        path.CreateDirectory();
        return path;
    }

    private sealed class FakeStore : IManualGameLocationStore
    {
        private readonly Dictionary<Game, AbsolutePath> _paths = new();

        public AbsolutePath this[Game game]
        {
            set => _paths[game] = value;
        }

        public bool TryGet(Game game, out AbsolutePath path) => _paths.TryGetValue(game, out path);

        public IReadOnlyDictionary<Game, AbsolutePath> All => _paths;

        public Task Set(Game game, AbsolutePath path, CancellationToken token = default)
        {
            _paths[game] = path;
            return Task.CompletedTask;
        }

        public Task Clear(Game game, CancellationToken token = default)
        {
            _paths.Remove(game);
            return Task.CompletedTask;
        }
    }
}
