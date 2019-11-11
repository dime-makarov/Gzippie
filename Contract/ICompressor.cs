using System;

namespace Dm.GZippie.Contract
{
    public interface ICompressor : IDisposable
    {
        void Compress(string sourcePath, string destinationPath);
    }
}
