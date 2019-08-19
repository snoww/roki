using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories.Impl
{
    public class Repository<T> : IRepository<T> where T : DbEntity
    {
        protected DbContext Context { get; set; }
        protected DbSet<T> Set { get; set; }

        public Repository(DbContext context)
        {
            Context = context;
            Set = context.Set<T>();
        }

        public void Add(T obj) =>
            Set.Add(obj);

        public void AddRange(params T[] objs) =>
            Set.AddRange(objs);

        public T GetById(int id) =>
            Set.FirstOrDefault(e => e.Id == id);

        public IEnumerable<T> GetAll() =>
            Set.ToList();

        public void Remove(int id) =>
            Set.Remove(this.GetById(id));

        public void Remove(T obj) =>
            Set.Remove(obj);

        public void RemoveRange(params T[] objs) =>
            Set.RemoveRange(objs);

        public void Update(T obj) =>
            Set.Update(obj);

        public void UpdateRange(params T[] objs) =>
            Set.UpdateRange(objs);
    }
}