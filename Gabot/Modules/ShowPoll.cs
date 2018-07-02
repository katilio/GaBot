using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
    public class ShowPoll : ModuleBase<SocketCommandContext>
    {
        [Command("showpoll")]
        public async Task DisplayPoll()
        {
            await Context.Channel.SendMessageAsync("showpoll command triggered");
        }
    }
}