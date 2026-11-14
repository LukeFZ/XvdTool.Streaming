using Spectre.Console.Cli;
using System.ComponentModel;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal abstract class Msixvc2CommandSettings : CommandSettings
{
    [Description("File Path / URL to the MSIXVC.")]
    [CommandArgument(0, "<path/url>")]
    public string? XvcPath { get; init; }

    [Description("Path to the .cik file to be used.")]
    [CommandOption("--cik")]
    public string? CikPath { get; init; }

    [Description("Base64-encoded content key (Encrypted packages only).")]
    [CommandOption("-c|--content-key")]
    public string? ContentKey { get; init; }

    [Description("Base64-encoded version key (Encrypted packages only, currently unused).")]
    [CommandOption("-v|--version-key")]
    public string? VersionKey { get; init; }
}