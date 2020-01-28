using Microsoft.EntityFrameworkCore;
using Roki.Services.Database;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IRepository<T> where T : DbEntity
    {
        void Add(T obj);
        void AddRange(params T[] objects);
        
        void Remove(T obj);
        void RemoveRange(params T[] objects);
        
        void Update(T obj);
        void UpdateRange(params T[] objects);
    }
    
    public class Repository<T> : IRepository<T> where T : DbEntity
    {
        protected Repository(DbContext context)
        {
            Context = context;
            Set = context.Set<T>();
        }

        protected DbContext Context { get; }
        protected DbSet<T> Set { get; }

        public void Add(T obj)
        {
            Set.Add(obj);
        }

        public void AddRange(params T[] objects)
        {
            Set.AddRange(objects);
        }

        public void Remove(T obj)
        {
            Set.Remove(obj);
        }

        public void RemoveRange(params T[] objects)
        {
            Set.RemoveRange(objects);
        }

        public void Update(T obj)
        {
            Set.Update(obj);
        }

        public void UpdateRange(params T[] objects)
        {
            Set.UpdateRange(objects);
        }
    }
}