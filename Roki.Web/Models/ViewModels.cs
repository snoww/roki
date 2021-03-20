using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Roki.Web.Models
{
    public class GuildChannelModel
    {
        // public string Section { get; set; }
        public GuildConfig GuildConfig { get; set; }
        public List<Channel> Channels { get; set; }
    }

    public class GuildConfigUpdate
    {
        public string Prefix { get; set; }
        [JsonPropertyName("guild-logging")]
        public string Logging { get; set; }
        [JsonPropertyName("guild-curr")]
        public string CurrencyGen { get; set; }
        [JsonPropertyName("guild-xp")]
        public string XpGain { get; set; }
        [JsonPropertyName("guild-show-help")]
        public string ShowHelpOnError { get; set; }
        [JsonPropertyName("guild_curr_icon")]
        public string CurrencyIcon { get; set; }
        [JsonPropertyName("guild_curr_name")]
        public string CurrencyName { get; set; }
        [JsonPropertyName("guild_curr_name_p")]
        public string CurrencyNamePlural { get; set; }
        [JsonPropertyName("guild_curr_prob")]
        public string CurrencyGenerationChance { get; set; }
        [JsonPropertyName("guild_curr_cd")]
        public string CurrencyGenerationCooldown { get; set; }
        [JsonPropertyName("guild_curr_drop")]
        public string CurrencyDropAmount { get; set; }
        [JsonPropertyName("guild_curr_drop_max")]
        public string CurrencyDropAmountMax { get; set; }
        [JsonPropertyName("guild_curr_drop_rare")]
        public string CurrencyDropAmountRare { get; set; }
        [JsonPropertyName("guild_curr_default")]
        public string CurrencyDefault { get; set; }
        [JsonPropertyName("guild_inv_default")]
        public string InvestingDefault { get; set; }
        [JsonPropertyName("guild_xp_pm")]
        public string XpPerMessage { get; set; }
        [JsonPropertyName("guild_xp_cd")]
        public string XpCooldown { get; set; }
        [JsonPropertyName("guild_xp_cd_fast")]
        public string XpFastCooldown { get; set; }
        [JsonPropertyName("guild_xp_notif")]
        public string NotificationLocation { get; set; }
        [JsonPropertyName("guild_bf_min")]
        public string BetFlipMin { get; set; }
        [JsonPropertyName("guild_bf_mult")]
        public string BetFlipMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min")]
        public string BetFlipMMinMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min_guess")]
        public string BetFlipMMinGuesses { get; set; }
        [JsonPropertyName("guild_bfm_min_correct")]
        public string BetFlipMMinCorrect { get; set; }
        [JsonPropertyName("guild_bfm_mult")]
        public string BetFlipMMultiplier { get; set; }
        [JsonPropertyName("guild_bd_min")]
        public string BetDiceMin { get; set; }
        [JsonPropertyName("guild_br_min")]
        public string BetRollMin { get; set; }
        [JsonPropertyName("guild_br_71")]
        public string BetRoll71Multiplier { get; set; }
        [JsonPropertyName("guild_br_92")]
        public string BetRoll92Multiplier { get; set; }
        [JsonPropertyName("guild_br_100")]
        public string BetRoll100Multiplier { get; set; }
        [JsonPropertyName("guild_trivia_min")]
        public string TriviaMinCorrect { get; set; }
        [JsonPropertyName("guild_trivia_easy")]
        public string TriviaEasy { get; set; }
        [JsonPropertyName("guild_trivia_med")]
        public string TriviaMedium { get; set; }
        [JsonPropertyName("guild_trivia_hard")]
        public string TriviaHard { get; set; }
        [JsonPropertyName("guild_jeopardy_mult")]
        public string JeopardyWinMultiplier { get; set; }
    }
}