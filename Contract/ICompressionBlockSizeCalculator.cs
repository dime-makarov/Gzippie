namespace Dm.Gzippie.Contract
{
    public interface ICompressionBlockSizeCalculator
    {
        long CalculateBlockSize(string srcPath);
    }
}