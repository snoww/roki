using System;
using System.Text.RegularExpressions;
using Roki.Common;
using Roki.Extensions;

namespace Roki.Modules.Games.Common
{
    public class JClue
    {
        public string Category { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
        public int Value { get; set; }
        public bool Available { get; set; } = true;

        public bool CheckAnswer(string answer)
        {
            var minAnswer = Regex.Replace(Answer.ToLowerInvariant(), "^the |a |an ", "");
            answer = SanitizeAnswer(answer);
            // if it contains an optional answer
            if (Answer.Contains('(', StringComparison.Ordinal) && Answer.Contains(')', StringComparison.Ordinal))
            {
                var optionalAnswer = minAnswer.SanitizeStringFull();
                minAnswer = Regex.Replace(minAnswer, "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))", "");
                var optLev = new Levenshtein(optionalAnswer);
                if (optLev.DistanceFrom(answer) <= Math.Round(optionalAnswer.Length * 0.1))
                    return true;
            }
            minAnswer = minAnswer.SanitizeStringFull();
            
            var minLev = new Levenshtein(minAnswer);
            var distance = minLev.DistanceFrom(answer);
            if (distance == 0)
                return true;
            if (minAnswer.Length <= 5)
                return distance == 0;
            if (minAnswer.Length <= 9)
                return distance <= 1;

            return distance <= Math.Round(minAnswer.Length * 0.15);
        }

        private static string SanitizeAnswer(string answer)
        {
            answer = answer.ToLowerInvariant();
            answer = Regex.Replace(answer, "^what |whats |where |wheres |who |whos ", "");
            answer = Regex.Replace(answer, "^is |are |was |were", "");
            return Regex.Replace(answer, "^the |a | an", "").SanitizeStringFull();
        }
    }
}