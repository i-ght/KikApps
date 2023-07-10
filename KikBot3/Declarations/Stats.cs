using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KikBot3.Declarations
{
    internal static class Stats
    {
        public static volatile int Convos;
        public static volatile int In;
        public static volatile int Out;
        public static volatile int Links;
        public static volatile int Completed;
        public static volatile int Restricts;
        public static volatile int KeepAlives;

#if BLASTER
        public static volatile int Blasts;
#endif
    }
}
