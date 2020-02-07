using System;

namespace Dm.Gzippie.Contract
{
    public interface ICompressor : IDisposable
    {
        void Compress();

        event Action<TimeSpan> OnCompleted;
    }
}
