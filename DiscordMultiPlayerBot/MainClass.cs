using System;

namespace DiscordMultiPlayerBot
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            LinkDatabase linkDatabase = new LinkDatabase();
            DiscordServer discordServer = new DiscordServer();
            TCPServer tcpServer = new TCPServer();
            tcpServer.SetDependancy(discordServer.SendToDiscord);
            discordServer.SetDependancy(tcpServer.SendToDMP, linkDatabase);
            tcpServer.Start();
            discordServer.Start();
        }
    }
}
