using System;
namespace DiscordConnector
{
    public enum DiscordClientMessageType
    {
        HEARTBEAT = 0,
        REGISTER = 1,
        MESSAGE = 2,
        SCREENSHOT = 3,
        DISCONNECT = 100,
    }

    public enum DiscordServerMessageType
    {
        HEARTBEAT = 0,
        REGISTER_RESPONSE = 1,
        MESSAGE = 2,
        DISCONNECT = 100,
    }
}
