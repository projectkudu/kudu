using Kudu.Core.Infrastructure;
using System;

namespace Kudu.TestHarness
{
    public static class Node
    {
        private const string NodeExe = "node.exe";

        public static void Execute(string workingDirectory, string commandFormat, params object[] args)
        {
            var command = String.Format(commandFormat, args);

            using (new LatencyLogger(NodeExe + " " + command))
            {
                var exe = new Executable(NodeExe, workingDirectory, idleTimeout: TimeSpan.FromSeconds(3600));
                exe.SetHomePath(Environment.GetEnvironmentVariable("USERPROFILE"));
                var result = exe.Execute(command);

                TestTracer.Trace("  stdout: {0}", result.Item1);
                TestTracer.Trace("  stderr: {0}", result.Item2);
            }
        }
    }
}
