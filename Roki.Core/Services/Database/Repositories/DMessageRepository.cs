using System;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IDMessageRepository: IRepository<DMessage>
    {
    }

    public class DMessageRepository : Repository<DMessage>, IDMessageRepository
    {
        public DMessageRepository(DbContext context) : base(context)
        {
        }
    }
}