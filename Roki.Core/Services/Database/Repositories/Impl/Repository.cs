using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories.Impl
{
    public class Repository<T> : IRepository<T> where T : DbEntity
    {
        public Repository(DbContext context)
        {
            Context = context;
            Set = context.Set<T>();
        }

        protected DbContext Context { get; set; }
        protected DbSet<T> Set { get; set; }

        public void Add(T obj)
        {
            Set.Add(obj);
        }

        public void AddRange(params T[] objs)
        {
            Set.AddRange(objs);
        }

        public T GetById(int id)
        {
            return Set.FirstOrDefault(e => e.Id == id);
        }

        public IEnumerable<T> GetAll()
        {
            return Set.ToList();
        }

        public void Remove(int id)
        {
            Set.Remove(GetById(id));
        }

        public void Remove(T obj)
        {
            Set.Remove(obj);
        }

        public void RemoveRange(params T[] objs)
        {
            Set.RemoveRange(objs);
        }

        public void Update(T obj)
        {
            Set.Update(obj);
        }

        public void UpdateRange(params T[] objs)
        {
            Set.UpdateRange(objs);
        }
    }
}