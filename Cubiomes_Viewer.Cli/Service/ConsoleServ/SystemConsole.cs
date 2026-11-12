using System.Text;

namespace Cubiomes_Viewer.Cli.Service.ConsoleServ;

public sealed class SystemConsole : ICliConsole
{
    private const char LineStart = '\r';

    public TextWriter Output { get; }
    public bool CanReplaceStatus { get; }

    private readonly TextWriter _error;
    private readonly Func<int> _getWidth;
    private readonly object _gate = new();

    /// <summary>How long the status shown in place is, or zero when none is.</summary>
    private int _statusLength;

    /// <param name="output">Where seeds and reports go.</param>
    /// <param name="error">Where messages and statuses go.</param>
    /// <param name="canReplaceStatus">Whether <paramref name="error"/> is a terminal, where a status can be replaced in place.</param>
    /// <param name="isOutputInTerminal">Whether <paramref name="output"/> shows in that terminal too, so that a status in place has to make room for it.</param>
    /// <param name="getWidth">How many characters fit in a line of the terminal, or zero when that is not known.</param>
    public SystemConsole(TextWriter output, TextWriter error, bool canReplaceStatus, bool isOutputInTerminal, Func<int> getWidth)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(getWidth);

        Output = canReplaceStatus && isOutputInTerminal ? new StatusClearingWriter(this, output) : output;
        _error = error;
        CanReplaceStatus = canReplaceStatus;
        _getWidth = getWidth;
    }

    /// <summary>The console of the process: statuses are replaced in place where the error stream is not redirected.</summary>
    public static SystemConsole Create() => new(Console.Out, Console.Error, !Console.IsErrorRedirected, !Console.IsOutputRedirected, GetWindowWidth);

    public void WriteMessage(string message)
    {
        lock (_gate)
        {
            ClearStatusLine();
            _error.WriteLine(message);
        }
    }

    public void WriteStatus(string status)
    {
        lock (_gate)
        {
            if (!CanReplaceStatus)
            {
                _error.WriteLine(status);
                return;
            }

            var shown = FitIntoLine(status);
            _error.Write(LineStart);
            _error.Write(shown.PadRight(_statusLength));
            _error.Flush();
            _statusLength = shown.Length;
        }
    }

    public void ClearStatus()
    {
        lock (_gate)
            ClearStatusLine();
    }

    private void ClearStatusLine()
    {
        if (_statusLength == 0)
            return;

        _error.Write(LineStart);
        _error.Write(new string(' ', _statusLength));
        _error.Write(LineStart);
        _error.Flush();
        _statusLength = 0;
    }

    /// <summary>A status that wraps into a second line could no longer be replaced as a whole.</summary>
    private string FitIntoLine(string status)
    {
        var width = _getWidth() - 1;
        return width > 0 && status.Length > width ? status[..width] : status;
    }

    private static int GetWindowWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Output that shows in the terminal of the status, which makes room before anything is written.</summary>
    private sealed class StatusClearingWriter(SystemConsole console, TextWriter output) : TextWriter
    {
        public override Encoding Encoding => output.Encoding;

        public override void Write(char value)
        {
            lock (console._gate)
            {
                console.ClearStatusLine();
                output.Write(value);
            }
        }

        public override void Write(string? value)
        {
            lock (console._gate)
            {
                console.ClearStatusLine();
                output.Write(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (console._gate)
            {
                console.ClearStatusLine();
                output.WriteLine(value);
            }
        }

        public override void Flush() => output.Flush();
    }
}
