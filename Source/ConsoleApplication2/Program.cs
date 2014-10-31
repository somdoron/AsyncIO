using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            object state = new object();
           
            Stopwatch stopwatch = Stopwatch.StartNew();
            var handle = GCHandle.Alloc(state);

            for (int i = 0; i < 10000000; i++)
            {                
                var address = GCHandle.ToIntPtr(handle);

                var handle2 = GCHandle.FromIntPtr(address);

                object state2 = handle2.Target;    
            }            

            stopwatch.Stop();

            Console.WriteLine(stopwatch.Elapsed);
            Console.WriteLine();
        }
    }
}
