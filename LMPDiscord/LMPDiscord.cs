using System;
using LmpCommon.Message.Data.Chat;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Context;
using Server.Log;
using Server.Plugin;
using Server.Server;
using Server.Settings.Structures;
using DiscordConnector;
using LmpCommon.Message.Interface;

namespace LMPDiscord
{
    public class LMPDiscord : LmpPlugin
    {
        DiscordConnectorMain connector;

        public LMPDiscord()
        {
            connector = new DiscordConnectorMain(SendToLMP, LunaLog.Debug);
        }

        public override void OnUpdate()
        {
            connector.Update();
        }

        public override void OnMessageReceived(ClientStructure client, IClientMessageBase messageData)
        {
            ChatMsgData chatMsgData = messageData.Data as ChatMsgData;
            if (chatMsgData != null)
            {
                connector.SendToDiscord("[" + chatMsgData.From + "] " + chatMsgData.Text);
            }
        }

        public void SendToLMP(string message)
        {
            var msgData = ServerContext.ServerMessageFactory.CreateNewMessageData<ChatMsgData>();
            msgData.From = GeneralSettings.SettingsStore.ConsoleIdentifier;
            msgData.Text = message;
            MessageQueuer.SendToAllClients<ChatSrvMsg>(msgData);
        }
    }
}
