using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using VSPackage.CPPCheckPlugin;

namespace UnitTests
{
    class Program
    {
        static void check_parsing()
        {
            var analyzer = new AnalyzerCppcheck();
            var problems =  analyzer.parseOutput(@"C:\Users\idow\source\repos\Project3\Project3\Source.cpp(4,2): DISABLED: required: Use of base type outside typedef. [signed int] (Rule AutosarC++18_03-A3.9.1)" +"\n" +@"C:\Users\idow\source\repos\Project3\Project3\Source.cpp(1):5: Metric.HIS.PARAM, HIS PARAM: 12.000000; 0.000000; 5.000000 VIOLATION: Routine: b", "");
            problems = analyzer.filterProblems(problems);
        }
        public static void Main(string[] args)
        {
            ThreadsTests.sanity();
            check_parsing();

            var analyzer = new AnalyzerCppcheck();
            var sourceFile = new SourceFile("C:\\Users\\idow\\source\\repos\\Project3\\Project3\\Source.cpp", @"C:\Users\idow\source\repos\Project3\Project3", "Project3", "vc2019");
            sourceFile.addIncludePath(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\VC\Tools\MSVC\14.16.27023\include");
            sourceFile.addIncludePath(@"C:\Program Files(x86)\Microsoft Visual Studio\2017\Community\VC\Tools\MSVC\14.16.27023\atlmfc\include");
            sourceFile.addIncludePath(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\VC\Auxiliary\VS\include");
            sourceFile.addIncludePath(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.17763.0\ucrt");
            sourceFile.addIncludePath(@"C:\Program Files(x86)\Windows Kits\10\Include\10.0.17763.0\um");
            sourceFile.addIncludePath(@"C:\Program Files(x86)\Windows Kits\10\Include\10.0.17763.0\shared");
            sourceFile.addIncludePath(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.17763.0\winrt");
            sourceFile.addIncludePath(@"C:\Program Files(x86)\Windows Kits\10\Include\10.0.17763.0\cppwinrt");
            sourceFile.addMacro("_DEBUG");
            sourceFile.addMacro("_DLL");
            sourceFile.addMacro("_MT");
            sourceFile.addMacro("_CPPUNWIND");
            sourceFile.addMacro("_UNICODE");
            analyzer.runLogic(sourceFile, new ManualResetEvent(false));
        }
    }
}
