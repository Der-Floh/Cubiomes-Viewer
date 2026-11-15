namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Takes the rows of one image from top to bottom and stores them somewhere.</summary>
internal interface IImageRowSink : IDisposable
{
    /// <summary>Adds the next row, whose buffer can be reused once the returned task is complete.</summary>
    ValueTask WriteRowAsync(ReadOnlyMemory<byte> row);

    /// <summary>Completes the image once every row is written.</summary>
    Task FinishAsync();
}
