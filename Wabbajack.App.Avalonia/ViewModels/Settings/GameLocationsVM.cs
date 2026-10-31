using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

/// <summary>A game in the "game folders" card's picker. <see cref="ToString" /> is what the ComboBox shows.</summary>
public sealed record GameOption(Game Game, string Name)
{
    public override string ToString() => Name;
}

/// <summary>One game's folder: where it came from, and whether the user set it.</summary>
public partial class GameLocationRowVM : ViewModel
{
    public GameLocationRowVM(Game game, string name, Func<Game, Task> clear)
    {
        Game = game;
        Name = name;
        ClearCommand = ReactiveCommand.CreateFromTask(() => clear(game), this.WhenAnyValue(x => x.IsManual));
    }

    public Game Game { get; }
    public string Name { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    [Reactive] public partial AbsolutePath Path { get; set; }
    [Reactive] public partial bool IsManual { get; set; }
    [Reactive] public partial string StatusText { get; set; } = string.Empty;

    /// <summary>Whether the folder is gone, which is the one state the user has to fix or clear.</summary>
    [Reactive] public partial bool IsMissing { get; set; }
}

/// <summary>
///     The Settings card that lets a game folder be named by hand. Steam, GOG, Epic, Origin and EA are
///     discovered by <see cref="IGameLocator" />; this is for everything those cannot see, and is consulted
///     before them, so a hand-picked folder wins over a stale library entry.
/// </summary>
public partial class GameLocationsVM : ViewModel
{
    private readonly IManualGameLocationStore _store;
    private readonly IGameLocator _locator;

    public GameLocationsVM(IManualGameLocationStore store, IGameLocator locator)
    {
        _store = store;
        _locator = locator;

        Games = GameRegistry.Games
            .Select(g => new GameOption(g.Key, g.Value.HumanFriendlyGameName))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Picker = new FilePickerVM(this)
        {
            PathType = FilePickerVM.PathTypeOptions.Folder,
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PromptTitle = "Select the folder the game is installed in"
        };

        SaveCommand = ReactiveCommand.CreateFromTask(Save,
            this.WhenAnyValue(x => x.SelectedGame).Select(game => game != null));
        RefreshCommand = ReactiveCommand.Create(Refresh);

        this.WhenAnyValue(x => x.SelectedGame)
            .Subscribe(game =>
            {
                if (game == null)
                {
                    Picker.TargetPath = default;
                    return;
                }

                Picker.TargetPath = _store.TryGet(game.Game, out var manual) ? manual : default;
                StatusMessage = _store.TryGet(game.Game, out _)
                    ? $"{game.Name} already has a folder set by hand. Choosing another replaces it."
                    : $"Choose the folder {game.Name} is installed in.";
            });

        Refresh();
    }

    public IReadOnlyList<GameOption> Games { get; }
    public ObservableCollection<GameLocationRowVM> Locations { get; } = new();
    public FilePickerVM Picker { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    [Reactive] public partial GameOption? SelectedGame { get; set; }
    [Reactive] public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>Rebuilds the list from what the locator and the store answer now.</summary>
    private void Refresh()
    {
        var selected = SelectedGame;
        Locations.Clear();

        foreach (var option in Games)
        {
            var manual = _store.TryGet(option.Game, out var manualPath);
            var found = _locator.TryFindLocation(option.Game, out var detectedPath);
            if (!manual && !found) continue;

            var path = manual ? manualPath : detectedPath;
            var missing = !path.DirectoryExists();

            Locations.Add(new GameLocationRowVM(option.Game, option.Name, Clear)
            {
                Path = path,
                IsManual = manual,
                IsMissing = missing,
                StatusText = manual
                    ? missing ? "Set by hand, but the folder is missing" : "Set by hand"
                    : "Found automatically"
            });
        }

        SelectedGame = selected;
        if (Locations.Count == 0)
            StatusMessage = "No games were found. Choose one above to set its folder by hand.";
    }

    private async Task Save()
    {
        if (SelectedGame is not { } game)
        {
            StatusMessage = "Choose a game first.";
            return;
        }

        var folder = Picker.TargetPath;
        if (folder == default || !folder.DirectoryExists())
        {
            StatusMessage = "Choose an existing folder.";
            return;
        }

        try
        {
            await _store.Set(game.Game, folder);
            StatusMessage = $"{game.Name} will use {folder}.";
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save that folder: {ex.Message}";
        }
    }

    private async Task Clear(Game game)
    {
        try
        {
            await _store.Clear(game);
            if (SelectedGame?.Game == game) Picker.TargetPath = default;
            StatusMessage = $"{game.MetaData().HumanFriendlyGameName} will be located automatically again.";
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not clear that folder: {ex.Message}";
        }
    }
}
