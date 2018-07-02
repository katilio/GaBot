using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
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
}
