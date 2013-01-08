using System.Collections.Generic;
using Kudu.Core.Infrastructure;
using Xunit;
using IniLookup = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

namespace Kudu.Core.Test
{
    public class IniFileFacts
    {
        public class ParseValues
        {
            [Fact]
            public void EmptySectionIgnored()
            {
                IniLookup lookup;

                IniFile.ParseValues(new[] { "[]", "a=b" }, out lookup);

                Assert.Equal(0, lookup.Count);
            }

            [Fact]
            public void SectionWithEmptyKeySkipped()
            {
                IniLookup lookup;

                IniFile.ParseValues(new[] { "[section]", "=b" }, out lookup);

                Assert.Equal(1, lookup.Count);
                Dictionary<string, string> values;
                Assert.True(lookup.TryGetValue("section", out values));
                Assert.Equal(0, values.Count);
            }

            [Fact]
            public void ParsesSectionsIntoKeyValuePairs()
            {
                IniLookup lookup;

                IniFile.ParseValues(new[] { "[section]", "", "    ", "\t", "key=\tvalue", "[s2]", "x=  y" }, out lookup);

                Assert.Equal(2, lookup.Count);
                Dictionary<string, string> s1;
                Assert.True(lookup.TryGetValue("section", out s1));
                Assert.Equal(1, s1.Count);
                Assert.Equal("value", s1["key"]);

                Dictionary<string, string> s2;
                Assert.True(lookup.TryGetValue("s2", out s2));
                Assert.Equal(1, s2.Count);
                Assert.Equal("y", s2["x"]);
            }

            [Fact]
            public void ParseInvalidPair()
            {
                IniLookup lookup;

                IniFile.ParseValues(new[] { "[section]", "=" }, out lookup);

                Assert.Equal(1, lookup.Count);
                Dictionary<string, string> s1;
                Assert.True(lookup.TryGetValue("section", out s1));
                Assert.Equal(0, s1.Count);
            }

            [Fact]
            public void ParsesMultipleEqualSigns()
            {
                IniLookup lookup;

                IniFile.ParseValues(new[] { "[section]", "", @"command = msbuild SimpleWebApplication/SimpleWebApplication.csproj /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""%TARGET%"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Debug;SolutionDir=""%SOURCE%""" }, out lookup);

                Assert.Equal(1, lookup.Count);
                Dictionary<string, string> s1;
                Assert.True(lookup.TryGetValue("section", out s1));
                Assert.Equal(1, s1.Count);
                Assert.Equal(@"msbuild SimpleWebApplication/SimpleWebApplication.csproj /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""%TARGET%"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Debug;SolutionDir=""%SOURCE%""", s1["command"]);
            }
        }
    }
}
