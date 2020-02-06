using System.Text;
using System.Threading;

namespace Dm.Gzippie.Contract
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
            var sb = new StringBuilder();

            sb.AppendLine("[");
            sb.AppendLine($"    {nameof(SrcPath)}: {SrcPath}");
            sb.AppendLine($"    {nameof(DestPath)}: {DestPath}");
            sb.AppendLine($"    {nameof(TempPath1)}: {TempPath1}");
            sb.AppendLine($"    {nameof(TempPath2)}: {TempPath2}");
            sb.AppendLine($"    {nameof(StartPosition)}: {StartPosition}");
            sb.AppendLine($"    {nameof(SizeInBytes)}: {SizeInBytes}");
            sb.AppendLine("]");

            return sb.ToString();
        }
    }
}
