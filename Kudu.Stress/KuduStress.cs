using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml; 
using Kudu.Contracts;
using Kudu.TestHarness;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace Kudu.Stress
{
    class KuduStress
    {
        static string appName = null;
        static string scenarioName = null;
        static int testDuration = -1;

        static DateTime startTime;

        static void Main(string[] args)
        {
            if (!ParseArgs(args))
            {
                return;
            }

            string logFileName = "KuduStressLog_" + DateTime.Now.ToString("mm-dd-yyyy_HH-mm-ss") + ".log";
            string logFilePath = Path.Combine(Environment.CurrentDirectory, logFileName);

            StreamWriter streamWriter = new StreamWriter(logFilePath);
            StressTextWriterTraceListener traceListener = new StressTextWriterTraceListener(streamWriter, true);
            System.Diagnostics.Trace.Listeners.Add(traceListener); 
            System.Diagnostics.Trace.Listeners.Add(new StressConsoleTraceListener(true));

            // load apps list
            Dictionary<string, GitApplication> appsList = ReadGitAppsList();
            if (!appsList.ContainsKey(appName))
            {
                Console.WriteLine ("Unable to continue. Invalid appname '" + appName + "' specified.");
                return;
            }

            StressTestCases testCases = new StressTestCases();
            testCases.TestApplication = appsList[appName];
            
            startTime = DateTime.Now;

            Trace.WriteLine(string.Format("Stress Starting Run.  Start time: {0},  Scenario Name: {1}, App Name: {2},  Duration: {3} secs", startTime, scenarioName, appName, testDuration));
            Console.WriteLine("The log file for this run is:   " + logFilePath);

            TimeSpan duration =TimeSpan.FromSeconds(testDuration);
            int iterationCount = 0;
            int testsPassed = 0 ;
            int testsFailed= 0; 
            while (DateTime.Now - startTime < duration)
            {
                iterationCount++;
                try
                {
                    switch (scenarioName)
                    {
                        case "GitPushScenario":
                            testCases.StressGitPushScenario();
                            LogIterationResult(true, scenarioName, iterationCount, "test passed");
                            break;
                        case "GitRedeployScenario":
                            testCases.StressGitRedeployScenario();
                            LogIterationResult(true, scenarioName, iterationCount, "test passed");
                            break;
                        default:
                            Console.WriteLine ("Unable to continue. Invalid scenario name specified:  " + scenarioName);
                            Environment.Exit(1);
                            break;
                    }
                    testsPassed ++;
                }
                catch (Exception ex)
                {
                    LogIterationResult(false, scenarioName, iterationCount, ex.ToString());
                    testsFailed++;
                }
                finally
                {
                    Trace.Flush();
                }
            }
            string msg = string.Format ("Test run complete. Total Iterations:  {0} , Passed: {1} , Failed: {2} ", iterationCount, testsPassed, testsFailed) ;
            Trace.WriteLine (msg);
        }

        static Dictionary<string, GitApplication> ReadGitAppsList()
        {
            Dictionary<string, GitApplication> appDictionary = new Dictionary<string, GitApplication>();
            XmlDocument xml = new XmlDocument();
            string GitAppsListFile = "GitApps.xml";
            if (File.Exists(GitAppsListFile))
            {
                xml.Load(GitAppsListFile);

                XmlNodeList nodes = xml.SelectNodes("/GitApplications/GitApplication");
                foreach (XmlNode node in nodes)
                {
                    GitApplication app = new GitApplication()
                    {
                        AppName = node.Attributes["Name"].Value,
                        FirstCommitID = node.Attributes["FirstCommitID"].Value,
                        SecondCommitID = node.Attributes["SecondCommitID"].Value,
                        GitUrl = node.Attributes["GitUrl"].Value,
                        VerificationContent = node.Attributes["Content"].Value,
                        FileToModify = node.Attributes["FileToModify"].Value,
                    };
                    appDictionary.Add(app.AppName, app);
                }
                return appDictionary;
            }
            else
            {
                throw new ApplicationException("Test error:  The applications list file 'GitApps.xml' was not found. Unable to continue");
            }
        }


        static private void LogIterationResult(bool passed, string testName, int iterationCount, string message)
        {
            DateTime currentTime = DateTime.Now;
            int elapsedSeconds = (int)(currentTime - startTime).TotalSeconds;

            string msg = string.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}", "Stress Result", iterationCount, DateTime.Now, passed ? "Passed" : "Failed",  elapsedSeconds, testName, message);
            Trace.WriteLine(msg);
        }

        static private bool ParseArgs(string[] args)
        {
            string usageTxt = "Usage (parameter options on separate line):  Kudu.Stress.exe " +
            "\r\n   /scenarioname:<GitPushScenario, GitRedeployScenario> " +
            "\r\n   /appname<AspNetWebApplication, GalleryServerProWeb, JabbR, (see GitApps.xml for complete list)> " +
            "\r\n   /duration:<run duration in seconds>" +
            "\r\nExample: Kudu.Stress.exe /scenarioname:GitPushScenario /appname:AspNetWebApplication /duration:600" ;
            string incorrectUsageTxt = "Incorrect command line. " + usageTxt;


            if (args.Length == 0 || args[0] == "/?" || args[0] == "-?" || args[0] == "?")
            {
                Console.WriteLine(usageTxt);
                return false;
            }

            foreach (string arg in args)
            {
                string[] parts = arg.Split(new string[]{":"}, 10, StringSplitOptions.None) ;
                if (parts.Length != 2)
                {
                    Console.WriteLine(incorrectUsageTxt);
                    return false;
                }

                switch (parts[0].ToLower())
                {
                    case "/scenarioname":
                        scenarioName = parts[1];
                        break;
                    case "/duration":
                        testDuration = int.Parse(parts[1]); 
                        break;
                    case "/appname":
                        appName = parts[1];
                        break;
                    default:
                        Console.WriteLine(incorrectUsageTxt);
                        return false;
                }
            }

            if (scenarioName == null || appName == null || testDuration <= 0)
            {
                Console.WriteLine(incorrectUsageTxt);
                return false;
            }

            return true; 
        }


    }

}
