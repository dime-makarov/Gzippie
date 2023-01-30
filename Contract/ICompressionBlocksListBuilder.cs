using System.Collections.Generic;

namespace Dm.Gzippie.Contract
{
    public interface ICompressionBlocksListBuilder
    {
        List<CompressionBlockInfo> BuildBlocksList(string srcPath, string destPath);
    }
}