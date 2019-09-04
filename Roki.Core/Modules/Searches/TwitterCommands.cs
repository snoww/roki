using System.Collections.Generic;
using System.Diagnostics;
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
                _twitterCtx = new TwitterContext(new SingleUserAuthorizer()
                {
                    CredentialStore = new SingleUserInMemoryCredentialStore()
                    {
                        ConsumerKey = config.TwitterConsumer,
                        ConsumerSecret = config.TwitterConsumerSecret,
                        AccessToken = config.TwitterAccessToken,
                        AccessTokenSecret = config.TwitterAccessSecret
                    }
                });
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task Twitter([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("Please provide a Twitter user.").ConfigureAwait(false);
                    return;
                }
                
                const int maxTweetsToReturn = 5;

                var tweets = await
                    (from search in _twitterCtx.Status
                        where search.Type == StatusType.User &&
                              search.ScreenName == query &&
                              search.Count == maxTweetsToReturn &&
                              search.TweetMode == TweetMode.Extended
                        select search)
                    .ToListAsync().ConfigureAwait(false);

                if (tweets == null)
                {
                    await ctx.Channel.SendErrorAsync("No tweets found.");
                    return;
                }

                var tweet = tweets[0];
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                    .WithDescription(tweet.Text ?? tweet.FullText)
                    .AddField("Retweets", tweet.RetweetCount, true)
                    .AddField("Likes", tweet.FavoriteCount ?? 0, true);
                
                await ctx.Channel.EmbedAsync(embed);
            }
            [RokiCommand, Usage, Description, Aliases]
            public async Task TwitterSearch([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await ctx.Channel.SendErrorAsync("Please provide a search query.").ConfigureAwait(false);
                    return;
                }
                
                const int maxTweetsToReturn = 5;
                const int maxTotalResults = 10;
                ulong sinceID = 1;
                ulong maxId;

                var combinedSearch = new List<Status>();
                
                var searchResponse = await
                    (from search in _twitterCtx.Search
                        where search.Type == SearchType.Search &&
                              search.Query == query &&
                              search.Count == maxTweetsToReturn &&
                              search.SinceID == sinceID &&
                              search.TweetMode == TweetMode.Extended
                        select search.Statuses)
                    .SingleOrDefaultAsync().ConfigureAwait(false);

                if (searchResponse != null)
                {
                    combinedSearch.AddRange(searchResponse);
                    var prevMaxId = ulong.MaxValue;
                    do
                    {
                        maxId = searchResponse.Min(status => status.StatusID) - 1;
                        Debug.Assert(maxId < prevMaxId);
                        prevMaxId = maxId;

                        searchResponse = await
                            (from search in _twitterCtx.Search
                                where search.Type == SearchType.Search &&
                                      search.Query == query &&
                                      search.Count == maxTweetsToReturn &&
                                      search.MaxID == maxId &&
                                      search.SinceID == sinceID &&
                                      search.TweetMode == TweetMode.Extended
                                select search.Statuses)
                            .SingleOrDefaultAsync().ConfigureAwait(false);

                    } while (searchResponse.Any() && combinedSearch.Count < maxTotalResults);
                    
                    await ctx.SendPaginatedConfirmAsync(0, p =>
                    {
                        var tweet = combinedSearch[p];
                        return new EmbedBuilder().WithOkColor()
                            .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                            .WithDescription(tweet.Text ?? tweet.FullText)
                            .AddField("Retweets", tweet.RetweetCount, true)
                            .AddField("Likes", tweet.FavoriteCount ?? 0, true);
                    }, combinedSearch.Count, 1).ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel.SendErrorAsync("No results found.");
                }
            }
        }
    }
    
}