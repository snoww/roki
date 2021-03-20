using System.Collections.Generic;
using System.Text.RegularExpressions;
using Roki.Web.Models;

namespace Roki.Web.Services
{
    public class ValidationService
    {
        private const string Required = "Required.";
        private const string GreaterThan0 = "Must be > 0.";
        private const string GreaterThanEqualTo0 = "Must be ≥ 0.";
        private const string GreaterThan1 = "Must be > 1.";
        public static Dictionary<string, string> ValidateGuildConfig(GuildConfigUpdate update, GuildConfig config)
        {
            var errors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(update.Prefix))
            {
                errors.Add("guild_prefix_error", Required);
            }
            else if (update.Prefix.Length > 5)
            {
                errors.Add("guild_prefix_error", "Max length 5 chars.");
            }
            else
            {
                config.Prefix = update.Prefix;
            }

            if (string.IsNullOrWhiteSpace(update.CurrencyName))
            {
                errors.Add("guild_curr_error", Required);
            }
            else if (update.CurrencyName.Length > 50)
            {
                errors.Add("guild_curr_error", "Max length 50 chars.");
            }
            else
            {
                config.CurrencyName = update.CurrencyName;
            }
            
            if (string.IsNullOrWhiteSpace(update.CurrencyNamePlural))
            {
                errors.Add("guild_curr_error", Required);
            }
            else if (update.CurrencyNamePlural.Length > 50)
            {
                errors.Add("guild_curr_p_error", "Max length 50 chars.");
            }
            else
            {
                config.CurrencyNamePlural = update.CurrencyNamePlural;
            }
            
            if (string.IsNullOrWhiteSpace(update.CurrencyIcon))
            {
                errors.Add("guild_curr_icon_error", Required);
            }
            else
            {
                config.CurrencyIcon = update.CurrencyIcon;
            }

            if (long.TryParse(update.CurrencyDefault, out long currDefault) && currDefault >= 0)
            {
                config.CurrencyDefault = currDefault;
            }
            else
            {
                errors.Add("guild_curr_default_cash_error", GreaterThanEqualTo0);
            }
            
            if (decimal.TryParse(update.InvestingDefault, out decimal invDefault) && invDefault >= 0)
            {
                config.InvestingDefault = invDefault;
            }
            else
            {
                errors.Add("guild_curr_default_inv_error", GreaterThan0);
            }

            if (double.TryParse(update.CurrencyGenerationChance, out double genChance) && genChance is >= 0 or <= 100)
            {
                config.CurrencyGenerationChance = genChance / 100;
            }
            else
            {
                errors.Add("guild_curr_prob_error", "Must be between 0 and 100.");
            }

            if (int.TryParse(update.CurrencyGenerationCooldown, out int genCd) && genCd >= 0)
            {
                config.CurrencyGenerationCooldown = genCd;
            }
            else
            {
                errors.Add("guild_curr_cd_error", GreaterThanEqualTo0);
            }
            
            if (int.TryParse(update.CurrencyDropAmount, out int dropAmount) && dropAmount >= 0)
            {
                config.CurrencyDropAmount = dropAmount;
            }
            else
            {
                errors.Add("guild_curr_drop_error", GreaterThanEqualTo0);
            }
            
            if (int.TryParse(update.CurrencyDropAmountMax, out int dropMax) && dropMax > 0)
            {
                if (dropMax >= dropAmount)
                {
                    config.CurrencyDropAmountMax = dropMax;
                }
                else
                {
                    errors.Add("guild_curr_drop_max_error", "Must be ≥ min drop.");
                }
            }
            else
            {
                errors.Add("guild_curr_drop_max_error", GreaterThan0);
            }
            
            if (int.TryParse(update.CurrencyDropAmountRare, out int dropRare) && dropRare > 0)
            {
                if (dropRare >= dropMax)
                {
                    config.CurrencyDropAmountRare = dropRare;
                }
                else
                {
                    errors.Add("guild_curr_drop_rare_error", "Must be ≥ max drop.");
                }
            }
            else
            {
                errors.Add("guild_curr_drop_rare_error", GreaterThan0);
            }
            
            if (int.TryParse(update.XpCooldown, out int xpCd) && xpCd >= 0)
            {
                config.XpCooldown = xpCd;
            }
            else
            {
                errors.Add("guild_xp_cd_error", GreaterThanEqualTo0);
            }
            
            if (int.TryParse(update.XpFastCooldown, out int xpFCd) && xpFCd >= 0)
            {
                config.XpFastCooldown = xpFCd;
            }
            else
            {
                errors.Add("guild_xp_cd_fast_error", GreaterThanEqualTo0);
            }
            
            if (int.TryParse(update.NotificationLocation, out int notLoc) && notLoc is >= 0 and <= 3)
            {
                config.NotificationLocation = notLoc;
            }
            else
            {
                errors.Add("guild-xp-notif-help", "Not a valid option.");
            }
            
            if (double.TryParse(update.JeopardyWinMultiplier, out double jpWin) && jpWin > 0)
            {
                config.JeopardyWinMultiplier = jpWin;
            }
            else
            {
                errors.Add("guild_jp_mult_error", GreaterThan0);
            }
            
            if (double.TryParse(update.TriviaMinCorrect, out double triviaMin) && triviaMin is > 0 and <= 100)
            {
                config.TriviaMinCorrect = triviaMin / 100;
            }
            else
            {
                errors.Add("guild_trivia_min_error", "Must be between 0 and 100.");
            }
            
            if (int.TryParse(update.TriviaEasy, out int triviaEasy) && triviaEasy > 0)
            {
                config.TriviaEasy = triviaEasy;
            }
            else
            {
                errors.Add("guild_trivia_easy_error", GreaterThan0);
            }
            
            if (int.TryParse(update.TriviaMedium, out int triviaMed) && triviaMed > 0)
            {
                config.TriviaMedium = triviaMed;
            }
            else
            {
                errors.Add("guild_trivia_med_error", GreaterThan0);
            }
            
            if (int.TryParse(update.TriviaHard, out int triviaHard) && triviaHard > 0)
            {
                config.TriviaHard = triviaHard;
            }
            else
            {
                errors.Add("guild_trivia_hard_error", GreaterThan0);
            }
            
            if (int.TryParse(update.BetFlipMin, out int bfMin) && bfMin > 0)
            {
                config.BetFlipMin = bfMin;
            }
            else
            {
                errors.Add("guild_bf_min_error", GreaterThan0);
            }
            
            if (double.TryParse(update.BetFlipMultiplier, out double bfMult) && bfMult > 1)
            {
                config.BetFlipMultiplier = bfMult;
            }
            else
            {
                errors.Add("guild_bf_mult_error", GreaterThan1);
            }
            
            if (int.TryParse(update.BetFlipMMinGuesses, out int bfmMinGuesses) && bfmMinGuesses > 1)
            {
                config.BetFlipMMinGuesses = bfmMinGuesses;
            }
            else
            {
                errors.Add("guild_bfm_min_guess_error", GreaterThan1);
            }
            
            if (double.TryParse(update.BetFlipMMinMultiplier, out double bfmMinMult) && bfmMinMult > 1)
            {
                config.BetFlipMMinMultiplier = bfmMinMult;
            }
            else
            {
                errors.Add("guild_bfm_min_bet_error", GreaterThan1);
            }


            if (double.TryParse(update.BetFlipMMultiplier, out double bfmMult) && bfmMult > 1)
            {
                config.BetFlipMMultiplier = bfmMult;
            }
            else
            {
                errors.Add("guild_bfm_mult_error", GreaterThan1);
            }
                        
            if (double.TryParse(update.BetFlipMMinCorrect, out double bfmMinCorrect) && bfmMinCorrect is > 0 and <= 100)
            {
                config.BetFlipMMinCorrect = bfmMinCorrect / 100;
            }
            else
            {
                errors.Add("guild_bfm_min_correct_error", "Must be between 0 and 100.");
            }
            
            if (int.TryParse(update.BetDiceMin, out int bdMin) && bdMin > 1)
            {
                config.BetDiceMin = bdMin;
            }
            else
            {
                errors.Add("guild_bd_min_error", GreaterThan1);
            }
            
            if (int.TryParse(update.BetRollMin, out int brMin) && brMin > 0)
            {
                config.BetRollMin = brMin;
            }
            else
            {
                errors.Add("guild_br_min_error", GreaterThan0);
            }
            
            if (double.TryParse(update.BetRoll71Multiplier, out double br71) && br71 > 1)
            {
                config.BetRoll71Multiplier = br71;
            }
            else
            {
                errors.Add("guild_br_min_error", GreaterThan1);
            }
            
            if (double.TryParse(update.BetRoll92Multiplier, out double br92) && br92 > 1)
            {
                config.BetRoll92Multiplier = br92;
            }
            else
            {
                errors.Add("guild_br_92_error", GreaterThan1);
            }
            
            if (double.TryParse(update.BetRoll100Multiplier, out double br100) && br100 > 1)
            {
                config.BetRoll100Multiplier = br100;
            }
            else
            {
                errors.Add("guild_br_100_error", GreaterThan1);
            }
            
            return errors;
        }
    }
}