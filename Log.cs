namespace ABCDEFG;

internal static class Log
{
    static StreamWriter _LogWriter;

    static readonly object _LogLock = new();

    public static void Prepare(string logFile)
    {
        _LogWriter = new StreamWriter(File.Open(logFile, FileMode.Create, FileAccess.Write, FileShare.Read));
    }

    public static void FlushLog()
    {
        lock (_LogLock)
        {
            _LogWriter.Flush();
        }
    }

    public static void WriteLine(string line)
    {
        lock (_LogLock)
        {
            Console.WriteLine(line);
            _LogWriter.WriteLine(line);
        }
    }

    public static void WriteLine(string line, ConsoleColor color)
    {
        lock (_LogLock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ResetColor();
            _LogWriter.WriteLine(line);
        }

    }

    public static void WriteLine()
    {
        lock (_LogLock)
        {
            Console.WriteLine();
            _LogWriter.WriteLine();
        }
    }
}