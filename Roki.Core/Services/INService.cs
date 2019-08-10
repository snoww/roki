using System.Threading.Tasks;

namespace Roki.Core.Services
{
    public interface INService
    {
        
    }
    public interface IUnloadableService
    {
        Task Unload();
    }

}