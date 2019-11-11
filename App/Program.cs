using System;
using Dm.GZippie.Contract;
using Dm.GZippie.Compressor;
using Dm.GZippie.Decompressor;

namespace Dm.GZippie.App
{
    public class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Program app = new Program();
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }

            return 0;
        }

        public virtual void Run()
        {
            //string srcPath = @"D:\Dox\bookmarks.html";
            //string destPath = @"D:\Dox\bookmarks_compressed.html";
            //
            //using (ICompressor cmpr = new GZipCompressor())
            //{
            //    cmpr.Compress(srcPath, destPath);
            //}


            string srcPath = @"D:\Dox\bookmarks_compressed.html";
            string destPath = @"D:\Dox\bookmarks_decompressed.html";
            
            using (IDecompressor dcmpr = new GZipDecompressor())
            {
                dcmpr.Decompress(srcPath, destPath);
            }
        }
    }
}
