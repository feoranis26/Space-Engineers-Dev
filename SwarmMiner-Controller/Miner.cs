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
        public class Miner
        {
            public long EntityId;
            public long ID;

            public MinerState State = MinerState.Unknown;
            public MinerState prevState = MinerState.Unknown;

            public List<string> CommandQueue = new List<string>();

            public bool busy = false;
            public int CurrentWaypointID = 0;
            public Path? path;
            public int TargetWaypointID = -999;

            public bool Docked = false;

            public Vector2I HoleNum = new Vector2I(0, 0);

            public string IGC_TAG;
            public IMyIntergridCommunicationSystem IGC;

            public int currentCommandNum = 0;

            public string lastSentCommand;

            public SwarmController ctl;

            public GlobalPos Pos;

            public MiningJob job;

            public IMyBroadcastListener Listener;

            public Miner(long id, long entityId, IMyBroadcastListener listener)
            {
                ID = id;
                EntityId = entityId;

                ctl = ProgHelper.prog.controller;

                Listener = listener;
            }

            public string EncodeString()
            {
                string str = "";

                str += EntityId.ToString() + "&";
                str += ID.ToString() + "&";

                MinerState st = State != MinerState.NotConnected ? State : (prevState != MinerState.NotConnected ? prevState : MinerState.Unknown);
                str += ((int)st).ToString() + "&";

                ProgHelper.prog.Echo("Saving queue");

                List<string> tempQueue = new List<string>(CommandQueue);
                tempQueue.RemoveRange(0, currentCommandNum);
                foreach (string cmd in tempQueue)
                    str += cmd + "$";

                str += "&";

                ProgHelper.prog.Echo("Saved queue");

                str += busy.ToString() + "&";
                str += CurrentWaypointID.ToString() + "&";
                str += TargetWaypointID.ToString() + "&";

                return str;
            }

            public Miner(string data, IMyIntergridCommunicationSystem IGC)
            {
                ProgHelper.prog.Echo(data);
                List<String> fields = data.Split('&').ToList();

                fields.Reverse();

                string eID = fields.Pop();
                ProgHelper.prog.Echo(eID);
                EntityId = long.Parse(eID);
                ProgHelper.prog.Echo("Parsed EntityID");

                ID = int.Parse(fields.Pop());
                ProgHelper.prog.Echo("Parsed ID");

                prevState = (MinerState)int.Parse(fields.Pop());
                State = MinerState.NotConnected;
                ProgHelper.prog.Echo("Parsed State");

                string[] commands = fields.Pop().Split('$');

                foreach (string cmd in commands)
                    CommandQueue.Add(cmd);

                ProgHelper.prog.Echo("Parsed command queue");

                //Make busy default to true until miner connects to prevent commands being sent until miner is actually connnected.
                busy = true; fields.Pop();// bool.Parse(fields.Pop());
                //ProgHelper.prog.Echo("Parsed busy");

                CurrentWaypointID = int.Parse(fields.Pop());
                ProgHelper.prog.Echo("Parsed current waypoint");

                TargetWaypointID = int.Parse(fields.Pop());
                ProgHelper.prog.Echo("Parsed target waypoint");

                ctl = ProgHelper.prog.controller;

                if (CurrentWaypointID >= 5000 && ctl.nav.GetNode(CurrentWaypointID) == null)
                    CurrentWaypointID = ctl.nav.Jobsite;

                if (TargetWaypointID >= 5000 && ctl.nav.GetNode(TargetWaypointID) == null)
                    TargetWaypointID = ctl.nav.Jobsite;

                Listener = IGC.RegisterBroadcastListener("SWMMiner " + ID);
            }

            public void Send(string message)
            {
                IGC.SendUnicastMessage(EntityId, IGC_TAG, message);
            }

            public void ProcessCommands()
            {
                if (!busy)
                {
                    if (CommandQueue.Count > 0 && currentCommandNum < CommandQueue.Count)
                    {
                        if (!isCommand(CommandQueue[currentCommandNum]))
                        {
                            CommandQueue.RemoveAt(currentCommandNum);
                            return;
                        }

                        lastSentCommand = CommandQueue[currentCommandNum];
                        Send(CommandQueue[currentCommandNum]);
                    }
                }
            }

            public bool isCommand(string msg)
            {
                List<string> split = msg.Split(';').ToList();

                if (split.Count < 3)
                    return false;

                int line = -1;
                bool parsed = int.TryParse(split[1], out line);
                return split[0] == "Command" && line != -1 && line < CommandQueue.Count && parsed;
            }

            public void OnCommandMsgReceived(string msg)
            {
                List<string> split = msg.Split(';').ToList();

                if (int.Parse(split[2]) >= CommandQueue.Count)
                    return;

                switch (split[1])
                {
                    case "Received":
                        //currentCommandNum = int.Parse(split[2]) + 1;
                        busy = true;
                        break;

                    case "OK":
                        ctl.CommandDone(this, msg);

                        currentCommandNum = int.Parse(split[2]) + 1;
                        break;

                    case "CANCEL":
                        currentCommandNum = int.Parse(split[2]) + 1;
                        break;

                    case "Processing":
                        currentCommandNum = int.Parse(split[2]);
                        break;
                }
            }

            public bool IsIdle()
            {
                if (State == MinerState.NotConnected || State == MinerState.Unknown)
                    return false;

                if (busy)
                    return false;

                if (TargetWaypointID != CurrentWaypointID)
                    return false;

                if (ctl.nav.IsMovePending(this))
                    return false;

                if (currentCommandNum < CommandQueue.Count)
                    return false;

                if (Docked && (State == MinerState.CargoFull || State == MinerState.H2Low || State == MinerState.OutOfCharge))
                    return false;

                return true;
            }

            public void QueueCommand(string cmd_msg)
            {
                CommandQueue.Add("Command;" + CommandQueue.Count + ";" + cmd_msg);
            }


            public void SetState(MinerState state)
            {
                Send("SetState;" + (int)state);
            }

            public void SetRotation(GlobalRot rot)
            {
                QueueCommand($"SetRotTarget;{rot.forward.EncodeString()};{rot.up.EncodeString()}");
            }

            public void EnableManualControl()
            {
                QueueCommand("EnableManualControl");
            }

            public void Dock()
            {
                QueueCommand("Dock");
            }

            public void Undock()
            {
                QueueCommand("Undock");
            }

            public void Abort()
            {
                Send("Abort");
            }
        }
    }
}
