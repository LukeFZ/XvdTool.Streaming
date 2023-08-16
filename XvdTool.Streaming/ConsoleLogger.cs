using Spectre.Console;

namespace XvdTool.Streaming;

public static class ConsoleLogger
{
    public static void WriteInfoLine(string line)
    {
        AnsiConsole.MarkupLine($"[white]INFO:[/] {line}");
    }

    public static void WriteWarnLine(string line)
    {
        AnsiConsole.MarkupLine($"[orange1]WARN:[/] {line}");
    }

    public static void WriteErrLine(string line)
    {
        AnsiConsole.MarkupLine($"[red]ERR:[/] {line}");
    }
}