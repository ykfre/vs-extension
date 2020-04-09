using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSPackage.CPPCheckPlugin
{
    public class Problem
    {
        public Problem(String severity, String message, String file, int line, int col, String baseProjectPath)
        {
            _severity = severity;
            _message = message;
            _file = file;
            _line = line;
            _col = col;
            _baseProjectPath = baseProjectPath;
        }

        public String Severity
        {
            get { return _severity; }
        }

        public String Message
        {
            get { return _message; }
            set { _message = value; }
        }

        // This should be file path relative to project root
        public String FullFileName
        {
            get { return _file; }
        }

        // file name only without path
        public String FileName
        {
            get
            {
                return System.IO.Path.GetFileName(_file);
            }
        }

        public String FilePath
        {
            get
            {
                if (String.IsNullOrWhiteSpace(_file))
                    return _file;
                else if (_file.Contains(":")) // Absolute path
                    return _file;
                else
                {
                    if (String.IsNullOrWhiteSpace(_baseProjectPath))
                        return _file;
                    // Relative path - making absolute
                    return _baseProjectPath.EndsWith("\\") ? _baseProjectPath + _file : _baseProjectPath + "\\" + _file;
                }
            }
        }

        public int Line
        {
            get { return _line; }
        }

        public int Col
        {
            get { return _col; }
        }

        public String BaseProjectPath
        {
            get { return _baseProjectPath; }
        }

        private String _severity;
        private String _message;
        private String _file;
        private String _baseProjectPath;
        private int _line;
        private int _col;
    }
}
