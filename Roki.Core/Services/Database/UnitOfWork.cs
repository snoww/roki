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
        IListingRepository Listing { get; }
        ISubscriptionRepository Subscriptions { get; }

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
        private IListingRepository _listing;
        private ISubscriptionRepository _subscriptions;
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
        public IListingRepository Listing => _listing ?? (_listing = new ListingRepository(Context));
        public ISubscriptionRepository Subscriptions => _subscriptions ?? (_subscriptions = new SubscriptionRepository(Context));

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