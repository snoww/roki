using System.Threading.Tasks;
using Discord;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Modules.Xp.Common;

namespace Roki.Modules.Xp
{
    public partial class Xp : RokiTopLevelModule
    {
        private readonly DbService _db;


        public Xp(DbService db)
        {
            _db = db;
        }
        
//        [RokiCommand, Description, Usage, Aliases]
//        public async Task Experience([Leftover] IUser user = null)
//        {
//            user = user ?? ctx.User;
//            using (var uow = _db.GetDbContext())
//            {
//                var dUser = uow.DUsers.GetOrCreate(user);
//                var xp = new XpLevel(dUser.TotalXp);
//                await ctx.Channel.SendMessageAsync($"{user.Username}\nLevel: {xp.Level}\nTotal XP: {xp.TotalXp}\nXP to next Level: {xp.RequiredXp}");
//            }
//        } 
//        
        [RokiCommand, Description, Usage, Aliases]
        public async Task UserId([Leftover] int id)
        {
            using (var uow = _db.GetDbContext())
            {
                var message = uow.DUsers.GetUserById(id);
                await ctx.Channel.SendMessageAsync(message.Username);
            }
        }

//        [RokiCommand, Description, Usage, Aliases]
//        public async Task Message([Leftover] int id)
//        {
//            using (var uow = _db.GetDbContext())
//            {
//                var message = uow.DMessages.GetMessageById(id);
//                await ctx.Channel.SendMessageAsync(message.Content);
//            }
//        }
    }
}