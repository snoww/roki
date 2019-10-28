namespace Roki.Core.Services
{
    public class Properties
    {
        public string Prefix { get; set; } = ".";
        public ulong BotId { get; set; } = 549644503351296040;
        
        public float CurrencyGenerationChance { get; set; } = 0.02f;
        public int CurrencyGenerationCooldown { get; set; } = 10;
        public string CurrencyIcon { get; set; } = "<:stone:269130892100763649>";
        public string CurrencyName { get; set; } = "Stone";
        public string CurrencyNamePlural { get; set; } = "Stones";
        public int CurrencyDropAmount { get; set; } = 1;
        public int? CurrencyDropAmountMax { get; set; } = 5;
        public int? CurrencyDropAmountRare { get; set; } = 100;

        public float Lottery4 { get; set; } = 0.055f;
        public float Lottery5 { get; set; } = 0.045f;
        public float LotteryJackpot { get; set; } = 0.9f;
        public int LotteryDraw { get; set; } = 7;
        

        public int XpPerMessage { get; set; } = 5;
        public int XpCooldown { get; set; } = 5;
    }
}