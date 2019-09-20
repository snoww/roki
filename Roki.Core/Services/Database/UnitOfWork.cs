using System;
using System.Threading.Tasks;
using Roki.Core.Services.Database.Repositories;
using Roki.Core.Services.Database.Repositories.Impl;

namespace Roki.Core.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        RokiContext Context { get; }

        IQuoteRepository Quotes { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
    
    public sealed class UnitOfWork : IUnitOfWork
    {
        private IQuoteRepository _quotes;

        public UnitOfWork(RokiContext context)
        {
            Context = context;
        }

        public RokiContext Context { get; }
        public IQuoteRepository Quotes => _quotes ?? (_quotes = new QuoteRepository(Context));

        public int SaveChanges()
        {
            return Context.SaveChanges();
        }

        public Task<int> SaveChangesAsync()
        {
            return Context.SaveChangesAsync();
        }

        public void Dispose()
        {
            Context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}