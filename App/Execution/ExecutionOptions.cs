using System;

namespace Dm.Gzippie.App.Execution
{
    public class ExecutionOptions
    {
        public ExecutionMode Mode;
        public string SourcePath;
        public string DestinationPath;
    }

    public enum ExecutionMode
    {
        Unknown,
        Compress,
        Decompress
    }
}
