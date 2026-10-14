using System.Text;
using Ratesim.Core;

namespace Ratesim;

public static class Program
{
    public static int Main(string[] args)
    {
        var encoding = new UTF8Encoding(false);
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), encoding) { NewLine = "\n" };
        using var stderr = new StreamWriter(Console.OpenStandardError(), encoding) { NewLine = "\n" };
        int code = Cli.Run(args, Console.In, stdout, stderr);
        stdout.Flush();
        stderr.Flush();
        return code;
    }
}
