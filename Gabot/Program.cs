using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Rest;
using Gabot.Modules;
using DM;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.IO;

namespace Gabot
{
    class Program
    {
        static void Main(string[] args) => new Program().RunBot().GetAwaiter().GetResult();

        public static DiscordSocketClient client;
        private CommandService commands;
        private IServiceProvider services;

        public static List<Poll> currentPolls = new List<Poll>();
        public static string parentDir = Environment.CurrentDirectory.ToString();
        public static string[] config = System.IO.File.ReadAllLines(parentDir + "/config.ini");
        public static string botToken = config[0].Substring(config[0].LastIndexOf('=') + 1);
        public static ulong parentGuildId = ulong.Parse(config[1].Substring(config[1].LastIndexOf('=') + 1));
        public static ulong guildChannelId = ulong.Parse(config[2].Substring(config[2].LastIndexOf('=') + 1));
        public static int moviesPerUser = int.Parse(config[3].Substring(config[3].LastIndexOf('=') + 1));
        public static int votesPerUser = int.Parse(config[4].Substring(config[4].LastIndexOf('=') + 1));
        public static string modPassword = config[5].Substring(config[5].LastIndexOf('=') + 1);

        public async Task RunBot()
        {
            client = new DiscordSocketClient();
            commands = new CommandService();
            services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .BuildServiceProvider();

            currentPolls = LoadPolls();
   
            client.Log += Log;
            await RegisterCommand();
            await RegisterReaction();
            await RegisterRemovedReaction();
            await client.LoginAsync(TokenType.Bot, botToken);
            await client.StartAsync();

            foreach (Poll poll in currentPolls) { await Polls.PollCountdown(poll); }

            await Task.Delay(-1);
        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        static async void OnProcessExit(DiscordSocketClient client)
        {
            await client.LogoutAsync();
            Console.WriteLine("I'm out of here");
            Console.ReadLine();
        }

        public async Task RegisterCommand()
        {
            client.MessageReceived += HandleCommandAsync;
            
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task RegisterReaction()
        {
            client.ReactionAdded += HandleReactions;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task RegisterRemovedReaction()
        {
            client.ReactionRemoved += HandleRemovedReaction;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        private async Task HandleRemovedReaction(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            Console.WriteLine(arg2.Name.ToString() + " " + arg3.Emote.Name + " " + arg3.Emote.ToString());
            foreach (Poll poll in Program.currentPolls)
            {
                //If message that got reacted is a message with a poll
                if (poll.messageIDs.Contains(arg3.MessageId) && poll.blockVotes == false)
                {
                    int emoteIndex = 0;
                    //If the emote name can be parsed as an int
                    if (int.TryParse(arg3.Emote.Name.ToString().Substring(0, 1), out emoteIndex))
                    {
                        //If the user didn't add the movie
                        if (!(poll.movieList[emoteIndex-1].addedByUser == arg3.User.ToString()))
                        { 
                            //If the user has voted for that movie
                            if (poll.movieList[emoteIndex - 1].votes.Contains(arg3.User.ToString()))
                            {
                                poll.movieList[emoteIndex - 1].votes.Remove(arg3.User.ToString());
                                Program.SavePolls(Program.currentPolls);
                                Console.WriteLine($"Removing vote for movie *{poll.movieList[emoteIndex - 1].title}* from *{arg3.User.ToString()}*");
                                await UpdateLastMessages(poll);
                            }
                        }
                    }
                }
            }
        }

        private async Task HandleReactions(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            Console.WriteLine(arg2.Name.ToString() + " " + arg3.Emote.Name + " " + arg3.Emote.ToString());
            foreach (Poll poll in Program.currentPolls)
            {
                //If message that got reacted is a message with a poll
                if (poll.messageIDs.Contains(arg3.MessageId) && poll.blockVotes == false)
                {
                    int emoteIndex = 0;
                    //If the emote name can be parsed as an int
                    if (int.TryParse(arg3.Emote.Name.ToString().Substring(0, 1), out emoteIndex))
                    {
                        //If the voting user hasn't voted for that movie yet
                        if (!poll.movieList[emoteIndex - 1].votes.Contains(arg3.User.ToString()))
                        {
                            //If the user has less than 2 votes in the poll (1 for added movie, 1 for vote)
                            int timesVoted = 0;
                            foreach (Movie movie in poll.movieList)
                            {
                                if (movie.votes.Contains(arg3.User.ToString()) && !movie.addedByUser.Contains(arg3.User.ToString()))
                                { timesVoted++; }
                            }
                            if (timesVoted < 1)
                            {
                                poll.movieList[emoteIndex - 1].votes.Add(arg3.User.ToString());
                                Program.SavePolls(Program.currentPolls);
                                Console.WriteLine($"Adding vote for movie *{poll.movieList[emoteIndex - 1].title}* from *{arg3.User.ToString()}*");
                                await UpdateLastMessages(poll);
                            }
                        }
                        else
                        {
                            var user = client.GetUser(arg3.UserId);
                            await user.SendMessageAsync($"Sorry {user.Mention}, you've already voted in that poll or the poll has ended or voting is currently locked. <3");
                        }
                    }
                }
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            int argPos = 0;

            var message = arg as SocketUserMessage;

            if (message is null || message.Author.IsBot) return;

            if (message.HasStringPrefix("!", ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))
            {
                var context = new SocketCommandContext(client, message);
                var result = await commands.ExecuteAsync(context, argPos, services);

                if (!result.IsSuccess)
                {
                    Console.WriteLine(result.ErrorReason);
                }

                else if (message.Content.Contains("hello"))
                {
                    await message.Channel.SendMessageAsync("hello " + message.Author);
                }
            }
        }

        public static async Task GetLastMessage(Poll poll)
        {
            var message = await client.GetGuild(parentGuildId).GetTextChannel(guildChannelId).GetMessageAsync(poll.messageIDs.Last());
            poll.lastMessage = message as RestUserMessage;
        }


        public static Embed MakePollEmbed(Poll poll)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor(new EmbedAuthorBuilder());
            builder.WithFooter(new EmbedFooterBuilder());
            builder.Footer.Text = "Vote for each movie by reacting to this message with :one:, :two:, etc. " +
                "\r\n" + $"Use the command '!addoption {(Program.currentPolls.FindIndex(x => x.title == poll.title) + 1).ToString()} *Option name* to add a new option to the poll." +
                Environment.NewLine + $"Ends at {poll.endDate.ToUniversalTime().ToShortTimeString()} UTC on {poll.endDate.ToLongDateString()}";
            builder.Author.Name = $"{poll.title} - Poll number #" + (Program.currentPolls.FindIndex(x => x.title == poll.title)+1).ToString();
            builder.Author.IconUrl = client.GetUser(poll.createdByUserId).GetAvatarUrl(ImageFormat.Auto, 128);
            builder.Title = $"*created by {poll.movieList[0].addedByUser}* - ends in: " + poll.endDate.Subtract(DateTime.Now).Hours + " hours and " + poll.endDate.Subtract(DateTime.Now).Minutes + " minutes.";
            builder.ThumbnailUrl = client.GetGuild(poll.createdInGuildId).IconUrl;
            //builder.ThumbnailUrl = "https://res.cloudinary.com/teepublic/image/private/s--aGYkg285--/t_Preview/b_rgb:191919,c_limit,f_jpg,h_630,q_90,w_630/v1478768274/production/designs/806299_1.jpg";
            int movieNumber = 1;
            foreach (Movie movie in poll.movieList)
            {
                bool isInlined = false;
                string votes = new string('X', movie.votes.Count);
                //Tried inline for long polls but it looks too messy atm
                //if (poll.movieList.Count() > 5) { isInlined = true; }
                builder.AddField($"{movieNumber} - {movie.title} *({movie.addedByUser.Split('#')[0]})*", $"Votes: **|{votes}|**", isInlined);
                movieNumber++;
            }
            return builder.Build();
        }    

        public static async Task UpdateLastMessages(Poll poll)
        {
            //if (poll.lastMessage == null) We want to do this for every message instead of just the last one
            foreach (PostedMessage pm in poll.postedMessages)
            {
                poll.lastMessage = await pm.GetMessageObj(client);
                //await GetLastMessage(poll);
                await poll.lastMessage.ModifyAsync(msg => msg.Embed = MakePollEmbed(poll));
            }
            //await poll.lastMessage.ModifyAsync(msg => msg.Embed = MakePollEmbed(poll));

        }

        /// <summary>
        /// Saves current polls in memory to a bin file
        /// </summary>
        /// <param name="list"></param>
        public static void SavePolls(List<Poll> list)
        {
            string parentDir = Environment.CurrentDirectory.ToString();
            string serializationFile = Path.Combine(parentDir, "polls.bin");
            using (Stream stream = File.Open(serializationFile, FileMode.Create))
            {
                var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                bformatter.Serialize(stream, list);
            }
        }

        /// <summary>
        /// Loads polls saved to bin file into current polls
        /// </summary>
        /// <returns></returns>
        public static List<Poll> LoadPolls()
        {
            string parentDir = Environment.CurrentDirectory.ToString();
            string serializationFile = Path.Combine(parentDir, "polls.bin");
            if (File.Exists(serializationFile))
            {
                Console.Write(File.Exists(serializationFile));
                using (Stream stream = File.Open(serializationFile, FileMode.Open))
                {
                    var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    stream.Position = 0;
                    List<Poll> pollList = (List<Poll>)bformatter.Deserialize(stream);
                    foreach (Poll poll in pollList) { GetLastMessage(poll); }
                    return pollList;
                }
            }
            else return new List<Poll>();
        }

        public static void PopulatePostedMessages(Poll poll, SocketCommandContext context, RestUserMessage message)
        {
            poll.lastMessage = message;
            poll.messageIDs.Add(message.Id);
            PostedMessage pm = new PostedMessage { channelId = message.Channel.Id, guildId = context.Guild.Id, messageId = message.Id };
            poll.postedMessages.Add(pm);
        }
    }

    public class Polls : ModuleBase<SocketCommandContext>
    {
        [Command("makepoll"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task CreatePoll(float length, string title, [Remainder] string movie)
        {
            Movie newMovie = new Movie();
            newMovie.title = movie;
            newMovie.addedByUser = Context.User.ToString();
            newMovie.votes = new List<string>();
            newMovie.votes.Add(Context.User.ToString());
            Poll newPoll = MakeNewPoll(title, newMovie, length, Context);
            Program.currentPolls.Add(newPoll);
            Program.SavePolls(Program.currentPolls);
            await ShowPoll(newPoll, Context);
            PollCountdown(newPoll);
            Console.WriteLine($"{newPoll.title} created with movie {newPoll.movieList[0].title}, by {newPoll.createdByUsername} id {newPoll.createdByUserId}, in guild {newPoll.createdInGuildId} and channel {newPoll.createdInGuildId} for {length} hours");
        }

        [Command("removepoll"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task RemovePoll(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Program.currentPolls.RemoveAt(pollIndex - 1);
                await Context.Channel.SendMessageAsync($"Poll number {pollIndex} removed!");
                Program.SavePolls(Program.currentPolls);
            }
        }

        [Command("removeoption"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task RemoveMovie(string pollNumber, string movieNumber)
        {
            int pollIndex = 0;
            int movieIndex = 0;
            if ((int.TryParse(pollNumber, out pollIndex) && int.TryParse(movieNumber, out movieIndex)))
            {
                Program.currentPolls[pollIndex - 1].movieList.RemoveAt(movieIndex-1);
                Console.WriteLine($"Movie number {movieIndex} removed from poll {pollIndex}!");
                await Program.UpdateLastMessages(Program.currentPolls[pollIndex - 1]);
                Program.SavePolls(Program.currentPolls);
            }
        }

        [Command("lockoptions"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task blockOptionsCommand(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockOptions(poll, true);
            }
        }

        [Command("lockvotes"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task blockVotesCommand(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockVotes(poll, true);
            }
        }

        [Command("unlockoptions"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task unBlockOptionsCommand(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockOptions(poll, false);
            }
        }

        [Command("unlockvotes"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task unBlockVotesCommand(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockVotes(poll, false);
            }
        }

        [Command("showpoll")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowPoll([Remainder]string pollnumber)
        {
            if (int.TryParse(pollnumber, out int pollIndex))
            {
                await Program.GetLastMessage(Program.currentPolls[pollIndex - 1]);
                Poll poll = Program.currentPolls[pollIndex - 1];
                await ShowPoll(poll, Context);
            }
            else await Context.Channel.SendMessageAsync("Please add a number after the command.");
        }

        //[Command("showallpolls"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        ////public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        //public async Task ListPolls()
        //{
        //    if (int.TryParse(pollnumber, out int pollIndex))
        //    {
        //        await Program.GetLastMessage(Program.currentPolls[pollIndex - 1]);
        //        Poll poll = Program.currentPolls[pollIndex - 1];
        //        var message = await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
        //        Program.currentPolls[pollIndex - 1].messageIDs.Add(message.Id);
        //        Program.currentPolls[pollIndex - 1].lastMessage = message;
        //        Program.SavePolls(Program.currentPolls);
        //    }
        //    else await Context.Channel.SendMessageAsync("Please add a number after the command.");
        //}

        [Command("addoption")]
        public async Task AddMovieAsync(int pollIndex, [Remainder]string movieTitle)
        {
            if (Program.currentPolls.Count < 10 && Program.currentPolls[pollIndex-1].blockOptions == false)
            {
                pollIndex -= 1;
                Poll poll = Program.currentPolls[pollIndex];
                bool alreadyAddedMovie = false;
                foreach (Movie movie in Program.currentPolls[pollIndex].movieList)
                {
                    if (movie.addedByUser == Context.User.ToString()) { alreadyAddedMovie = true; break; }
                }
                if (!alreadyAddedMovie)
                {
                    Movie newMovie = new Movie
                    {
                        title = movieTitle,
                        addedByUser = Context.User.ToString(),
                        votes = new List<string>()
                    };
                    newMovie.votes.Add(Context.User.Id.ToString());
                    Program.currentPolls[pollIndex].movieList.Add(newMovie);
                    Program.SavePolls(Program.currentPolls);
                    await Program.UpdateLastMessages(poll);
                }
                else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, you've already added a movie in that poll. <3");
            }
            else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, there are already 10 options in the poll, please remove one first.");
        }


        [Command("forceaddoption")]
        public async Task ForceAddMovieAsync(int pollIndex, [Remainder]string movieTitle)
        {
            if (Program.currentPolls.Count < 10)
            { 
                pollIndex -= 1;
                Poll poll = Program.currentPolls[pollIndex];
                Movie newMovie = new Movie
                {
                    title = movieTitle,
                    addedByUser = Context.User.ToString(),
                    votes = new List<string>()
                };
                newMovie.votes.Add(Context.User.Id.ToString());
                Program.currentPolls[pollIndex].movieList.Add(newMovie);
                Program.SavePolls(Program.currentPolls);
                await Program.UpdateLastMessages(poll);
            }
            else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, there are already 10 options in the poll, please remove one first.");
        }

        public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        {
            var message = await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
            Program.PopulatePostedMessages(poll, Context, message);
            Program.SavePolls(Program.currentPolls);
        }

        public static async Task PollCountdown(Poll poll)
        {
            if (poll.endDate > DateTime.Now)
            {
                await Task.Delay(poll.endDate - poll.startDate);
                var destinationChannel = Program.client.GetGuild(poll.createdInGuildId).GetTextChannel(poll.createdInChannelId);
                poll.movieList.Sort((x, y) => y.votes.Count - x.votes.Count);
                await destinationChannel.SendMessageAsync($"Poll *{poll.title}* is over! The results are...");
                await destinationChannel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
                await BlockOptions(poll, true);
                await BlockVotes(poll, true);
            }
        }

        public static async Task BlockOptions(Poll poll, bool isBlocked)
        {
            poll.blockOptions = isBlocked;
        }

        public static async Task BlockVotes(Poll poll, bool isBlocked)
        {
            poll.blockVotes = isBlocked;
        }

        public Poll MakeNewPoll(string title, Movie movie, float days, SocketCommandContext Context)
        {
            Poll newPoll = new Poll();
            newPoll.createdByUsername = Context.User.Username;
            newPoll.createdByUserId = Context.User.Id;
            newPoll.createdInGuildId = Context.Guild.Id;
            newPoll.createdInChannelId = Context.Channel.Id;
            newPoll.title = title;
            newPoll.startDate = DateTime.Now;
            newPoll.endDate = newPoll.startDate.AddHours(days);
            newPoll.movieList.Add(movie);
            newPoll.messageIDs = new List<ulong>();
            return newPoll;
        }
    }
}
