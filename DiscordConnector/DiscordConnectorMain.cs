using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MessageStream2;

namespace DiscordConnector
{
    public class DiscordConnectorMain
    {
        private Action<string> sendMethod;
        private Action<string> logMethod;
        private TcpClient client;
        private ulong linkkey = 0;
        private long lastConnect;
        private long lastReceive;
        private long lastSend;
        private long HEARTBEAT = 5 * TimeSpan.TicksPerSecond;
        private long TIMEOUT = 40 * TimeSpan.TicksPerSecond;
        private byte[] buffer = new byte[8192];
        bool readingHeader = true;
        private int bufferPos = 0;
        private int bufferSize = 8;
        private DiscordServerMessageType messageType;
        private IPAddress[] addresses;

        public DiscordConnectorMain(Action<string> sendMethod, Action<string> logMethod)
        {
            this.sendMethod = sendMethod;
            this.logMethod = logMethod;
            string linkFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "linkkey.txt");
            if (!File.Exists(linkFilePath))
            {
                Random rand = new Random();
                byte[] randBytes = new byte[8];
                rand.NextBytes(randBytes);
                linkkey = BitConverter.ToUInt64(randBytes, 0);
                File.WriteAllText(linkFilePath, linkkey.ToString());
            }
            else
            {
                linkkey = ulong.Parse(File.ReadAllText(linkFilePath));
            }
            logMethod("[Discord] Your link key is: " + linkkey);
        }

        public void SendToDiscord(string message)
        {
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(message);
                SendMessage(DiscordClientMessageType.MESSAGE, mw.GetMessageBytes());
            }
        }

        public void Update()
        {
            long timeNow = DateTime.UtcNow.Ticks;
            if (client == null && timeNow > lastConnect + TIMEOUT)
            {
                lastConnect = timeNow;
                lastReceive = timeNow;
                lastSend = timeNow;
                Reconnect();
            }
            if (timeNow > lastSend + HEARTBEAT)
            {
                lastSend = timeNow;
                SendMessage(DiscordClientMessageType.HEARTBEAT, null);
            }
            if (timeNow > lastReceive + TIMEOUT)
            {
                lastConnect = timeNow;
                lastReceive = timeNow;
                lastSend = timeNow;
                Reconnect();
            }
            ReceiveMessage();
        }

        private void ReceiveMessage()
        {
            if (client == null || !client.Connected || client.Available == 0)
            {
                return;
            }
            lastReceive = DateTime.UtcNow.Ticks;
            int bytesToRead = bufferSize - bufferPos;
            if (bytesToRead > client.Available)
            {
                bytesToRead = client.Available;
            }
            int bytesRead = client.GetStream().Read(buffer, bufferPos, bytesToRead);
            bufferPos += bytesRead;
            if (bufferPos == bufferSize)
            {
                if (readingHeader)
                {
                    using (MessageReader mr = new MessageReader(buffer))
                    {
                        messageType = (DiscordServerMessageType)mr.Read<int>();
                        int length = mr.Read<int>();
                        if (length == 0)
                        {
                            HandleMessage();
                            bufferPos = 0;
                            bufferSize = 8;
                            readingHeader = true;
                        }
                        else
                        {
                            bufferPos = 0;
                            bufferSize = length;
                            readingHeader = false;
                            return;
                        }
                    }
                }
                else
                {
                    HandleMessage();
                    bufferPos = 0;
                    bufferSize = 8;
                    readingHeader = true;
                }
            }

        }
        private void HandleMessage()
        {
            if (messageType == DiscordServerMessageType.REGISTER_RESPONSE)
            {
                logMethod("Registered!");
            }
            if (messageType == DiscordServerMessageType.MESSAGE)
            {
                using (MessageReader mr = new MessageReader(buffer))
                {
                    string message = mr.Read<string>();
                    sendMethod(message);
                }
            }
        }

        public void Reconnect()
        {
            if (client != null && client.Connected)
            {
                try
                {
                    client.Close();
                    client = null;
                }
                catch (Exception e)
                {
                    logMethod("[Discord] Error closing connection: " + e.Message);
                }
            }
            logMethod("[Discord] Connecting");
            try
            {
                IPAddress connectIP = null;
                if (addresses == null)
                {
                    addresses = Dns.GetHostAddresses("godarklight.info.tm");
                    foreach (IPAddress addr in addresses)
                    {
                        if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            connectIP = addr;
                            break;
                        }
                    }
                }
                else
                {
                    foreach (IPAddress addr in addresses)
                    {
                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                        {
                            connectIP = addr;
                            break;
                        }
                    }
                    addresses = null;
                }
                if (connectIP != null)
                {
                    client = new TcpClient(connectIP.AddressFamily);
                    client.Connect(connectIP, 21584);
                    SendRegister();
                }
            }
            catch (Exception e)
            {
                logMethod("[Discord] Error connecting: " + e.Message);
            }
        }

        public void SendRegister()
        {
            logMethod("[Discord] Registering with DiscordMultiPlayer bot");
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<ulong>(linkkey);
                SendMessage(DiscordClientMessageType.REGISTER, mw.GetMessageBytes());
            }
        }

        private void SendMessage(DiscordClientMessageType type, byte[] data)
        {
            if (client == null || !client.Connected)
            {
                if (type != DiscordClientMessageType.MESSAGE && type != DiscordClientMessageType.SCREENSHOT)
                {
                    logMethod("[Discord] Cannot send, not connected");
                }
                return;
            }
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<int>((int)type);
                if (data == null || data.Length == 0)
                {
                    mw.Write<int>(0);
                }
                else
                {
                    mw.Write<byte[]>(data);
                }
                byte[] sendBytes = mw.GetMessageBytes();
                try
                {
                    client.GetStream().Write(sendBytes, 0, sendBytes.Length);
                }
                catch (Exception e)
                {
                    logMethod("[Discord] Error sending to discord bot: " + e.Message);
                }
            }
        }
    }
}
