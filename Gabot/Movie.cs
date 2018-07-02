using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Gabot.Modules
{
    public class Movie
    {
        public string title;
        SocketUser addedByUser;
        List<SocketUser> votes;
    }
}
