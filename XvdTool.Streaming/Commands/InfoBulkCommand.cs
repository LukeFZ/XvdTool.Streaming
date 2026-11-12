using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal sealed class InfoBulkCommand : XvdCommand<InfoBulkCommand.Settings>
{
    public sealed class Settings : XvdCommandSettings
    {
        [Description("Files to get info for. Supports exact paths and wildcard patterns.")]
        [CommandOption("-f|--files")]
        public string[]? Files { get; init; }

        [Description("File path to save the output into.")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }
        
        [Description("Exclude files matching any wildcard pattern, for example: *.txt, */something/*, something/*/*")]
        [CommandOption("-e|--exclude")]
        [TypeConverter(typeof(XvdFilePattern.Converter))]
        public XvdFilePattern[]? Exclude { get; init; }
        
        [Description("Include files matching any wildcard pattern, for example: *.txt, */something/*, something/*/*")]
        [CommandOption("-i|--include")]
        [TypeConverter(typeof(XvdFilePattern.Converter))]
        public XvdFilePattern[]? Include { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Debug.Assert(settings.XvcPath != null, "settings.XvcPath != null");

        if (!string.IsNullOrEmpty(settings.OutputPath) && File.Exists(settings.OutputPath)) 
        {
            File.Delete(settings.OutputPath);
        }
        
        var filePaths = File.ReadAllLines(settings.XvcPath);
        var fileDict = new HashSet<string>();
        foreach (var filePath in filePaths) 
        {
            var components = new Uri(filePath).AbsolutePath.Split('/');
            if (components.Length < 3 || (!components[^1].EndsWith(".xvc") && !components[^1].EndsWith(".msixvc"))) 
            {
                continue;
            }
             
            var versionId = components[^2];
            if (fileDict.Contains(versionId)) 
            {
                continue;
            }
             
            fileDict.Add(versionId);
            var xvdFile = filePath.StartsWith("http")
                ? StreamedXvdFile.OpenFromUrl(filePath)
                : StreamedXvdFile.OpenFromFile(filePath, false);
 
            xvdFile.Parse();
            var outStr = $"== {versionId} ==" + xvdFile.PrintInfoBulk(settings.Files, settings.Exclude, settings.Include) + "\n";
            Console.WriteLine(outStr);
            
            if (!string.IsNullOrEmpty(settings.OutputPath)) 
            {
                File.AppendAllTextAsync(settings.OutputPath, $"{filePath}:\n\n{outStr}\n");
            }
        }

        return 0;
    }
}