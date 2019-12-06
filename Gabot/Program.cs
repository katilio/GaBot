using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Rest;
using Gabot.Modules;
using DM;
//using Google.Apis.YouTube.v3;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.IO;
using System.Net.Http;
using Com.CloudRail.SI;
using Com.CloudRail.SI.Services;
using Com.CloudRail.SI.ServiceCode.Commands.CodeRedirect;
using System.Threading;

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
            CloudRail.AppKey = "5b45f83e484e1768971200f5";
            
            //await Task.Run(() => YTservice.Login());
            await RegisterCommand();
            await RegisterReaction();
            await RegisterRemovedReaction();
            await client.LoginAsync(TokenType.Bot, botToken);
            await client.StartAsync();

            foreach (Poll poll in currentPolls)
            {
                var tokenSource = new CancellationTokenSource();
                poll.countdownToken = tokenSource;
                //tokenSource.Cancel();
                await Polls.PollCountdown(poll, poll.countdownToken.Token);
            }
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
                //If message that got reacted is a message with a poll and votes are allowed
                if (poll.messageIDs.Contains(arg3.MessageId) && poll.blockVotes == false)
                {
                    int emoteIndex = 0;
                    if (arg3.Emote.Name == "🔟") { emoteIndex = 10; }
                    //If the emote name can be parsed as an int or it's 10, which can't be parsed
                    if (emoteIndex == 10 || int.TryParse(arg3.Emote.Name.ToString().Substring(0, 1), out emoteIndex))
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
                                await UpdateLastMessages(poll, false);
                            }
                        }
                    }
                }
            }
        }

        private async Task HandleReactions(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            Console.WriteLine(arg3.User.ToString() + " " + arg3.Emote.Name + " " + arg3.Emote.ToString());
            Poll poll = Program.currentPolls.Find(p => p.messageIDs.Contains(arg1.Id));
            //foreach (Poll poll in Program.currentPolls)
            //{
                //If message that got reacted is a message with a poll and votes are allowed
                if (poll.messageIDs.Contains(arg3.MessageId) && poll.blockVotes == false && arg3.UserId != client.CurrentUser.Id)
                {
                    int emoteIndex = 0;
                    if (arg3.Emote.Name == "🔟") { emoteIndex = 10; }
                    //If the emote name can be parsed as an int or it's 10, which can't be parsed
                    if (emoteIndex == 10 || int.TryParse(arg3.Emote.Name.ToString().Substring(0, 1), out emoteIndex))
                    {
                        //If the voting user hasn't voted for that movie yet
                        if (!poll.movieList[emoteIndex - 1].votes.Contains(arg3.User.ToString()))
                        {
                            //If the user has less than 2 votes in the poll (1 for added movie, 1 for vote)
                            int timesVoted = 0;
                            foreach (Movie movie in poll.movieList)
                            {
                                if (movie.votes.Contains(arg3.User.ToString()) && !(movie.addedByUser == arg3.User.ToString()))
                                { timesVoted++; }
                            }
                            if (timesVoted < Program.votesPerUser)
                            {
                                poll.movieList[emoteIndex - 1].votes.Add(arg3.User.ToString());
                                Program.SavePolls(Program.currentPolls);
                                Console.WriteLine($"Adding vote for movie *{poll.movieList[emoteIndex - 1].title}* from *{arg3.User.ToString()}*");
                                await UpdateLastMessages(poll, false);
                            }
                            else
                            {
                                var user = client.GetUser(arg3.UserId);
                                await user.SendMessageAsync($"Sorry {user.Mention}, you've already voted in that poll. <3");
                            }
                        }
                        else
                        {
                            var user = client.GetUser(arg3.UserId);
                            await user.SendMessageAsync($"Sorry {user.Mention}, you've already voted in that poll, for that movie, or voting is disabled. <3");
                        }
                    }
                }
                else if (poll.blockVotes == false)
                {
                    var user = client.GetUser(arg3.UserId);
                    await user.SendMessageAsync($"Sorry {user.Mention}, voting is currently locked for this poll. <3");
                }
            //}
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
                    //await context.Channel.SendMessageAsync("Command incorrect. Use *!help* or *!modhelp password* here or via DM for more info.");
                }

                else if (message.Content.Contains("hello"))
                {
                    await message.Channel.SendMessageAsync("hello " + message.Author);
                }
            }
        }

        //public static async Task GetLastMessage(Poll poll)
        //{
        //    var message = await client.GetGuild(parentGuildId).GetTextChannel(guildChannelId).GetMessageAsync(poll.messageIDs.Last());
        //    poll.lastMessage = message as RestUserMessage;
        //}


        public static Embed MakePollEmbed(Poll poll)
        {
            string[] pollEmotes = new string[] { "1⃣", "2⃣", "3⃣", "4⃣", "5⃣", "6⃣", "7⃣", "8⃣", "9⃣", "🔟" };
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor(new EmbedAuthorBuilder());
            builder.Color = Color.DarkRed;
            builder.WithFooter(new EmbedFooterBuilder());
            builder.Footer.Text = "Vote for any of the options by reacting to this message with :one:, :two:, etc. " +
                "\r\n" + $"Use the command '!addoption {(Program.currentPolls.FindIndex(x => x.title == poll.title) + 1).ToString()} *Option name* to add a new option to the poll." +
                Environment.NewLine + $"Ends at {poll.endDate.ToUniversalTime().ToShortTimeString()} UTC on {poll.endDate.ToLongDateString()}";
            builder.Author.Name = $"{poll.title} - Poll number #" + (Program.currentPolls.FindIndex(x => x.title == poll.title)+1).ToString();
            builder.Author.IconUrl = client.GetUser(poll.createdByUserId).GetAvatarUrl(ImageFormat.Auto, 128);
            builder.Title = $"*created by {poll.movieList[0].addedByUser}* - {(poll.endDate < DateTime.Now ? "Poll ended " + -(int)Math.Ceiling((poll.endDate - DateTime.Now).TotalHours) + " hours and " + -(poll.endDate - DateTime.Now).Minutes + " minutes ago.*" : "Poll is active, " + (int)Math.Floor((poll.endDate - DateTime.Now).TotalHours) + " hours " + (poll.endDate - DateTime.Now).Minutes + " minutes left.*")}";
            builder.ThumbnailUrl = client.GetGuild(poll.createdInGuildId).IconUrl;
            int movieNumber = 1;
            foreach (Movie movie in poll.movieList)
            {
                bool isInlined = false;
                string votes = new string('X', movie.votes.Count);
                //Tried inline for long polls but it looks too messy atm
                //if (poll.movieList.Count() > 5) { isInlined = true; }
                builder.AddField($"{pollEmotes[movieNumber-1]} - `{movie.title}` - *[{movie.addedByUser.Split('#')[0]}]*", $"Votes: **|{votes}|**", isInlined);
                //Looks ugly to add a field in between
                //builder.AddField("\u200B", "\u200B");
                movieNumber++;
            }
            return builder.Build();
        }    

        public static async Task UpdateLastMessages(Poll poll, bool addReactions)
        {
            //if (poll.lastMessage == null) We want to do this for every message instead of just the last one
            foreach (PostedMessage pm in poll.postedMessages)
            {
                poll.lastMessage = await pm.GetMessageObj(client);
                //await GetLastMessage(poll);
                if (!(poll.lastMessage == null))
                { 
                    await poll.lastMessage.ModifyAsync(msg => msg.Embed = MakePollEmbed(poll));
                    string[] pollEmotes = new string[] { "1⃣", "2⃣", "3⃣", "4⃣", "5⃣", "6⃣", "7⃣", "8⃣", "9⃣", "🔟" };
                    int reactionIndex = 1;
                    if (addReactions)
                    {
                        // Nice to have: optimize so we don't try to add reactions that already exist
                        foreach (Movie movie in poll.movieList)
                        {
                            var msgReactions = poll.lastMessage.Reactions;
                            IEmote emote = new Emoji(pollEmotes[reactionIndex - 1]);
                            if (!msgReactions.ContainsKey(emote))
                            { 
                                await poll.lastMessage.AddReactionAsync(emote);
                            }
                            reactionIndex++;

                        }
                    }
                }
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
                    //foreach (Poll poll in pollList) { GetLastMessage(poll); }
                    return pollList;
                }
            }
            else return new List<Poll>();
        }

        public static void PopulatePostedMessages(Poll poll, SocketCommandContext context, RestUserMessage message)
        {
            //Only get messages that are not DMs for now. Perhaps add support for DMs later on.
            if (!context.IsPrivate)
            { 
                poll.lastMessage = message;
                poll.messageIDs.Add(message.Id);
                PostedMessage pm = new PostedMessage { channelId = message.Channel.Id, guildId = context.Guild.Id, messageId = message.Id };
                poll.postedMessages.Add(pm);
            }
        }
    }

    public class Polls : ModuleBase<SocketCommandContext>
    {
        //[Command("subs")]
        //public async Task Subs()
        //{
        //    long followers = 0;
        //    long lastFollowers = 0;
        //    Com.CloudRail.SI.Types.ChannelMetaData result = Program.YTservice.GetChannel("UCYLHs-NR2le96KKqXofUOXg");
        //    followers = result.GetFollowers();
        //    Console.WriteLine(followers.ToString());
        //    //IEmote gabberAh = new Emoji("<:gabberBliss:446167531183538206>");
        //    IEmote y = new Emoji("🇾");
        //    IEmote a = new Emoji("🅰");
        //    IEmote s = new Emoji("🇸");
        //    IEmote fire = new Emoji("🔥");
        //    IEmote kat = new Emoji("<:katUDied:436968277067956224>");
        //    await Context.Channel.SendMessageAsync($"Gab now has **{fire} {result.GetFollowers()} {fire} subscribers**! Only {100000 - result.GetFollowers()} left to 100k!! " + y + a + s + kat);
        //    lastFollowers = result.GetFollowers();
        //    bool under100k = true;
        //    Task.Run(async () =>
        //    {
        //    while (under100k == true)
        //    {
        //        if (followers < 100000)
        //        {
        //            await Task.Delay(3000);
        //            Com.CloudRail.SI.Types.ChannelMetaData result2 = Program.YTservice.GetChannel("UCYLHs-NR2le96KKqXofUOXg");
        //            followers = result2.GetFollowers();
        //            if (followers > lastFollowers)
        //            {
        //                await Context.Channel.SendMessageAsync($"Gab now has **{fire} {result2.GetFollowers()} {fire} subscribers**! Only {100000 - result2.GetFollowers()} left to 100k!! " + y + a + s + kat);
        //                lastFollowers = result2.GetFollowers();
        //            }
        //        }
        //        else under100k = false;
        //    }
        //    });
        //}

        [Command("help")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowHelp()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Command help";
            builder.ThumbnailUrl = Program.client.CurrentUser.GetAvatarUrl(ImageFormat.Png, 128);
            builder.AddField("`!showpoll poll_number` - (All users, can be done through DM)", "Example: '***!showpoll 1***' - Displays indicated poll in channel. Can be used in DMs, but please note that votes through DMs WON'T be counted, and the message won't be updated when the poll changes.", false);
            builder.AddField("\u200B", "\u200B");
            builder.AddField("`!listpolls` - (All users, can be done through DM)", "Shows a list of all polls in the bot, including expired ones.", false);
            builder.AddField("\u200B", "\u200B");
            builder.AddField("`!addoption poll_number Movie title` - (All users, can be done through DM)", "Example: '***!addoption 1 LOTR: The Two Towers***' Adds a new option to the indicated poll, if less than 9 options exist. Can be used in DMs.", false);
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine($"User {Context.User.Username} requested help at {DateTime.Now}.");
        }

        [Command("modhelp")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowModHelp(string password)
        {
            if (password == Program.modPassword)
            {
                EmbedBuilder builder = new EmbedBuilder();
                builder.Title = "Command help";
                builder.ThumbnailUrl = Program.client.CurrentUser.GetAvatarUrl(ImageFormat.Png, 128);
                builder.AddField("`!makepoll *length in hours* 'Poll title' Starting movie title` - (Only users that can mention @everyone, cannot be done through DMs)", "Example: ***!makepoll 48 'Friday 13th Horror Poll!' Jason X*** - Creates a poll that lasts for 48 hours and includes the mentioned movie. Results will be posted after the specified time in the channel where the poll was created. Please note the poll won't disappear until you use !removepoll.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!forceaddoption *poll number* 'password' Movie title` - (Mod password required, please do via DM)", "Example: ***!forceaddoption 2 modpassword The Lion King 3*** - Overrides the 'max 1 movie per user' rule and adds a new option to the poll as long as there are less than 9 already.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!removepoll *poll number* 'password'` - (Mod password required, please do via DM)", "Example: '***!removepoll 1 modpassword***' Deletes indicated poll.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!removeoption *poll number* *movie number* 'password'` - (Mod password required, please do via DM)", "Example: ***'!removeoption 1 3 modpassword'*** - Deletes movie number 3 in poll number 1.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!lock/unlockvotes *poll number* 'password'` - (Mod password required, please do via DM)", "Example: '***!lockvotes 2 modpassword***' Locks voting in poll 2. No new votes accepted, can't remove votes either.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!lock/unlockoptions *poll number* 'password'` - (Mod password required, please do via DM)", "Example: '***!unlockoptions 3 modpassword***' Unlocks options in poll 3. No movies can be added or removed.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!edittitle *poll number* 'password' 'New title'` - (Mod password required, please do via DM)", "Example: '***!edittitle 1 modpassword New poll title***' Edits the title of poll 1.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!edittime *poll number* 'password' 'amount of time'` - (Mod password required, please do via DM)", "Example: '***!edittime 3 modpassword 2***' Extends the duration of poll 3 by 2 hours. Use a negative number to remove time, and decimals if you need values smaller than an hour. '-1.5' will make the poll end 90 minutes earlier. Once expired, polls can be extended but votes and options need to be unlocked manually.", false);
                builder.AddField("\u200B", "\u200B");
                builder.AddField("`!editoption *poll number* *option number* 'password' 'New title'` - (Mod password required, please do via DM)", "Example: '***!editoption 3 5 modpassword Titanic***' Changes the title of option 5 in poll 3 to Titanic.", false);
                await Context.Channel.SendMessageAsync("", false, builder.Build());
                Console.WriteLine($"User {Context.User.Username} requested mod help at {DateTime.Now}.");
            }
            else
            {
                await Context.User.SendMessageAsync("Incorrect password. Please write the command again as *!modhelp Password*.");
                Console.WriteLine($"User {Context.User.Username} failed to request mod help at {DateTime.Now}.");
            }
        }

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
            PollCountdown(newPoll, newPoll.countdownToken.Token);
            Console.WriteLine($"{newPoll.title} created with movie {newPoll.movieList[0].title}, by {newPoll.createdByUsername} id {newPoll.createdByUserId}, in guild {newPoll.createdInGuildId} and channel {newPoll.createdInGuildId} for {length} hours");
        }

        [Command("edittitle")]
        public async Task EditPoll(int pollNumber, string password, [Remainder] string NewTitle)
        {
            if (password == Program.modPassword)
            {
                Program.currentPolls[pollNumber - 1].title = NewTitle;
                Program.SavePolls(Program.currentPolls);
                await Program.UpdateLastMessages(Program.currentPolls[pollNumber - 1], false);
                Console.WriteLine($"User {Context.User.Username} edited title of poll {pollNumber-1} at {DateTime.Now}.");
            }
        }

        [Command("edittime")]
        public async Task EditTime(int pollNumber, string password, [Remainder] float TimeDiff)
        {
            if (password == Program.modPassword)
            {
                Poll poll = Program.currentPolls[pollNumber - 1];
                poll.endDate = poll.endDate.AddHours(TimeDiff);
                Program.SavePolls(Program.currentPolls);
                poll.countdownToken.Cancel();
                var newToken = new CancellationTokenSource();
                poll.countdownToken = newToken;
                PollCountdown(Program.currentPolls[pollNumber - 1], newToken.Token);
                await Program.UpdateLastMessages(Program.currentPolls[pollNumber - 1], false);
                Console.WriteLine($"User {Context.User.Username} edited the time of poll {pollNumber-1} for {TimeDiff} hours at {DateTime.Now}.");
            }
        }

        [Command("removepoll")]
        public async Task RemovePoll(string pollNumber,[Remainder] string password)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex) && password == Program.modPassword)
            {
                Program.currentPolls.RemoveAt(pollIndex - 1);
                await Context.Channel.SendMessageAsync($"Poll number {pollIndex} removed!");
                Program.SavePolls(Program.currentPolls);
            }
            Console.WriteLine($"User {Context.User.Username} removed poll number {pollIndex} at {DateTime.Now}.");
        }

        [Command("removeoption")]
        public async Task RemoveMovie(string pollNumber, string movieNumber, [Remainder] string password)
        {
            int pollIndex = 0;
            int movieIndex = 0;
            if ((int.TryParse(pollNumber, out pollIndex) && int.TryParse(movieNumber, out movieIndex)) && password == Program.modPassword)
            {
                Program.currentPolls[pollIndex - 1].movieList.RemoveAt(movieIndex-1);
                Console.WriteLine($"Movie number {movieIndex} removed from poll {pollIndex}!");
                await Program.UpdateLastMessages(Program.currentPolls[pollIndex - 1], true);
                Program.SavePolls(Program.currentPolls);
            }
            Console.WriteLine($"User {Context.User.Username} removed option {movieIndex} from poll {pollIndex} at {DateTime.Now}.");
        }

        [Command("lockoptions")]
        public async Task blockOptionsCommand(string pollNumber, [Remainder] string password)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex) && password == Program.modPassword)
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockOptions(poll, true);
            }
            Console.WriteLine($"User {Context.User.Username} locked options in poll {pollIndex} at {DateTime.Now}.");
        }

        [Command("lockvotes")]
        public async Task blockVotesCommand(string pollNumber, [Remainder] string password)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex) && password == Program.modPassword)
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockVotes(poll, true);
                Console.WriteLine($"User {Context.User.Username} locked votes in poll {pollIndex} at {DateTime.Now}.");
            }
        }

        [Command("unlockoptions")]
        public async Task unBlockOptionsCommand(string pollNumber, [Remainder] string password)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex) && password == Program.modPassword)
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockOptions(poll, false);
                Console.WriteLine($"User {Context.User.Username} unlocked options in poll {pollIndex} at {DateTime.Now}.");
            }
        }

        [Command("unlockvotes")]
        public async Task unBlockVotesCommand(string pollNumber, [Remainder] string password)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex) && password == Program.modPassword)
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await BlockVotes(poll, false);
                Console.WriteLine($"User {Context.User.Username} unlocked votes in poll {pollIndex} at {DateTime.Now}.");
            }
        }

        [Command("showpoll")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowPoll([Remainder]string pollnumber)
        {
            if (int.TryParse(pollnumber, out int pollIndex))
            {
                //await Program.GetLastMessage(Program.currentPolls[pollIndex - 1]);
                Poll poll = Program.currentPolls[pollIndex - 1];
                await ShowPoll(poll, Context);
                Console.WriteLine($"User {Context.User.Username} requested to show poll {pollIndex} at {DateTime.Now}. in {Context.Channel.Name}");
            }
            else await Context.User.SendMessageAsync("Please add a correct number after the command. For example, '!showpoll 2' if you want to see Poll number 2.");
        }

        [Command("listpolls")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ListAllPolls()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor(new EmbedAuthorBuilder());
            builder.WithFooter(new EmbedFooterBuilder());
            builder.Color = Color.Blue;
            builder.Footer.Text = "Use '!showpoll *poll number*' to display a particular poll!";
            builder.Author.Name = $"Current poll list!";
            builder.Author.IconUrl = Program.client.CurrentUser.GetAvatarUrl(ImageFormat.Png, 256);
            builder.Title = $"There are a total of {Program.currentPolls.Count} polls in the list.";
            if (!Context.IsPrivate) { builder.ThumbnailUrl = Context.Guild.IconUrl; }
            char[] separator = new char[] { '#' };
            if (Program.currentPolls.Count > 0)
            {
                int pollNumber = 1;
                foreach (Poll poll in Program.currentPolls)
                {
                    builder.AddField($"`{pollNumber} - " + poll.title + "` - created by " + poll.createdByUsername.Split(separator, 1)[0], $"*Voting: {(poll.blockVotes ? "closed": "open")}. New options: {(poll.blockOptions ? "closed" : "open")}. {(poll.endDate < DateTime.Now ? "Poll ended " + -(int)Math.Ceiling((poll.endDate - DateTime.Now).TotalHours) + " hours ago.*" : "Poll is active, " + (int)Math.Ceiling((poll.endDate - DateTime.Now).TotalHours) + " hours left.*")}");
                    //builder.AddField("\u200B", "\u200B");
                    pollNumber++;
                }
                await Context.Channel.SendMessageAsync("", false, builder.Build());
                Console.WriteLine($"User {Context.User.Username} requested to see poll list at {DateTime.Now}. in {Context.Channel.Name}");
            }
            else await Context.Channel.SendMessageAsync("There are currently no active polls. Use !makepoll *length in hours* 'Poll title' *Movie title* to create a new one.");
        }

        [Command("addoption")]
        public async Task AddMovieAsync(int pollIndex, [Remainder]string movieTitle)
        {
            pollIndex -= 1;
            if (Program.currentPolls[pollIndex].movieList.Count < 10 && Program.currentPolls[pollIndex-1].blockOptions == false)
            {
                Poll poll = Program.currentPolls[pollIndex];
                int moviesAdded = 0;
                foreach (Movie movie in Program.currentPolls[pollIndex].movieList)
                {
                    if (movie.addedByUser == Context.User.ToString()) { moviesAdded++; break; }
                }
                if (moviesAdded < Program.votesPerUser)
                {
                    Movie newMovie = new Movie
                    {
                        title = movieTitle,
                        addedByUser = Context.User.ToString(),
                        votes = new List<string>()
                    };
                    newMovie.votes.Add(Context.User.ToString());
                    Program.currentPolls[pollIndex].movieList.Add(newMovie);
                    Program.SavePolls(Program.currentPolls);
                    await Program.UpdateLastMessages(poll, true);
                    Console.WriteLine($"User {Context.User.Username} added option to poll {pollIndex} at {DateTime.Now}. in {Context.Channel.Name}");
                }
                else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, you've already added a movie in that poll. <3");
            }
            else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, the maximum number of options has been reached and we can't add any more.");
        }

        [Command("editoption")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task EditMovie(int pollNumber, int movieNumber, string password, [Remainder]string movieTitle)
        {
            if (password == Program.modPassword)
            {
                if (Program.currentPolls[pollNumber-1] != null && Program.currentPolls[pollNumber-1].movieList[movieNumber-1] != null)
                {
                    Poll poll = Program.currentPolls[pollNumber - 1];
                    int movieIndex = movieNumber - 1;
                    poll.movieList[movieIndex].title = movieTitle;
                    Program.SavePolls(Program.currentPolls);
                    await Program.UpdateLastMessages(poll, false);
                    Console.WriteLine($"User {Context.User.Username} edited option {movieNumber-1} in poll {pollNumber-1} at {DateTime.Now}. in {Context.Channel.Name}");
                }
                else await Context.Channel.SendMessageAsync("Password is incorrect.");
            }
        }

        [Command("forceaddoption")]
        public async Task ForceAddMovieAsync(int pollIndex, string password, [Remainder]string movieTitle)
        {
            if (Program.currentPolls.Count < 10 && password == Program.modPassword)
            { 
                pollIndex -= 1;
                Poll poll = Program.currentPolls[pollIndex];
                Movie newMovie = new Movie
                {
                    title = movieTitle,
                    addedByUser = Context.User.ToString(),
                    votes = new List<string>()
                };
                newMovie.votes.Add(Context.User.ToString());
                Program.currentPolls[pollIndex].movieList.Add(newMovie);
                Program.SavePolls(Program.currentPolls);
                await Program.UpdateLastMessages(poll, true);
                Console.WriteLine($"User {Context.User.Username} force added option to poll {pollIndex} at {DateTime.Now}. in {Context.Channel.Name}");
            }
            else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, there are already 10 options in the poll, please remove one first.");
        }



        public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        {
            var message = await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
            Program.PopulatePostedMessages(poll, Context, message);
            Program.SavePolls(Program.currentPolls);
            int reactionIndex = 1;
            string[] pollEmotes = new string[] { "1⃣", "2⃣", "3⃣", "4⃣", "5⃣", "6⃣", "7⃣", "8⃣", "9⃣", "🔟", "0️" };
            foreach (Movie movie in Program.currentPolls.Find(x => x.title == poll.title).movieList)
            {
                //IEmote emote = Context.Guild.Emotes.First(x => x.Name == pollEmotes[reactionIndex]);
                IEmote emote = new Emoji(pollEmotes[reactionIndex-1]);
                await message.AddReactionAsync(emote);
                reactionIndex++;
            }
        }

        public static async Task PollCountdown(Poll poll, CancellationToken token)
        {
            if (poll.endDate > DateTime.Now)
            {
                await Task.Delay(poll.endDate - DateTime.Now, token);
                await BlockOptions(poll, true);
                await BlockVotes(poll, true);
                var destinationChannel = Program.client.GetGuild(poll.createdInGuildId).GetTextChannel(poll.createdInChannelId);
                poll.movieList.Sort((x, y) => y.votes.Count - x.votes.Count);
                await destinationChannel.SendMessageAsync($"Poll *{poll.title}* is over! The results are...");
                await destinationChannel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
            }
        }

        public static async Task BlockOptions(Poll poll, bool isBlocked)
        {
            poll.blockOptions = isBlocked;
            Program.SavePolls(Program.currentPolls);
        }

        public static async Task BlockVotes(Poll poll, bool isBlocked)
        {
            poll.blockVotes = isBlocked;
            Program.SavePolls(Program.currentPolls);
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
            newPoll.countdownToken = new CancellationTokenSource();
            newPoll.movieList.Add(movie);
            newPoll.messageIDs = new List<ulong>();
            return newPoll;
        }
    }
}
