using System;
using System.Reflection;
using Xunit;

namespace Kudu.TestHarness.Xunit
{
    // copied from https://github.com/xunit/xunit/blob/master/src/xunit.runner.msbuild/xunit.cs
    public class xunit : global::Xunit.Runner.MSBuild.xunit
    {
        private FieldInfo getCancel;
        private FieldInfo getDisplayNameFormatter;

        protected override global::Xunit.XmlTestExecutionVisitor CreateVisitor(string assemblyFileName, System.Xml.Linq.XElement assemblyElement)
        {
            if (TeamCity)
            {
                return new KuduXunitTeamCityVisitor(Log, assemblyElement, () => GetCancel(), displayNameFormatter: GetDisplayNameFormatter());
            }

            return base.CreateVisitor(assemblyFileName, assemblyElement);
        }

        private bool GetCancel()
        {
            if (getCancel == null)
            {
                getCancel = typeof(global::Xunit.Runner.MSBuild.xunit).GetField("cancel", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            }

            return (bool)getCancel.GetValue(this);
        }

        private TeamCityDisplayNameFormatter GetDisplayNameFormatter()
        {
            if (getDisplayNameFormatter == null)
            {
                getDisplayNameFormatter = typeof(global::Xunit.Runner.MSBuild.xunit).GetField("teamCityDisplayNameFormatter", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            }

            return (TeamCityDisplayNameFormatter)getDisplayNameFormatter.GetValue(this);
        }
    }
}