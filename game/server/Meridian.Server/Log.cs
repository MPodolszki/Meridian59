namespace Meridian.Server;

/// <summary>Minimales Logging auf stdout (unter Linux landet das per systemd im Journal).</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}");
    }
}
