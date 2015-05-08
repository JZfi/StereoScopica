using System;

namespace StereoScopica
{
    class Program
    {
#if NETFX_CORE
        [MTAThread]
#else
        [STAThread]
#endif
        static void Main()
        {
            using (var program = new StereoScopica())
                program.Run();

        }
    }
}