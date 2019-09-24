using System;
using System.Threading.Tasks;
using NLog;
using Roki.Core.Services.Database.Repositories;

namespace Roki.Core.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        RokiContext Context { get; }

        IQuoteRepository Quotes { get; }
        IDUserRepository DUsers { get; }
        IDMessageRepository DMessages { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
    
    public sealed class UnitOfWork : IUnitOfWork
    {
        private IQuoteRepository _quotes;
        private IDUserRepository _dUsers;
        private IDMessageRepository _dMessages;

        public UnitOfWork(RokiContext context)
        {
            Context = context;
        }

        public RokiContext Context { get; }
        public IQuoteRepository Quotes => _quotes ?? (_quotes = new QuoteRepository(Context));
        public IDUserRepository DUsers => _dUsers ?? (_dUsers = new DUserRepository(Context));
        public IDMessageRepository DMessages => _dMessages ?? (_dMessages = new DMessageRepository(Context));

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