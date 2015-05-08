using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CanonHandler
{
    class Program
    {
#if NETFX_CORE
        [MTAThread]
#else
        [STAThread]
#endif
        static void Main(string[] args)
        {
            Settings settings = new Settings();
            if (CommandLine.Parser.Default.ParseArguments(args, settings))
            {
                using (CanonHandler cameraHandler = new CanonHandler(settings))
                {
                    cameraHandler.Start();
                }
            }
        }
    }
}
