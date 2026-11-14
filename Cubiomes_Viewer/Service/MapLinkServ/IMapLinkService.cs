using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.MapLinkServ;

/// <summary>Writes views of the map as web addresses and reads them back, so that a view can be shared as a link.</summary>
public interface IMapLinkService
{
    /// <summary>The address of a view: <paramref name="baseAddress"/> with the view as query parameters, leaving out those at their defaults.</summary>
    string Format(MapLink link, string baseAddress);

    /// <summary>
    /// Reads a view from a whole address, from its query or from bare parameters. Text without a seed is no link; for an address,
    /// <paramref name="problem"/> then says so. The world, place and zoom a link leaves out stay those of <paramref name="current"/>,
    /// the other values take their defaults, and values that can't be read stay those of <paramref name="current"/> and are named in
    /// <paramref name="problem"/>.
    /// </summary>
    bool TryParse(string? text, MapLink current, [NotNullWhen(true)] out MapLink? link, out string? problem);
}
