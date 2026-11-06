namespace ABCDEFG;

internal static class Log
{
    static StreamWriter _LogWriter;
    static StreamWriter _ErrorWriter;

    static readonly object _LogLock = new();

    public static void Prepare(string logFile, string errorFile)
    {
        _LogWriter = new StreamWriter(File.Open(logFile, FileMode.Create, FileAccess.Write, FileShare.Read));
        _ErrorWriter = new StreamWriter(File.Open(errorFile, FileMode.Create, FileAccess.Write, FileShare.Read));
    }

    public static void FlushLog()
    {
        lock (_LogLock)
        {
            _LogWriter.Flush();
            _ErrorWriter.Flush();
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

    public static void Error(string line)
    {
        lock (_LogLock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(line);
            Console.ResetColor();

            line = "[ERROR] " + line.Trim();
            _LogWriter.WriteLine(line);
            _ErrorWriter.WriteLine(line);
        }
    }

    public static void Warning(string line)
    {
        lock (_LogLock)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(line);
            Console.ResetColor();

            line = "[WARN] " + line.Trim();
            _LogWriter.WriteLine(line);
            _ErrorWriter.WriteLine(line);
        }
    }

    public static void Verbose(string line)
    {
        if (Config.Instance.LogVerbose)
            WriteLine(line, ConsoleColor.DarkGray);
    }
}