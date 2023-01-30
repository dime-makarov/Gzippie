using System.IO;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Compressor
{
    public class CompressionBlockSizeCalculator : ICompressionBlockSizeCalculator
    {
        /// <summary>
        ///     Max number of compression threads.
        /// </summary>
        private const int MaxThreads = 4;

        /// <summary>
        ///     Calculates the size of one compression block.
        /// </summary>
        /// <param name="srcPath"></param>
        public long CalculateBlockSize(string srcPath)
        {
            var fi = new FileInfo(srcPath);

            var size = fi.Length / MaxThreads;

            return size + 1;
        }
    }
}