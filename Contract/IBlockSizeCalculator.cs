namespace Dm.Gzippie.Contract
{
    public interface IBlockSizeCalculator
    {
        long CalculateBlockSize(string srcPath);
    }
}