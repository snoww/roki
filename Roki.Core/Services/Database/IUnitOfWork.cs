using System;
using System.Threading.Tasks;
using Roki.Core.Services.Database.Repositories;

namespace Roki.Core.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        RokiContext Context { get; }
        
        IQuoteRepository Quotes { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}