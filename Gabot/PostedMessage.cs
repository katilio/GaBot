using System;
using Discord.WebSocket;
using Discord.Rest;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gabot
{
    [Serializable]
    public class PostedMessage
    {
        public ulong guildId;
        public ulong channelId;
        public ulong messageId;

        public async Task<RestUserMessage> GetMessageObj(DiscordSocketClient client)
        {
            var message = await client.GetGuild(guildId).GetTextChannel(channelId).GetMessageAsync(messageId);
            return message as RestUserMessage;
        }
    }
}
