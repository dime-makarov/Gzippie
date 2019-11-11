using System.Threading;

namespace Dm.GZippie.Contract
{
    public class DecompressBlockInfo
    {
        public string SrcPath;

        public string DestPath;

        public string TempPath1;

        public string TempPath2;

        public long StartPosition;

        public long SizeInBytes;

        public ManualResetEvent BlockProcessedEvent;

        public override string ToString()
        {
            return string.Format("{0} : {1} : {2}", StartPosition, SizeInBytes, TempPath2);
        }
    }
}
