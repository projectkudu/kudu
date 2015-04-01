using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class VsSolutionFacts : IDisposable
    {
        private static readonly string _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        [Fact]
        public void PathPropertyReturnsInputFilePath()
        {
            // Arrange
            var solutionFileContent =
@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012";
            var solutionFile = CreateSolutionFile(solutionFileContent);

            // Act
            var solution = new VsSolution(solutionFile);

            // Assert
            Assert.Equal(solutionFile, solution.Path);
        }

        [Fact]
        public void VsSolutionParsesSolutionFile()
        {
            // Arrange
            var solutionFileContent =
@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Test"", ""Test\Test.csproj"", ""{80E5FD2A-A29A-47C7-8DE9-853D14295BAC}""
EndProject";

            var solutionFile = CreateSolutionFile(solutionFileContent);

            // Act
            var solution = new VsSolution(solutionFile);

            // Assert
            Assert.Equal(1, solution.Projects.Count());
            Assert.Equal("Test", solution.Projects.First().ProjectName);
        }

        [Fact]
        public void VsSolutionParserReadsProjectDetails()
        {
            // Arrange
            var solutionFileContent =
@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""SomeProject"", ""Test\Test.csproj"", ""{80E5FD2A-A29A-47C7-8DE9-853D14295BAC}""
EndProject";

            var projectContent =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
    <PropertyGroup>
        <ProjectGuid>{975304D3-BFFA-45B4-BBC9-9A27323A670C}</ProjectGuid>
        <ProjectTypeGuids>{E3E379DF-F4C6-4180-9B81-6769533ABE47};{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
    </PropertyGroup>
</Project>";
            var solutionFile = CreateSolutionFile(solutionFileContent);
            var projectFile = CreateProjectFile(solutionFile, @"Test\Test.csproj", projectContent);

            // Act
            var solution = new VsSolution(solutionFile);
            var project = solution.Projects.First();

            // Assert
            Assert.Equal("SomeProject", project.ProjectName);
            Assert.Contains(new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc"), project.ProjectTypeGuids);
            Assert.Equal(projectFile, project.AbsolutePath);
            Assert.True(project.IsWap);
            Assert.False(project.IsWebSite);
        }

        [Theory]
        [InlineData(@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{E24C65DC-7377-472B-9ABA-BC803B73C61A}"") = ""WebSite1"", ""http://localhost:56801"", ""{CD1592DA-6712-4E3A-940D-EF5B8346785C}""
    ProjectSection(WebsiteProperties) = preProject
        TargetFrameworkMoniker = "".NETFramework,Version%3Dv4.0""
        Debug.AspNetCompiler.PhysicalPath = ""x:\debug-path\WebSite1\""
        Release.AspNetCompiler.PhysicalPath = ""x:\release-path\WebSite1\""
    EndProjectSection
EndProject", @"x:\release-path\WebSite1\")]
        [InlineData(@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{E24C65DC-7377-472B-9ABA-BC803B73C61A}"") = ""WebSite1"", ""http://localhost:56801"", ""{CD1592DA-6712-4E3A-940D-EF5B8346785C}""
    ProjectSection(WebsiteProperties) = preProject
        TargetFrameworkMoniker = "".NETFramework,Version%3Dv4.0""
        Debug.AspNetCompiler.PhysicalPath = ""x:\debug-path\WebSite1\""
    EndProjectSection
EndProject", @"x:\debug-path\WebSite1\")]
        public void VsSolutionParserReadsWebsiteDetails(string solutionFileContent, string absolutePath)
        {
            var solutionFile = CreateSolutionFile(solutionFileContent);

            // Act
            var solution = new VsSolution(solutionFile);
            var project = solution.Projects.First();

            // Assert
            Assert.Equal("WebSite1", project.ProjectName);
            Assert.Empty(project.ProjectTypeGuids);
            Assert.Equal(absolutePath, project.AbsolutePath);
            Assert.False(project.IsWap);
            Assert.True(project.IsWebSite);
        }

        private static string CreateSolutionFile(string solutionFileContent)
        {
            string testDirectory = Path.Combine(_testDirectory, Path.GetRandomFileName());
            var solutionFile = Path.Combine(testDirectory, "Test.solution");
            Directory.CreateDirectory(testDirectory);
            File.WriteAllText(solutionFile, solutionFileContent);
            return solutionFile;
        }

        private static string CreateProjectFile(string solutionFile, string projectPath, string projectContent)
        {
            var solutionDir = Path.GetDirectoryName(solutionFile);
            // The project path is relative to the solution. Convert it to an absolute path.
            projectPath = Path.Combine(solutionDir, projectPath);
            var projectDir = Path.GetDirectoryName(projectPath);

            EnsureDirectory(projectDir);
            File.WriteAllText(projectPath, projectContent);

            return projectPath;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void Dispose()
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}