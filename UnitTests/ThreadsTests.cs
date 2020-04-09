using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using VSPackage.CPPCheckPlugin;
namespace UnitTests
{
    class ThreadsTests
    {
        static public void sanity()
        {
            int global = 0;
            var events = new List<AutoResetEvent>();
            for (int i = 0; i < 10; i++)
            {
                events.Add(new AutoResetEvent(false));
            }

            var threadManager = new ThreadManager(4, (SourceFile file, bool isChanged, ManualResetEvent killEvent) =>
            {
                WaitHandle[] waitHandles2 = { killEvent };
                if(WaitHandle.WaitAll(waitHandles2, 300))
                {
                    return;
                }
                int i = Interlocked.Increment(ref global);
                events[i-1].Set();
            });

            for (int i = 0; i < 10; i++)
            {
                var sourceFile = new SourceFile($"{i}", $"{i}", "Project3", "vc2019");
                threadManager.Add(sourceFile, isChanged: false);
            }

            WaitHandle.WaitAll(events.ToArray());
            Trace.Assert(global == 10);
        }
    }
}
