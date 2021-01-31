using System;

namespace Roki.Modules.Xp.Common
{
    public struct XpLevel
    {
        public int Level { get; }
        public int ProgressXp { get; }
        public int RequiredXp { get; }
        public int TotalXp { get; }
        public int TotalRequiredXp { get; }

        public XpLevel(int xp)
        {
            TotalXp = xp;
            const double factor = 2.5;

            Level = (int) Math.Floor(Math.Sqrt(xp) / factor);
            var levelFloor = (int) Math.Pow(Level * factor, 2);
            ProgressXp = xp - levelFloor;
            RequiredXp = (int) Math.Pow((Level + 1) * factor, 2) - levelFloor;
            TotalRequiredXp = xp + RequiredXp;
        }

        public XpLevel AddXp(int xp)
        {
            return new(TotalXp + xp);
        }
    }
}