namespace Kudu.Core.Infrastructure
{
    internal static class ParserHelpers
    {
        internal static bool IsSingleNewLine(string value)
        {
            if (value.Length == 2 && value[0] == '\r' && value[1] == '\n')
            {
                return true;
            }
            return value.Length == 1 && value[0] == '\n';
        }
    }
}
