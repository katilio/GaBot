using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Gabot.Modules;

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

        private DiscordSocketClient client;
        private CommandService commands;
        private IServiceProvider services;

        public static List<Poll> currentPolls = new List<Poll>();

        public async Task RunBot()
        {
            client = new DiscordSocketClient();
            commands = new CommandService();
            services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .BuildServiceProvider();

            currentPolls = LoadPolls();

            string parentDir = Environment.CurrentDirectory.ToString();

            string[] config = System.IO.File.ReadAllLines(parentDir + "/config.ini");

            string botToken = config[0].Substring(config[0].LastIndexOf('=')+1);

            client.Log += Log;

            await RegisterCommand();

            await RegisterReaction();

            await client.LoginAsync(TokenType.Bot, botToken);

            await client.StartAsync();

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

        private async Task HandleReactions(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            Console.WriteLine(arg2.Name.ToString() + " " + arg3.Emote.Name);
            if (arg3.Emote.Name.Contains("1"))
            {
                await arg2.SendMessageAsync("Adding vote for movie");
            }
            else if (arg3.Emote.Name.Contains("2"))
            {
                await arg2.SendMessageAsync($"Adding vote for movie {Program.currentPolls[0].movieList[1]}");
                Program.currentPolls[0].movieList[1].votes.Add(arg3.User.ToString());
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


        public static Embed MakePollEmbed(Poll poll)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor(new EmbedAuthorBuilder());
            builder.WithFooter(new EmbedFooterBuilder());
            builder.Footer.Text = "Vote for each movie by reacting with :one:, :two:, etc.";
            builder.Author.Name = "Movie poll - #" + Program.currentPolls.FindIndex(x => x.title == poll.title).ToString();
            builder.Author.IconUrl = "https://res.cloudinary.com/teepublic/image/private/s--aGYkg285--/t_Preview/b_rgb:191919,c_limit,f_jpg,h_630,q_90,w_630/v1478768274/production/designs/806299_1.jpg";
            builder.Title = poll.title + " - ends in: " + poll.endDate.Subtract(DateTime.Now).Hours + " hours.";
            builder.ThumbnailUrl = "https://res.cloudinary.com/teepublic/image/private/s--aGYkg285--/t_Preview/b_rgb:191919,c_limit,f_jpg,h_630,q_90,w_630/v1478768274/production/designs/806299_1.jpg";
            int movieNumber = 1;
            foreach (Movie movie in poll.movieList)
            {
                string votes = new string('X', movie.votes.Count);
                builder.AddField($"{movieNumber} - {movie.title} (added by {movie.addedByUser})", $"Votes: |{votes}|");
                movieNumber++;
            }
            return builder.Build();
        }    

        public static async Task ShowNewPoll(Poll poll, SocketCommandContext Context)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
                .AddField(poll.title, poll.movieList[0].title, true);
            Embed embed = embedBuilder.Build();
            await Context.Channel.SendMessageAsync("", false, embed);
        }


        public Poll MakeNewPoll(string title, Movie movie, int days)
        {
            Poll newPoll = new Poll();
            newPoll.title = title;
            newPoll.startDate = DateTime.Now;
            newPoll.endDate = newPoll.startDate.AddDays(days);
            newPoll.movieList.Add(movie);
            return newPoll;
        }

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
                    return pollList;
                }
            }
            else return new List<Poll>();
        }

    }

    public class Polls : ModuleBase<SocketCommandContext>
    {
        [Command("makepoll"), RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        public async Task CreatePoll(string title, [Remainder] string movie)
        {
            Movie newMovie = new Movie();
            newMovie.title = movie;
            newMovie.addedByUser = Context.User.ToString();
            newMovie.votes = new List<string>();
            newMovie.votes.Add(Context.User.ToString());
            Poll newPoll = MakeNewPoll(title, newMovie, 2);
            Program.currentPolls.Add(newPoll);
            Program.SavePolls(Program.currentPolls);
            await Program.ShowNewPoll(newPoll, Context);
            await Context.Channel.SendMessageAsync($"{newPoll.title} created with movie {newPoll.movieList[0].title}");
        }

        
        [Command("showpoll")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowPoll([Remainder]string pollnumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollnumber, out pollIndex))
            {
                Poll poll = Program.currentPolls[pollIndex - 1];
                await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
                //Find a way to add message ID to the poll a
                //Program.currentPolls[pollIndex -1].messageIDs.Add(Context.Channel.)
            }
            else await Context.Channel.SendMessageAsync("Please add a number after the command.");
        }

        [Command("addmovie")]
        public async Task AddMovieAsync([Remainder]string movieTitle)
        {
            Movie newMovie = new Movie
            {
                title = movieTitle,
                addedByUser = Context.User.ToString(),
                votes = new List<string>()
            };
            newMovie.votes.Add(Context.User.ToString());
            Program.currentPolls.First().movieList.Add(newMovie);
            Program.SavePolls(Program.currentPolls);
            await ShowPoll("0");
        }

        public async Task ShowNewPoll(Poll poll, SocketCommandContext Context)
        {
            await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
        }


        public Poll MakeNewPoll(string title, Movie movie, int days)
        {
            Poll newPoll = new Poll();
            newPoll.title = title;
            newPoll.startDate = DateTime.Now;
            newPoll.endDate = newPoll.startDate.AddDays(days);
            newPoll.movieList.Add(movie);
            return newPoll;
        }
    }
}
