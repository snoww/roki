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

        private string MinAnswer { get; set; }
        private string OptionalAnswer { get; set; }

        public void SanitizeAnswer()
        {
            var minAnswer = Regex.Replace(Answer.ToLowerInvariant(), "^the |a |an ", "");
            // if it contains an optional answer
            if (Answer.Contains('(', StringComparison.Ordinal) && Answer.Contains(')', StringComparison.Ordinal))
            {
                OptionalAnswer = minAnswer.SanitizeStringFull();
                minAnswer = Regex.Replace(minAnswer, "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))", "");
            }

            MinAnswer = minAnswer.SanitizeStringFull();
        }

        public bool CheckAnswer(string answer)
        {
            answer = SanitizeAnswer(answer);
            if (!string.IsNullOrEmpty(OptionalAnswer))
            {
                var optLev = new Levenshtein(OptionalAnswer);
                if (optLev.DistanceFrom(answer) <= Math.Round(OptionalAnswer.Length * 0.1))
                    return true;
            }

            var minLev = new Levenshtein(MinAnswer);
            var distance = minLev.DistanceFrom(answer);
            if (distance == 0)
                return true;
            if (MinAnswer.Length <= 5)
                return distance == 0;
            if (MinAnswer.Length <= 9)
                return distance <= 1;

            return distance <= Math.Round(MinAnswer.Length * 0.15);
        }

        private static string SanitizeAnswer(string answer)
        {
            answer = answer.ToLowerInvariant();
            answer = Regex.Replace(answer, "^what |whats |where |wheres |who |whos ", "");
            answer = Regex.Replace(answer, "^is |are |was |were", "");
            return Regex.Replace(answer, "^the |a |an ", "").SanitizeStringFull();
        }
    }
}