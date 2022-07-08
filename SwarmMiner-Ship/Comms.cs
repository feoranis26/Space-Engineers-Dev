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
        public class Comms
        {
            IMyIntergridCommunicationSystem IGC;
            IMyBroadcastListener BListener;
            IMyUnicastListener Listener;

            string IGC_TAG = "SwarmMiner";
            string IGC_UNIQUE_TAG = "SWMMiner";

            public string SystemName = "";
            public long SystemID = -1;

            public ConnectionState connectionState = ConnectionState.NotConnected;

            public Program prog;
            public Actions actions;

            public Heartbeat hb = new Heartbeat();

            public Matrix systemWorldMatrix;

            public int ID = -1;

            public void Init(Program p, IMyIntergridCommunicationSystem igc, Actions a)
            {
                IGC = igc;
                BListener = IGC.RegisterBroadcastListener(IGC_TAG);
                Listener = IGC.UnicastListener;
                prog = p;
                actions = a;

                hb.prog = p;
            }

            public void ProcessIncoming()
            {
                while (Listener.HasPendingMessage)
                {
                    MyIGCMessage message = Listener.AcceptMessage();
                    hb.ProcessMessage(message);

                    if (message.Tag == IGC_TAG && message.Data is string)
                    {
                        string msg = (string)message.Data;
                        long sender = message.Source;

                        //prog.Echo("Reply from " + sender + " :" + msg);

                        string[] split = msg.Split(';');

                        switch (split[0])
                        {
                            case "Connection":
                                ProcessConnectionMSG(split[1], sender);

                                if (split[1] == "ID")
                                    ID = int.Parse(split[2]);

                                break;
                            case "SystemWorldMatrix":
                                systemWorldMatrix = split[1].ToMatrix();
                                break;
                            case "Command":
                                ProcessAction(msg);
                                break;
                            case "Telemetry":
                                SendTelemetry();
                                break;
                            case "Abort":
                                actions.Abort();
                                break;

                            case "SetState":
                                prog.state = (MinerState)int.Parse(split[1]);
                                break;
                        }
                    }
                }
            }

            public void Tick(UpdateType update)
            {
                hb.Tick1(update);
                if(connectionState != ConnectionState.Connected && (update & UpdateType.Update100) > 0)
                    LookForConnections();

                if (connectionState == ConnectionState.Connected && (update & UpdateType.Update10) > 0)
                {
                    try
                    {
                        hb.Tick();
                    }
                    catch(HeartbeatTimeoutException)
                    {
                        Disconnect();
                    }
                }

                ProcessIncoming();
            }

            int ticksSinceConnectAttempt = 0;
            public void LookForConnections()
            {
                prog.Echo("Looking for connections...");
                SearchForHosts();

                if (!Listener.HasPendingMessage & !BListener.HasPendingMessage && SystemID == -1)
                {
                    ticksSinceConnectAttempt++;
                    if(ticksSinceConnectAttempt > 10)
                    {
                        //prog.Me.
                        Disconnect();
                    }
                }

                if (SystemID != -1 && connectionState == ConnectionState.Connecting)
                    TryConnectTo(SystemID);
            }

            public void ProcessConnectionMSG(string msg, long id)
            {
                if (msg == "HostAvailable" && connectionState != ConnectionState.Connected)
                    TryConnectTo(id);

                if (connectionState == ConnectionState.Connecting && id == SystemID)
                {
                    if (msg == "RequestConfirm")
                        ConfirmConnection(id);
                    else if (msg == "ConnectionSuccessful")
                        Connected(id);
                }
            }

            public void SearchForHosts()
            {
                IGC.SendBroadcastMessage(IGC_TAG, "Connection;RequestForAvailableHosts");
            }

            public void TryConnectTo(long id)
            {
                prog.Echo("Attempting connection to: " + id);

                SystemID = id;
                connectionState = ConnectionState.Connecting;
                IGC.SendUnicastMessage(id, IGC_TAG, "Connection;MinerConnectionRequest");
                ticksSinceConnectAttempt = 0;
            }

            public void ConfirmConnection(long id)
            {
                IGC.SendUnicastMessage(id, IGC_TAG, "Connection;ConfirmConnection");
            }

            public void Connected(long id)
            {
                SystemID = id;
                connectionState = ConnectionState.Connected;

                IGC_UNIQUE_TAG = "SWMMiner " + ID;
                hb.InitClient(prog, id, IGC, "SWMMiner HB " + ID);
                prog.Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
            }

            public int currentCommand = -1;
            public void ProcessAction(string msg)
            {
                List<string> split = msg.Split(';').ToList();

                split.Reverse();
                split.Pop();

                currentCommand = int.Parse(split.Pop());
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Command;Received;" + currentCommand);

                switch (split.Pop())
                {
                    case "Goto":
                        actions.GotoCMD(msg);
                        break;

                    case "Mine":
                        actions.MineCMD(msg);
                        break;

                    case "SetRotTarget":
                        actions.RotTarget = new GlobalRot(split.Pop().ToVector(), split.Pop().ToVector());
                        break;

                    case "EnableManualControl":
                        actions.PoseTarget = null;
                        actions.PosTarget = null;
                        actions.RotTarget = null;

                        prog.motion.EnableManualControl();
                        break;

                    case "Dock":
                        actions.Dock();
                        break;

                    case "Undock":
                        actions.Undock();
                        break;
                }
            }

            public void SendTelemetry()
            {
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Telemetry;IsBusy/" + actions.busy);
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Telemetry;State/" + (int)prog.state);
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Telemetry;Pos/" + ((Vector3)prog.motion.rc.GetPosition()).EncodeString());
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Telemetry;Docked/" + actions.Docked);


                if (currentCommand != -1)
                {
                    if (!actions.busy && currentCommand != -1)
                        CMDEnd(actions.Aborted);
                    else
                        IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Command;Processing;" + (int)currentCommand);
                }
            }

            public void RequestNextCommand()
            {
                SendTelemetry();
                //IGC.SendUnicastMessage(SystemID, IGC_TAG, "Telemetry;Ready");
            }

            void Disconnect()
            {
                connectionState = ConnectionState.NotConnected;
                prog.Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;
                SystemID = -1;
                currentCommand = -1;
                hb.UninitClient();
            }

            public void CMDEnd(bool aborted = false)
            {
                string status = aborted ? "CANCEL" : "OK";
                IGC.SendBroadcastMessage(IGC_UNIQUE_TAG, "Command;" + status + ";" + currentCommand);
            }
        }
    }
}
