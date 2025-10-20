using BenchmarkDotNet.Running;

namespace FirstMake.Performance;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
