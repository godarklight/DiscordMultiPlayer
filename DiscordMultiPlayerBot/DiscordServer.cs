using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
namespace DiscordMultiPlayerBot
{
    public class DiscordServer
    {
        private List<LinkRequest> requests = new List<LinkRequest>();
        private LinkDatabase database;
        private Action<ulong, string> sendToDMP;
        public void SetDependancy(Action<ulong, string> callback, LinkDatabase database)
        {
            this.sendToDMP = callback;
            this.database = database;
        }

        // Program entry point
        public void Start()
        {
            // Call the Program constructor, followed by the 
            // MainAsync method and wait until it finishes (which should be never).
            MainAsync().GetAwaiter().GetResult();
        }

        public void SendToDiscord(ulong linkkey, string message)
        {
            ulong server = database.GetServerFromKey(linkkey);
            ulong channel = database.GetChannelFromKey(linkkey);
            if (server != 0 && channel != 0)
            {
                Console.WriteLine("[Server->Discord:" + linkkey + "] " + message);
                _client.GetGuild(server).GetTextChannel(channel).SendMessageAsync(message);
            }
            else
            {
                Console.WriteLine("[Server->Discord:Unlinked] " + message);
            }
        }

        private readonly DiscordSocketClient _client;

        // Keep the CommandService and DI container around for use with commands.
        // These two types require you install the Discord.Net.Commands package.
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public DiscordServer()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                // How much logging do you want to see?
                LogLevel = LogSeverity.Info,

                // If you or another service needs to do anything with messages
                // (eg. checking Reactions, checking the content of edited/deleted messages),
                // you must set the MessageCacheSize. You may adjust the number as needed.
                //MessageCacheSize = 50,

                // If your platform doesn't have native WebSockets,
                // add Discord.Net.Providers.WS4Net from NuGet,
                // add the `using` at the top, and uncomment this line:
                //WebSocketProvider = WS4NetProvider.Instance
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                // Again, log level:
                LogLevel = LogSeverity.Info,

                // There's a few more properties you can set,
                // for example, case-insensitive commands.
                CaseSensitiveCommands = false,
            });

            // Subscribe the logging handler to both the client and the CommandService.
            _client.Log += Log;
            _commands.Log += Log;

            // Setup your DI container.
            _services = ConfigureServices();

        }

        // If any services require the client, or the CommandService, or something else you keep on hand,
        // pass them as parameters into this method as needed.
        // If this method is getting pretty long, you can seperate it out into another file using partials.
        private static IServiceProvider ConfigureServices()
        {
            var map = new ServiceCollection();
            // Repeat this for all the service classes
            // and other dependencies that your commands might need.
            //.AddSingleton(new SomeServiceClass());

            // When all your required services are in the collection, build the container.
            // Tip: There's an overload taking in a 'validateScopes' bool to make sure
            // you haven't made any mistakes in your dependency graph.
            return map.BuildServiceProvider();
        }

        // Example of a logging handler. This can be re-used by addons
        // that ask for a Func<LogMessage, Task>.
        private static Task Log(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}");
            Console.ResetColor();

            // If you get an error saying 'CompletedTask' doesn't exist,
            // your project is targeting .NET 4.5.2 or lower. You'll need
            // to adjust your project's target framework to 4.6 or higher
            // (instructions for this are easily Googled).
            // If you *need* to run on .NET 4.5 for compat/other reasons,
            // the alternative is to 'return Task.Delay(0);' instead.
            return Task.CompletedTask;
        }

        private async Task MainAsync()
        {
            // Centralize the logic for commands into a separate method.
            await InitCommands();

            // Login and connect.
            string discordToken = File.ReadAllText("DiscordToken.txt").Trim();
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();
            await _client.SetGameAsync("messages", null, ActivityType.Watching);

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(Timeout.Infinite);
        }

        private async Task InitCommands()
        {
            // Either search the program and add all Module classes that can be found.
            // Module classes MUST be marked 'public' or they will be ignored.
            // You also need to pass your 'IServiceProvider' instance now,
            // so make sure that's done before you get here.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            // Or add Modules manually if you prefer to be a little more explicit:
            //await _commands.AddModuleAsync<SomeModule>(_services);
            // Note that the first one is 'Modules' (plural) and the second is 'Module' (singular).

            // Subscribe a handler to see if a message invokes a command.
            _client.MessageReceived += HandleCommandAsync;
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            // Bail out if it's a System Message.
            var msg = arg as SocketUserMessage;
            if (msg == null) return;

            // We don't want the bot to respond to itself or other bots.
            if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot) return;

            // Create a number to track where the prefix ends and the command begins
            int pos = 0;
            // Replace the '!' with whatever character
            // you want to prefix your commands with.
            // Uncomment the second half if you also want
            // commands to be invoked by mentioning the bot instead.
            //if (msg.HasCharPrefix('!', ref pos) /* || msg.HasMentionPrefix(_client.CurrentUser, ref pos) */)
            //{
            // Create a Command Context.
            //var context = new SocketCommandContext(_client, msg);

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully).
            //var result = await _commands.ExecuteAsync(context, pos, _services);

            // Uncomment the following lines if you want the bot
            // to send a message if it failed.
            // This does not catch errors from commands with 'RunMode.Async',
            // subscribe a handler for '_commands.CommandExecuted' to see those.
            //if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            //    await msg.Channel.SendMessageAsync(result.ErrorReason);
            //}

            SocketGuildChannel guildChannel = msg.Channel as SocketGuildChannel;
            if (guildChannel != null)
            {
                if (msg.HasMentionPrefix(_client.CurrentUser, ref pos) && msg.Content.Contains("link"))
                {
                    await msg.Channel.SendMessageAsync("Please DM me your link key");
                    LinkRequest lr = new LinkRequest();
                    lr.author = msg.Author.Id;
                    lr.channel = msg.Channel.Id;
                    lr.server = guildChannel.Guild.Id;
                    lock (requests)
                    {
                        requests.Add(lr);
                    }
                }
                else
                {
                    ulong linkkey = database.GetLinkFromChannel(msg.Channel.Id);
                    if (linkkey != 0)
                    {
                        Console.WriteLine("[Discord->Server:" + linkkey + "] [" + msg.Author.Username + "] " + msg.Content);
                        sendToDMP(linkkey, "[" + msg.Author.Username + "] " + msg.Content);
                    }
                    else
                    {
                        Console.WriteLine("[Discord->Server:Unlinked] " + msg.Content);
                    }
                }
            }

            SocketDMChannel dMChannel = msg.Channel as SocketDMChannel;
            if (dMChannel != null)
            {
                ulong linkkey = 0;
                if (ulong.TryParse(msg.Content, out linkkey))
                {
                    LinkRequest linkRequest = null;
                    foreach (LinkRequest lr in requests)
                    {
                        if (lr.author == msg.Author.Id)
                        {
                            linkRequest = lr;
                            Console.WriteLine("Linking " + lr.channel + " to " + linkkey);
                            database.SetLink(lr.server, lr.channel, linkkey);
                        }
                    }
                    if (linkRequest != null)
                    {
                        lock (requests)
                        {
                            requests.Remove(linkRequest);
                        }
                        await msg.Channel.SendMessageAsync("Thankyou! You are now be linked.");
                    }
                    else
                    {
                        await msg.Channel.SendMessageAsync("You must type '@DiscordMultiPlayer link' in the channel you wish to link");
                    }

                }
                else
                {
                    await msg.Channel.SendMessageAsync("Type only just the link key number as the message.");
                }
            }
        }
    }
}