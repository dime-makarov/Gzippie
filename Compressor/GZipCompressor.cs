using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Dm.Gzippie.Contract;

namespace Dm.Gzippie.Compressor
{
    public class GZipCompressor : ICompressor
    {
        protected string SrcPath;
        protected string DestPath;
        protected List<CompressBlockInfo> Blocks;

        /// <summary>
        /// Compress
        /// </summary>
        public virtual void Compress(string sourcePath, string destinationPath)
        {
            SrcPath = sourcePath;
            DestPath = destinationPath;

            long blockSize = CalculateBlockSize();
            Blocks = BuildBlockInfoList(blockSize);

            foreach (CompressBlockInfo block in Blocks)
            {
                ThreadPool.QueueUserWorkItem(CompressThreadFunc, block);
            }

            Thread thrOutput = new Thread(OutputThreadFunc);
            thrOutput.Start(Blocks);
            thrOutput.Join();
        }


        protected virtual void CompressThreadFunc(object param)
        {
            CompressBlockInfo block = (CompressBlockInfo)param;

            // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/io/stream.cs#L50
            // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
            // The buffer is short-lived and is likely to be collected at Gen0, and it offers a significant improvement in performance.
            byte[] buffer = new byte[81920];
            long totalBytesRead = 0;

            using (FileStream srcStream = new FileStream(block.SrcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream destStream = new FileStream(block.TempPath, FileMode.OpenOrCreate))
                {
                    using (GZipStream gzipStream = new GZipStream(destStream, CompressionMode.Compress))
                    {
                        for (; ; )
                        {
                            long leftBytesToRead = block.OriginalSizeInBytes - totalBytesRead;
                            long bytesToRead = Math.Min(leftBytesToRead, buffer.Length);

                            // Position
                            srcStream.Position = block.StartPosition + totalBytesRead;

                            int bytesRead = srcStream.Read(buffer, 0, (int)bytesToRead); // cast to int is safe here.

                            if (bytesRead == 0) break;

                            totalBytesRead += bytesRead;

                            gzipStream.Write(buffer, 0, bytesRead);
                        }

                        // block.CompressedSizeInBytes = destStream.Length;
                    }
                }
            }

            // Set event for output thread
            block.BlockProcessedEvent.Set();
        }

        protected virtual long CalculateBlockSize()
        {
            FileInfo fi = new FileInfo(SrcPath);

            // TODO: Calculate via max threads etc.

            return 64000;
        }

        protected virtual List<CompressBlockInfo> BuildBlockInfoList(long blockSize)
        {
            FileInfo fi = new FileInfo(SrcPath);
            long quotinent = fi.Length / blockSize;
            long remainder = fi.Length % blockSize;
            List<CompressBlockInfo> blocks = new List<CompressBlockInfo>((int)quotinent + 1); // 32 Gb is 32000 Mb -> cast to int is safe here
            long startPos = 0;
            int i;

            for (i = 0; i < quotinent; i++)
            {
                blocks.Add(new CompressBlockInfo
                {
                    SequenceNumber = i,
                    StartPosition = startPos,
                    OriginalSizeInBytes = blockSize,
                    SrcPath = SrcPath,
                    DestPath = DestPath,
                    TempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()),
                    BlockProcessedEvent = new ManualResetEvent(false)
                });

                startPos += blockSize;
            }

            blocks.Add(new CompressBlockInfo
            {
                SequenceNumber = i, // i is already increased
                StartPosition = startPos,
                OriginalSizeInBytes = remainder,
                SrcPath = SrcPath,
                DestPath = DestPath,
                TempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()),
                BlockProcessedEvent = new ManualResetEvent(false)
            });

            return blocks;
        }


        protected virtual void OutputThreadFunc(object param)
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
        

        public void Dispose()
        {
            if (Blocks != null)
            {
                foreach(CompressBlockInfo block in Blocks)
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

                Blocks = null;
            }
        }
    }
}
