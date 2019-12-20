using System;
using System.Net.Sockets;
namespace DiscordMultiPlayerBot
{
    public class DiscordLinkConnection
    {
        public TcpClient tcpClient;
        public ulong linkKey;
        public long lastSend;
        public long lastReceive;
        public byte[] buffer = new byte[8192];
        public DiscordConnector.DiscordClientMessageType messageType;
        public int bufferPos = 0;
        public int bufferSize = 8;
        public bool readingHeader = true;
    }
}
