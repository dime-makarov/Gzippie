using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Compressor
{
    public sealed class GZipCompressor : ICompressor
    {
        private List<CompressBlockInfo> _blocks;
        private readonly string _destPath;
        private readonly IBlockSizeCalculator _blockSizeCalculator;

        private readonly string _srcPath;

        /// <summary>
        ///     Ctor.
        /// </summary>
        /// <param name="sourcePath">Path to the file to compress.</param>
        /// <param name="destinationPath">Path to the file where to compress to.</param>
        /// <param name="blockSizeCalculator">An IBlockSizeCalculator instance.</param>
        public GZipCompressor(string sourcePath, string destinationPath, IBlockSizeCalculator blockSizeCalculator)
        {
            _srcPath = sourcePath;
            _destPath = destinationPath;
            _blockSizeCalculator = blockSizeCalculator;

            _blocks = BuildBlockInfoList();
        }

        /// <summary>
        ///     Compression completed event.
        /// </summary>
        public event Action<TimeSpan> OnCompleted;

        /// <summary>
        ///     Main compression method.
        /// </summary>
        public void Compress()
        {
            var t1 = DateTime.Now;

            foreach (var block in _blocks) ThreadPool.QueueUserWorkItem(CompressThreadFunc, block);

            var thrOutput = new Thread(OutputThreadFunc);
            thrOutput.Start(_blocks);
            thrOutput.Join();

            var t2 = DateTime.Now;

            OnCompleted?.Invoke(t2 - t1);
        }

        /// <summary>
        ///     Dispose logic.
        /// </summary>
        public void Dispose()
        {
            if (_blocks != null)
            {
                foreach (var block in _blocks)
                    try
                    {
                        File.Delete(block.TempPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: {1}", block.TempPath, ex.Message);
                    }

                _blocks = null;
            }
        }

        /// <summary>
        ///     Builds list of source file compression blocks.
        /// </summary>
        /// <remarks>Each compression block will be compressed in separate thread.</remarks>
        private List<CompressBlockInfo> BuildBlockInfoList()
        {
            var blockSize = _blockSizeCalculator.CalculateBlockSize(_srcPath);

            var fi = new FileInfo(_srcPath);
            var quotinent = fi.Length / blockSize;
            var remainder = fi.Length % blockSize;
            var blocks = new List<CompressBlockInfo>((int)quotinent + 1); // cast to int is safe here
            long startPos = 0;
            int i;

            for (i = 0; i < quotinent; i++)
            {
                blocks.Add(new CompressBlockInfo
                {
                    SequenceNumber = i,
                    StartPosition = startPos,
                    OriginalSizeInBytes = blockSize,
                    SrcPath = _srcPath,
                    DestPath = _destPath,
                    // TempPath will be filled in the process of compression
                    BlockProcessedEvent = new ManualResetEvent(false)
                });

                startPos += blockSize;
            }

            blocks.Add(new CompressBlockInfo
            {
                SequenceNumber = i, // i is already increased
                StartPosition = startPos,
                OriginalSizeInBytes = remainder,
                SrcPath = _srcPath,
                DestPath = _destPath,
                // TempPath will be filled in the process of compression
                BlockProcessedEvent = new ManualResetEvent(false)
            });

            return blocks;
        }

        /// <summary>
        ///     Compresses one given block of source file.
        /// </summary>
        /// <param name="param">Instance of <see cref="CompressBlockInfo" /> class.</param>
        private void CompressThreadFunc(object param)
        {
            var block = (CompressBlockInfo)param;

            // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/io/stream.cs#L50
            // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
            // The buffer is short-lived and is likely to be collected at Gen0, and it offers a significant improvement in performance.
            var buffer = new byte[81920];
            long totalBytesRead = 0;

            using (var srcStream = new FileStream(block.SrcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                block.TempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

                using (var destStream = new FileStream(block.TempPath, FileMode.OpenOrCreate))
                {
                    using (var gzipStream = new GZipStream(destStream, CompressionMode.Compress))
                    {
                        for (;;)
                        {
                            var leftBytesToRead = block.OriginalSizeInBytes - totalBytesRead;
                            var bytesToRead = Math.Min(leftBytesToRead, buffer.Length);

                            srcStream.Position = block.StartPosition + totalBytesRead;

                            var bytesRead = srcStream.Read(buffer, 0, (int)bytesToRead); // cast to int is safe here.

                            if (bytesRead == 0) break;

                            totalBytesRead += bytesRead;

                            gzipStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }

            // Set event for output thread
            block.BlockProcessedEvent.Set();
        }

        /// <summary>
        ///     Output results of block compression to destination file.
        /// </summary>
        /// <param name="param">List of compression blocks.</param>
        private void OutputThreadFunc(object param)
        {
            if (param == null) return;

            var blocks = (List<CompressBlockInfo>)param;

            if (blocks.Count == 0) return;

            // All DestPathes contain the same value
            var destFilePath = blocks[0].DestPath;

            // Will keep output file stream opened
            using (var outStream = new FileStream(destFilePath, FileMode.Create))
            {
                // Write count of blocks
                var cob = BitConverter.GetBytes(blocks.Count);
                outStream.Write(cob, 0, cob.Length);

                // Reserve bytes for compressed block sizes
                var zeros = BitConverter.GetBytes((long)0);
                for (var i = 0; i < blocks.Count; i++) outStream.Write(zeros, 0, zeros.Length);

                var buffer = new byte[81920];

                // We need to wait threads in order of SequenceNumber
                for (var i = 0; i < blocks.Count; i++)
                {
                    blocks[i].BlockProcessedEvent.WaitOne();

                    // Write block
                    using (var tempStream = new FileStream(blocks[i].TempPath, FileMode.Open))
                    {
                        for (;;)
                        {
                            var bytesRead = tempStream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0) break;

                            outStream.Write(buffer, 0, bytesRead);
                        }

                        blocks[i].CompressedSizeInBytes = tempStream.Length;
                    }
                }

                // Write compressed block lengths into reserved fields
                outStream.Position = cob.Length;
                for (var i = 0; i < blocks.Count; i++)
                {
                    var csib = BitConverter.GetBytes(blocks[i].CompressedSizeInBytes);
                    outStream.Write(csib, 0, csib.Length);
                }
            }
        }
    }
}