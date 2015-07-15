﻿using Ark.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark
{
    public enum Opcode
    {
        AuthFailed = -1,
        ServerResponse = 0,
        Generic,
        Auth,
        Keepalive,
        GetPlayers,
        KickPlayer,
        BanPlayer,
        ScheduledTask,
        ChatMessage
    }

    public class Rcon
    {
        public ConnectionInfo CurrentServerInfo {get; set;}
        private Client Client {get; set;}
        public bool IsRunning {get; set;}

        private Dictionary<PacketType, Dictionary<Opcode, Action<Packet>>> PacketHandlers {get; set;}
        public event EventHandler<HostnameEventArgs> HostnameUpdated;
        public event EventHandler<PlayerCountEventArgs> CurrentPlayerCountUpdated;
        public event EventHandler<ConsoleLogEventArgs> ConsoleLogUpdated;
        public event EventHandler<ChatLogEventArgs> ChatLogUpdated;        
        public event EventHandler<PlayersEventArgs> PlayersUpdated;
        public event EventHandler<ServerAuthEventArgs> ServerAuthFailed;
        public event EventHandler<ServerAuthEventArgs> ServerAuthSucceeded;
        public event EventHandler<ServerConnectionEventArgs> ServerConnectionFailed;
        public event EventHandler<ServerConnectionEventArgs> ServerConnectionDropped;
        public event EventHandler<ServerConnectionEventArgs> ServerConnectionSucceeded;
        public event EventHandler<ServerConnectionEventArgs> ServerConnectionStarting;
        public event EventHandler<ServerConnectionEventArgs> ServerConnectionDisconnected;

        public Rcon()
        {
            PacketHandlers = new Dictionary<PacketType,Dictionary<Opcode,Action<Packet>>>();
            PacketHandlers[PacketType.Server] = new Dictionary<Opcode,Action<Packet>>();
            PacketHandlers[PacketType.ResponseValue] = new Dictionary<Opcode,Action<Packet>>();
            PacketHandlers[PacketType.AuthResponse] = new Dictionary<Opcode,Action<Packet>>();
            PacketHandlers[PacketType.Server][Opcode.ServerResponse]            = OnConsoleLogUpdated;
            PacketHandlers[PacketType.ResponseValue][Opcode.Generic]            = OnConsoleLogUpdated;
            PacketHandlers[PacketType.ResponseValue][Opcode.GetPlayers]         = OnGetPlayers;
            PacketHandlers[PacketType.ResponseValue][Opcode.ChatMessage]        = OnGetChatMessage;
            PacketHandlers[PacketType.ResponseValue][Opcode.KickPlayer]         = OnKickPlayer;
            PacketHandlers[PacketType.ResponseValue][Opcode.BanPlayer]          = OnBanPlayer;
            PacketHandlers[PacketType.ResponseValue][Opcode.ScheduledTask]      = OnScheduledTask;
            PacketHandlers[PacketType.ResponseValue][Opcode.Keepalive]          = (p) => Console.WriteLine("Keepalive");
            PacketHandlers[PacketType.AuthResponse][Opcode.Auth]                = OnServerAuthSuccess;
            PacketHandlers[PacketType.AuthResponse][Opcode.AuthFailed]          = OnServerAuthFail;
        }

        public bool IsConnected
        {
            get {return Client != null && Client.IsConnected;}
        }
        public async Task Connect(ConnectionInfo connectionInfo)
        {
            Disconnect();

            CurrentServerInfo = connectionInfo;

            Client = new Client();
            Client.ServerConnectionFailed += OnServerConnectionFailed;
            Client.ServerConnectionDropped += OnServerConnectionDropped;
            Client.ServerConnectionSucceeded += OnServerConnectionSucceeded;
            Client.ServerConnectionStarting += OnServerConnectionStarting;
            Client.ServerConnectionDisconnected += OnServerConnectionDisconnected;
            Client.ReceivedPacket += OnReceivedPacket;

            await Client.Connect(CurrentServerInfo.Hostname, CurrentServerInfo.Port);
        }

        public void Disconnect()
        {
            if(IsConnected)
                Client.Disconnect();
        }

        private void PurgeClient()
        {
            CurrentServerInfo = null;
            Client.Dispose();
            Client = null;
            if (HostnameUpdated != null)
                HostnameUpdated(this, new HostnameEventArgs(){NewHostname = "", Timestamp = DateTime.Now});
            CurrentPlayerCountUpdated(this, new PlayerCountEventArgs(){PlayerCount = 0});
        }

        public async Task Run()
        {
            IsRunning = true;
            while(IsRunning)
            {
                if(IsConnected)
                {
                    await Client.Update();
                }
                await Task.Delay(10);
            }
        }

        #region Commands

        public async Task Update()
        {
            await Client.Update();
        }
        public void RequestAuth(string password)
        {
            Client.SendPacket(new Packet(Opcode.Auth, PacketType.Auth, password));
        }

        public bool ExecCommand(Opcode code, string command)
        {
            if(!IsConnected)
                return false;

            Client.SendPacket(new Packet(code, PacketType.ExecCommand, command));
            return true;
        }

        public bool ExecCommand(string command, bool writeToConsole = false)
        {
            if(!ExecCommand(Opcode.Generic, command))
                return false;

            if(writeToConsole && ConsoleLogUpdated != null)
                ConsoleLogUpdated(this, new ConsoleLogEventArgs(){Message = "> " + command, Timestamp = DateTime.Now});

            return true;
        }

        public void ExecuteScheduledTask(string TaskName, string TaskCommand)
        {
            if (!ExecCommand(Opcode.ScheduledTask, TaskCommand))
                return;

            if (ConsoleLogUpdated != null)
                ConsoleLogUpdated(this, new ConsoleLogEventArgs() { Message = "EXECUTED SCHEDULED TASK: " + TaskName, Timestamp = DateTime.Now });
                ConsoleLogUpdated(this, new ConsoleLogEventArgs() { Message = "TASK COMMAND: " + TaskCommand, Timestamp = DateTime.Now });
        }
        
        public void Say(string message, string nickname)
        {

            var formattedMessage = (nickname == null) ? message : "(" + nickname + "): " + message;
            if(!ExecCommand(Opcode.ChatMessage, "serverchat " + formattedMessage))
                return;
        }

        public void ConsoleCommand(string message, string nickname)
        {

            var formattedMessage = (nickname == null) ? message : "(" + nickname + "): " + message;
            if (!ExecCommand(Opcode.Generic, message))
                return;

            if (ConsoleLogUpdated != null)
                ConsoleLogUpdated(this, new ConsoleLogEventArgs() { Message = formattedMessage, Timestamp = DateTime.Now });
        }

        public void Echo(string message)
        {
            ExecCommand("echo " + message);
        }

        public void GetPlayers()
        {
            if (IsConnected)
            ExecCommand(Opcode.GetPlayers, "listplayers");
        }

        public void KickPlayer(ulong steamid)
        {
            ExecCommand(Opcode.KickPlayer, "kickplayer " + steamid.ToString());
        }

        public void BanPlayer(ulong steamid)
        {
            ExecCommand(Opcode.BanPlayer, "banplayer " + steamid.ToString());
        }

        #endregion Commands

        #region Client Handlers

        private void OnReceivedPacket(object sender, PacketEventArgs args)
        {
            var packet = args.Packet;
            Console.WriteLine(packet.Type.ToString() + "," + packet.Opcode.ToString());
            if(PacketHandlers.ContainsKey(packet.Type))
                if(PacketHandlers[packet.Type].ContainsKey(packet.Opcode))
                    if(PacketHandlers[packet.Type][packet.Opcode] != null)
                        PacketHandlers[packet.Type][packet.Opcode](packet);
        }

        private void OnServerConnectionFailed(object sender, ServerConnectionEventArgs args)
        {
            args.ConnectionInfo = CurrentServerInfo;
            PurgeClient();

            if(ServerConnectionFailed != null)
                ServerConnectionFailed(this, args);
        }

        public void OnServerConnectionDropped(object sender, ServerConnectionEventArgs args)
        {
            args.ConnectionInfo = CurrentServerInfo;
            PurgeClient();

            if(ServerConnectionDropped != null)
                ServerConnectionDropped(this, args);
        }

        public void OnServerConnectionSucceeded(object sender, ServerConnectionEventArgs args)
        {
            args.ConnectionInfo = CurrentServerInfo;
            if(ServerConnectionSucceeded != null)
                ServerConnectionSucceeded(this, args);
            if (HostnameUpdated != null)
                HostnameUpdated(this, new HostnameEventArgs() { NewHostname = CurrentServerInfo.Hostname, Timestamp = DateTime.Now });
            RequestAuth(CurrentServerInfo.Password);
        }

        private void OnServerConnectionStarting(object sender, ServerConnectionEventArgs args)
        {
            args.ConnectionInfo = CurrentServerInfo;
            if(ServerConnectionStarting != null)
                ServerConnectionStarting(this, args);
        }

        private void OnServerConnectionDisconnected(object sender, ServerConnectionEventArgs args)
        {
            args.ConnectionInfo = CurrentServerInfo;
            PurgeClient();

            if(ServerConnectionDisconnected != null)
                ServerConnectionDisconnected(this, args);
        }
        #endregion Client Handlers

        #region Rcon Handlers

        private void OnServerAuthSuccess(Packet packet)
        {
            if(ServerAuthSucceeded != null)
                ServerAuthSucceeded(this, new ServerAuthEventArgs{Message = "Successfully authenticated.", Timestamp = packet.Timestamp});
            Echo("Clearing incoming packet stream.");
            GetPlayers();
        }


        private void OnServerAuthFail(Packet packet)
        {
            if(ServerAuthFailed != null)
                ServerAuthFailed(this, new ServerAuthEventArgs{Message = "Server authentication failed.  Check that your server password is correct.", Timestamp = packet.Timestamp});

            Disconnect();
        }

        private void OnConsoleLogUpdated(Packet packet)
        {
            var message = packet.DataAsString();
            if (message.Trim() == "Server received, But no response!!") message = "Command Executed Successfully!";
            
            if(packet.Opcode == Opcode.Generic || packet.Opcode == Opcode.ServerResponse)
            {
                if (ConsoleLogUpdated != null)
                    ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                    {
                        Message = "SERVER CONSOLE: " + message,
                        Timestamp = packet.Timestamp
                    });
            }

        }

        private void OnScheduledTask(Packet packet)
        {
            var message = packet.DataAsString();

            if (packet.Opcode == Opcode.ScheduledTask)
            {
                if (ConsoleLogUpdated != null)
                    ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                    {
                        Message = "TASK SERVER RESPONSE: " + message,
                        Timestamp = packet.Timestamp
                    });
            }

        }

        private void OnGetChatMessage(Packet packet)
        {
            var message = packet.DataAsString();
            if (message.Trim() == "Server received, But no response!!") return;

            if (packet.Opcode == Opcode.ChatMessage)
            {
                if (ChatLogUpdated != null)
                    if (message.StartsWith("SERVER:"))
                    {
                        ChatLogUpdated(this, new ChatLogEventArgs()
                        {
                            Message = message,
                            Timestamp = packet.Timestamp,
                            IsAdmin = true
                        });
                    }
                    else
                    {
                        ChatLogUpdated(this, new ChatLogEventArgs()
                        {
                            Message = message,
                            Timestamp = packet.Timestamp,
                            IsAdmin = false
                        });
                    }
                    
            }
        }

        private void OnKickPlayer(Packet packet)
        {
            var message = packet.DataAsString();
            if (message.Trim() == "Server received, But no response!!") return;

            if (packet.Opcode == Opcode.KickPlayer)
            {
                if (ConsoleLogUpdated != null)
                    ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                    {
                        Message = "KICK PLAYER COMMAND EXECUTED: " + message,
                        Timestamp = packet.Timestamp
                    });
                GetPlayers();
            }
        }

        private void OnBanPlayer(Packet packet)
        {
            var message = packet.DataAsString();
            if (message.Trim() == "Server received, But no response!!") return;

            if (packet.Opcode == Opcode.BanPlayer)
            {
                if (ConsoleLogUpdated != null)
                    ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                    {
                        Message = "BAN PLAYER COMMAND EXECUTED: " + message,
                        Timestamp = packet.Timestamp
                    });
                GetPlayers();
            }
        }

        private void OnGetPlayers(Packet packet)
        {
            try
            {
                var players = new List<Player>();
                var message = packet.DataAsString();
                if (message.Trim() == "No Players Connected")
                {
                    if (CurrentPlayerCountUpdated != null)
                        CurrentPlayerCountUpdated(this, new PlayerCountEventArgs() { PlayerCount = players.Count });
                    if (PlayersUpdated != null)
                        PlayersUpdated(this, new PlayersEventArgs() { Players = players });
                    if (ConsoleLogUpdated != null)
                        ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                        {
                            Message = "Server Response: No Players Connected",
                            Timestamp = packet.Timestamp
                        });
                    return;
                }
                var str = packet.DataAsString();
                string[] lines = str.Split('\n');
                string[] cleanLines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                foreach (string line in cleanLines)
                {
                    var lineData = line.Replace("...", " ");
                    string[] split1 = lineData.Split(new char[] { '.' }, 2);
                    string[] split2 = split1[1].Split(new char[] { ',' }, 2);
                    string playerNumber = split1[0].Trim();
                    string name = split2[0].Trim();
                    string steamId = split2[1].Trim();

                    players.Add(new Player
                    {
                        PlayerNumber = int.Parse(playerNumber),
                        Name = name,
                        SteamID = UInt64.Parse(steamId)
                    });

                }
                if (CurrentPlayerCountUpdated != null)
                    CurrentPlayerCountUpdated(this, new PlayerCountEventArgs() { PlayerCount = players.Count });
                if (PlayersUpdated != null)
                    PlayersUpdated(this, new PlayersEventArgs() { Players = players });
                if (ConsoleLogUpdated != null)
                    ConsoleLogUpdated(this, new ConsoleLogEventArgs()
                    {
                        Message = "Server Response: Player List Updated",
                        Timestamp = packet.Timestamp
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.InnerException);
            }
        }
        

        #endregion Rcon Handlers

    }
}
