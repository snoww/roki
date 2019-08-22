using Discord;
using Roki.Core.Services;

namespace Roki.Modules.Searches.Services
{
    public class PokemonService : INService
    {
        public Color GetColorOfPokemon(string color)
        {
            switch (color)
            {
                case "black":
                    return new Color(0x000000);
                case "blue":
                    return new Color(0x0000FF);
                case "brown":
                    return new Color(0xA52A2A);
                case "gray":
                    return new Color(0x808080);
                case "green":
                    return new Color(0x008000);
                case "pink":
                    return new Color(0xFFC0CB);
                case "purple":
                    return new Color(0x800080);
                case "red":
                    return new Color(0xFF0000);
                case "white":
                    return new Color(0xFFFFFF);
                case "yellow":
                    return new Color(0xFFFF00);
                default:
                    return new Color(0x000000);
            }
        }
        
    }
}