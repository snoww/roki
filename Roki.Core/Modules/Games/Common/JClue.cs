using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<string> _optionalAnswers = new List<string>();

        public void SanitizeAnswer()
        {
            var minAnswer = Regex.Replace(Answer.ToLowerInvariant(), "^the |a |an ", "");
            // if it contains an optional answer
            if (Answer.Contains('(', StringComparison.Ordinal) && Answer.Contains(')', StringComparison.Ordinal))
            {
                // currently this wont be correctly split: "termite (in term itemize) (mite accepted)"
                var optional = minAnswer.Split('(', ')')[1];
                
                // example: "cruisers (or ships)"
                if (optional.StartsWith("or", StringComparison.Ordinal))
                {
                    var optionals = optional.Split("or");
                    foreach (var op in optionals)
                    {
                        // 2nd condition example "mare(s or maria)"
                        if (string.IsNullOrEmpty(op) || op.SanitizeStringFull().Length < 2) continue;
                        _optionalAnswers.Add(op.SanitizeStringFull());
                    }
                }
                // example: "endurance (durability accepted)"
                else if (optional.EndsWith("accepted", StringComparison.Ordinal))
                {
                    _optionalAnswers.Add(Regex.Replace(optional, "also accepted|accepted$", "").SanitizeStringFull());
                }
                // example: "The Daily Planet ("Superman")"
                else if (optional.Contains('"', StringComparison.Ordinal))
                {
                    _optionalAnswers.Add(optional.Split('"', '"')[1].SanitizeStringFull());
                }
                // this one is kinda hard to do since there are cases where it isn't valid
                // valid example added: "MoMA (the Museum of Modern Art)"
                // not valid example but added: "(the University of) Chicago", "the (San Francisco) 49ers"
                // valid example but not added: "Republic of Korea (South Korea)"
                else if (optional.SanitizeStringFull().Length > minAnswer.SanitizeStringFull().Length * 1.5)
                {
                    _optionalAnswers.Add(optional.SanitizeStringFull());
                }
                _optionalAnswers.Add(minAnswer.SanitizeStringFull());

                minAnswer = Regex.Replace(minAnswer, "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))", "");
            }

            MinAnswer = minAnswer.SanitizeStringFull();
        }

        public bool CheckAnswer(string answer)
        {
            answer = SanitizeAnswer(answer);
            if (_optionalAnswers.Count > 0)
            {
                var optLev = new Levenshtein(answer);
                if (_optionalAnswers.Any(optionalAnswer => optLev.DistanceFrom(optionalAnswer) <= Math.Round(optionalAnswer.Length * 0.1)))
                {
                    return true;
                }
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