namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Takes the rows of one image from top to bottom and stores them somewhere.</summary>
internal interface IImageRowSink : IDisposable
{
    void WriteRow(ReadOnlySpan<byte> row);

    /// <summary>Completes the image once every row is written.</summary>
    void Finish();
}
