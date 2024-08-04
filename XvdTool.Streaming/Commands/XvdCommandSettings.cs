using System.ComponentModel;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal abstract class XvdCommandSettings : CommandSettings
{
    [Description("File Path / URL to the XVC.")]
    [CommandArgument(0, "<path/url>")]
    public string? XvcPath { get; init; }
}