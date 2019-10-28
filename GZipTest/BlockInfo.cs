using System.Threading;

namespace GZipTest
{
    public class CompressBlockInfo
    {
        /// <summary>
        /// Sequence number of block (starts from zero)
        /// </summary>
        public long SequenceNumber;

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
            return string.Format("{0} : {1} : {2}", SequenceNumber, OriginalSizeInBytes, TempPath);
        }
    }
}
