using System;

namespace GZipTest
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
            //using (GZipCompressor cmpr = new GZipCompressor(srcPath, destPath))
            //{
            //    cmpr.Compress();
            //}


            string srcPath = @"D:\Dox\bookmarks_compressed.html";
            string destPath = @"D:\Dox\bookmarks_decompressed.html";
            
            using (GZipDecompressor dcmpr = new GZipDecompressor(srcPath, destPath))
            {
                dcmpr.Decompress();
            }
        }
    }
}
