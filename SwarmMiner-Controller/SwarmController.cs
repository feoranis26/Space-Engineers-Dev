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
        public class SwarmController
        {
            //TODO SwarmController and Comms utility classes for use in other projects


            public List<Miner> Miners = new List<Miner>();

            public MinerComms comms;
            public Navigation nav;
            public Program prog;

            public bool AreAllMinersKnown = false;

            Dictionary<int, IMyProgrammableBlock> DockControllers = new Dictionary<int, IMyProgrammableBlock>();

            public void Init(MinerComms c, Navigation n, Program p)
            {
                comms = c;
                nav = n;
                prog = p;

                MyIni parser = new MyIni();
                MyIniParseResult res;
                if (!parser.TryParse(p.Me.CustomData, out res))
                {
                    throw new ArgumentException();
                }


                foreach (int id in nav.Docks)
                {
                    string dockName = nav.GetNode(id).DockPBName;
                    try
                    {
                        IMyProgrammableBlock PB = p.gridUtils.GetBlockWithTag<IMyProgrammableBlock>($"[SWM {dockName}]");
                        DockControllers.Add(id, PB);
                    }
                    catch(MissingBlockException)
                    {

                    }
                }
            }

            public bool IsDockAvailable(int id)
            {
                IMyProgrammableBlock ctl = DockControllers[id];

                if (ctl.CustomData.Contains("UNAVAILAVLE"))
                    return false;

                if (ctl.CustomData.Contains("CLOSED"))
                    return false;

                return true;
            }

            void Control()
            {
                if (!CheckForUnknownMiners())
                    return;

                if (Job.State == SystemState.Halt)
                    return;

                switch (Job.State)
                {
                    case SystemState.Stopped:
                        foreach (Miner m in Miners)
                        {
                            if (m.State == MinerState.NotConnected || m.State == MinerState.Unknown)
                                continue;

                            if (m.State != MinerState.CargoFull && m.State != MinerState.H2Low && m.State != MinerState.OutOfCharge && m.State != MinerState.Ejecting)
                                MoveMinerToHangar(m);
                        }
                        break;

                    case SystemState.Running:
                        RunAllMiners();

                        foreach (Miner m in Miners)
                            if (m.job == null && m.IsIdle())
                                    MoveMinerToHangar(m);

                        if (Job.IsDone())
                            Job.State = SystemState.Stopped;
                        break;
                }
            }

            public void Tick(UpdateType update)
            {
                if ((update & UpdateType.Update10) > 0)
                {
                    UpdateMiners();
                    CheckDocks();
                }

                if ((update & UpdateType.Update100) > 0)
                    Control();
            }

            public void UpdateMiners()
            {
                if (!CheckForUnknownMiners())
                    return;

                foreach (Miner miner in Miners)
                    UpdateMiner(miner);
            }

            public void CommandDone(Miner miner, string msg)
            {
                try
                {
                    List<string> split = msg.Split(';').ToList();

                    string cmd = miner.CommandQueue[int.Parse(split[2])];

                    List<string> cmd_split = cmd.Split(';').ToList();

                    if (cmd_split[2] == "Mine" && miner.job != null && cmd_split[10] == miner.job.drillSite.Node.ID.ToString())
                    {
                        miner.job.drillSite.Done = true;
                        Job.Jobs.Remove(miner.job);
                        miner.job = null;

                        miner.SetState(MinerState.Ready);
                    }
                }
                catch { prog.Echo("Problem with command feedback!"); }
            }

            public void UpdateMiner(Miner miner)
            {
                //if(IsSafeToMove())
                nav.CheckMove(miner);
                CheckResources(miner);
                CheckJob(miner);
            }

            public bool CheckForUnknownMiners()
            {
                AreAllMinersKnown = false;
                foreach (Miner miner in Miners)
                {
                    if (miner.State == MinerState.Unknown || miner.State == MinerState.NotConnected)
                        return false;
                }
                AreAllMinersKnown = true;
                return true;
            }

            List<Miner> HangarExitOrderedMiners = new List<Miner>();

            public void MoveMinerToHangar(Miner miner)
            {
                if (nav.HangarDocks.Contains(miner.CurrentWaypointID))
                    return;

                foreach (MoveOrder o in nav.OrderQueue)
                    if (nav.HangarDocks.Contains(o.nodeID))
                        return;

                int dockID = nav.GetEmptyHangarDock(miner);

                if (dockID != -1 && !miner.busy)
                {
                    nav.MoveMinerTo(miner, dockID);
                }
            }

            public void MoveMinerToDock(Miner miner)
            {
                if (nav.Docks.Contains(miner.CurrentWaypointID))
                    return;

                foreach(MoveOrder o in nav.OrderQueue)
                    if (nav.Docks.Contains(o.nodeID))
                        return;

                int dockID = nav.GetEmptyDock(miner);

                if (dockID != -1 && !miner.busy)
                {
                    nav.MoveMinerTo(miner, dockID);
                }
            }

            public void MoveAllMinersToDocks()
            {
                foreach (Miner m in Miners)
                    if (m.IsIdle())
                        MoveMinerToDock(m);
            }

            public void MoveAllMinersToHangar()
            {
                foreach (Miner m in Miners)
                    if (m.IsIdle())
                        MoveMinerToHangar(m);
            }

            public void RunAllMiners()
            {
                foreach (Miner m in Miners)
                    if (m.job == null && (m.State == MinerState.Ready || m.State == MinerState.Active) && m.IsIdle())
                        Job.AssignMiner(m);
            }

            List<int> docksToReserve = new List<int>();
            void CheckDocks()
            {
                docksToReserve.Clear();

                foreach (Miner miner in Miners)
                {
                    if (miner.TargetWaypointID != miner.CurrentWaypointID && miner.Docked)
                        miner.Undock();

                    if (nav.Docks.Contains(miner.TargetWaypointID))
                        docksToReserve.Add(miner.TargetWaypointID);

                    if ((nav.HangarDocks.Contains(miner.CurrentWaypointID) || nav.Docks.Contains(miner.CurrentWaypointID)) && miner.TargetWaypointID == miner.CurrentWaypointID && !miner.Docked && !miner.busy)
                        miner.Dock();

                    if(!miner.busy && nav.Docks.Contains(miner.CurrentWaypointID) && DockControllers.ContainsKey(miner.CurrentWaypointID))
                        DockControllers[miner.CurrentWaypointID].TryRun("%shipArrived");
                }

                /*
                foreach (Miner miner in Miners)
                {
                    if (nav.Docks.Contains(miner.TargetWaypointID))
                    {
                        if (miner.TargetWaypointID != miner.CurrentWaypointID)
                        {
                            docksToReserve.Add(miner.TargetWaypointID);

                            if (miner.Docked)
                                miner.Undock();
                        }
                        else if (!miner.Docked && !miner.busy)
                            miner.Dock();
                    }
                    else if (miner.Docked && !miner.busy)
                        miner.Undock();
                }

                */
                foreach (MoveOrder o in nav.OrderQueue)
                    if (nav.Docks.Contains(o.nodeID))
                        docksToReserve.Add(o.nodeID);

                foreach (int dock in nav.Docks)
                {
                    if (!DockControllers.ContainsKey(dock))
                        continue;

                    if (docksToReserve.Contains(dock))
                        DockControllers[dock].TryRun("%reserve");
                    else
                        DockControllers[dock].TryRun("%remove_reserve");
                }
            }

            void CheckResources(Miner miner)
            {
                if (miner.State == MinerState.H2Low || miner.State == MinerState.OutOfCharge || miner.State == MinerState.CargoFull || miner.State == MinerState.Ejecting)
                {
                    if (!nav.IsMovePending(miner) && miner.CurrentWaypointID == miner.TargetWaypointID)
                    {
                        if (miner.busy)
                            miner.Abort();

                        if(miner.State != MinerState.Ejecting)
                            MoveMinerToDock(miner);
                    }
                }
            }

            void CheckJob(Miner miner)
            {
                if (miner.job != null && miner.CurrentWaypointID != miner.job.drillSite.Node.ID && miner.CurrentWaypointID == miner.TargetWaypointID && !miner.IsIdle())
                    miner.Abort(); //Miner is in wrong hole

                if (miner.job != null && miner.CurrentWaypointID == miner.job.drillSite.Node.ID && miner.CurrentWaypointID == miner.TargetWaypointID && !miner.IsIdle() && (Job.State == SystemState.Stopped || Job.State == SystemState.Paused))
                {
                    miner.Abort(); //System is paused or stopped
                    return;
                }

                if (miner.job == null)
                    return;

                if (miner.State == MinerState.CargoFull || miner.State == MinerState.H2Low || miner.State == MinerState.OutOfCharge || miner.State == MinerState.Ejecting)
                    return;

                if (!miner.IsIdle())
                    return;

                if (Job.State != SystemState.Running)
                    return;


                //prog.Echo("MINER #" + miner.ID);
                //prog.Echo("MINER JOB? " + miner.job);
                //prog.Echo("MINER JOB HOLE" + miner.job.drillSite);
                //prog.Echo("MINER JOB HOLE NUM " + miner.job.drillSite.Node.ID);



                miner.SetState(MinerState.Active);

                if (miner.CurrentWaypointID != miner.job.drillSite.Node.ID)
                {
                    nav.MoveMinerTo(miner, miner.job.drillSite.Node.ID);
                }
                else
                {
                    QueueMine(miner, miner.job.drillSite);
                }
            }

            public void QueueMine(Miner miner, DrillSite site)
            {
                string msg = "Mine;";
                msg += site.Pos.ToGlobal(nav.WorldMatrix).pos.EncodeString() + ";";

                GlobalRot rot = new LocalRot(Vector3.Forward, Vector3.Up).ToGlobal(nav.WorldMatrix);
                msg += rot.forward.EncodeString() + ";";
                msg += rot.up.EncodeString() + ";";

                msg += site.Depth.ToString() + ";";

                msg += Job.MiningSpeed.ToString() + ";";
                msg += 10.ToString() + ";";
                msg += 15.ToString() + ";";
                msg += site.Node.ID.ToString() + ";";

                miner.QueueCommand(msg);
            }

            public Miner GetById(long id)
            {
                foreach (Miner m in Miners)
                {
                    if (m.ID == id)
                    {
                        return m;
                    }
                }
                return null;
            }

            public Miner GetByEntityId(long id)
            {
                foreach (Miner m in Miners)
                {
                    if (m.EntityId == id)
                    {
                        return m;
                    }
                }
                return null;
            }

            public void AddMiner(Miner miner)
            {
                Miners.Add(miner);
            }
            public void ReconnectExistingMiner(Miner miner)
            {
                miner.SetState(miner.prevState);
            }

            public void MinerDisconnected(Miner miner)
            {
                if (miner.State != MinerState.Unknown && miner.State != MinerState.NotConnected)
                    miner.prevState = miner.State;

                miner.State = MinerState.NotConnected;
            }

            public int ConnectedMiners
            {
                get
                {
                    int amount = 0;
                    foreach (Miner miner in Miners)
                    {
                        if (miner.State != MinerState.NotConnected)
                        {
                            amount++;
                        }
                    }
                    return amount;
                }
            }

            public int BusyMiners
            {
                get
                {
                    int amount = 0;
                    foreach (Miner miner in Miners)
                    {
                        if (miner.busy)
                        {
                            amount++;
                        }
                    }
                    return amount;
                }
            }

            public bool IsSafeToMove()
            {
                /*foreach (Miner miner in Miners)
                {
                    if (miner.TargetWaypointID != miner.CurrentWaypointID && miner.TargetWaypointID != -999)
                    {
                        return false;
                    }
                }*/
                return true;
            }

            public bool IsNodeOccupied(int nodeID)
            {
                foreach (Miner miner in Miners)
                    if (miner.CurrentWaypointID == nodeID)
                        return true;
                return false;
            }

            public string Save()
            {
                string str = "";
                foreach (Miner miner in Miners)
                    str += miner.EncodeString() + "|";

                str += "*";

                str += ((int)Job.State).ToString();

                return str;
            }

            public void Load(string data)
            {
                Miners.Clear();
                string[] split = data.Split('*');

                if (split.Length > 0)
                {

                    string[] minerData = split[0].Split('|');

                    foreach (string minerD in minerData)
                    {
                        if (minerD == "")
                            continue;

                        Miner m = new Miner(minerD, prog.IGC);

                        m.IGC_TAG = comms.IGC_TAG;
                        m.IGC = prog.IGC;

                        Miners.Add(m);
                    }

                    Job.State = (SystemState)int.Parse(split[1]);
                }
            }
        }
    }
}
