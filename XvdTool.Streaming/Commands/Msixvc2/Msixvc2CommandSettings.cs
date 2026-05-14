using Spectre.Console.Cli;
using System.ComponentModel;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal abstract class Msixvc2CommandSettings : CommandSettings
{
    [Description("File Path / URL to the MSIXVC.")]
    [CommandArgument(0, "<path/url>")]
    public string? XvcPath { get; init; }

    [Description("(Encrypted packages only) Base64-encoded content key")]
    [CommandOption("-c|--content-key")]
    public string? ContentKey { get; init; }

    [Description("(Encrypted packages only, currently unused) Base64-encoded version key")]
    [CommandOption("-v|--version-key")]
    public string? VersionKey { get; init; }
}