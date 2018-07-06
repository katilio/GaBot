using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Gabot.Modules
{
    [Serializable]
    public class Movie
    {
        public string title;
        public string addedByUser;
        public List<String> votes = new List<string>();
    }
}
