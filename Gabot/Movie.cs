using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Gabot.Modules
{
<<<<<<< HEAD
    [Serializable]
    public class Movie
    {
        public string title;
        public string addedByUser;
        public List<String> votes = new List<string>();
=======
    public class Movie
    {
        public string title;
        SocketUser addedByUser;
        List<SocketUser> votes;
>>>>>>> ccf0a8e... Basic command recognition, reaction handling, poll creation
    }
}
