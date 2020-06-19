using System;
using Dm.Gzippie.App.Execution;
using Dm.Gzippie.Contract;
using Dm.Gzippie.Compressor;
using Dm.Gzippie.Decompressor;

namespace Dm.Gzippie.App
{
    public class Program
    {
        static int Main(string[] args)
        {
            try
            {
                ICommandLineParser parser = new VmCommandLineParser();
                ExecutionOptions options = parser.Parse(args);

                if (options != null)
                {
                    Program app = new Program();
                    
                    if (options.Mode == ExecutionMode.Compress)
                    {
                        app.Compress(options.SourcePath, options.DestinationPath);
                    }
                    else if (options.Mode == ExecutionMode.Decompress)
                    {
                        app.Decompress(options.SourcePath, options.DestinationPath);
                    }
                    else
                    {
                        Console.WriteLine("Available values for Mode: Compress|Decompress");
                        return 1;
                    }
                }
                else
                {
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }

            return 0;
        }

        public virtual void Compress(string srcPath, string destPath)
        {
            using (ICompressor cmpr = new GZipCompressor(srcPath, destPath))
            {
                cmpr.OnCompleted += CompressionCompleted;
                cmpr.Compress();
            }
        }

        public virtual void Decompress(string srcPath, string destPath)
        {
            using (IDecompressor dcmpr = new GZipDecompressor(srcPath, destPath))
            {
                dcmpr.OnCompleted += DecompressionCompleted;
                dcmpr.Decompress();
            }
        }

        private void CompressionCompleted(TimeSpan duration)
        {
            Console.WriteLine("Compression completed in {0} ms", duration.TotalMilliseconds);
        }

        private void DecompressionCompleted(TimeSpan duration)
        {
            Console.WriteLine("Decompression completed in {0} ms", duration.TotalMilliseconds);
        }
    }
}
