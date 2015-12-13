using System.Linq;

namespace RestBus.AspNet.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var mergedArgs = new[] { "--server", "RestBus.AspNet" }.Concat(args).ToArray();
            Microsoft.AspNet.Hosting.Program.Main(mergedArgs);
        }
    }
}
