using Spectre.Console.Cli;
using XvdTool.Streaming.Commands;

namespace XvdTool.Streaming;

internal class Program
{
    static void Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            //config.PropagateExceptions();

            config.AddCommand<InfoCommand>("info")
                .WithDescription("Prints information about a given file.")
                .WithExample("info", "c:/file.msixvc")
                .WithExample("info", "c:/file.msixvc", "-o log.txt")
                .WithExample("info", "https://assets1.xboxlive.com/...");

            config.AddCommand<ExtractCommand>("extract")
                .WithDescription("Decrypts and extracts the files contained in a given file.")
                .WithExample("extract", "c:/file.msixvc")
                .WithExample("extract", "c:/file.msixvc", "-o c:/output")
                .WithExample("extract", "https://assets1.xboxlive.com/...");

            config.AddCommand<VerifyCommand>("verify")
                .WithDescription("Checks the integrity of the given file. (Local only)")
                .WithExample("verify", "c:/file.msixvc");

            config.AddCommand<DecryptCommand>("decrypt")
                .WithDescription("Decrypts the given file. (Local only)")
                .WithExample("decrypt", "c:/file.msixvc");

            config.AddCommand<ExtractEmbeddedXvdCommand>("extract-embedded-xvd")
                .WithDescription("Extracts an embedded XVD from a given file.")
                .WithExample("extract-embedded-xvd", "c:/file.xvc")
                .WithExample("extract-embedded-xvd", "https://assets1.xboxlive.com/...");
        });

        app.Run(args);
    }
}