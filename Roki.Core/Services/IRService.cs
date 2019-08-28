using System.Threading.Tasks;

namespace Roki.Core.Services
{
    public interface IRService
    {
    }

    public interface IUnloadableService
    {
        Task Unload();
    }
}