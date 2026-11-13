using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Service.IconStyleServ;

/// <summary>Switches the icons of the buttons between their outlined and filled forms.</summary>
public interface IIconStyleService
{
    void Apply(IconStyle style);
}
