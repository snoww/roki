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
        public double CurrencyGenerationChance { get; set; }
        [JsonPropertyName("guild_curr_cd")]
        public int CurrencyGenerationCooldown { get; set; }
        [JsonPropertyName("guild_curr_drop")]
        public int CurrencyDropAmount { get; set; }
        [JsonPropertyName("guild_curr_drop_max")]
        public int CurrencyDropAmountMax { get; set; }
        [JsonPropertyName("guild_curr_drop_rare")]
        public int CurrencyDropAmountRare { get; set; }
        [JsonPropertyName("guild_curr_default")]
        public long CurrencyDefault { get; set; }
        [JsonPropertyName("guild_inv_default")]
        public decimal InvestingDefault { get; set; }
        [JsonPropertyName("guild_xp_pm")]
        public int XpPerMessage { get; set; }
        [JsonPropertyName("guild_xp_cd")]
        public int XpCooldown { get; set; }
        [JsonPropertyName("guild_xp_cd_fast")]
        public int XpFastCooldown { get; set; }
        [JsonPropertyName("guild_xp_notif")]
        public int NotificationLocation { get; set; }
        [JsonPropertyName("guild_bf_min")]
        public int BetFlipMin { get; set; }
        [JsonPropertyName("guild_bf_mult")]
        public double BetFlipMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min")]
        public double BetFlipMMinMultiplier { get; set; }
        [JsonPropertyName("guild_bfm_min_guess")]
        public int BetFlipMMinGuesses { get; set; }
        [JsonPropertyName("guild_bfm_min_correct")]
        public double BetFlipMMinCorrect { get; set; }
        [JsonPropertyName("guild_bfm_mult")]
        public double BetFlipMMultiplier { get; set; }
        [JsonPropertyName("guild_bd_min")]
        public int BetDiceMin { get; set; }
        [JsonPropertyName("guild_br_min")]
        public int BetRollMin { get; set; }
        [JsonPropertyName("guild_br_71")]
        public double BetRoll71Multiplier { get; set; }
        [JsonPropertyName("guild_br_92")]
        public double BetRoll92Multiplier { get; set; }
        [JsonPropertyName("guild_br_100")]
        public double BetRoll100Multiplier { get; set; }
        [JsonPropertyName("guild_trivia_min")]
        public double TriviaMinCorrect { get; set; }
        [JsonPropertyName("guild_trivia_easy")]
        public int TriviaEasy { get; set; }
        [JsonPropertyName("guild_trivia_med")]
        public int TriviaMedium { get; set; }
        [JsonPropertyName("guild_trivia_hard")]
        public int TriviaHard { get; set; }
        [JsonPropertyName("guild_jeopardy_mult")]
        public double JeopardyWinMultiplier { get; set; }
    }
}