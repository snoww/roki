using System.Collections.Immutable;
using Discord;

namespace Roki.Core.Services
{
    public interface IConfiguration
    {
        ulong ClientId { get; }
        string Token { get; }
        string GoogleApi { get; }

        ImmutableArray<ulong> OwnerIds { get; }

        DbConfig Db { get; }

        bool IsOwner(IUser u);
    }

//    public class RestartConfig
//    {
//        public RestartConfig(string cmd, string args)
//        {
//            this.Cmd = cmd;
//            this.Args = args;
//        }
//        
//        public string Cmd { get; }
//        public string Args { get; }
//    }

    public class DbConfig
    {
        public DbConfig(string type, string connectionString)
        {
            Type = type;
            ConnectionString = connectionString;
        }

        public string Type { get; }
        public string ConnectionString { get; }
    }
}