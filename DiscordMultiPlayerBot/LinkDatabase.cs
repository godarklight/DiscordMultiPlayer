using System;
using System.Collections.Generic;
using System.IO;
namespace DiscordMultiPlayerBot
{
    public class LinkDatabase
    {
        //LHS = discord channel id
        //RHS = link key
        //Database: discord->link key
        //Rev database: link key->discord
        //Servers: link key->server
        private Dictionary<ulong, ulong> servers = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, ulong> database = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, ulong> database_reverse = new Dictionary<ulong, ulong>();
        public LinkDatabase()
        {
            if (File.Exists("database.txt"))
            {
                string[] loadData = File.ReadAllLines("database.txt");
                foreach (string line in loadData)
                {
                    if (line.Contains("="))
                    {
                        int commapos = line.IndexOf(",");
                        int equalpos = line.IndexOf("=");
                        ulong server = ulong.Parse(line.Substring(0, commapos));
                        ulong channel = ulong.Parse(line.Substring(commapos + 1, equalpos - commapos - 1));
                        ulong linkkey = ulong.Parse(line.Substring(equalpos + 1));
                        database.Add(channel, linkkey);
                        database_reverse.Add(linkkey, channel);
                        servers.Add(linkkey, server);
                    }
                }
            }
            else
            {
                Save();
            }
        }

        public void SetLink(ulong server, ulong channel, ulong linkkey)
        {
            lock (database)
            {
                servers[linkkey] = server;
                database[channel] = linkkey;
                database_reverse[linkkey] = channel;
                Save();
            }
        }

        public void RemoveLink(ulong channel)
        {
            lock (database)
            {
                servers.Remove(database[channel]);
                database_reverse.Remove(database[channel]);
                database.Remove(channel);
                Save();
            }
        }

        public ulong GetServerFromKey(ulong linkkey)
        {
            return servers.ContainsKey(linkkey) ? servers[linkkey] : 0;
        }

        public ulong GetChannelFromKey(ulong linkkey)
        {
            return database_reverse.ContainsKey(linkkey) ? database_reverse[linkkey] : 0;
        }

        public ulong GetLinkFromChannel(ulong channel)
        {
            return database.ContainsKey(channel) ? database[channel] : 0;
        }

        private void Save()
        {
            using (StreamWriter sw = new StreamWriter("database.txt"))
            {
                foreach (KeyValuePair<ulong, ulong> kvp in database)
                {
                    ulong server = servers[kvp.Value];
                    sw.WriteLine(server + "," + kvp.Key + "=" + kvp.Value);
                }
                sw.WriteLine();
            }
        }
    }
}
