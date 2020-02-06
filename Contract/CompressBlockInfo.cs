using System.Text;
using System.Threading;

namespace Dm.Gzippie.Contract
{
    public class CompressBlockInfo
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
