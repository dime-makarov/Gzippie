using System;
using CommandLine;
using Dm.Gzippie.Contract;
using Dm.Gzippie.Compressor;
using Dm.Gzippie.Decompressor;

namespace Dm.Gzippie.App
{
    public class Options
    {
        [Option('m', "mode", Required = true, HelpText = "Mode: Compress|Decompress")]
        public string Mode { get; set; }

        [Option('s', "src", Required = true, HelpText = "Source file path")]
        public string SourcePath { get; set; }

        [Option('d', "dest", Required = true, HelpText = "Destination file path")]
        public string DestinationPath { get; set; }
    }

    public class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Options options = null;

                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed<Options>((opts) =>
                    {
                        options = opts;
                    });

                if (options != null)
                {
                    Program app = new Program();
                    
                    if (options.Mode.Equals("Compress", StringComparison.InvariantCultureIgnoreCase))
                    {
                        app.Compress(options.SourcePath, options.DestinationPath);
                    }
                    else if (options.Mode.Equals("Decompress", StringComparison.InvariantCultureIgnoreCase))
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

            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();
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
            using (IDecompressor dcmpr = new GZipDecompressor())
            {
                dcmpr.Decompress(srcPath, destPath);
            }
        }

        private void CompressionCompleted(TimeSpan duration)
        {
            Console.WriteLine("Compression completed in {0} ms", duration.TotalMilliseconds);
        }
    }
}
