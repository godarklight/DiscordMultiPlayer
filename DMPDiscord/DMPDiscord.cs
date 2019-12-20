using System;
using DiscordConnector;
using DarkMultiPlayerServer;
using DarkMultiPlayerCommon;
using MessageStream2;

namespace DMPDiscord
{
    public class DMPDiscord : DMPPlugin
    {
        private DiscordConnectorMain connector;

        public DMPDiscord()
        {
            connector = new DiscordConnectorMain(this.SendToDMP, DarkLog.Debug);
        }

        public override void OnUpdate()
        {
            connector.Update();
        }

        public override void OnMessageReceived(ClientObject client, ClientMessage messageData)
        {
            try
            {
                if (messageData.type == ClientMessageType.CHAT_MESSAGE)
                {
                    using (MessageReader mr = new MessageReader(messageData.data))
                    {
                        ChatMessageType messageType = (ChatMessageType)mr.Read<int>();
                        string fromPlayer = mr.Read<string>();
                        if (messageType == ChatMessageType.CHANNEL_MESSAGE)
                        {
                            string channel = mr.Read<string>();
                            if (channel == "")
                            {
                                string message = mr.Read<string>();
                                connector.SendToDiscord("[" + fromPlayer + "] " + message);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DarkLog.Debug("Error relaying chat message: " + e);
            }
        }

        public void SendToDMP(string messageToSend)
        {
            DarkMultiPlayerServer.Messages.Chat.SendChatMessageToAll(messageToSend);
        }
    }
}