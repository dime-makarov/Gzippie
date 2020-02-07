using System;

namespace Dm.Gzippie.Contract
{
    public interface IDecompressor : IDisposable
    {
        void Decompress();

        event Action<TimeSpan> OnCompleted;
    }
}
