using System;

namespace Roki.Modules.Xp.Common
{
    public struct XpLevel
    {
        public int Level { get; }
        public int ProgressXp { get; }
        public int RequiredXp { get; }
        public int TotalXp { get; }

        public XpLevel(int xp)
        {
            TotalXp = xp;
            const double factor = 2.5;

            Level = (int) Math.Floor(Math.Sqrt(xp) / factor);
            ProgressXp = (int) Math.Pow(Level * factor, 2);
            RequiredXp = (int) Math.Pow((Level + 1) * factor, 2);
        }
    }
}