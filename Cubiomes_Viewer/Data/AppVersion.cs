using System.Reflection;

namespace Cubiomes_Viewer.Data;

/// <summary>The version the build stamped into the app.</summary>
public static class AppVersion
{
    private const string Unknown = "0.0.0";

    public static string Current { get; } = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Unknown;
}
