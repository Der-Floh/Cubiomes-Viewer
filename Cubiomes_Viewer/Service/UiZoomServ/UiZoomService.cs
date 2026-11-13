namespace Cubiomes_Viewer.Service.UiZoomServ;

public sealed class UiZoomService : IUiZoomService
{
    private const double PercentPerScale = 100;

    public event EventHandler? Changed;

    public double Scale { get; private set; } = 1;

    public void Apply(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(percent);

        var scale = percent / PercentPerScale;
        if (scale.Equals(Scale))
            return;

        Scale = scale;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
