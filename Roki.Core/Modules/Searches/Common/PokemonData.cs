using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Roki.Modules.Searches.Common
{
    public class PokemonModel
    {
        public Dictionary<string, PokemonData> Pokemon { get; set; }
        
        public class PokemonData
        {
            public int Api { get; set; }
            public string Sprite { get; set; }
            public string Name { get; set; }
        }
    }
}