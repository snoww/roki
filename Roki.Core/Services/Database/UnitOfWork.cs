using System;
using System.Threading.Tasks;
using Roki.Core.Services.Database.Repositories;
using Roki.Core.Services.Database.Repositories.Impl;

namespace Roki.Core.Services.Database
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        public RokiContext Context { get; }

        private IQuoteRepository _quotes;
        public IQuoteRepository Quotes => _quotes ?? (_quotes = new QuoteRepository(Context));

        public UnitOfWork(RokiContext context)
        {
            Context = context;
        }

        public int SaveChanges() => Context.SaveChanges();

        public Task<int> SaveChangesAsync() => Context.SaveChangesAsync();

        public void Dispose()
        {
            Context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}