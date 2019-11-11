using System;

namespace Dm.Gzippie.Contract
{
    public interface ICompressor : IDisposable
    {
        void Compress(string sourcePath, string destinationPath);
    }
}
