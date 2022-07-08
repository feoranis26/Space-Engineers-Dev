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
        public class CMDProcessor
        {

            const string SETNAME_ARGUMENT = "!setname";
            const string MOVE_MINER_TO_ARGUMENT = "%move";

            SwarmController ctl;
            MinerComms comms;

            public void Init(MinerComms c, SwarmController ct)
            {
                comms = c;
                ctl = ct;
            }
            public void Process(string arg)
            {
                comms.program.Echo(arg);
                string[] split = arg.Split(' ');
                if(split.Length == 0) { return; }
                switch (split[0])
                {
                    case SETNAME_ARGUMENT:
                        if(split.Length < 2) { return; }
                        comms.SystemName = split[1];
                        break;
                    case MOVE_MINER_TO_ARGUMENT:
                        if (split.Length < 3) { return; }
                        ctl.prog.nav.MoveMinerTo(ctl.Miners[int.Parse(split[1])], int.Parse(split[2]));
                        break;
                    case "%setw":
                        if (split.Length < 3) { return; }
                        ctl.Miners[int.Parse(split[1])].CurrentWaypointID = int.Parse(split[2]);
                        break;

                    case "%sett":
                        if (split.Length < 3) { return; }
                        ctl.Miners[int.Parse(split[1])].TargetWaypointID = int.Parse(split[2]);
                        break;

                    case "%mstate":
                        if (split.Length < 3) { return; }
                        ctl.Miners[int.Parse(split[1])].SetState((MinerState)int.Parse(split[2]));
                        break;
                    case "%manual":
                        if (split.Length < 2) { return; }
                        ctl.Miners[int.Parse(split[1])].EnableManualControl();
                        break;
                    case "%mforward":
                        if (split.Length < 2) { return; }
                        ctl.Miners[int.Parse(split[1])].SetRotation(ctl.prog.nav.GetNode(ctl.Miners[int.Parse(split[1])].CurrentWaypointID).Pose.rot.ToGlobal(ctl.nav.WorldMatrix));
                        break;
                    case "%state":
                        if (split.Length < 2) { return; }
                        Job.State = (SystemState)int.Parse(split[1]);
                        break;
                    case "%save":
                        ctl.prog.Save();
                        break;
                    case "%reset":
                        ctl.Load("*0");
                        comms.program.Storage = "*0";
                        break;
                    case "%clearCMDs":
                        if (split.Length < 2) { return; }
                        ctl.Miners[int.Parse(split[1])].CommandQueue.Clear();
                        break;
                    case "%abort":
                        if (split.Length < 2) { return; }
                        ctl.Miners[int.Parse(split[1])].Abort();
                        break;

                    case "%stop":
                        if (split.Length < 2) { return; }
                        ctl.Miners[int.Parse(split[1])].Abort();
                        ctl.Miners[int.Parse(split[1])].job = null;
                        break;

                    case "%dock":
                        if (split.Length < 2) { return; }
                        ctl.MoveMinerToDock(ctl.Miners[int.Parse(split[1])]);
                        break;

                    case "%mine":
                        if (split.Length < 2) { return; }
                        //ctl.QueueMine(ctl.Miners[int.Parse(split[1])], Job.Holes[int.Parse(split[2])]);
                        Job.AssignMiner(ctl.Miners[int.Parse(split[1])]);
                        break;

                    case "%stopMine":
                        if (split.Length < 2) { return; }
                        //ctl.QueueMine(ctl.Miners[int.Parse(split[1])], Job.Holes[int.Parse(split[2])]);
                        ctl.Miners[int.Parse(split[1])].job = null;
                        break;

                    case "%removeMiner":
                        if (split.Length < 2) { return; }
                        ctl.Miners.RemoveAt(int.Parse(split[1]));

                        foreach (Miner miner in ctl.Miners)
                            miner.ID = ctl.Miners.IndexOf(miner);

                        break;

                    case "!setJobSize":
                        if (split.Length < 4) { return; }
                        Job.Size.X = int.Parse(split[1]);
                        Job.Size.Y = int.Parse(split[2]);
                        Job.Size.Z = int.Parse(split[3]);

                        Job.GenerateHoles();
                        break;

                    case "!setHoleSize":
                        if (split.Length < 3) { return; }
                        Job.HoleSize.X = int.Parse(split[1]);
                        Job.HoleSize.Y = int.Parse(split[2]);

                        Job.GenerateHoles();
                        break;

                    case "!resetJob":
                        Job.GenerateHoles();
                        break;

                    case "Start":
                        Job.State = SystemState.Running;
                        break;

                    case "Pause":
                        Job.State = SystemState.Paused;
                        break;

                    case "Stop":
                        Job.State = SystemState.Stopped;
                        break;

                    case "Halt":
                        Job.State = SystemState.Halt;
                        break;

                    case "!setMineSpeed":
                        if (split.Length < 2) { return; }
                        Job.MiningSpeed = float.Parse(split[1]);
                        break;

                    case "%hangar":
                        if (split.Length < 2) { return; }
                        ctl.MoveMinerToHangar(ctl.Miners[int.Parse(split[1])]);
                        break;

                    case "%dock-all":
                        ctl.MoveAllMinersToDocks();
                        break;

                    case "%hangar-all":
                        ctl.MoveAllMinersToHangar();
                        break;

                    case "&acceptNewMiners":
                        ctl.prog.comms.AcceptNewMiners = true;
                        break;

                    case "%debug":
                        ctl.prog.drawDebug = true;
                        break;

                    case "%noDebug":
                        ctl.prog.drawDebug = false;
                        break;

                    case "%stopMoves":
                        ctl.nav.OrderQueue.Clear();
                        break;
                }
            }
        }
    }
}
