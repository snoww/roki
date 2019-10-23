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
        ICurrencyTransactionRepository Transaction { get; }
        ILotteryRepository Lottery { get; }
        IStoreRepository Store { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
    
    public sealed class UnitOfWork : IUnitOfWork
    {
        private IQuoteRepository _quotes;
        private IDUserRepository _dUsers;
        private IDMessageRepository _dMessages;
        private ICurrencyTransactionRepository _transaction;
        private ILotteryRepository _lottery;
        private IStoreRepository _store;
        public UnitOfWork(RokiContext context)
        {
            Context = context;
        }

        public RokiContext Context { get; }
        public IQuoteRepository Quotes => _quotes ?? (_quotes = new QuoteRepository(Context));
        public IDUserRepository DUsers => _dUsers ?? (_dUsers = new DUserRepository(Context));
        public IDMessageRepository DMessages => _dMessages ?? (_dMessages = new DMessageRepository(Context));
        public ICurrencyTransactionRepository Transaction => _transaction ?? (_transaction = new CurrencyTransactionRepository(Context));
        public ILotteryRepository Lottery => _lottery ?? (_lottery = new LotteryRepository(Context));
        public IStoreRepository Store => _store ?? (_store = new StoreRepository(Context));

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