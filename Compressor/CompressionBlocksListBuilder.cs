using System.Collections.Generic;
using System.IO;
using System.Threading;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Compressor
{
    public class CompressionBlocksListBuilder : ICompressionBlocksListBuilder
    {
        private readonly ICompressionBlockSizeCalculator _compressionBlockSizeCalculator;

        public CompressionBlocksListBuilder(ICompressionBlockSizeCalculator compressionBlockSizeCalculator)
        {
            _compressionBlockSizeCalculator = compressionBlockSizeCalculator;
        }

        /// <summary>
        ///     Builds a list of source file compression blocks.
        /// </summary>
        /// <remarks>It is supposed that each compression block will be compressed in a separate thread.</remarks>
        public List<CompressionBlockInfo> BuildBlocksList(string srcPath, string destPath)
        {
            var blockSize = _compressionBlockSizeCalculator.CalculateBlockSize(srcPath);

            var fi = new FileInfo(srcPath);
            var quotinent = fi.Length / blockSize;
            var remainder = fi.Length % blockSize;
            var blocks = new List<CompressionBlockInfo>((int)quotinent + 1); // cast to int is safe here
            long startPos = 0;
            int i;

            for (i = 0; i < quotinent; i++)
            {
                blocks.Add(CreateCompressionBlockInfo(i, startPos, blockSize, srcPath, destPath));
                startPos += blockSize;
            }

            blocks.Add(CreateCompressionBlockInfo(i, startPos, remainder, srcPath, destPath));

            return blocks;
        }

        private static CompressionBlockInfo CreateCompressionBlockInfo(
            long sequenceNumber,
            long startPosition,
            long originalSizeInBytes,
            string sourcePath,
            string destinationPath)
        {
            return new CompressionBlockInfo
            {
                SequenceNumber = sequenceNumber,
                StartPosition = startPosition,
                OriginalSizeInBytes = originalSizeInBytes,
                SrcPath = sourcePath,
                DestPath = destinationPath,
                BlockProcessedEvent = new ManualResetEvent(false)
            };
        }
    }
}