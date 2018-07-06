﻿using Discord;
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
                if (poll.messageIDs.Contains(arg3.MessageId))
                {
                    int emoteIndex = 0;
                    //If the emote name can be parsed as an int
                    if (int.TryParse(arg3.Emote.Name.ToString().Substring(0, 1), out emoteIndex))
                    {
                        //If the user didn't add the movie
                        if (poll.movieList[emoteIndex-1].addedByUser != arg3.User.ToString())
                        { 
                            //If the user has voted for that movie
                            if (poll.movieList[emoteIndex - 1].votes.Contains(arg3.User.ToString()))
                            {
                                poll.movieList[emoteIndex - 1].votes.Remove(arg3.User.ToString());
                                Program.SavePolls(Program.currentPolls);
                                await arg2.SendMessageAsync($"Removing vote for movie *{poll.movieList[emoteIndex - 1].title}* from *{arg3.User.ToString()}*");
                                await UpdateLastMessage(poll);
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
                if (poll.messageIDs.Contains(arg3.MessageId))
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
                                await arg2.SendMessageAsync($"Adding vote for movie *{poll.movieList[emoteIndex - 1].title}* from *{arg3.User.ToString()}*");
                                await UpdateLastMessage(poll);
                            }
                        }
                        else
                        {
                            var user = client.GetUser(arg3.UserId);
                            await user.SendMessageAsync($"Sorry {user.Mention}, you've already voted in that poll. <3");
                        }
                        //arg2.SendMessageAsync("You've already voted in that poll.");
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
            builder.Footer.Text = "Vote for each movie by reacting with :one:, :two:, etc. " + $"Ends at {poll.endDate.ToUniversalTime().ToShortTimeString()} UTC on {poll.endDate.ToLongDateString()}";
            builder.Author.Name = "MOVIE POLL - #" + (Program.currentPolls.FindIndex(x => x.title == poll.title)+1).ToString();
            builder.Author.IconUrl = "https://res.cloudinary.com/teepublic/image/private/s--aGYkg285--/t_Preview/b_rgb:191919,c_limit,f_jpg,h_630,q_90,w_630/v1478768274/production/designs/806299_1.jpg";
            builder.Title = poll.title + $", *created by {poll.movieList[0].addedByUser}* - ends in: " + poll.endDate.Subtract(DateTime.Now).Hours + " hours and " + poll.endDate.Subtract(DateTime.Now).Minutes + " minutes.";
            builder.ThumbnailUrl = "https://res.cloudinary.com/teepublic/image/private/s--aGYkg285--/t_Preview/b_rgb:191919,c_limit,f_jpg,h_630,q_90,w_630/v1478768274/production/designs/806299_1.jpg";
            int movieNumber = 1;
            foreach (Movie movie in poll.movieList)
            {
                string votes = new string('X', movie.votes.Count);
                char[] chars = new char[] { '-', 'x', '-' };
                //string votes = new string(chars, 0, movie.votes.Count);
                builder.AddField($"{movieNumber} - {movie.title} *({movie.addedByUser.Split('#')[0]})*", $"Votes: **|{votes}|**");
                movieNumber++;
            }
            return builder.Build();
        }    

        public static async Task UpdateLastMessage(Poll poll)
        {
            if (poll.lastMessage == null)
            {
                await GetLastMessage(poll);
                await poll.lastMessage.ModifyAsync(msg => msg.Embed = MakePollEmbed(poll));
            }
            await poll.lastMessage.ModifyAsync(msg => msg.Embed = MakePollEmbed(poll));

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
            Poll newPoll = MakeNewPoll(title, newMovie, length);
            Program.currentPolls.Add(newPoll);
            Program.SavePolls(Program.currentPolls);
            await ShowNewPoll(newPoll, Context);
            PollCountdown(newPoll, Context);
            await Context.Channel.SendMessageAsync($"{newPoll.title} created with movie {newPoll.movieList[0].title}");
        }

        [Command("removepoll"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task RemovePoll(string pollNumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollNumber, out pollIndex))
            {
                Program.currentPolls.RemoveAt(pollIndex - 1);
                await Context.Channel.SendMessageAsync($"Poll number {pollIndex} removed!");
            }
        }

        [Command("removemovie"), RequireUserPermission(Discord.GuildPermission.MentionEveryone)]
        public async Task RemoveMovie(string pollNumber, string movieNumber)
        {
            int pollIndex = 0;
            int movieIndex = 0;
            if ((int.TryParse(pollNumber, out pollIndex) && int.TryParse(movieNumber, out movieIndex)))
            {
                Program.currentPolls[pollIndex - 1].movieList.RemoveAt(movieIndex-1);
                await Context.Channel.SendMessageAsync($"Movie number {movieIndex} removed from poll {pollIndex}!");
            }
        }


        [Command("showpoll")]
        //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
        public async Task ShowPoll([Remainder]string pollnumber)
        {
            int pollIndex = 0;
            if (int.TryParse(pollnumber, out pollIndex))
            {
                Program.GetLastMessage(Program.currentPolls[pollIndex-1]);
                Poll poll = Program.currentPolls[pollIndex - 1];
                var message = await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
                Program.currentPolls[pollIndex - 1].messageIDs.Add(message.Id);
                Program.currentPolls[pollIndex - 1].lastMessage = message;
                Program.SavePolls(Program.currentPolls);
            }
            else await Context.Channel.SendMessageAsync("Please add a number after the command.");
        }

        [Command("addmovie")]
        public async Task AddMovieAsync(int pollIndex, [Remainder]string movieTitle)
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
                await Program.UpdateLastMessage(poll);
            }
            else await Context.User.SendMessageAsync($"Sorry {Context.User.Mention}, you've already added a movie in that poll. <3");
        }

        public async Task ShowNewPoll(Poll poll, SocketCommandContext Context)
        {
            var message = await Context.Channel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
            poll.lastMessage = message;
            poll.messageIDs.Add(message.Id);
            Program.SavePolls(Program.currentPolls);
        }

        //Unnecessary?
        public static async Task PollCountdown(Poll poll, SocketCommandContext Context)
        {
            if (poll.endDate > DateTime.Now)
            { 
                await Task.Delay(poll.endDate - poll.startDate);
                var destinationChannel = Context.Guild.GetTextChannel(Program.guildChannelId);
                poll.movieList.Sort((x, y) => y.votes.Count - x.votes.Count);
                await destinationChannel.SendMessageAsync($"Poll *{poll.title}* is over! Results:");
                await Context.Guild.GetTextChannel(462664432481468418).SendMessageAsync("", false, Program.MakePollEmbed(poll));
            }
        }

        public static async Task PollCountdown(Poll poll)
        {
            if (poll.endDate > DateTime.Now)
            {
                await Task.Delay(poll.endDate - poll.startDate);
                var destinationChannel = Program.client.GetGuild(Program.parentGuildId).GetTextChannel(Program.guildChannelId);
                poll.movieList.Sort((x, y) => y.votes.Count - x.votes.Count);
                await destinationChannel.SendMessageAsync($"Poll *{poll.title}* is over! Results:");
                await destinationChannel.SendMessageAsync("", false, Program.MakePollEmbed(poll));
            }
        }

        public Poll MakeNewPoll(string title, Movie movie, float days)
        {
            Poll newPoll = new Poll();
            newPoll.title = title;
            newPoll.startDate = DateTime.Now;
            newPoll.endDate = newPoll.startDate.AddHours(days);
            newPoll.movieList.Add(movie);
            newPoll.messageIDs = new List<ulong>();
            return newPoll;
        }
    }
}
