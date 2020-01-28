using Microsoft.EntityFrameworkCore;
using Roki.Services.Database.Data;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IPokedexRepository : IRepository<Pokemon>
    {
        
    }

    public interface IMoveRepository : IRepository<Move>
    {
        
    }

    public interface IAbilityRepository : IRepository<Ability>
    {
        
    }
    
    public class PokedexRepository : Repository<Pokemon>, IPokedexRepository
    {
        protected PokedexRepository(DbContext context) : base(context)
        {
        }
    }
    
    public class MoveRepository : Repository<Move>, IMoveRepository
    {
        protected MoveRepository(DbContext context) : base(context)
        {
        }
    }
    
    public class AbilityRepository : Repository<Ability>, IAbilityRepository
    {
        protected AbilityRepository(DbContext context) : base(context)
        {
        }
    }
}