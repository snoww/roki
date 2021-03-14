using System;

namespace Roki.Modules.Xp.Common
{
    public struct XpLevel
    {
        public int Level { get; }
        public long ProgressXp { get; }
        public long RequiredXp { get; }
        public long TotalXp { get; }
        public long TotalRequiredXp { get; }

        public XpLevel(long xp)
        {
            TotalXp = xp;
            const double factor = 2.5;

            Level = (int) Math.Floor(Math.Sqrt(xp) / factor);
            var levelFloor = (int) Math.Pow(Level * factor, 2);
            ProgressXp = xp - levelFloor;
            RequiredXp = (long) Math.Pow((Level + 1) * factor, 2) - levelFloor;
            TotalRequiredXp = levelFloor + RequiredXp;
        }

        public XpLevel AddXp(long xp)
        {
            return new(TotalXp + xp);
        }
    }
}