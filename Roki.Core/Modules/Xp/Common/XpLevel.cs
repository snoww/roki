namespace Roki.Modules.Xp.Common
{
    public class XpLevel
    {
        public int Level { get; }
        public int LevelXp { get; }
        public int RequiredXp { get; }
        public int TotalXp { get; }

        public XpLevel(int xp)
        {
            if (xp < 0)
                xp = 0;

            TotalXp = xp;

            const int baseXp = 36;
            
            var required = baseXp;
            var totalXp = 0;
            var lvl = 1;
            while (true)
            {
                required = (int) (baseXp + baseXp / 4.0 * (lvl - 1));
                
                if (required + totalXp > xp)
                    break;

                totalXp += required;
                lvl++;
            }

            Level = lvl - 1;
            LevelXp = xp - totalXp;
            RequiredXp = required;
        }
    }
}