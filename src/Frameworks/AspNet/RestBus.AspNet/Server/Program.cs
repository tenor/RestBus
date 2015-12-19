using System.Linq;

namespace RestBus.AspNet.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var mergedArgs = new[] { "--" + Server.ConfigServerArgumentName, Server.ConfigServerAssembly }.Concat(args).ToArray();
            Microsoft.AspNet.Hosting.Program.Main(mergedArgs);
        }
    }
}
