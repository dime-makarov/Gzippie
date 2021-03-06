﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Compressor
{
    public sealed class GZipCompressor : ICompressor
    {
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="sourcePath">Path to the file to compress.</param>
        /// <param name="destinationPath">Path to the file where to compress to.</param>
        public GZipCompressor(string sourcePath, string destinationPath)
        {
            _srcPath = sourcePath;
            _destPath = destinationPath;
            _blocks = BuildBlockInfoList();
        }

        /// <summary>
        /// Compression completed event.
        /// </summary>
        public event Action<TimeSpan> OnCompleted;

        /// <summary>
        /// Max number of compression threads.
        /// </summary>
        private const int MaxThreads = 4;

        private string _srcPath;
        private string _destPath;
        private List<CompressBlockInfo> _blocks;
 

        /// <summary>
        /// Main compression method.
        /// </summary>
        public void Compress()
        {
            var t1 = DateTime.Now;

            foreach (CompressBlockInfo block in _blocks)
            {
                ThreadPool.QueueUserWorkItem(CompressThreadFunc, block);
            }

            Thread thrOutput = new Thread(OutputThreadFunc);
            thrOutput.Start(_blocks);
            thrOutput.Join();

            var t2 = DateTime.Now;

            OnCompleted?.Invoke(t2 - t1);
        }

        /// <summary>
        /// Builds list of source file compression blocks.
        /// </summary>
        /// <remarks>Each compression block will be compressed in separate thread.</remarks>
        private List<CompressBlockInfo> BuildBlockInfoList()
        {
            long blockSize = CalculateBlockSize();

            FileInfo fi = new FileInfo(_srcPath);
            long quotinent = fi.Length / blockSize;
            long remainder = fi.Length % blockSize;
            List<CompressBlockInfo> blocks = new List<CompressBlockInfo>((int)quotinent + 1); // cast to int is safe here
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
        /// Calculates the size of one compression block.
        /// </summary>
        private long CalculateBlockSize()
        {
            FileInfo fi = new FileInfo(_srcPath);

            long size = fi.Length / MaxThreads;

            return size + 1;
        }

        /// <summary>
        /// Compresses one given block of source file.
        /// </summary>
        /// <param name="param">Instance of <see cref="CompressBlockInfo"/> class.</param>
        private void CompressThreadFunc(object param)
        {
            CompressBlockInfo block = (CompressBlockInfo)param;

            // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/io/stream.cs#L50
            // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
            // The buffer is short-lived and is likely to be collected at Gen0, and it offers a significant improvement in performance.
            byte[] buffer = new byte[81920];
            long totalBytesRead = 0;

            using (FileStream srcStream = new FileStream(block.SrcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                block.TempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

                using (FileStream destStream = new FileStream(block.TempPath, FileMode.OpenOrCreate))
                {
                    using (GZipStream gzipStream = new GZipStream(destStream, CompressionMode.Compress))
                    {
                        for (; ; )
                        {
                            long leftBytesToRead = block.OriginalSizeInBytes - totalBytesRead;
                            long bytesToRead = Math.Min(leftBytesToRead, buffer.Length);

                            srcStream.Position = block.StartPosition + totalBytesRead;

                            int bytesRead = srcStream.Read(buffer, 0, (int)bytesToRead); // cast to int is safe here.

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
        /// Output results of block compression to destination file.
        /// </summary>
        /// <param name="param">List of compression blocks.</param>
        private void OutputThreadFunc(object param)
        {
            if (param == null)
            {
                return;
            }

            List<CompressBlockInfo> blocks = (List<CompressBlockInfo>)param;

            if (blocks.Count == 0)
            {
                return;
            }

            // All DestPathes contain the same value
            string destFilePath = blocks[0].DestPath;

            // Will keep output file stream opened
            using (FileStream outStream = new FileStream(destFilePath, FileMode.Create))
            {
                // Write count of blocks
                byte[] cob = BitConverter.GetBytes(blocks.Count);
                outStream.Write(cob, 0, cob.Length);

                // Reserve bytes for compressed block sizes
                byte[] zeros = BitConverter.GetBytes((long)0);
                for (int i = 0; i < blocks.Count; i++)
                {
                    outStream.Write(zeros, 0, zeros.Length);
                }

                byte[] buffer = new byte[81920];

                // We need to wait threads in order of SequenceNumber
                for (int i = 0; i < blocks.Count; i++)
                {
                    blocks[i].BlockProcessedEvent.WaitOne();

                    // Write block
                    using (FileStream tempStream = new FileStream(blocks[i].TempPath, FileMode.Open))
                    {
                        for (; ; )
                        {
                            int bytesRead = tempStream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0) break;

                            outStream.Write(buffer, 0, bytesRead);
                        }

                        blocks[i].CompressedSizeInBytes = tempStream.Length;
                    }
                }

                // Write compressed block lengths into reserved fields
                outStream.Position = cob.Length;
                for (int i = 0; i < blocks.Count; i++)
                {
                    byte[] csib = BitConverter.GetBytes(blocks[i].CompressedSizeInBytes);
                    outStream.Write(csib, 0, csib.Length);
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
                foreach(CompressBlockInfo block in _blocks)
                {
                    try
                    {
                        File.Delete(block.TempPath);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(string.Format("{0}: {1}", block.TempPath, ex.Message));
                    }
                }

                _blocks = null;
            }
        }

        private class CompressBlockInfo
        {
            /// <summary>
            /// Sequence number of block (starts from zero)
            /// </summary>
            public long SequenceNumber;

            /// <summary>
            /// Start position in original file
            /// </summary>
            public long StartPosition;

            /// <summary>
            /// Block size in bytes
            /// </summary>
            public long OriginalSizeInBytes;

            /// <summary>
            /// Compressed block size in bytes
            /// </summary>
            public long CompressedSizeInBytes;

            /// <summary>
            /// Path to source file
            /// </summary>
            public string SrcPath;

            /// <summary>
            /// Path to destination file
            /// </summary>
            public string DestPath;

            /// <summary>
            /// Path to the file for gzipped block content
            /// </summary>
            public string TempPath;

            /// <summary>
            /// Raised when block is processed
            /// </summary>
            public ManualResetEvent BlockProcessedEvent;


            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendLine("[");
                sb.AppendLine($"    {nameof(SequenceNumber)}: {SequenceNumber}");
                sb.AppendLine($"    {nameof(StartPosition)}: {StartPosition}");
                sb.AppendLine($"    {nameof(OriginalSizeInBytes)}: {OriginalSizeInBytes}");
                sb.AppendLine($"    {nameof(CompressedSizeInBytes)}: {CompressedSizeInBytes}");
                sb.AppendLine($"    {nameof(SrcPath)}: {SrcPath}");
                sb.AppendLine($"    {nameof(DestPath)}: {DestPath}");
                sb.AppendLine($"    {nameof(TempPath)}: {TempPath}");
                sb.AppendLine("]");

                return sb.ToString();
            }
        }
    }
}
