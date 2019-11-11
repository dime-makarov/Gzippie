using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Dm.GZippie.App
{
    public class GZipDecompressor : IDisposable
    {
        protected string SrcPath;
        protected string DestPath;
        protected List<DecompressBlockInfo> Blocks;
        protected List<Thread> Threads;

        public GZipDecompressor(string srcPath, string destPath)
        {
            SrcPath = srcPath;
            DestPath = destPath;
        }

        public void Decompress()
        {
            Blocks = BuildDecompressBlockInfo();
            Threads = new List<Thread>();

            foreach (DecompressBlockInfo block in Blocks)
            {
                Thread thr = new Thread(DecompressThreadFunc);
                Threads.Add(thr);
                thr.Start(block);

                Console.WriteLine(block);
            }

            Thread thrOutput = new Thread(OutputThreadFunc);
            thrOutput.Start(Blocks);
            thrOutput.Join();
        }

        protected virtual List<DecompressBlockInfo> BuildDecompressBlockInfo()
        {
            List<DecompressBlockInfo> blocks = new List<DecompressBlockInfo>();

            using (FileStream srcStream = new FileStream(SrcPath, FileMode.Open))
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
                        SrcPath = this.SrcPath,
                        DestPath = this.DestPath,
                        StartPosition = currStartPos,
                        SizeInBytes = blockSize,
                        TempPath1 = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()),
                        TempPath2 = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()),
                        BlockProcessedEvent = new ManualResetEvent(false)
                    });

                    currStartPos += blockSize;
                }
            }

            return blocks;
        }


        protected virtual void DecompressThreadFunc(object param)
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


        protected virtual void OutputThreadFunc(object param)
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


        public void Dispose()
        {
            if (Blocks != null)
            {
                foreach (DecompressBlockInfo block in Blocks)
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

                Blocks = null;
            }
        }
    }
}
