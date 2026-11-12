using Cubiomes_Viewer.Cli.Data;

namespace Cubiomes_Viewer.Cli.Service.ArgumentServ;

/// <summary>Reads options written as <c>--name value</c> or <c>--name=value</c>, each at most once.</summary>
public sealed class ArgumentParser : IArgumentParser
{
    private const string OptionPrefix = "-";
    private const string LongOptionPrefix = "--";
    private const char ValueSeparator = '=';

    private static readonly string[] _fileOptions = [CliOptionNames.Session, CliOptionNames.Out];
    private static readonly string[] _switches = [CliOptionNames.Reset, CliOptionNames.SelfTest, CliOptionNames.Version];

    /// <summary>Paths that differ only in case name the same file where the file system ignores case.</summary>
    private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public CliRequest Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Any(IsHelp))
            return new HelpRequest();

        return CreateRequest(ReadOptions(arguments));
    }

    private static bool IsHelp(string argument) => argument is CliOptionNames.Help or CliOptionNames.ShortHelp;

    private static Dictionary<string, string?> ReadOptions(IReadOnlyList<string> arguments)
    {
        Dictionary<string, string?> options = new(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var (name, value) = Split(arguments[index]);
            if (_fileOptions.Contains(name))
                value = RequireFile(name, value ?? TakeNext(arguments, ref index));
            else if (!_switches.Contains(name))
                throw new InvalidArgumentsException($"Unknown option '{name}'.");
            else if (value is not null)
                throw new InvalidArgumentsException($"{name} takes no value.");

            if (!options.TryAdd(name, value))
                throw new InvalidArgumentsException($"{name} is given more than once.");
        }

        return options;
    }

    private static (string Name, string? Value) Split(string argument)
    {
        if (!argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            throw new InvalidArgumentsException($"Unexpected argument '{argument}'.");

        var separator = argument.IndexOf(ValueSeparator, StringComparison.Ordinal);
        return separator < 0 ? (argument, null) : (argument[..separator], argument[(separator + 1)..]);
    }

    private static string? TakeNext(IReadOnlyList<string> arguments, ref int index)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith(LongOptionPrefix, StringComparison.Ordinal))
            return null;

        index++;
        return arguments[index];
    }

    private static string RequireFile(string name, string? value)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidArgumentsException($"{name} needs a file.") : value;

    private static CliRequest CreateRequest(Dictionary<string, string?> options)
    {
        if (options.ContainsKey(CliOptionNames.Version))
            return Alone(options, CliOptionNames.Version, new VersionRequest());
        if (options.ContainsKey(CliOptionNames.SelfTest))
            return Alone(options, CliOptionNames.SelfTest, new SelfTestRequest());

        if (options.GetValueOrDefault(CliOptionNames.Session) is not { } sessionPath)
            throw new InvalidArgumentsException($"Name the search session to run with {CliOptionNames.Session} <file>.");

        var outputPath = options.GetValueOrDefault(CliOptionNames.Out);
        if (outputPath is not null && IsSameFile(sessionPath, outputPath))
            throw new InvalidArgumentsException("The seeds cannot go to the session file itself.");

        return new SearchRequest { SessionPath = sessionPath, OutputPath = outputPath, Reset = options.ContainsKey(CliOptionNames.Reset) };
    }

    private static CliRequest Alone(Dictionary<string, string?> options, string name, CliRequest request)
        => options.Count == 1 ? request : throw new InvalidArgumentsException($"{name} cannot be combined with other options.");

    private static bool IsSameFile(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), _pathComparison);
}
