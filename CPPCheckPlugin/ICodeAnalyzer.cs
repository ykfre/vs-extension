using System.Collections.Generic;
using System;
using EnvDTE;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Resources;
using System.Reflection;
using System;
using System.Collections;
using Newtonsoft.Json;
using System.Collections;

namespace VSPackage.CPPCheckPlugin
{
    using System.Threading;
    public class ThreadAbortException : Exception
    {
        public ThreadAbortException()
        {
        }
    }

    public class ProcessCreateException : Exception
    {
        public ProcessCreateException(int returnCode, String error, String output)
        {
            m_output = output;
            m_error = error;
        }

        private int returnCode { get; }

        String m_error;
        String m_output;
    }
    public abstract class ICodeAnalyzer : IDisposable
    {
        public enum SuppressionScope
        {
            suppressThisMessage,
            suppressThisMessageSolutionWide,
            suppressThisMessageGlobally,
            suppressThisTypeOfMessageFileWide,
            suppressThisTypeOfMessageProjectWide,
            suppressThisTypeOfMessagesSolutionWide,
            suppressThisTypeOfMessagesGlobally,
            suppressAllMessagesThisFileProjectWide,
            suppressAllMessagesThisFileSolutionWide,
            suppressAllMessagesThisFileGlobally
        };

        public enum SuppressionStorage
        {
            Project,
            Solution,
            Global
        }

        public enum AnalysisType { DocumentSavedAnalysis, ProjectAnalysis };

        public class ProgressEvenArgs : EventArgs
        {
            public ProgressEvenArgs(int progress, int filesChecked = 0, int totalFilesNumber = 0)
            {
                Debug.Assert(progress >= 0 && progress <= 100);
                Progress = progress; TotalFilesNumber = totalFilesNumber;
                FilesChecked = filesChecked;
            }
            public int Progress { get; set; }
            public int FilesChecked { get; set; }
            public int TotalFilesNumber { get; set; }
        }

        public delegate void progressUpdatedHandler(object sender, ProgressEvenArgs e);
        public event progressUpdatedHandler ProgressUpdated;

        protected void onProgressUpdated(int progress, int filesChecked = 0, int totalFiles = 0)
        {
            // Make a temporary copy of the event to avoid possibility of 
            // a race condition if the last subscriber unsubscribes 
            // immediately after the null check and before the event is raised.
            if (ProgressUpdated != null)
            {
                ProgressUpdated(this, new ProgressEvenArgs(progress, filesChecked, totalFiles));
            }
        }

        public ICodeAnalyzer()
        {

            _numCores = Environment.ProcessorCount;
            _threadManager = new ThreadManager(3, startAnalyzerProcess);
        }

        ~ICodeAnalyzer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {

                // Free any other managed objects here.
                //
            }
            _disposed = true;
        }

        private bool _disposed = false;
        public abstract void analyze(List<ConfiguredFiles> configuredFiles, bool analysisOnSavedFile);

        public Problem parsSeveriety(String line, String projectPath)
        {

            int severiety_start_index = line.IndexOf(": ") + ": ".Length;
            int severiety_end_index = line.IndexOf(":", severiety_start_index);
            String severiety = line.Substring(severiety_start_index, severiety_end_index - severiety_start_index);
            int line_start_index = line.Substring(0, severiety_start_index).LastIndexOf("(");
            String filePath = line.Substring(0, line_start_index);
            line_start_index += 1;
            int col_start_index = line.IndexOf(",", line_start_index);
            int line_num = Int32.Parse(line.Substring(line_start_index, col_start_index - line_start_index));
            col_start_index += 1;
            int col_num = Int32.Parse(line.Substring(col_start_index, line.IndexOf(")", col_start_index) - col_start_index));
            String message = line.Substring(severiety_end_index + 1);
            return new Problem(severiety, message, filePath, line_num, col_num, projectPath);
        }

        public Problem parseMetric(String line, String projectPath)
        {

            int message_start_index = line.IndexOf(": ") + 1;
            int message_end_index = line.IndexOf(":", message_start_index + 1);
            String message = line.Substring(message_start_index, message_end_index - message_start_index);
            String severiety = "required";
            int line_start_index = line.Substring(0, line.IndexOf(": ")).LastIndexOf("(");
            String filePath = line.Substring(0, line_start_index);
            line_start_index += 1;
            int line_end_index = line.IndexOf(")", line_start_index);
            int line_num = Int32.Parse(line.Substring(line_start_index, line_end_index - line_start_index));
            int courrent_level_start_index = message_end_index + 2;
            int courrent_level_end_index = line.IndexOf(";", courrent_level_start_index);
            var current_level_str = line.Substring(courrent_level_start_index, courrent_level_end_index - courrent_level_start_index);
            double current_level = Convert.ToDouble(current_level_str);
            int min_level_start_index = courrent_level_end_index + "; ".Length;
            int min_level_end_index = line.IndexOf(";", min_level_start_index);
            double min_level = Convert.ToDouble(line.Substring(min_level_start_index, min_level_end_index - min_level_start_index));
            int max_level_start_index = min_level_end_index + "; ".Length;
            int max_level_end_index = line.IndexOf(" ", max_level_start_index);
            double max_level = 0;
            try
            {
                max_level = Convert.ToDouble(line.Substring(max_level_start_index, max_level_end_index - max_level_start_index));
            }
            catch (Exception)
            {
                return new Problem(severiety, $"{message} current_value is {current_level} but required to be at least {min_level}", filePath, line_num, 0, projectPath);

            }

            return new Problem(severiety, $"{message} current_value is {current_level} but required to be between {min_level} and {max_level}", filePath, line_num, 0, projectPath);
        }

        int getProblemPage(Problem problem, String pagesFile)
        {
            using (StreamReader r = new StreamReader(pagesFile))
            {
                string json = r.ReadToEnd();
                var items = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                foreach (var rule in items.Keys)
                {
                    if (problem.Message.Contains(rule))
                    {
                        return items[rule];
                    }
                }
                return 0;
            }
        }

        public List<Problem> parseOutput(String output, String projectPath, String autosarJsonPath, String axivionJsonPath)
        {
            var lines = output.Split(new[] { '\r', '\n' });
            var problems = new List<Problem>();
            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                try
                {
                    if (!line.Contains("Metric"))
                    {
                        problems.Add(parsSeveriety(line, projectPath));
                    }
                    else
                    {
                        problems.Add(parseMetric(line, projectPath));
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        problems[problems.Count - 1].Message += String.Format(" {0}", line);
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            foreach (var problem in problems)
            {
                int problemPage = getProblemPage(problem, axivionJsonPath);
                problem.AxivionAddress = CPPCheckPluginPackage.AXIVION_GUIDE_HTML + $"#page={problemPage}";
                problemPage = getProblemPage(problem, autosarJsonPath);
                problem.AutosarAddress = CPPCheckPluginPackage.AUTOSAR_GUIDE_HTML + $"#page={problemPage}";
            }
            return problems;
        }

        public List<Problem> filterProblems(List<Problem> problems)
        {
            var filtered_problems = new List<Problem>();
            foreach (var problem in problems)
            {
                if (!(problem.Severity.Contains("DISABLED") || problem.Severity.Contains("advisory") ||
                    problem.Severity.Contains("low") || problem.Message.ToLower().Contains("unused field")))
                {
                    filtered_problems.Add(problem);
                }
            }
            return filtered_problems;
        }

        protected void run(SourceFile sourceFile, bool isChanged)
        {
            if (null != sourceFile)
            {
                _threadManager.Add(sourceFile, true);
            }
        }

        public void addProblemsToToolwindow(List<Problem> problems, String filePath, bool shouldClear)
        {
            CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
            {
                await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (shouldClear)
                {
                    MainToolWindow.Instance.clear();
                    _cachedInformation[filePath].problems.Clear();
                }
                if (MainToolWindow.Instance == null || problems == null)
                    return;

                foreach (var problem in problems)
                {
                    _cachedInformation[filePath].problems.Add(problem);

                    MainToolWindow.Instance.displayProblem(problem, false);
                }
            });
        }

        public (String, String, System.Diagnostics.Process) runProcess(String filePath, String arguments, String workingDirectory, bool shouldOutputAsync, ManualResetEvent killEvent, String cppFile, StringDictionary environments = null)
        {
            if (environments == null) { environments = new StringDictionary(); };
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            var startInfo = new ProcessStartInfo();
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.FileName = filePath;
            process.StartInfo.CreateNoWindow = true;
            foreach (String key in environments.Keys)
            {
                process.StartInfo.EnvironmentVariables[key] = environments[key];
            }
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            var outputWaitHandle = new ManualResetEvent(false);
            var errorWaitHandle = new ManualResetEvent(false);

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    if (shouldOutputAsync)
                    {
                        CPPCheckPluginPackage.addTextToOutputWindow(e.Data, cppFile);
                    }
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    errorWaitHandle.Set();
                }
                else
                {
                    if (shouldOutputAsync)
                    {
                        CPPCheckPluginPackage.addTextToOutputWindow(e.Data, cppFile);
                    }
                    error.AppendLine(e.Data);
                }
            };
            // Start the process.
            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // Wait for analysis completion
            while (!(outputWaitHandle.WaitOne(500) &&
              errorWaitHandle.WaitOne(500)))
            {
                if (killEvent.WaitOne(0))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {

                    }
                    throw new ThreadAbortException();
                }
            }
            if (process.ExitCode != 0)
            {
                throw new ProcessCreateException(process.ExitCode, output.ToString(), error.ToString());
            }
            return (output.ToString(), error.ToString(), process);
        }

        public String getCafeCcCommand(SourceFile file, String outputPath)
        {
            String command = "-B. ";
            foreach (var include in file.IncludePaths)
            {
                command += "-I\"" + include + "\"" + " ";
            }
            foreach (var define in file.Macros)
            {
                command += "-D_MT " + "-D\"" + define + "\"" + " ";
            }
            command += "--type_system=Windows64 --wchar_t --no_ms_permissive --ms_c++17 -c ";
            command += String.Format("-o {0} {1}", outputPath, file.FilePath);
            return command;
        }

        private String findAxivionDir(String filePath)
        {
            var dir = filePath;
            while (true)
            {
                dir = Path.GetDirectoryName(dir);
                var axivionDir = dir + "\\Axivion";
                if (Directory.Exists(axivionDir))
                {
                    return axivionDir;
                }

                if (dir == Path.GetDirectoryName(dir))
                {
                    throw new FileNotFoundException(String.Format("couldn't find axivion dir {0}", filePath));
                }
            }
        }

        public String getStyleCheckCommand(String inputIr, String axivionDir, String rfgFilePath)
        {
            var xml = System.Xml.Linq.XElement.Load(axivionDir + "\\build.conf");

            var projectsItem = (from item in xml.Descendants("project")
                                select item).ToList()[0];

            var databases = (from item in projectsItem.Descendants("action")
                             where (String)item.Attribute("tool") == "database" && (String)item.Attribute("name") == "database"
                             select item).ToList()[0];
            var ignore_list = (from item in databases.Descendants("input")
                               where (String)item.Attribute("name") == "file_owners"
                               select item).ToList()[0];
            var ignore_list2 = (from item in ignore_list.Descendants("element")
                                where (String)item.Attribute("value") == "__ignore__"
                                select (String)item.Attribute("key")).ToList();
            String ignore_command = "";
            foreach (var rule in ignore_list2)
            {
                ignore_command += String.Format("-exclude \"{0}\" ", rule);
            }
            return $"-quiet  -unit -ir {inputIr} -rfg {rfgFilePath} {String.Join("|", ignore_command)} -vs_mode";
        }

        public virtual void runLogic(SourceFile file, String autosarJsonPath, String axivionJsonPath, ManualResetEvent killEvent)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var axivionDir = findAxivionDir(file.FilePath);
                var cafeCcCommand = getCafeCcCommand(file, tempFile);
                var processPath = "cafeCC.exe";
                var filePath = file.FilePath;
                CPPCheckPluginPackage.addTextToOutputWindow(String.Format("{0} {1}", processPath, cafeCcCommand), filePath);
                var result = runProcess(processPath, cafeCcCommand, file.BaseProjectPath, shouldOutputAsync: true, killEvent: killEvent, cppFile: filePath);
                var error = result.Item2;
                if (error.Contains(": error #"))
                {
                    Problem[] tempProblems = { new Problem("Error", error, filePath, 0, 0, file.BaseProjectPath) };
                    addProblemsToToolwindow(tempProblems.ToList(), filePath, shouldClear: false);

                    MainToolWindow.Instance.bringToFront();
                    throw new Exception("errors found in compilation");
                }
                var tempFile2 = Path.GetTempFileName();

                var arguments = $"{tempFile} {tempFile2}";
                processPath = "ir2rfg";
                CPPCheckPluginPackage.addTextToOutputWindow(String.Format("{0} {1}", processPath, arguments), filePath);
                runProcess(processPath, arguments, ".", shouldOutputAsync: true, killEvent: killEvent, cppFile: filePath);
                var stylecheckCommand = getStyleCheckCommand(tempFile, axivionDir, tempFile2);
                StringDictionary env = new StringDictionary();
                CPPCheckPluginPackage.addTextToOutputWindow($"BAUHAUS_CONFIG is is {axivionDir}", filePath);
                env["BAUHAUS_CONFIG"] = axivionDir;
                processPath = "stylecheck.exe";
                CPPCheckPluginPackage.addTextToOutputWindow(String.Format("{0} {1}", processPath, stylecheckCommand), filePath);
                result = runProcess(processPath, stylecheckCommand, file.BaseProjectPath, shouldOutputAsync: true, killEvent: killEvent, environments: env, cppFile: filePath);
                var output = result.Item1;
                error = result.Item2;

                var problems = parseOutput(output, file.BaseProjectPath, autosarJsonPath, axivionJsonPath);
                problems = filterProblems(problems);

                addProblemsToToolwindow(problems.GetRange(0, Math.Min(100, problems.Count)), filePath, shouldClear: true);


                MainToolWindow.Instance.bringToFront();
                MainToolWindow.Instance._ui.ResetSorting();

            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private void startAnalyzerProcess(SourceFile sourceFile, bool isChanged, ManualResetEvent killEvent)
        {
            // Should be removed in the future
            if (null == sourceFile)
            {
                return;
            }
            if (isChanged)
            {
                MainToolWindow.Instance._ui.FilesLines[sourceFile.FilePath] = new List<int>();
            }
            var filePath = sourceFile.FilePath;
            if (!isChanged && _cachedInformation.Keys.Contains(filePath))
            {
                CPPCheckPluginPackage.addTextToOutputWindow(_cachedInformation[sourceFile.FilePath].output, filePath,
                    shouldClear: true);
                if (_cachedInformation[filePath].isFinished)
                {
                    addProblemsToToolwindow(_cachedInformation[filePath].problems.GetRange(0, _cachedInformation[filePath].problems.Count), filePath, shouldClear: true);
                    MainToolWindow.Instance._ui.ResetSorting();
                    return;
                }
                _cachedInformation[filePath].problems.Clear();
            }
            else if (!_cachedInformation.Keys.Contains(filePath))
            {
                _cachedInformation[filePath] = new CachedInformation();
            }

            CPPCheckPluginPackage.Instance.JoinableTaskFactory.Run(async () =>
            {
                await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
                MainToolWindow.Instance.clear();
            });
            try
            {
                onProgressUpdated(0);
                CPPCheckPluginPackage.addTextToOutputWindow("Starting analyzer", filePath, shouldClear: true);
                runLogic(sourceFile, autosarJsonPath: CPPCheckPluginPackage.autosarJsonPath,
                    axivionJsonPath: CPPCheckPluginPackage.axivionJsonPath, killEvent: killEvent);
                CPPCheckPluginPackage.addTextToOutputWindow("Analysis completed", filePath);
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                Problem[] problems = { new Problem("Error", e.ToString(), filePath, 0, 0, sourceFile.BaseProjectPath) };
                addProblemsToToolwindow(problems.ToList(), filePath, shouldClear: false);
                MainToolWindow.Instance.bringToFront();

            }
            _cachedInformation[filePath].isFinished = true;
            onProgressUpdated(100);
        }

        private static string solutionSuppressionsFilePath()
        {
            return CPPCheckPluginPackage.solutionPath() + "\\" + CPPCheckPluginPackage.solutionName() + "_solution_suppressions.cfg";
        }

        private static string projectSuppressionsFilePath(string projectBasePath, string projectName)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(projectBasePath) && !String.IsNullOrWhiteSpace(projectName));
            Debug.Assert(Directory.Exists(projectBasePath));
            return projectBasePath + "\\" + projectName + "_project_suppressions.cfg";
        }

        private static string globalSuppressionsFilePath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\CppcheckVisualStudioAddIn\\suppressions.cfg";
        }


        protected string _projectBasePath = null; // Base path for a project currently being checked
        protected string _projectName = null; // Name of a project currently being checked

        protected int _numCores;

        private ThreadManager _threadManager = null;

        public Dictionary<string, CachedInformation> _cachedInformation = new Dictionary<string, CachedInformation>();
        public int switchedWindow = 1;
    }
}
