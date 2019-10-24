using CommandLine;

namespace Roki.Common
{
    public static class OptionParser
    {
        public static (T, bool) ParseFrom<T>(T options, string[] args) where T : ICommandOptions
        {
            using var p = new Parser(x => x.HelpWriter = null);
            var res = p.ParseArguments<T>(args);
            options = res.MapResult(x => x, x => options);
            options.NormalizeOptions();

            return (options, res.Tag == ParserResultType.Parsed);
        }
    }
}