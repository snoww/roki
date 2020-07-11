using CommandLine;

namespace Roki.Common
{
    public static class OptionsParser
    {
        public static T ParseFrom<T>(T options, string[] args) where T : ICommandArgs
        {
            using var p = new Parser(x =>
            {
                x.HelpWriter = null;
                x.CaseInsensitiveEnumValues = true;
            });
            var res = p.ParseArguments<T>(args);
            options = res.MapResult(x => x, x => options);
            options.NormalizeArgs();

            return options;
        }
    }
}