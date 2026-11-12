using Cubiomes_Viewer.Cli.Repository.FileRepo;
using Cubiomes_Viewer.Cli.Service.ArgumentServ;
using Cubiomes_Viewer.Cli.Service.CommandServ;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Cli.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the commands of the CLI and logging that writes warnings to <paramref name="console"/>, except those of
    /// the self-test, whose command lists failed checks itself.
    /// </summary>
    public static IServiceCollection AddCubiomesCli(this IServiceCollection services, ICliConsole console)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        // logging
        services.AddSingleton(console);
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Warning)
            .AddFilter(typeof(SelfTestService).FullName, LogLevel.Error));
        services.AddSingleton<ILoggerProvider, CliLoggerProvider>();

        // repositories
        services.AddSingleton<IFileStore, FileStore>();

        // commands
        services.AddSingleton<IArgumentParser, ArgumentParser>();
        services.AddSingleton<ISearchCommand, SearchCommand>();
        services.AddSingleton<ISelfTestCommand, SelfTestCommand>();
        services.AddSingleton<ICliApplication, CliApplication>();

        return services;
    }
}
