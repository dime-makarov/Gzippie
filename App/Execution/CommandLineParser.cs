using System;
using CommandLine;

namespace Dm.Gzippie.App.Execution
{
    public class CommandLineParser : ICommandLineParser
    {
        public ExecutionOptions Parse(string[] args)
        {
            ExecutionOptions execOpts = null;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>((opts) =>
                {
                    execOpts = new ExecutionOptions();

                    if (opts.Mode.Equals("Compress", StringComparison.InvariantCultureIgnoreCase))
                    {
                        execOpts.Mode = ExecutionMode.Compress;
                    }
                    else if (opts.Mode.Equals("Decompress", StringComparison.InvariantCultureIgnoreCase))
                    {
                        execOpts.Mode = ExecutionMode.Decompress;
                    }
                    else
                    {
                        execOpts.Mode = ExecutionMode.Unknown;
                    }

                    execOpts.SourcePath = opts.SourcePath;
                    execOpts.DestinationPath = opts.DestinationPath;
                });

            return execOpts;
        }

        private class Options
        {
            [Option('m', "mode", Required = true, HelpText = "Mode: Compress|Decompress")]
            public string Mode { get; set; }

            [Option('s', "src", Required = true, HelpText = "Source file path")]
            public string SourcePath { get; set; }

            [Option('d', "dest", Required = true, HelpText = "Destination file path")]
            public string DestinationPath { get; set; }
        }
    }
}
