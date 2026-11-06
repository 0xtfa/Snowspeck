using BenchmarkDotNet.Running;

namespace Snowspeck.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Length > 0 ? args : ["--filter", "*"]);
}
