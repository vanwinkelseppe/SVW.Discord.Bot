using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SVW.Discord.Data;

namespace SVW.Discord.Bot.Modules
{
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        // Dependency Injection will fill this value in for us
        
        public HighscoreDatabaseContext DbContext { get; set; }

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");

        

        // Get info on a user, or the user who invoked the command if one is not specified
        [Command("userinfo")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user = user ?? Context.User;

            await ReplyAsync(user.ToString());
        }

        [Command("lastcount")]
        public async Task LastCount()
        {
            var last = await DbContext.LastMessage();

            await ReplyAsync(last.Message);
        }


        //[Remainder] //takes the rest of the command's arguments as one argument, rather than splitting every space
        [Command("amount")]
        public async Task AmountAsync([Remainder] string text)
        {
            if (int.TryParse(text, out var number))
            {
                var amount = await DbContext.Logs.CountAsync(_ => _.Number == number && _.WasValid);
                await ReplyAsync($"Number {number} has been counted {amount} {(amount != 0 ? "times" : "time")}.");
            }
            else
            {
                await ReplyAsync("Thats not a number");
            }

        }

        // 'params' will parse space-separated elements into a list
        [Command("list")]
        public Task ListAsync(params string[] objects)
            => ReplyAsync("You listed: " + string.Join("; ", objects));

        // Setting a custom ErrorMessage property will help clarify the precondition error
        [Command("guild_only")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        public Task GuildOnlyCommand()
            => ReplyAsync("Nothing to see here!");
    }
}
