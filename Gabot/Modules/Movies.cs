using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot.Modules
{
    public class Movies : ModuleBase<SocketCommandContext>
    {
        [Command("addmovie"), RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        public async Task AddMovie()
        {
            await Context.Channel.SendMessageAsync("addmovie command triggered");
        }

        [Command("removemovie"), RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        public async Task RemoveMovie()
        {
            await Context.Channel.SendMessageAsync("removemovie command triggered");
        }
    }
}
