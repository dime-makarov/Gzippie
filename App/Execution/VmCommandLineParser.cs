using System;

namespace Dm.Gzippie.App.Execution
{
    public class VmCommandLineParser : ICommandLineParser
    {
        public ExecutionOptions Parse(string[] args)
        {
            if (args.Length < 3)
            {
                throw new Exception("Wrong number of command line parameters.");
            }

            ExecutionOptions execOpts = new ExecutionOptions();

            if (args[0].Equals("Compress", StringComparison.InvariantCultureIgnoreCase))
            {
                execOpts.Mode = ExecutionMode.Compress;
            }
            else if (args[0].Equals("Decompress", StringComparison.InvariantCultureIgnoreCase))
            {
                execOpts.Mode = ExecutionMode.Decompress;
            }
            else
            {
                execOpts.Mode = ExecutionMode.Unknown;
            }

            execOpts.SourcePath = args[1];
            execOpts.DestinationPath = args[2];

            return execOpts;
        }
    }
}
