using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using LinqToTwitter;
using LinqToTwitter.Common;
using LinqToTwitter.OAuth;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class TwitterCommands : RokiSubmodule
        {
            private readonly TwitterContext _twitterCtx;
            
            public TwitterCommands(IRokiConfig config)
            {
                _twitterCtx = new TwitterContext(new SingleUserAuthorizer
                {
                    CredentialStore = new SingleUserInMemoryCredentialStore
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
                    await Context.Channel.SendErrorAsync("Please provide a Twitter user.").ConfigureAwait(false);
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

                if (tweets.Count == 0)
                {
                    await Context.Channel.SendErrorAsync("No tweets found.");
                    return;
                }

                await Context.SendPaginatedMessageAsync(0, p =>
                {
                    var tweet = tweets[p];

                    if (tweet.Entities.MediaEntities.Count > 0)
                    {
                        return new EmbedBuilder().WithDynamicColor(Context)
                            .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                            .WithDescription(tweet.Text ?? tweet.FullText)
                            .AddField("Retweets", tweet.RetweetCount, true)
                            .AddField("Likes", tweet.FavoriteCount ?? 0, true)
                            .WithImageUrl(tweet.Entities.MediaEntities[0].MediaUrlHttps)
                            .WithFooter($"{tweet.CreatedAt:g}");
                    }
                    
                    return new EmbedBuilder().WithDynamicColor(Context)
                    .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                    .WithDescription(tweet.Text ?? tweet.FullText)
                    .AddField("Retweets", tweet.RetweetCount, true)
                    .AddField("Likes", tweet.FavoriteCount ?? 0, true)
                    .WithFooter($"{tweet.CreatedAt:g}");
                    
                }, tweets.Count, 1, false);
            }
            
            [RokiCommand, Usage, Description, Aliases]
            public async Task TwitterSearch([Leftover] string query = null)
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    await Context.Channel.SendErrorAsync("Please provide a search query.").ConfigureAwait(false);
                    return;
                }
                
                const int maxTweetsToReturn = 5;
                const int sinceId = 1;

                var combinedSearch = new List<Status>();
                
                var searchResponse = await
                    (from search in _twitterCtx.Search
                        where search.Type == SearchType.Search &&
                              search.Query == query &&
                              search.Count == maxTweetsToReturn &&
                              search.SinceID == sinceId &&
                              search.TweetMode == TweetMode.Extended
                        select search.Statuses)
                    .SingleOrDefaultAsync().ConfigureAwait(false);

                if (searchResponse == null)
                {
                    await Context.Channel.SendErrorAsync("No results found.");
                    return;
                }
                
                combinedSearch.AddRange(searchResponse);
                
                await Context.SendPaginatedMessageAsync(0, p =>
                {
                    var tweet = combinedSearch[p];

                    if (tweet.Entities.MediaEntities.Count > 0)
                    {
                        return new EmbedBuilder().WithDynamicColor(Context)
                            .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                            .WithDescription(tweet.Text ?? tweet.FullText)
                            .AddField("Retweets", tweet.RetweetCount, true)
                            .AddField("Likes", tweet.FavoriteCount ?? 0, true)
                            .WithImageUrl(tweet.Entities.MediaEntities[0].MediaUrlHttps)
                            .WithFooter($"{tweet.CreatedAt:g}");
                    }
                    
                    return new EmbedBuilder().WithDynamicColor(Context)
                        .WithAuthor($"{tweet.User.Name} (@{tweet.User.ScreenNameResponse})", tweet.User.ProfileImageUrl)
                        .WithDescription(tweet.Text ?? tweet.FullText)
                        .AddField("Retweets", tweet.RetweetCount, true)
                        .AddField("Likes", tweet.FavoriteCount ?? 0, true)
                        .WithFooter($"{tweet.CreatedAt:g}");
                    
                }, combinedSearch.Count, 1).ConfigureAwait(false);
            }
        }
    }
}