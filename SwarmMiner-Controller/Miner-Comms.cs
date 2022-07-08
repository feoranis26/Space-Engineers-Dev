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
        public class MinerComms
        {
            IMyIntergridCommunicationSystem IGC;
            IMyBroadcastListener BListener;
            IMyUnicastListener Listener;

            public string IGC_TAG = "SwarmMiner";

            public string SystemName = "Swarm Miner";

            SwarmController controller;

            public List<long> ConnectingIDs = new List<long>();

            public Program program;

            public Heartbeat hb = new Heartbeat();

            public bool AcceptNewMiners = false;

            public void Init(Program prog, IMyIntergridCommunicationSystem igc, SwarmController ctl)
            {
                IGC = igc;
                BListener = IGC.RegisterBroadcastListener(IGC_TAG);
                Listener = IGC.UnicastListener;
                controller = ctl;

                program = prog;

                hb.InitHost(program, IGC);
            }

            public void Tick(UpdateType update)
            {
                if ((update & UpdateType.Update10) > 0)
                {
                    hb.Tick();

                    foreach (Miner miner in controller.Miners)
                    {
                        if (!hb.IsConnected(miner.EntityId) && miner.State != MinerState.NotConnected)
                        {
                            Disconnect(miner);
                            continue;
                        }
                        else if (!hb.IsConnected(miner.EntityId))
                            continue;

                        miner.ProcessCommands();

                        miner.Send("Telemetry;");
                        SendSystemInfoToMiner(miner);
                    }
                }
                ProcessIncoming();
            }

            void ProcessIncoming()
            {
                while (BListener.HasPendingMessage)
                {
                    MyIGCMessage message = BListener.AcceptMessage();
                    ProcessMessage(message);
                }

                while (Listener.HasPendingMessage)
                {
                    MyIGCMessage message = Listener.AcceptMessage();
                    if (message.Tag == IGC_TAG)
                        ProcessMessage(message);
                }

                foreach (Miner m in controller.Miners)
                {
                    while (m.Listener.HasPendingMessage)
                    {
                        MyIGCMessage message = m.Listener.AcceptMessage();
                        ProcessMessage(message);
                    }
                }
            }

            void ProcessMessage(MyIGCMessage message)
            {
                if (message.Data is string)
                {
                    string msg = (string)message.Data; 
                    long id = message.Source;

                    string[] split = msg.Split(';');

                    switch (split[0])
                    {
                        case "Connection":
                            ProcessConnectionRequests(split[1], id);
                            break;
                    }

                    if (!hb.IsConnected(message.Source) || controller.GetByEntityId(id) == null)
                        return;

                    switch (split[0])
                    {
                        case "Telemetry":
                            ProcessTelemetryFeedback(split[1], id);
                            break;
                        case "Command":
                            ProcessCommandfeedback(msg, id);
                            break;
                    }
                }
                //hb.ProcessMessage(message);
            }

            void ProcessConnectionRequests(string msg, long id)
            {
                if (msg == "RequestForAvailableHosts")
                    PingIfAvailable(id);

                if (msg == "MinerConnectionRequest")
                    ConnectMiner(id);

                if (msg == "ConfirmConnection")
                    ConnectionConfirmed(id);
            }

            void ProcessTelemetryFeedback(string msg, long id)
            {
                string[] split = msg.Split('/');
                switch (split[0])
                {
                    case "IsBusy":
                        controller.GetByEntityId(id).busy = bool.Parse(split[1]);
                        break;
                    case "State":
                        controller.GetByEntityId(id).State = (MinerState)int.Parse(split[1]);
                        break;
                    case "Pos":
                        controller.GetByEntityId(id).Pos = new GlobalPos(split[1].ToVector());
                        break;
                    case "Docked":
                        controller.GetByEntityId(id).Docked = bool.Parse(split[1]);
                        break;
                }
            }

            void ProcessCommandfeedback(string msg, long id)
            {
                controller.GetByEntityId(id).OnCommandMsgReceived(msg);
            }
            void PingIfAvailable(long id)
            {
                if (AcceptNewMiners)
                {
                    IGC.SendUnicastMessage(id, IGC_TAG, "Connection;HostAvailable");
                    return;
                }

                foreach(Miner miner in controller.Miners)
                {
                    if(miner.EntityId == id)
                    {
                        IGC.SendUnicastMessage(id, IGC_TAG, "Connection;HostAvailable");
                        return;
                    }
                }
            }

            void ConnectMiner(long id)
            {
                if (hb.IsConnected(id))
                    return;

                program.Echo("Connecting miner :" + id);
                IGC.SendUnicastMessage(id, IGC_TAG, "Connection;RequestConfirm");
                ConnectingIDs.Add(id);
            }

            void ConnectionConfirmed(long id)
            {
                if (ConnectingIDs.Count == 0 || !ConnectingIDs.Contains(id))
                    return;

                if (hb.IsConnected(id))
                    return;

                foreach (Miner m in controller.Miners)
                {
                    if (m.EntityId == id)
                    {
                        controller.ReconnectExistingMiner(m);
                        IGC.SendUnicastMessage(id, IGC_TAG, "Connection;ID;" + m.ID);
                        IGC.SendUnicastMessage(id, IGC_TAG, "Connection;ConnectionSuccessful");
                        hb.AddClient(id, "SWMMiner HB " + m.ID);
                        ConnectingIDs.Remove(id);

                        return;
                    }
                }


                IGC.SendUnicastMessage(id, IGC_TAG, "Connection;ID;" + controller.Miners.Count);
                IGC.SendUnicastMessage(id, IGC_TAG, "Connection;ConnectionSuccessful");

                IMyBroadcastListener listener = IGC.RegisterBroadcastListener("SWMMiner " + controller.Miners.Count);

                hb.AddClient(id, "SWMMiner " + controller.Miners.Count);
                Miner miner = new Miner(controller.Miners.Count, id, listener);
                miner.State = MinerState.Stopped;

                miner.IGC_TAG = IGC_TAG;
                miner.IGC = IGC;

                controller.AddMiner(miner);
                //controller.MoveMinerToWaypoint(miner, 0);

                ConnectingIDs.Remove(id);
            }

            public void Disconnect(Miner miner)
            {
                ConnectingIDs.Remove(miner.EntityId);
                controller.MinerDisconnected(miner);
            }

            public void SendSystemInfoToMiner(Miner miner)
            {
                miner.Send("SystemWorldMatrix;" + program.nav.WorldMatrix.EncodeString());
            }
        }
    }
}
