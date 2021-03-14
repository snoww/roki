using CommandLine;

namespace Roki.Common
{
    public static class OptionsParser
    {
        public static T ParseFrom<T>(T options, string[] args) where T : ICommandOptions
        {
            using var p = new Parser(x =>
            {
                x.HelpWriter = null;
                x.CaseInsensitiveEnumValues = true;
            });
            p.ParseArguments<T>(args)
                .WithParsed(x => options = x)
                .WithNotParsed(_ => options = default);
            if (options != null)
            {
                options.NormalizeOptions();
            }

            return options;
        }
    }
}