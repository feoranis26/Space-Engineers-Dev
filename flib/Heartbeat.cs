using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class Heartbeat
        {
            IMyIntergridCommunicationSystem IGC;
            string IGCTag = "Heartbeat";
            public string SessionID = "";

            long ID;
            public List<HeartbeatClientData> Clients = new List<HeartbeatClientData>();
            bool isHost = false;

            int timeout = 25;
            int ticksSinceLastPing = 0;

            public Program prog;
            public void InitClient(Program p, long id, IMyIntergridCommunicationSystem igc, string tag = "Heartbeat")
            {
                ID = id;
                IGC = igc;
                IGCTag = tag;
                prog = p;
            }

            public void UninitClient()
            {
                ID = 0;
                SessionID = "";
                ticksSinceLastPing = 0;
            }

            public void InitHost(Program p, IMyIntergridCommunicationSystem igc)
            {
                IGC = igc;
                isHost = true;

                prog = p;
                SessionID = DateTime.Now.Millisecond.ToString();
            }

            public void Tick()
            {
                if (isHost)
                {
                    hostTick();
                    return;
                }

                ticksSinceLastPing++;
                if (ticksSinceLastPing > timeout)
                {
                    throw new HeartbeatTimeoutException();
                }

                IGC.SendBroadcastMessage(IGCTag, "Pong;" + SessionID);
            }

            int speen = 0;

            public void Tick1(UpdateType update)
            {
                if (prog != null)
                {
                    string str = "";
                    if (speen == 0) { str = "|"; }
                    if (speen == 1) { str = "/"; }
                    if (speen == 2) { str = "-"; }
                    if (speen == 3) { str = "\\"; }

                    prog.Echo("Connection alive! " + str + " Time: " + ticksSinceLastPing);
                }
            }
            void hostTick()
            {
                for (int i = 0; i < Clients.Count; i++)
                {
                    HeartbeatClientData data = Clients[i];
                    IGC.SendUnicastMessage(data.id, data.Listener.Tag, "Ping;" + SessionID);

                    data.ticksSinceLastResponse++;
                    if (data.ticksSinceLastResponse > timeout)
                    {
                        Clients.Remove(data);
                    }

                    while (data.Listener.HasPendingMessage)
                    {
                        ProcessMessage(data.Listener.AcceptMessage());
                    }
                }
            }

            public void ProcessMessage(MyIGCMessage message)
            {
                if (isHost)
                {
                    foreach (HeartbeatClientData data in Clients)
                    {
                        if (data.id == message.Source && (string)message.Data == "Pong;" + SessionID)
                        {
                            data.ticksSinceLastResponse = 0;
                        }
                    }
                }
                else
                {
                    if (message.Tag == IGCTag && message.Source == ID)
                    {
                        if (SessionID == "")
                            SessionID = ((string)message.Data).Split(';')[1];

                        ticksSinceLastPing = 0;
                        speen++;
                        if (speen > 3)
                        {
                            speen = 0;
                        }
                    }
                }
            }

            public void AddClient(long id, string tag)
            {
                IMyBroadcastListener listener = IGC.RegisterBroadcastListener(tag);
                Clients.Add(new HeartbeatClientData(id, listener));
            }

            public bool IsConnected(long id)
            {
                foreach (HeartbeatClientData client in Clients)
                {
                    if (client.id == id)
                        return true;
                }
                return false;
            }
        }

        public class HeartbeatClientData
        {
            public long id;
            public int ticksSinceLastResponse;
            public IMyBroadcastListener Listener;

            public HeartbeatClientData(long i, IMyBroadcastListener listener)
            {
                id = i;
                Listener = listener;
            }
        }

        public class HeartbeatTimeoutException : Exception
        {
            public HeartbeatTimeoutException()
            {
            }

            public HeartbeatTimeoutException(string message)
                : base(message)
            {
            }

            public HeartbeatTimeoutException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}
