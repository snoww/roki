using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using LinqToTwitter;
using Roki.Common.Attributes;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class TwitterCommands : RokiSubmodule
        {
            private readonly TwitterContext _twitterCtx;
            
            public TwitterCommands(IConfiguration config)
            {
                _twitterCtx = new TwitterContext(new ApplicationOnlyAuthorizer
                {
                    CredentialStore = new InMemoryCredentialStore
                    {
                        ConsumerKey = config.TwitterConsumer,
                        ConsumerSecret = config.TwitterConsumerSecret
                    }
                });
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Twitter([Leftover] string query)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("Please provide a Twitter user.").ConfigureAwait(false);
                    return;
                }
                
                const int maxTweetsToReturn = 5;

                var tweets = await
                    (from tweet in _twitterCtx.Status
                        where tweet.Type == StatusType.User &&
                              tweet.ScreenName == query &&
                              tweet.Count == maxTweetsToReturn &&
                              tweet.TweetMode == TweetMode.Extended
                        select tweet)
                    .ToListAsync().ConfigureAwait(false);

                if (tweets == null)
                {
                    await ctx.Channel.SendErrorAsync("No tweets found.");
                    return;
                }

                var latest = tweets[0];
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{latest.User.Name} (@{latest.User.ScreenName})", latest.User.ProfileImageUrl)
                    .WithDescription(latest.Text)
                    .AddField("Likes", latest.FavoriteCount ?? 0, true)
                    .AddField("Retweets", latest.RetweetCount, true);
                
                await ctx.Channel.EmbedAsync(embed);
            }
        }
    }
    
}