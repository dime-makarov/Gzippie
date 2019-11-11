using System;

namespace Dm.Gzippie.Contract
{
    public interface IDecompressor : IDisposable
    {
        void Decompress(string sourcePath, string destinationPath);
    }
}
