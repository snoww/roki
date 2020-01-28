using System.Threading.Tasks;

namespace Roki
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            await new Roki().RunAndBlockAsync();
        }
    }
}