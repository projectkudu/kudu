using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration;

namespace Kudu.FunctionalTests.Infrastructure
{
    public class GitTestConfig 
    {
        private string _configPath;

        public GitTestConfig(string configPath)
        {
            _configPath = configPath;
        }
        
        public string Name { get; set; }
        public string RepoName { get; set; }
        public string RepoUrl { get; set; }
        public string RepoCloneUrl { get; set; }
        public string DefaultBranchName { get; set; }
        public string VerificationText { get; set; }
        public HttpStatusCode ExpectedResponseCode { get; set; }
        public bool Skip { get; set; }

        // Need to return IEnumerable<object[]> so it can be passed to the constructor of 
        // PropertyDataAttribute (xUnit extensions)
        public List<object[]> GetTests()
        {
            List<object[]> tests = new List<object[]>();
            using (var reader = new CsvReader(new StreamReader(_configPath)))
            {
                while (reader.Read())
                {                    
                    var testData = new object[] { reader.GetField("Name"), reader.GetField("RepoName"),
                                                  reader.GetField("RepoUrl"), reader.GetField("RepoCloneUrl"), 
                                                  reader.GetField("DefaultBranchName"), reader.GetField("VerificationText"), 
                                                  reader.GetField<HttpStatusCode>("ExpectedResponseCode"), reader.GetField<bool>("Skip") };
                    tests.Add(testData);                                        
                }
            }
            
            return tests;
        }
    }
}
