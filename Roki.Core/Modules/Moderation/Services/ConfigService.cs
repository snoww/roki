using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Roki.Services;
using Roki.Services.Database.Maps;

namespace Roki.Modules.Moderation.Services
{
    public class ConfigService : IRokiService
    {
        private readonly IMongoService _mongo;

        public ConfigService(IMongoService mongo)
        {
            _mongo = mongo;
        }

        public async Task<GuildConfig> GetGuildConfigAsync(ulong guildId)
        {
            Guild guild = await _mongo.Context.GetGuildAsync(guildId).ConfigureAwait(false);
            return guild.Config ?? new GuildConfig();
        }
        
        public async Task<ChannelConfig> GetChannelConfigAsync(ITextChannel channel)
        {
            Channel dbChannel = await _mongo.Context.GetOrAddChannelAsync(channel).ConfigureAwait(false);
            ChannelConfig config = dbChannel.Config;
            if (config == null)
            {
                Guild guild = await _mongo.Context.GetGuildAsync(channel.GuildId).ConfigureAwait(false);
                return new ChannelConfig
                {
                    Logging = guild.Config.Logging,
                    CurrencyGeneration = guild.Config.CurrencyGeneration,
                    XpGain = guild.Config.XpGain,
                    Modules = guild.Config.Modules,
                    Commands = guild.Config.Commands
                };
            }

            return config;
        }

        public static EmbedBuilder PrintGuildConfig(ConfigCategory category, GuildConfig guildConfig)
        {
            var configString = new StringBuilder();
            var builder = new EmbedBuilder();

            if (category.HasFlag(ConfigCategory.Settings))
            {
                configString.Append("```");
                configString.Append("Prefix=").Append('"').Append(guildConfig.Prefix).AppendLine("\"");
                configString.Append("OkColor=").AppendLine(guildConfig.OkColor.ToString("X"));
                configString.Append("ErrorColor=").AppendLine(guildConfig.ErrorColor.ToString("X"));
                configString.Append("Logging=").AppendLine(guildConfig.Logging.ToString());
                configString.Append("CurrencyGeneration=").AppendLine(guildConfig.CurrencyGeneration.ToString());
                configString.Append("XpGain=").AppendLine(guildConfig.XpGain.ToString());
                configString.Append("Modules=").AppendLine(string.Join(";", guildConfig.Modules.Select(x => x.Key + "=" + x.Value)));
                configString.Append("Commands=").AppendLine(string.Join(";", guildConfig.Commands.Select(x => x.Key + "=" + x.Value)));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.Settings.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.Currency))
            {
                configString.Append("```");
                configString.Append("CurrencyGenerationChance=").AppendLine(guildConfig.CurrencyGenerationChance.ToString(CultureInfo.InvariantCulture));
                configString.Append("CurrencyGenerationCooldown=").AppendLine(guildConfig.CurrencyGenerationCooldown.ToString(CultureInfo.InvariantCulture));
                configString.Append("CurrencyIcon=").AppendLine(guildConfig.CurrencyIcon);
                configString.Append("CurrencyName=").AppendLine(guildConfig.CurrencyName);
                configString.Append("CurrencyNamePlural=").AppendLine(guildConfig.CurrencyNamePlural);
                configString.Append("CurrencyDropAmount=").AppendLine(guildConfig.CurrencyDropAmount.ToString(CultureInfo.InvariantCulture));
                configString.Append("CurrencyDropAmountMax=").AppendLine(guildConfig.CurrencyDropAmountMax?.ToString(CultureInfo.InvariantCulture));
                configString.Append("CurrencyDropAmountRare=").AppendLine(guildConfig.CurrencyDropAmountRare?.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.Currency.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.Xp))
            {
                configString.Append("```");
                configString.Append("XpPerMessage=").AppendLine(guildConfig.XpPerMessage.ToString(CultureInfo.InvariantCulture));
                configString.Append("XpCooldown=").AppendLine(guildConfig.XpCooldown.ToString(CultureInfo.InvariantCulture));
                configString.Append("XpFastCooldown=").AppendLine(guildConfig.XpFastCooldown.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.Xp.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.BetFlip))
            {
                configString.Append("```");
                configString.Append("BetFlipMin=").AppendLine(guildConfig.BetFlipMin.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetFlipMultiplier=").AppendLine(guildConfig.BetFlipMultiplier.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetFlipMMinGuesses=").AppendLine(guildConfig.BetFlipMMinGuesses.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetFlipMMinCorrect=").AppendLine(guildConfig.BetFlipMMinCorrect.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetFlipMMultiplier=").AppendLine(guildConfig.BetFlipMMultiplier.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.BetFlip.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.BetDie))
            {
                configString.Append("```");
                configString.Append("BetDieMin=").AppendLine(guildConfig.BetDieMin.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.BetDie.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.BetRoll))
            {
                configString.Append("```");
                configString.Append("BetRollMin=").AppendLine(guildConfig.BetRollMin.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetRoll71Multiplier=").AppendLine(guildConfig.BetRoll71Multiplier.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetRoll92Multiplier=").AppendLine(guildConfig.BetRoll92Multiplier.ToString(CultureInfo.InvariantCulture));
                configString.Append("BetRoll100Multiplier=").AppendLine(guildConfig.BetRoll100Multiplier.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.BetRoll.ToString(), configString.ToString());
                configString.Clear();
            }
            if (category.HasFlag(ConfigCategory.Trivia))
            {
                configString.Append("```");
                configString.Append("TriviaMinCorrect=").AppendLine(guildConfig.TriviaMinCorrect.ToString(CultureInfo.InvariantCulture));
                configString.Append("TriviaEasy=").AppendLine(guildConfig.TriviaEasy.ToString(CultureInfo.InvariantCulture));
                configString.Append("TriviaMedium=").AppendLine(guildConfig.TriviaMedium.ToString(CultureInfo.InvariantCulture));
                configString.Append("TriviaHard=").AppendLine(guildConfig.TriviaHard.ToString(CultureInfo.InvariantCulture));
                configString.AppendLine("```");
                builder.AddField(ConfigCategory.Trivia.ToString(), configString.ToString());
                configString.Clear();
            }

            return builder;
        }

        public static EmbedBuilder PrintChannelConfig(ChannelConfig channelConfig)
        {
            var configString = new StringBuilder();
            configString.Append("```");
            configString.Append("Logging=").AppendLine(channelConfig.Logging.ToString());
            configString.Append("CurrencyGeneration=").AppendLine(channelConfig.CurrencyGeneration.ToString());
            configString.Append("XpGain=").AppendLine(channelConfig.XpGain.ToString());
            configString.Append("Modules=").AppendLine(string.Join(";", channelConfig.Modules.Select(x => x.Key + "=" + x.Value)));
            configString.Append("Commands=").AppendLine(string.Join(";", channelConfig.Commands.Select(x => x.Key + "=" + x.Value)));
            configString.AppendLine("```");

            return new EmbedBuilder().AddField(ConfigCategory.Settings.ToString(), configString.ToString());
        }
    }

    
    [Flags]
    public enum ConfigCategory
    {
        Settings = 1,
        Currency = 2,
        Xp = 4,
        BetFlip = 8,
        BetDie = 16,
        BetRoll = 32,
        Trivia = 64,
        All = Settings | Currency | Xp | BetFlip | BetDie | BetRoll | Trivia
    }
}