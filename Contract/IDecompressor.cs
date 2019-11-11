using System;

namespace Dm.GZippie.Contract
{
    public interface IDecompressor : IDisposable
    {
        void Decompress(string sourcePath, string destinationPath);
    }
}
