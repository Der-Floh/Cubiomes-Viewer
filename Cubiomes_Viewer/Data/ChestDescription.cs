namespace Cubiomes_Viewer.Data;

/// <summary>One container of a picked structure as the callout lists it: what and where it is, and a line per kind of item in it.</summary>
public sealed record ChestDescription(string Title, IReadOnlyList<string> Contents);
