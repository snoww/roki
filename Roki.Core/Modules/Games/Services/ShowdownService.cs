using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Roki.Core.Services;

namespace Roki.Modules.Games.Services
{
    public class ShowdownService : IRService
    {
/*        public static readonly Dictionary<string, string> actions = new Dictionary<string, string>()
        {
            { "debug", "ignored" },
            { "move", "{0} used {1} on {2}." },
            { "switch", "this is a move" },
            { "detailschange", "this is a move" },
            { "replace", "this is a move" },
            { "swap", "this is a move" },
            { "cant", "this is a move" },
            { "faint", "this is a move" },
            { "-fail", "this is a move" },
            { "-notarget", "this is a move" },
            { "-miss", "this is a move" },
            { "-damage", "this is a move" },
            { "-heal", "this is a move" },
            { "-sethp", "this is a move" },
            { "-status", "this is a move" },
            { "-cureteam", "this is a move" },
            { "-boost", "this is a move" },
            { "-unboost", "this is a move" },
            { "-setboost", "this is a move" },
            { "-swapboost", "this is a move" },
            { "-invertboost", "this is a move" },
            { "-clearboost", "this is a move" },
            { "-clearpositiveboost", "this is a move" },
            { "-copyboost", "this is a move" },
            { "-weather", "this is a move" },
            { "-fieldstart", "this is a move" },
            { "-fieldend", "this is a move" },
            { "-sidestart", "this is a move" },
            { "-sideend", "this is a move" },
            { "-start", "this is a move" },
            { "-end", "this is a move" },
            { "-crit", "this is a move" },
            { "-supereffective", "this is a move" },
            { "-resisted", "this is a move" },
            { "-immune", "this is a move" },
            { "-item", "this is a move" },
            { "-enditem", "this is a move" },
            { "-ability", "this is a move" },
            { "-endability", "this is a move" },
            { "-transform", "this is a move" },
            { "-mega", "this is a move" },
            { "-primal", "this is a move" },
            { "-burst", "this is a move" },
            { "-zpower", "this is a move" },
            { "-zbroken", "this is a move" },
            { "-activate", "this is a move" },
            { "-hint", "this is a move" },
            { "-center", "this is a move" },
            { "-message", "this is a move" },
            { "-combine", "this is a move" },
            { "-waiting", "this is a move" },
            { "-prepare", "this is a move" },
            { "-mustrecharge", "this is a move" },
            { "-nothing", "this is a move" },
            { "-hitcount", "this is a move" },
            { "-singlemove", "this is a move" },
            { "-singleturn", "this is a move" },
        };*/

        public ShowdownService()
        {
        }

        public async Task<string> StartAiGameAsync()
        {
            using (var proc = new Process())
            {
                proc.StartInfo.FileName = "./scripts/battle.sh";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.Start();
                var reader = proc.StandardOutput;
                var output = await reader.ReadToEndAsync().ConfigureAwait(false);
                proc.WaitForExit();

                return output;
            }
        }

        public async Task<string> ParseGame(string game)
        {
            var index = game.IndexOf("|\n", StringComparison.InvariantCultureIgnoreCase);
            
            var gameIntro = game.Substring(0, index);
            var gameChunks = game.Substring(index + 1).Split("|\n");

            return "";
        }

        public List<List<string>> ParseIntro(string intro)
        {
            var lines = intro.Split('\n');
            var p1Poke = new List<string>();
            var p2Poke = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("|poke|p1"))
                {
                    var poke = line.Split('|');
                    p1Poke.Add(poke[2]);
                }
                else if (line.StartsWith("|poke|p2"))
                {
                    var poke = line.Split('|');
                    p2Poke.Add(poke[2]);
                }
            }

            var toReturn = new List<List<string>> {p1Poke, p2Poke};

            return toReturn;
        }

        private static List<string> ParseTurn(string turn)
        {
            var turnDetails = new List<string>();
            var lines = turn.Split('\n');
            foreach (var line in lines)
            {
                var action = line.Split('|');
                if (action.Length <= 1) continue;
                switch (action[1])
                {
                    case "debug":
                        break;
                    case "move":
                        if (action[4] == "")
                            break;
                        turnDetails.Add(action[2].Substring(5) != action[4].Substring(5)
                            ? $"Player {action[2][1]}'s {action[2].Substring(5)} used {action[3]} on Opponent's {action[4].Substring(5)}.'"
                            : $"Player {action[2][1]}'s {action[2].Substring(5)} used {action[3]} on its own {action[4].Substring(5)}.'");
                        break;
                    case "switch":
                        turnDetails.Add($"Player {action[2][1]} switched in {action[3].Replace(", L", ", LVL")}, HP: {action[4]}");
                        break;
                    case "drag":
                        turnDetails.Add($"Player {action[2][1]}'s {action[3].Replace(", L", ", LVL")} was dragged out! HP: {action[4]}");
                        break;
                    case "detailschange":
                        break;
                    case "replace":
                        turnDetails.Add($"Player {action[2][1]}'s {action[2]}'s illusion faded. HP: {action[4]}");
                        break;
                    case "swap":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "cant":
                        turnDetails.Add($"Player {action[2][1]}'s {action[2].Substring(5)} couldn't move. Reason: {action[3]}");
                        break;
                    case "faint":
                        turnDetails.Add($"Player {action[2][1]}'s {action[2].Substring(5)} has fainted");
                        break;
                    case "-fail":
                        turnDetails.Add($"But it failed");
                        break;
                    case "-notarget":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-miss":
                        turnDetails.Add($"But it missed");
                        break;
                    case "-damage":
                        turnDetails.Add(action.Length >= 4 && action[4].StartsWith("[from]")
                            ? action[4].Contains("item:")
                                ? $"{action[2].Substring(5)} took damage from its {action[4].Substring(12)} and is now at {action[3]} HP"
                                : $"{action[2].Substring(5)} took damage from {action[5].Substring(5)}'s {action[4].Substring(7)} and is now at {action[3]} HP"
                            : $"{action[2].Substring(5)} took damage and is now at {action[3]} HP");
                        break;
                    case "-heal":
                        if (action.Length >= 4 && action[4].StartsWith("[from]"))
                        {
                            if (action[4].Contains("item:"))
                                turnDetails.Add($"{action[2].Substring(5)} restored health from its {action[4].Substring(13)} and is now at {action[3]} HP");
                            else if (action[4].Contains("move:"))
                                turnDetails.Add($"{action[2].Substring(5)} restored health from {action[5].Substring(5)}'s {action[4].Substring(7)} and is now at {action[3]} HP");
                            else
                                turnDetails.Add($"{action[2].Substring(5)} restored health from its {action[4].Substring(16)} and is now at {action[3]} HP");
                        }
                        else
                            turnDetails.Add($"{action[2].Substring(5)} restored health and is now at {action[3]} HP");
                        break;
                    case "-sethp":
                        turnDetails.Add(action.Length >= 4 && action[4].StartsWith("[from]")
                            ? $"{action[2].Substring(5)}'s HP has been set by {action[4].Substring(13)} and is now at {action[3]} HP"
                            : $"{action[2].Substring(5)}'s HP has been set to {action[3]} HP"); // should never see this
                        break;
                    case "-status":
                        turnDetails.Add(action.Length >= 4 && action[4].StartsWith("[from]")
                            ? action[4].Contains("move:")
                                ? $"{action[2].Substring(5)} has been inflicted with {action[3]} from {action[4].Substring(13)}"
                                : $"{action[2].Substring(5)} has been inflicted with {action[3]} by its {action[4].Substring(13)}"
                            : $"{action[2].Substring(5)} has status: {action[3]}");
                        break;
                    case "-curestatus":
                        turnDetails.Add($"{action[2].Substring(5)} has recovered from {action[3]}");
                        break;
                    case "-cureteam":
                        turnDetails.Add($"Player {action[2][1]}'s team has recovered from all status effects");
                        break;
                    case "-boost":
                        turnDetails.Add(action[4] == "1"
                            ? $"{action[2].Substring(5)}'s {action[3]} has been boosted by {action[4]} stage"
                            : $"{action[2].Substring(5)}'s {action[3]} has been boosted by {action[4]} stages");
                        break;
                    case "-unboost":
                        turnDetails.Add(action[4] == "1"
                            ? $"{action[2].Substring(5)}'s {action[3]} has been lowered by {action[4]} stage"
                            : $"{action[2].Substring(5)}'s {action[3]} has been lowered by {action[4]} stages");
                        break;
                    case "-setboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s {action[3]} has been set to stage {action[4]}");
                        break;
                    case "-swapboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s stat boosts has been swapped with {action[2].Substring(5)}. {action[4]}");
                        break;
                    case "-invertboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s stat boosts has been inverted!");
                        break;
                    case "-clearboost":
                        turnDetails.Add($"All of {action[2].Substring(5)}'s stat boosts has been cleared!");
                        break;
                    case "-clearallboost":
                        turnDetails.Add($"All stat boosts on the field has been cleared!");
                        break;
                    case "-clearpositiveboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s positive stat boosts has been cleared by {action[3].Substring(5)}'s {action[4].Substring(6)}");
                        break;
                    case "-clearnegativeboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s negative stat boosts has been cleared!");
                        break;
                    case "-copyboost":
                        turnDetails.Add($"{action[2].Substring(5)}'s boosts has been copied to {action[3].Substring(5)}");
                        break;
                    case "-weather":
                        if (action.Length == 3)
                            turnDetails.Add($"The weather is now {action[2]}");
                        if (action[3].StartsWith("[from]"))
                        {
                            if (action[3].Contains("ability"))
                                turnDetails.Add($"The weather is now: {action[2]} because of {action[4].Substring(10)}'s {action[3].Substring(7)}");
                        }
                        else if (action[3].StartsWith("[up"))
                            turnDetails.Add($"{action[2]} is still active");
                        else
                            turnDetails.Add($"The weather has cleared.");
                        break;
                    case "-fieldstart":
                        turnDetails.Add(action[3].StartsWith("[of")
                            ? $"{action[3].Substring(9)}'s {action[2].Substring(6)} has effected the battlefield"
                            : $"{action[2].Substring(6)} has started from {action[4].Substring(10)}'s {action[3].Substring(16)}");
                        break;
                    case "-fieldend":
                        turnDetails.Add($"{action[2].Substring(6)} hsa ended");
                        break;
                    case "-sidestart":
                        turnDetails.Add(action[3].StartsWith("move")
                            ? $"Player {action[2][1]}'s side now has {action[3].Substring(6)}"
                            : $"Player {action[2][1]}'s side now has {action[3]}");
                        break;
                    case "-sidened":
                        turnDetails.Add(action[3].StartsWith("move")
                            ? $"Player {action[2][1]}'s {action[3].Substring(6)} has now ended"
                            : $"Player {action[2][1]}'s {action[3]} has now ended");
                        break;
                    case "-start":
                        if (action[3].StartsWith("move:"))
                            turnDetails.Add($"{action[2].Substring(5)} is effected by {action[3].Substring(6)}");
                        else if (action[3].StartsWith("type"))
                            turnDetails.Add($"{action[2].Substring(5)} is now a {action[4]} type, {action[5].Substring(7)}");
                        else
                            turnDetails.Add($"{action[2].Substring(5)} now has volatile status {action[3]}");
                        break;
                    case "-end":
                        turnDetails.Add($"{action[2].Substring(5)}'s {action[3]} has faded");
                        break;
                    case "-crit":
                        turnDetails.Add("A critical strike!");
                        break;
                    case "-supereffective":
                        turnDetails.Add("That was super effective!");
                        break;
                    case "-resisted":
                        turnDetails.Add("That was not very effective");
                        break;
                    case "-immune":
                        turnDetails.Add($"It doesn't effect {action[2].Substring(5)}");
                        break;
                    case "-item":
                        turnDetails.Add($"{action[2].Substring(5)} is holding {action[3]}");
                        break;
                    case "-enditem":
                        turnDetails.Add(action.Length >= 5
                            ? action[5].StartsWith("[from")
                                ? $"{action[2].Substring(5)}'s {action[3]} has been destroyed by {action[4].Substring(13)}"
                                : $"{action[2].Substring(5)}'s {action[3]} has been consumed"
                            : $"{action[2].Substring(5)}'s {action[3]} has been used");
                        break;
                    case "-ability":
                        turnDetails.Add($"{action[2].Substring(5)}'s ability {action[3]}");
                        break;
                    case "-endability":
                        turnDetails.Add($"{action[2].Substring(5)} no longer has {action[3]} ability");
                        break;
                    case "-transform":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-mega":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-primal":
                        turnDetails.Add($"Player {action[2][2]}'s {action[2].Substring(5)} has reverted to its primal forme");
                        break;
                    case "-burst":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-zpower":
                        turnDetails.Add($"{action[2].Substring(5)} is now using its Z-Power");
                        break;
                    case "-zbroken":
                        turnDetails.Add($"The Z-Move has broken through {action[2].Substring(5)}'s protect!");
                        break;
                    case "-activate":
                        turnDetails.Add($"{action[2].Substring(5)}'s {action[3]} has activated");
                        break;
                    case "-hint":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-center":
                        turnDetails.Add($"This line should never appear. If it did, something went horribly wrong, please @snow about this!!!\n`{line}`");
                        break;
                    case "-message":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-combine":
                        turnDetails.Add($"This line should never appear. If it did, something went horribly wrong, please @snow about this!!!\n`{line}`"); break;
                    case "-waiting":
                        turnDetails.Add($"please @snow about this!!!\n`{line}`");
                        break;
                    case "-prepare":
                        turnDetails.Add($"{action[2].Substring(5)} is prepping to use {action[3]}");
                        break;
                    case "-mustrecharge":
                        turnDetails.Add($"{action[2].Substring(5)} must recharge");
                        break;
                    case "-nothing":
                        turnDetails.Add($"This line should never appear. If it did, something went horribly wrong, please @snow about this!!!\n`{line}`");
                        break;
                    case "-hitcount":
                        turnDetails.Add(action[3] == "1"
                            ? $"It hit {action[3]} time"
                            : $"It hit {action[3]} times");
                        break;
                    case "-singlemove":
                        // ignored
                        break;
                    case "-singleturn":
                        // ignored
                        break;
                    default:
                        //ignored
                        break;
                }
            }

            return turnDetails;
        }

        public List<List<string>> ParseTurns(string game)
        {
            var turns = game.Split("|turn");
            return turns.Select(ParseTurn).ToList();
        }
    }
}