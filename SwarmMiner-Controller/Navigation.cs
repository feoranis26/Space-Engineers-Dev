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
        public class Navigation
        {
            float PATH_NODE_DIST_TGT = 8;
            float FINAL_DIST_TGT = 0.5f;

            public List<PathNode> Nodes = new List<PathNode>();
            public List<int> HangarNodes = new List<int>();
            public int Jobsite;


            public List<int> HangarDocks = new List<int>();
            public List<int> Docks = new List<int>();


            public Matrix WorldMatrix;

            GridUtils gridUtils;

            Program prog;

            public List<MoveOrder> OrderQueue = new List<MoveOrder>();

            Logger logger;

            public void Init(Program p, GridUtils g)
            {
                gridUtils = g;

                WorldMatrix = gridUtils.GetBlockWithTag<IMyRemoteControl>("[SWMRoot]").WorldMatrix;

                prog = p;

                logger = new Logger(p.log, "NAV");

                ParseNodes(p.Me.CustomData);
            }

            public void Tick()
            {
                WorldMatrix = gridUtils.GetBlockWithTag<IMyRemoteControl>("[SWMRoot]").WorldMatrix;

                ExecuteOrders();
            }

            public void DrawVisuals()
            {
                if (prog.controller.Miners.Count > 0)
                {
                    foreach (Miner miner in prog.controller.Miners)
                    {
                        if (miner.TargetWaypointID != -1 && GetNode(miner.TargetWaypointID) != null)
                            prog.debug.DrawLine(GetNode(miner.CurrentWaypointID).Pose.pos.ToGlobal(WorldMatrix).pos, GetNode(miner.TargetWaypointID).Pose.pos.ToGlobal(WorldMatrix).pos, Color.Pink, 0.1f);

                        if (miner.path.HasValue && (bool)miner.path?.isObstructed)
                        {
                            continue;
                        }
                        else if (miner.path.HasValue)
                        {
                            Color color = DateTime.Now.Millisecond < 500 ? Color.Red : Color.Yellow;

                            prog.debug.DrawLine(miner.Pos.pos, GetNode((int)miner.path?.path[0]).Pose.pos.ToGlobal(WorldMatrix).pos, color, 0.25f);

                            if (miner.path?.path.Count > 1)
                                VisualizePath(miner, 0);
                        }
                    }

                    foreach (MoveOrder o in OrderQueue)
                    {
                        Color color = DateTime.Now.Millisecond % 500 < 250 ? Color.Red : Color.Orange;

                        if (!o.isObstructed)
                            prog.debug.DrawLine(o.miner.Pos.pos, GetNode(o.nodeID).Pose.pos.ToGlobal(WorldMatrix).pos, Color.Purple, 0.1f);
                        else
                            prog.debug.DrawLine(GetNode(o.miner.CurrentWaypointID).Pose.pos.ToGlobal(WorldMatrix).pos, GetNode(o.nodeID).Pose.pos.ToGlobal(WorldMatrix).pos, color, 0.1f);
                    }
                }

                foreach (PathNode node in Nodes)
                    Visualize(node);
            }
            void Visualize(PathNode node)
            {
                Vector3 pos = node.Pose.pos.ToGlobal(WorldMatrix).pos;
                foreach (int child_id in node.Children)
                {
                    Vector3 pos2 = GetNode(child_id).Pose.pos.ToGlobal(WorldMatrix).pos;
                    prog.debug.DrawLine(pos, pos2, Color.LimeGreen, 0.05f);
                }

                if (GetNode(node.ParentID) != null)
                    prog.debug.DrawLine(pos, GetNode(node.ParentID).Pose.pos.ToGlobal(WorldMatrix).pos, Color.GreenYellow, 0.05f);

                //prog.debug.DrawGPS(node.DisplayName, node.Pose.pos.ToGlobal(WorldMatrix).pos, Color.White);
                prog.debug.DrawLine(pos, pos + node.Pose.rot.ToGlobal(WorldMatrix).forward, Color.Blue, 0.05f);
                prog.debug.DrawLine(pos, pos + node.Pose.rot.ToGlobal(WorldMatrix).up, Color.Blue, 0.05f);
            }
            void ParseNodes(string data)
            {
                MyIni p = new MyIni();
                MyIniParseResult res;
                if (!p.TryParse(data, out res))
                {
                    throw new ArgumentException();
                }

                List<string> sections = new List<string>();
                p.GetSections(sections);

                foreach (string section_name in sections)
                    if (section_name.Split(' ')[0] == "Node")
                        ParseNode(section_name, p);


                string[] hDockIDs = p.Get("Config", "HangarDocks").ToString().Split(',');
                string[] dockIDs = p.Get("Config", "DockNodes").ToString().Split(',');


                foreach (string hDockID in hDockIDs)
                    HangarDocks.Add(int.Parse(hDockID));

                foreach (string dockID in dockIDs)
                    Docks.Add(int.Parse(dockID));

                Jobsite = p.Get("Config", "jobsite").ToInt32();
            }

            void ParseNode(string nodeSection, MyIni ini)
            {
                LocalPos Position = new LocalPos(ini.Get(nodeSection, "Position").ToString().ToVector());

                Vector3 rotUp = Vector3.Up;

                try
                {
                    if (ini.Get(nodeSection, "Up").ToString() != "")
                        rotUp = ini.Get(nodeSection, "Up").ToString().ToVector();
                }
                catch { }

                LocalRot Rotation = new LocalRot(ini.Get(nodeSection, "Forward").ToString().ToVector(), rotUp);

                string[] reachableNodesStr = ini.Get(nodeSection, "ReachableNodeIDs").ToString().Split(',');

                List<int> ReachableNodes = new List<int>();

                foreach (string nodeId in reachableNodesStr)
                {
                    int id;
                    if (int.TryParse(nodeId, out id))
                        ReachableNodes.Add(id);
                }

                int ID = int.Parse(ini.Get(nodeSection, "NodeID").ToString());
                int pID = int.Parse(ini.Get(nodeSection, "ParentNodeID").ToString());
                string dsp_name = ini.Get(nodeSection, "DisplayName").ToString();

                PathNode node = new PathNode(new LocalPose(Position, Rotation), ID, pID, ReachableNodes, dsp_name);

                node.DockPBName = ini.Get(nodeSection, "DockName").ToString();
                node.precision = (ini.Get(nodeSection, "Precision").ToString() != "") ? (float)ini.Get(nodeSection, "Precision").ToDouble() : 8f;

                Nodes.Add(node);
            }

            public PathNode GetNode(int id)
            {
                foreach (PathNode node in Nodes)
                {
                    if (node.ID == id)
                        return node;
                }

                return null;
            }

            public Vector3 GetSystemPosition()
            {
                return WorldMatrix.Translation;
            }

            bool IsNodeChildrenOf(int childrenID, PathNode node)
            {
                if (node.ID == childrenID)
                    return true;

                bool found = false;
                foreach (int id in node.Children)
                {
                    if (id == childrenID)
                        return true;

                    found = found || IsNodeChildrenOf(childrenID, GetNode(id));
                }

                return found;
            }

            public List<int> temp_waypoints = new List<int>();
            public List<int> FindPathTo(PathNode start, PathNode end, long minerID = -1)
            {
                List<int> ans = new List<int>();
                ans.Add(start.ID);

                if (start.ID == end.ID)
                {
                    temp_waypoints.Clear();
                    return ans;
                }
                if (IsNodeChildrenOf(end.ID, start))
                {
                    foreach (int id in start.Children)
                    {
                        if (!prog.controller.IsNodeOccupied(id) && IsNodeChildrenOf(end.ID, GetNode(id)) && !temp_waypoints.Contains(id) && !NodeHasTraffic(id, minerID))
                        {
                            if (end.ID == id)
                            {
                                ans.Add(id);
                                temp_waypoints.Clear();
                                return ans;
                            }
                            else
                            {
                                temp_waypoints.Add(id);

                                List<int> path = FindPathTo(GetNode(id), end, minerID);
                                if (path != null)
                                    return ans.Concat(path).ToList();
                            }
                        }
                    }
                }
                else if (start.ParentID != -1)
                {
                    if (!prog.controller.IsNodeOccupied(start.ParentID) && !temp_waypoints.Contains(start.ParentID) && !NodeHasTraffic(start.ParentID, minerID))
                    {

                        temp_waypoints.Add(start.ParentID);

                        List<int> path = FindPathTo(GetNode(start.ParentID), end, minerID);
                        if (path != null)
                            return ans.Concat(path).ToList();
                    }
                }
                return null;
            }

            public int GetEmptyHangarDock(Miner miner)
            {
                bool dockEmpty;
                foreach (int dock in HangarDocks)
                {
                    dockEmpty = true;
                    foreach (Miner m in prog.controller.Miners)
                    {
                        if (m.ID != miner.ID && (m.CurrentWaypointID == dock || m.TargetWaypointID == dock))
                        {
                            dockEmpty = false;
                            break;
                        }
                    }

                    foreach (MoveOrder o in OrderQueue)
                    {
                        if (o.nodeID == dock)
                        {
                            dockEmpty = false;
                            break;
                        }
                    }
                    if (dockEmpty)
                    {
                        return dock;
                    }
                }

                return 0;
            }

            public int GetEmptyDock(Miner miner)
            {
                bool dockEmpty;
                foreach (int dock in Docks)
                {
                    dockEmpty = true;
                    foreach (Miner m in prog.controller.Miners)
                    {
                        if (m.ID != miner.ID && (m.CurrentWaypointID == dock || m.TargetWaypointID == dock || !prog.controller.IsDockAvailable(dock)))
                        {
                            dockEmpty = false;
                            break;
                        }
                    }

                    foreach (MoveOrder o in OrderQueue)
                    {
                        if (o.nodeID == dock)
                        {
                            dockEmpty = false;
                            break;
                        }
                    }

                    if (dockEmpty)
                    {
                        return dock;
                    }
                }

                return -1;
            }

            void QueueGoto(Miner miner, GlobalPose pose, int? nodeID = null, float precision = 5)
            {
                string msg = "Goto;";
                msg += pose.pos.pos.EncodeString() + ";";

                GlobalRot rot = pose.rot;
                msg += rot.forward.EncodeString() + ";";
                msg += rot.up.EncodeString() + ";";

                if (nodeID.HasValue)
                    msg += nodeID + ";";

                msg += precision.ToString() + ";";

                miner.QueueCommand(msg);
            }


            bool QueueGotoWaypoint(Miner miner, int waypointID)
            {
                QueueGoto(miner, GetNode(waypointID).Pose.ToGlobal(WorldMatrix), waypointID, GetNode(waypointID).precision);
                return true;
            }

            void CheckPath(Miner miner)
            {
                if (miner.TargetWaypointID != -999 && miner.CurrentWaypointID != miner.TargetWaypointID && !miner.busy && !miner.path.HasValue)
                {
                    logger.Log($"Attempting to find path for miner {miner.ID}");
                    temp_waypoints.Clear();
                    List<int> path = FindPathTo(GetNode(miner.CurrentWaypointID), GetNode(miner.TargetWaypointID), miner.ID);
                    if(path != null)
                        miner.path = new Path(path);
                    else
                        logger.Log($"No path found!");

                }
            }

            bool MoveMiner(MoveOrder o)
            {
                if (!prog.controller.IsSafeToMove())
                    return false;

                temp_waypoints.Clear();
                List<int> path = FindPathTo(GetNode(o.miner.CurrentWaypointID), GetNode(o.nodeID), o.miner.ID);

                Path p = new Path(path);

                bool pathPresent = path != null;

                bool ok = pathPresent && IsPathClear(p, o.miner.ID);
                if (ok)
                {
                    o.miner.TargetWaypointID = o.nodeID;
                    logger.Log($"Starting move of miner {o.miner.ID} to {o.nodeID}");
                }
                else if (pathPresent)
                {
                    o.isObstructed = true;
                    o.displayStatus = "OBSTRUCTED";
                }
                else
                {
                    prog.display = $"PATH FROM {o.miner.CurrentWaypointID} to {o.nodeID} NOT FOUND!";
                    o.displayStatus = "NO PATH";
                }

                return ok;
            }

            void ExecuteOrders()
            {
                //prog.Echo("Executing orders!");
                foreach (MoveOrder o in OrderQueue)
                {
                    if (MoveMiner(o))
                    {
                        OrderQueue.Remove(o);
                        break;
                    }
                }
            }

            public void MoveMinerTo(Miner miner, int waypointID)
            {
                //if (!prog.controller.IsSafeToMove())
                //return false;

                OrderQueue.Add(new MoveOrder(miner, waypointID));
            }

            public void CheckMove(Miner miner)
            {
                CheckPath(miner);

                if (miner.CurrentWaypointID == miner.TargetWaypointID)
                    return;

                if (!miner.path.HasValue || (bool)miner.path?.isObstructed)
                {
                    if (miner.path.HasValue)
                    {
                        logger.Log($"Path of miner {miner.ID} is obstructed!");

                        string path = "";
                        foreach (int nodeID in miner.path?.path)
                            path += nodeID + ", ";
                        logger.Log($"Path is {path}");
                    }
                    else
                        logger.Log($"Path for miner {miner.ID} not found!");



                    return;
                }

                float dist_tgt = miner.path?.path.Count() > 1 ? PATH_NODE_DIST_TGT : FINAL_DIST_TGT;
                int NextNodeID = (int)miner.path?.path[0];

                if (miner.Docked)
                    return;

                if (!miner.busy/* && !NodeIsOccupied(NextNodeID, miner.ID) && (miner.path?.path.Count <= 1 || !NodeIsOccupied((int)miner.path?.path[1], miner.ID))*/)
                {
                    QueueGotoWaypoint(miner, NextNodeID);
                    logger.Log($"Moving {miner.ID} to {NextNodeID}");
                }/*
                else
                {
                    prog.display = "NEXT NODE OCCUPIED FOR MINER" + miner.ID;
                    return;
                }*/
                if (Vector3.Distance(miner.Pos.pos, GetNode(NextNodeID).Pose.pos.ToGlobal(WorldMatrix).pos) < GetNode(NextNodeID).precision)
                {
                    logger.Log($"Miner {miner.ID} arrived at {NextNodeID}");

                    miner.CurrentWaypointID = NextNodeID;
                    miner.path?.path.RemoveAt(0);

                    if (miner.path?.path.Count == 0)
                        miner.path = null;
                }
            }

            void VisualizePath(Miner miner, int index)
            {
                Color color = DateTime.Now.Millisecond < 500 ? Color.Red : Color.Yellow;

                PathNode current = GetNode((int)miner.path?.path[index]);
                PathNode next = GetNode((int)miner.path?.path[index + 1]);


                prog.debug.DrawLine(current.Pose.pos.ToGlobal(WorldMatrix).pos, next.Pose.pos.ToGlobal(WorldMatrix).pos, color, 0.1f);

                if (miner.path?.path.Count > index + 2)
                    VisualizePath(miner, index + 1);
            }

            public bool IsMovePending(Miner miner)
            {
                foreach (MoveOrder o in OrderQueue)
                    if (o.miner == miner)
                        return true;
                return false;
            }

            public bool NodeHasTraffic(int NodeID, long minerID = -1)
            {
                foreach (MoveOrder o in OrderQueue)
                {
                    if (!o.isObstructed && (o.nodeID == NodeID || o.miner.CurrentWaypointID == NodeID) && o.miner.ID != minerID)
                        return true;
                }

                foreach (Miner miner in prog.controller.Miners)
                {
                    if (miner.ID == minerID)
                        continue;

                    if (miner.CurrentWaypointID == NodeID || miner.TargetWaypointID == NodeID)
                        return true;

                    if (miner.path.HasValue && miner.path.Value.path != null && miner.path.Value.path.Count > 0)
                        foreach (int nID in miner.path.Value.path)
                        {
                            //if (nID == Jobsite || GetNode(nID).precision >= 99)
                            //    continue; //If node is a ramp area ignore it

                            if (nID == NodeID)
                                return true;
                        }
                }

                return false;
            }

            public bool NodeIsOccupied(int NodeID, long minerID = -1)
            {
                foreach (Miner miner in prog.controller.Miners)
                {
                    if (miner.ID == minerID)
                        continue;

                    if (miner.CurrentWaypointID == NodeID)
                        return true;

                    
                    if (miner.path.HasValue && miner.path.Value.path != null && miner.path.Value.path.Count > 0)
                    {
                        if (miner.path?.path[0] == NodeID)
                            return true;
                        continue;
                        if (miner.path?.path.Count > 1 && miner.path?.path[1] == NodeID)
                            return true;
                    }
                }

                return false;
            }

            public bool IsPathClear(Path path, long minerID = -1)
            {
                foreach (int id in path.path)
                {
                    //if (path.path.IndexOf(id) == 0 || id == Jobsite || GetNode(id).precision >= 99)
                    //    continue; //If node is a ramp area ignore it

                    if (NodeHasTraffic(id, minerID))
                        return false;
                }

                return true;
            }


            public string Save()
            {
                string str = "";
                foreach (MoveOrder ord in OrderQueue)
                    str += $"{ord.miner.ID}?{ord.nodeID}=";
                return str;
            }

            public void Load(string str)
            {
                string[] split = str.Split('=');

                if (split.Length == 0)
                    return;

                foreach (string cmd in split)
                {
                    if (cmd.Length <= 2)
                        continue;

                    string[] cmd_split = cmd.Split('?');

                    int minerID = int.Parse(cmd_split[0]);
                    int tgtNodeID = int.Parse(cmd_split[1]);

                    if (tgtNodeID >= 5000 && GetNode(tgtNodeID) == null)
                        tgtNodeID = Jobsite;

                    if (prog.controller.GetById(minerID) != null && GetNode(tgtNodeID) != null)
                        MoveMinerTo(prog.controller.GetById(minerID), tgtNodeID);
                }
            }
        }

        public class MoveOrder
        {
            public Miner miner;
            public int nodeID;
            public bool isObstructed;
            public string displayStatus;

            public MoveOrder(Miner m, int n)
            {
                miner = m;
                nodeID = n;
                isObstructed = false;
                displayStatus = "IN QUEUE";
            }
        }
    }
}
