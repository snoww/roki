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
        IUserRepository Users { get; }
        IMessageRepository Messages { get; }
        ICurrencyTransactionRepository Transaction { get; }
        ILotteryRepository Lottery { get; }
        IListingRepository Listing { get; }
        ISubscriptionRepository Subscriptions { get; }
        ITradesRepository Trades { get; }
        IEventsRepository Events { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
    
    public sealed class UnitOfWork : IUnitOfWork
    {
        private IQuoteRepository _quotes;
        private IUserRepository _users;
        private IMessageRepository _messages;
        private ICurrencyTransactionRepository _transaction;
        private ILotteryRepository _lottery;
        private IListingRepository _listing;
        private ISubscriptionRepository _subscriptions;
        private ITradesRepository _trades;
        private IEventsRepository _events;
        public UnitOfWork(RokiContext context)
        {
            Context = context;
        }

        public RokiContext Context { get; }
        public IQuoteRepository Quotes => _quotes ??= new QuoteRepository(Context);
        public IUserRepository Users => _users ??= new UserRepository(Context);
        public IMessageRepository Messages => _messages ??= new MessageRepository(Context);
        public ICurrencyTransactionRepository Transaction => _transaction ??= new CurrencyTransactionRepository(Context);
        public ILotteryRepository Lottery => _lottery ??= new LotteryRepository(Context);
        public IListingRepository Listing => _listing ??= new ListingRepository(Context);
        public ISubscriptionRepository Subscriptions => _subscriptions ??= new SubscriptionRepository(Context);
        public ITradesRepository Trades => _trades ??= new TradesRepository(Context);
        public IEventsRepository Events => _events ??= new EventsRepository(Context);

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