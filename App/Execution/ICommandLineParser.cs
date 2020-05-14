using System;

namespace Dm.Gzippie.App.Execution
{
    interface ICommandLineParser
    {
        ExecutionOptions Parse(string[] args);
    }
}
