using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.LogHelper
{
    public static class LogServiceHelper
    {
        static string[] LogFileExtensions = new string[] { ".txt", ".log", ".htm" };
        /// <summary>
        /// Matches filenames to filter with .txt, .log, or .htm files
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Boolean if true or false</returns>
        public static bool MatchFilters(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                foreach (string ext in LogFileExtensions)
                {
                    if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Given a path to a directory, scan all the .log, .txt files in the directory and subdirectories to store information in memory and work with the data
        /// </summary>
        /// <param name="directory">The path to the directory in which Kudu is storing log files. (*note same location in azure)</param>
        /// <returns>Dictionary where the key is the fullname or absolute path of the log file that we scanned and the value is the length of that file.</returns>
        public static Dictionary<string, long> GetDirectoryFiles(string directory)
        {
            //using a stack, store the directory names in the data structure, and follow a post-order traversal in traversing the log files
            Stack<string> stack = new Stack<string>();
            Dictionary<string, long> files = new Dictionary<string, long>();
            //begin by pushing the directory where the files are
            stack.Push(directory);
            string currentDirectory = null;
            while (stack.Count > 0)
            {
                //FIFO get the top directory to scan through
                currentDirectory = stack.Pop();
                string[] subDirectories;
                subDirectories = Directory.GetDirectories(currentDirectory);
                //traverse each file and add the file path to the dictionary
                foreach (string fileName in Directory.GetFiles(currentDirectory))
                {
                    //be sure that the files are what we are looking for
                    if (!MatchFilters(fileName))
                    {
                        continue;
                    }
                    files.Add(fileName, new FileInfo(fileName).Length);
                    System.Diagnostics.Trace.WriteLine(fileName);
                }

                //after adding all the files to the dictionary for the current directory, push the paths of the subdirectories to the stack and continue the loop
                foreach (string subDirectory in subDirectories)
                {
                    stack.Push(subDirectory);
                }
            }
            return files;
        }
    }


}
