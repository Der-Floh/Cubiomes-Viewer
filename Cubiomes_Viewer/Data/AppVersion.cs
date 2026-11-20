using System.Reflection;

namespace Cubiomes_Viewer.Data;

/// <summary>The version the build stamped into the app.</summary>
public static class AppVersion
{
    /// <summary>The version of builds that were given none, such as development builds.</summary>
    public const string Unreleased = "0.0.0";

    public static string Current { get; } = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Unreleased;
}
