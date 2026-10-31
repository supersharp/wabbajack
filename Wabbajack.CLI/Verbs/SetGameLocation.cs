using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Names a game's install folder by hand, for a copy no store handler can find, or forgets one that was
///     named before. This is the command-line half of the app's Settings card; both write the same file.
/// </summary>
public class SetGameLocation
{
    private readonly ILogger<SetGameLocation> _logger;
    private readonly IManualGameLocationStore _locations;

    public SetGameLocation(ILogger<SetGameLocation> logger, IManualGameLocationStore locations)
    {
        _logger = logger;
        _locations = locations;
    }

    public static VerbDefinition Definition = new("set-game-location",
        "Sets or clears a game's install folder by hand, used before automatic store detection",
        new[]
        {
            new OptionDefinition(typeof(string), "g", "game",
                "Game name, such as SkyrimSpecialEdition or \"Skyrim Special Edition\""),
            new OptionDefinition(typeof(AbsolutePath), "p", "path", "The folder the game is installed in"),
            new OptionDefinition(typeof(bool), "c", "clear", "Forget the manual folder instead of setting one")
        });

    internal async Task<int> Run(string game, AbsolutePath path, bool clear, CancellationToken token)
    {
        if (!TryFindGame(game, out var found))
        {
            _logger.LogError("Unknown game '{Game}'. Use one of: {Games}", game,
                string.Join(", ", GameRegistry.Games.Keys.OrderBy(g => g.ToString())));
            return 1;
        }

        if (clear)
        {
            try
            {
                await _locations.Clear(found, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not clear the folder for {Game}", found);
                return 1;
            }

            _logger.LogInformation("{Game} will be located automatically again", found);
            return 0;
        }

        if (path == default)
        {
            _logger.LogError("Nothing to set. Pass --path <folder>, or --clear to forget the current one");
            return 1;
        }

        if (!path.DirectoryExists())
        {
            _logger.LogError("The folder {Folder} does not exist", path);
            return 1;
        }

        try
        {
            await _locations.Set(found, path, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save the folder for {Game}", found);
            return 1;
        }

        _logger.LogInformation("{Game} is installed at {Folder}", found, path);
        return 0;
    }

    private static bool TryFindGame(string name, out Game game)
    {
        // The enum spelling is what scripts use; the human name is what a user reads in the app.
        if (Enum.TryParse<Game>(name, true, out game) && GameRegistry.Games.ContainsKey(game))
            return true;

        foreach (var candidate in GameRegistry.Games)
        {
            if (string.Equals(candidate.Key.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Value.HumanFriendlyGameName, name, StringComparison.OrdinalIgnoreCase))
            {
                game = candidate.Key;
                return true;
            }
        }

        game = default;
        return false;
    }
}
