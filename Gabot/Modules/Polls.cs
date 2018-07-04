<<<<<<< HEAD
﻿using Discord;
using Discord.Commands;
=======
﻿using Discord.Commands;
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
<<<<<<< HEAD
    //public class Polls : ModuleBase<SocketCommandContext>
    //{

    //    [Command("makepoll"), RequireUserPermission(Discord.GuildPermission.ManageChannels)]
    //    public async Task CreatePoll(string title, [Remainder] string movie)
    //    {
    //        Movie newMovie = new Movie();
    //        newMovie.title = movie;
    //        Poll newPoll = MakeNewPoll(title, newMovie, 2);
    //        //currentPolls.Add(newPoll);
    //        await ShowNewPoll(newPoll, Context);
    //        await Context.Channel.SendMessageAsync($"{newPoll.title} created with movie {newPoll.movieList[0].title}");
    //    }

    //    [Command("showpoll")]
    //    //public async Task ShowPoll(Poll poll, SocketCommandContext Context)
    //    public async Task ShowPoll([Remainder]string pollnumber)
    //    {
    //        await Context.Channel.SendMessageAsync("showpoll command triggered");
    //    }

    //    public async Task ShowNewPoll (Poll poll, SocketCommandContext Context)
    //    {
    //        EmbedBuilder embedBuilder = new EmbedBuilder()
    //            .AddField(poll.title, poll.movieList[0].title, true);
    //        Embed embed = embedBuilder.Build();
    //        await Context.Channel.SendMessageAsync("", false, embed);
    //    }



    //    public Poll MakeNewPoll (string title, Movie movie, int days)
    //    {
    //        Poll newPoll = new Poll();
    //        newPoll.title = title;
    //        newPoll.startDate = DateTime.Now;
    //        newPoll.endDate = newPoll.startDate.AddDays(days);
    //        newPoll.movieList.Add(movie);
    //        return newPoll;
    //    }
    //}
=======
    public class Polls : ModuleBase<SocketCommandContext>
    {
        [Command("makepoll"), RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        public async Task CreatePoll()
        {
            Movie newMovie = new Movie();
            newMovie.title = "movie test";
            Poll newPoll = MakeNewPoll("test poll", newMovie, 2);
            //DisplayNewPoll
            await Context.Channel.SendMessageAsync($"{newPoll.title} created with movie {newPoll.movieList[0].title}");
        }

        [Command("showpoll")]
        public async Task DisplayPoll()
        {
            await Context.Channel.SendMessageAsync("showpoll command triggered");
        }

        public Poll MakeNewPoll (string title, Movie movie, int days)
        {
            Poll newPoll = new Poll();
            newPoll.title = title;
            newPoll.startDate = DateTime.Now;
            newPoll.endDate = newPoll.startDate.AddDays(days);
            newPoll.movieList.Add(movie);
            return newPoll;
        }
    }
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
}
