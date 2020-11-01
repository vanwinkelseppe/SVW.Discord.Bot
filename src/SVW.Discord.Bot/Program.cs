using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SVW.Discord.Bot.Services;
using SVW.Discord.Data;
using SVW.Discord.Domain;

namespace SVW.Discord.Bot
{

    /// <summary>
    /// TODO MOVE TO SETTINGS
    /// </summary>


    class Program
    {
        private ulong GuildId = 0;
        private ulong RoleId = 0;
        private ulong ChannelId = 0;
        private const string Token = "";


        private DiscordSocketClient _client;

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // You should dispose a service provider created using ASP.NET
            // when you are finished using it, at the end of your app's lifetime.
            // If you use another dependency injection framework, you should inspect
            // its documentation for the best way to do this.
            using (var services = ConfigureServices())
            {
                _client = services.GetRequiredService<DiscordSocketClient>();

                var context = services.GetRequiredService<HighscoreDatabaseContext>();

                _client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;
                _client.Ready += () => ReadyAsync(context);
                _client.MessageReceived += (_) => MessageReceivedAsync(context, _);



                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.

                await _client.LoginAsync(TokenType.Bot, Token);
                await _client.StartAsync();


                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(Timeout.Infinite);

            }
        }

        private async Task ReadyAsync(IHighscoreContext context)
        {
            Console.WriteLine($"{_client.CurrentUser} is connected!");

            var lastMessage = await context.LastMessage(); 
            if (_client.Guilds.FirstOrDefault(_ => _.Id == GuildId)?.GetChannel(ChannelId) is IMessageChannel channel)
            {
                if (lastMessage == null)
                {
                
                    var msg = await channel.GetMessagesAsync(limit: 5000).FlattenAsync();
                    await context.Logs.AddRangeAsync(msg.Reverse().Select(_ =>
                    {
                        if (int.TryParse(_.Content, out var number))
                        {
                            return new Log
                            {
                                DateTime = _.Timestamp.DateTime,
                                Message = _.Content,
                                MessageId = _.Id,
                                UserId = _.Author.Id,
                                Number = number,
                                WasValid = true
                            };
                        }
                        else
                        {
                            return new Log
                            {
                                DateTime = _.Timestamp.DateTime,
                                Message = _.Content,
                                MessageId = _.Id,
                                UserId = _.Author.Id,
                                Number = 0,
                                WasValid = false
                            };
                        }


                    }));

                    await context.SaveChangesAsync(new CancellationToken());
                }
                else
                {
                    var msg = await channel.GetMessagesAsync(lastMessage.MessageId, Direction.After).FlattenAsync(); 
                    await context.Logs.AddRangeAsync(msg.Select(_ =>
                    {
                        if (int.TryParse(_.Content, out var number))
                        {
                            return new Log
                            {
                                DateTime = _.Timestamp.DateTime,
                                Message = _.Content,
                                MessageId = _.Id,
                                UserId = _.Author.Id,
                                Number = number,
                                WasValid = true
                            };
                        }
                        else
                        {
                            return new Log
                            {
                                DateTime = _.Timestamp.DateTime,
                                Message = _.Content,
                                MessageId = _.Id,
                                UserId = _.Author.Id,
                                Number = 0,
                                WasValid = false
                            };
                        }


                    }));

                    await context.SaveChangesAsync(new CancellationToken());

                }
            }
            
        }

        private async Task LogMessageAsync(IHighscoreContext context, SocketMessage message, bool wasValid, int value = 0)
        {
            await context.Logs.AddAsync(new Log
            {
                DateTime = message.Timestamp.DateTime,
                Message = message.Content,
                MessageId = message.Id,
                UserId = message.Author.Id,
                WasValid = wasValid,
                Number = value
            });

            await context.SaveChangesAsync(new CancellationToken());
        }

        private async Task MessageReceivedAsync(IHighscoreContext context, SocketMessage message)
        {
            if (message.Channel.Id == ChannelId)
            {
                var lastMessage = await context.LastMessage();
                if (lastMessage == null)
                {
                    lastMessage = new Log {WasValid = true};
                }
                if (int.TryParse(message.Content, out var number))
                {



                    var wasMessageValid = false;
                    if (lastMessage.WasValid)
                    {
                        if ((lastMessage.Number + 1) == number)
                        {
                            wasMessageValid = true;
                            if (GetMax() < number)
                            {
                                await NewMax(number);
                            }
                        }
                        else
                        {
                            await Fail(message.Author, $"{lastMessage.Number + 1}", message.Content, true, number);
                        }
                    }
                    else
                    {
                        if (1 == number)
                        {
                            wasMessageValid = true;
                        }
                        else
                        {
                            await Fail(message.Author, $"{lastMessage.Number + 1}", message.Content, false, 0);
                        }
                    }


                    await message.AddReactionAsync(new Emoji(wasMessageValid ? "✅" : "❌"));
                    await Task.Run(() => LogMessageAsync(context, message, wasMessageValid, number));
                }
                else
                {
                    await message.AddReactionAsync(new Emoji("❌"));

                    await Fail(message.Author, $"{lastMessage.Number + 1}", message.Content, false, 0);

                    await Task.Run(() => LogMessageAsync(context, message, false, 0));
                }
                
            }
        }

        private async Task NewMax(int number)
        {
            var channel = _client.Guilds.FirstOrDefault(_ => _.Id == GuildId)?.GetChannel(ChannelId);

            if (channel != null)
            {
                var channelNameWithoutNumber = new string(channel.Name.Where(_ => !char.IsDigit(_)).ToArray());
                await AssignChannelName($"{channelNameWithoutNumber}{number}");
            }
        }
        private async Task Fail(SocketUser author, string expected, string response, bool wasValid, int value)
        {
            var maximum = GetMax();
            await SendFailedMessage(author, expected, response, $"{maximum}");
            await AssignRole(author.Id);
        }


        private async Task SendFailedMessage(SocketUser author, string expected, string response, string record)
        {

            var guild = _client.Guilds.FirstOrDefault(_ => _.Id == GuildId);
            if (guild != null)
            {
                var defaultChannel = guild?.DefaultChannel;
                var userOnPole = guild.GetRole(RoleId).Members.FirstOrDefault();

                var authorBuilder = new EmbedAuthorBuilder();
                authorBuilder.WithName(author.Username);
                authorBuilder.WithIconUrl(author.GetAvatarUrl());
                var builder = new EmbedBuilder { Author = authorBuilder };

                builder.WithTitle("I did something stupid.");
                builder.AddField("Responded with", response, true);
                builder.AddField("Expected", expected, true);
                builder.AddField("Record", record);


                await defaultChannel.SendMessageAsync(null, false, builder.Build());
                if (userOnPole != null) 
                    await defaultChannel.SendMessageAsync($"!haalvandepaal {userOnPole.Mention}");
                await defaultChannel.SendMessageAsync($"!nagel {author.Mention}");
            }
        }


        private int GetMax()
        {
            var channel = _client.Guilds.FirstOrDefault(_ => _.Id == GuildId)?.GetChannel(ChannelId);

            if (channel == null) return 0;
            var numberString =new string(channel.Name.Where(char.IsDigit).ToArray());
            return int.TryParse(numberString, out var number) ? number : 0;
        }

        private async Task AssignChannelName(string name)
        {
            try
            {
                var channel = _client.Guilds.FirstOrDefault(_ => _.Id == GuildId)?.GetChannel(ChannelId);
                if (channel != null) await channel.ModifyAsync(_ => _.Name = name);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Cannot set Channelname {e}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private async Task AssignRole(ulong userId)
        {
            var guild = _client.Guilds.FirstOrDefault(_ => _.Id == GuildId);

            var role = guild?.GetRole(RoleId);
            var userOnRole = role?.Members.FirstOrDefault();
            var userToAttachRole = guild?.GetUser(userId);

            if (userOnRole != null) await userOnRole.RemoveRoleAsync(role);
            if (userToAttachRole != null) await userToAttachRole.AddRoleAsync(role);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddDbContext<HighscoreDatabaseContext>(options =>
                    options.UseSqlite(new SqliteConnectionStringBuilder
                        {
                            DataSource = "Discord.db"
                        }.ToString()
                    ))
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                
                //.AddSingleton<HttpClient>()
                //.AddSingleton<PictureService>()
                .BuildServiceProvider();
        }
    }
}
