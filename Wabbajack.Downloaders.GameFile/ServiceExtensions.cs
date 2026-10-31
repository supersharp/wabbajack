using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders;

public static class ServiceExtensions
{
    public static IServiceCollection AddGameFileDownloader(this IServiceCollection services)
    {
        return services.AddAllSingleton<IDownloader, IDownloader<GameFileSource>,
            GameFileDownloader>();
    }

    public static IServiceCollection AddStandardGameLocator(this IServiceCollection services)
    {
        // The locator takes an optional store; its one-argument constructor is what a host with nowhere to
        // keep manual folders resolves, so no null implementation needs registering here.
        return services.AddAllSingleton<IGameLocator, GameLocator>();
    }
}