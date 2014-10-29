using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageScale.AsyncIO
{
    public static class ForceDotNet
    {
        internal static bool Forced { get; private set; }

        public static void Force()
        {
            Forced = true;
        }
    }
}
