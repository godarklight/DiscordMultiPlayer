using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using MessageStream2;
using DiscordConnector;

namespace DiscordMultiPlayerBot
{
    public class TCPServer
    {
        private bool running = true;
        private Thread receiveThread;
        private List<DiscordLinkConnection> clients = new List<DiscordLinkConnection>();
        private long HEARTBEAT = 5 * TimeSpan.TicksPerSecond;
        private long TIMEOUT = 40 * TimeSpan.TicksPerSecond;
        private Action<ulong, string> sendToDiscord;

        public void SetDependancy(Action<ulong, string> callback)
        {
            this.sendToDiscord = callback;
        }

        public void SendToDMP(ulong linkkey, string message)
        {
            foreach (DiscordLinkConnection dlc in clients)
            {
                if (dlc.linkKey == linkkey)
                {
                    SendMessage(dlc, message);
                }
            }
        }

        public void Start()
        {
            receiveThread = new Thread(new ThreadStart(ReceiveMain));
            receiveThread.Start();
            TcpListener tcpServer = new TcpListener(IPAddress.IPv6Any, 21584);
            tcpServer.Start();
            BeginAccept(tcpServer);
        }

        public void ReceiveMain()
        {
            while (running)
            {
                long timeNow = DateTime.UtcNow.Ticks;
                Thread.Sleep(100);
                lock (clients)
                {
                    for (int i = clients.Count - 1; i >= 0; i--)
                    {
                        DiscordLinkConnection dlc = clients[i];
                        if (!dlc.tcpClient.Connected)
                        {
                            try
                            {
                                dlc.tcpClient.Close();
                            }
                            catch
                            {
                                Console.WriteLine("Removed " + dlc.linkKey + " from clients.");
                            }
                            clients.RemoveAt(i);
                            continue;
                        }
                        if (timeNow > dlc.lastSend + HEARTBEAT)
                        {
                            SendHeartbeat(dlc);
                        }
                        if (timeNow > dlc.lastReceive + TIMEOUT)
                        {
                            SendDisconnect(dlc);
                            clients.RemoveAt(i);
                            continue;
                        }
                        try
                        {
                            if (dlc.tcpClient.Connected && dlc.tcpClient.Available > 0)
                            {
                                dlc.lastReceive = timeNow;
                                int bytesToRead = dlc.bufferSize - dlc.bufferPos;
                                if (bytesToRead > dlc.tcpClient.Available)
                                {
                                    bytesToRead = dlc.tcpClient.Available;
                                }
                                int bytesRead = dlc.tcpClient.GetStream().Read(dlc.buffer, dlc.bufferPos, bytesToRead);
                                dlc.bufferPos += bytesRead;
                                if (dlc.bufferPos == dlc.bufferSize)
                                {
                                    if (dlc.readingHeader)
                                    {
                                        using (MessageReader mr = new MessageReader(dlc.buffer))
                                        {
                                            dlc.messageType = (DiscordClientMessageType)mr.Read<int>();
                                            int length = mr.Read<int>();
                                            if (length == 0)
                                            {
                                                HandleMessage(dlc);
                                                dlc.bufferPos = 0;
                                                dlc.bufferSize = 8;
                                                dlc.readingHeader = true;
                                            }
                                            else
                                            {
                                                dlc.bufferPos = 0;
                                                dlc.bufferSize = length;
                                                dlc.readingHeader = false;
                                                continue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        HandleMessage(dlc);
                                        dlc.bufferPos = 0;
                                        dlc.bufferSize = 8;
                                        dlc.readingHeader = true;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error receiving from " + dlc.linkKey + ", error: " + e.Message);
                        }
                    }
                }
            }
        }

        private void HandleMessage(DiscordLinkConnection dlc)
        {
            if (dlc.messageType == DiscordClientMessageType.REGISTER)
            {
                //Don't allow servers to change their link key on the same connection.
                if (dlc.linkKey == 0)
                {
                    using (MessageReader mr = new MessageReader(dlc.buffer))
                    {
                        dlc.linkKey = mr.Read<ulong>();
                        Console.WriteLine("Registered " + dlc.linkKey + " from " + dlc.tcpClient.Client.RemoteEndPoint);
                        SendRegisterOK(dlc);
                    }
                }
                else
                {
                    SendDisconnect(dlc);
                }
            }
            if (dlc.messageType == DiscordClientMessageType.MESSAGE)
            {
                using (MessageReader mr = new MessageReader(dlc.buffer))
                {
                    string message = mr.Read<string>();
                    sendToDiscord(dlc.linkKey, message);
                    Console.WriteLine("[DMP->Discord:" + dlc.linkKey + "] " + message);
                }
            }
        }

        private void SendToDLC(DiscordLinkConnection dlc, DiscordServerMessageType type, byte[] data)
        {
            dlc.lastSend = DateTime.UtcNow.Ticks;
            if (!dlc.tcpClient.Connected)
            {
                lock (clients)
                {
                    Console.WriteLine(dlc.linkKey + " disconnected.");
                    clients.Remove(dlc);
                }
                return;
            }
            if (dlc.linkKey == 0)
            {
                return;
            }
            try
            {
                byte[] sendData;
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
                    sendData = mw.GetMessageBytes();
                }
                dlc.tcpClient.GetStream().Write(sendData, 0, sendData.Length);
            }
            catch
            {
                if (type != DiscordServerMessageType.DISCONNECT)
                {
                    Console.WriteLine("Error sending, disconnecting " + dlc.linkKey);
                    SendDisconnect(dlc);
                }
            }
        }

        private void SendHeartbeat(DiscordLinkConnection dlc)
        {
            SendToDLC(dlc, DiscordServerMessageType.HEARTBEAT, null);
        }

        private void SendRegisterOK(DiscordLinkConnection dlc)
        {
            SendToDLC(dlc, DiscordServerMessageType.REGISTER_RESPONSE, null);
        }

        private void SendMessage(DiscordLinkConnection dlc, string message)
        {
            using (MessageWriter mw = new MessageWriter())
            {
                mw.Write<string>(message);
                SendToDLC(dlc, DiscordServerMessageType.MESSAGE, mw.GetMessageBytes());
            }
        }

        private void SendDisconnect(DiscordLinkConnection dlc)
        {
            SendToDLC(dlc, DiscordServerMessageType.DISCONNECT, null);
            try
            {
                dlc.tcpClient.Close();
            }
            catch
            {
                Console.WriteLine("Disconnected " + dlc.linkKey);
            }
            lock (clients)
            {
                clients.Remove(dlc);
            }
        }

        private void BeginAccept(TcpListener tcpServer)
        {
            if (!running)
            {
                return;
            }
            try
            {
                tcpServer.BeginAcceptTcpClient(HandleAccept, tcpServer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error accepting client: " + e.Message);
            }
        }

        private void HandleAccept(IAsyncResult ar)
        {
            if (!running)
            {
                return;
            }
            long timeNow = DateTime.UtcNow.Ticks;
            TcpListener tcpServer = (TcpListener)ar.AsyncState;
            try
            {
                TcpClient tcpClient = tcpServer.EndAcceptTcpClient(ar);
                DiscordLinkConnection newDlc = new DiscordLinkConnection();
                newDlc.tcpClient = tcpClient;
                newDlc.lastSend = timeNow;
                newDlc.lastReceive = timeNow;
                lock (clients)
                {
                    clients.Add(newDlc);
                }
                Console.WriteLine("Accepted new client: " + tcpClient.Client.RemoteEndPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error accepting client: " + e.Message);
            }
            BeginAccept(tcpServer);
        }
    }
}
