using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Decompressor
{
    public sealed class GZipDecompressor : IDecompressor
    {
        public GZipDecompressor(string sourcePath, string destinationPath)
        {
            _srcPath = sourcePath;
            _destPath = destinationPath;
            _blocks = BuildDecompressBlockInfo();
        }

        /// <summary>
        /// Decompression completed event.
        /// </summary>
        public event Action<TimeSpan> OnCompleted;

        private string _srcPath;
        private string _destPath;
        private List<DecompressBlockInfo> _blocks;


        /// <summary>
        /// Main decompression method.
        /// </summary>
        public void Decompress()
        {
            var t1 = DateTime.Now;

            foreach (DecompressBlockInfo block in _blocks)
            {
                ThreadPool.QueueUserWorkItem(DecompressThreadFunc, block);
            }

            Thread thrOutput = new Thread(OutputThreadFunc);
            thrOutput.Start(_blocks);
            thrOutput.Join();

            var t2 = DateTime.Now;

            OnCompleted?.Invoke(t2 - t1);
        }

        /// <summary>
        /// Builds list of source file decompression blocks.
        /// </summary>
        /// <remarks>Each decompression block will be decompressed in separate thread.</remarks>
        private List<DecompressBlockInfo> BuildDecompressBlockInfo()
        {
            List<DecompressBlockInfo> blocks = new List<DecompressBlockInfo>();

            using (FileStream srcStream = new FileStream(_srcPath, FileMode.Open))
            {
                byte[] blockCountBuffer = new byte[4];
                srcStream.Read(blockCountBuffer, 0, 4);
                int blockCount = BitConverter.ToInt32(blockCountBuffer, 0);

                long currStartPos = 4 + (8 * blockCount); // int + (long * blockCount)

                for (int i = 0; i < blockCount; i++)
                {
                    byte[] blockSizeBuffer = new byte[8];
                    srcStream.Read(blockSizeBuffer, 0, 8);
                    long blockSize = BitConverter.ToInt64(blockSizeBuffer, 0);

                    blocks.Add(new DecompressBlockInfo
                    {
                        SrcPath = this._srcPath,
                        DestPath = this._destPath,
                        StartPosition = currStartPos,
                        SizeInBytes = blockSize,
                        // TempPath1 will be filled in the process of compression 
                        // TempPath2 will be filled in the process of compression
                        BlockProcessedEvent = new ManualResetEvent(false)
                    });

                    currStartPos += blockSize;
                }
            }

            return blocks;
        }

        /// <summary>
        /// Decompresses one given block of source file.
        /// </summary>
        /// <param name="param">Instance of <see cref="DecompressBlockInfo"/> class.</param>
        private void DecompressThreadFunc(object param)
        {
            DecompressBlockInfo block = (DecompressBlockInfo)param;

            long startPos = block.StartPosition;
            long totalBytesToRead = block.SizeInBytes;

            // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/io/stream.cs#L50
            // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
            // The buffer is short-lived and is likely to be collected at Gen0, and it offers a significant improvement in performance.
            byte[] buffer = new byte[81920];
            long totalBytesRead = 0;

            // Extract part from source compressed file
            using (FileStream srcStream = new FileStream(block.SrcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                block.TempPath2 = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

                using (FileStream destStream = new FileStream(block.TempPath2, FileMode.OpenOrCreate))
                {
                    for (; ; )
                    {
                        long leftBytesToRead = totalBytesToRead - totalBytesRead;
                        long bytesToRead = Math.Min(leftBytesToRead, buffer.Length);

                        // Position
                        srcStream.Position = startPos + totalBytesRead;

                        int bytesRead = srcStream.Read(buffer, 0, (int)bytesToRead); // cast to int is safe here.

                        if (bytesRead == 0) break;

                        totalBytesRead += bytesRead;

                        destStream.Write(buffer, 0, bytesRead);
                    }
                }
            }

            // Decompress part
            using (FileStream srcStream = new FileStream(block.TempPath2, FileMode.Open, FileAccess.Read))
            {
                using (GZipStream gzipStream = new GZipStream(srcStream, CompressionMode.Decompress))
                {
                    block.TempPath1 = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

                    using (FileStream destStream = new FileStream(block.TempPath1, FileMode.OpenOrCreate))
                    {
                        for (; ; )
                        {
                            int bytesRead = gzipStream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0) break;

                            destStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }

            // Set event for output thread
            block.BlockProcessedEvent.Set();
        }

        /// <summary>
        /// Output results of block decompression to destination file.
        /// </summary>
        /// <param name="param">List of decompression blocks.</param>
        private void OutputThreadFunc(object param)
        {
            if (param == null)
            {
                return;
            }

            List<DecompressBlockInfo> blocks = (List<DecompressBlockInfo>)param;

            if (blocks.Count == 0)
            {
                return;
            }

            // All DestPathes contain the same value
            string destFilePath = blocks[0].DestPath;

            // Will keep output file stream opened
            using (FileStream outStream = new FileStream(destFilePath, FileMode.Create))
            {
                byte[] buffer = new byte[81920];

                // We need to wait threads in order of SequenceNumber
                for (int i = 0; i < blocks.Count; i++)
                {
                    blocks[i].BlockProcessedEvent.WaitOne();

                    // Write block
                    using (FileStream tempStream = new FileStream(blocks[i].TempPath1, FileMode.Open))
                    {
                        for (; ; )
                        {
                            int bytesRead = tempStream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0) break;

                            outStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Dispose logic.
        /// </summary>
        public void Dispose()
        {
            if (_blocks != null)
            {
                foreach (DecompressBlockInfo block in _blocks)
                {
                    try
                    {
                        File.Delete(block.TempPath1);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("{0}: {1}", block.TempPath1, ex.Message));
                    }

                    try
                    {
                        File.Delete(block.TempPath2);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("{0}: {1}", block.TempPath2, ex.Message));
                    }
                }

                _blocks = null;
            }
        }
    }
}
