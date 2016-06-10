using System.Collections.Generic;

namespace Kudu.Core.Jobs
{
    public class WindowsScriptHost : ScriptHostBase
    {
        private static readonly string[] Supported = { ".cmd", ".bat", ".exe" };

        // for cmd /c, once any following args are quoted,
        // the overall args must be wrapped with an extra outer quote.
        // for instance, below is not working.
        //> cmd /c "program.exe" "first arg" "second arg"
        //'program.exe" "first' is not recognized as an internal or external command
        //> cmd /c "my program.exe" "first arg" "second arg"
        //'my' is not recognized as an internal or external command
        // To fix, we must wrap with an extra outer quote.
        //> cmd /c ""program.exe" "first arg" "second arg""
        // This also works if no arg is passed.
        //> cmd /c ""program.exe""
        public WindowsScriptHost()
            : base("cmd", "/c \"\"{0}\"{1}\"")
        {
        }

        public override IEnumerable<string> SupportedExtensions
        {
            get { return Supported; }
        }
    }
}