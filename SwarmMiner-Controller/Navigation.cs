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

            public IEnumerator pathfinder;
            public List<PathfindData> PathfindQueue = new List<PathfindData>();
            public Dictionary<PathfindData, List<int>> FoundPaths = new Dictionary<PathfindData, List<int>>();
            public Dictionary<int, long> ReservedNodes = new Dictionary<int, long>();
            public Dictionary<long, List<int>> NodesReservedByMiner = new Dictionary<long, List<int>>();

            IEnumerator CommandQueueExecuter;

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

                RunPathfinder();
                RunCommandQueue();
            }

            void RunCommandQueue()
            {
                if (CommandQueueExecuter == null)
                    CommandQueueExecuter = ExecuteOrders();
                else
                {
                    if (!CommandQueueExecuter.MoveNext())
                        CommandQueueExecuter = null;
                }
            }

            void RunPathfinder()
            {
                if (pathfinder != null)
                {
                    if (!pathfinder.MoveNext())
                    {
                        pathfinder = null;

                        /*logger.Log($"Pathfinder done!");

                        string pathstr = "";
                        foreach (int nodeID in FoundPaths.Last().Value)
                            pathstr += nodeID + ", ";
                        logger.Log($"Path is {pathstr}");
                        */

                        PathfindQueue.Remove(FoundPaths.Last().Key);
                    }
                }
                else if (PathfindQueue.Count > 0)
                {
                    PathfindData data = PathfindQueue[0];
                    pathfinder = Pathfinder(GetNode(data.from), GetNode(data.to));
                }
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
                        else if (miner.path.HasValue && miner.path?.path.Count > 0)
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

                if (ReservedNodes.ContainsKey(node.ID))
                    prog.debug.DrawSphere(new BoundingSphereD(pos, 1), Color.Red);
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
                bool isRamp = ini.Get(nodeSection, "IsRampArea").ToBoolean();

                PathNode node = new PathNode(new LocalPose(Position, Rotation), ID, pID, ReachableNodes, dsp_name, isRamp);

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
            public IEnumerator<bool> Pathfinder(PathNode start, PathNode end)
            {
                PathNode current = start;
                List<int> chked = new List<int>();

                List<int> path = new List<int>();

                path.Clear();
                //logger.Log("Start pathfinder");
                //logger.Log($"From {start.ID} to {end.ID}");

                while (current != end)
                {
                    path.Add(current.ID);
                    chked.Add(current.ID);
                    if (IsNodeChildrenOf(end.ID, current))
                    {
                        //logger.Log($"{end.ID} is a child of {current.ID}");

                        foreach (int id in current.Children)
                        {
                            if (!chked.Contains(id) && IsNodeChildrenOf(end.ID, GetNode(id)))
                            {
                                current = GetNode(id);
                                break;
                            }
                        }
                    }
                    else if (current.ParentID != -1 && !chked.Contains(current.ParentID))
                    {
                        //logger.Log($"{end.ID} is not a child of {current.ID}");
                        //logger.Log($"Going to parent...");

                        current = GetNode(current.ParentID);
                    }
                    else
                    {
                        //logger.Log($"{end.ID} is not a child of {current.ID}");
                        //logger.Log($"{current.ID} has no parent!");
                        //logger.Log($"No path can be found from {start.ID} to {end.ID}!");

                        FoundPaths.Add(new PathfindData(start.ID, end.ID), null);
                        yield break;
                    }

                    /*
                    string pathstr = "";
                    foreach (int nodeID in path)
                        pathstr += nodeID + ", ";
                    logger.Log($"Path is {pathstr}");
                    */

                    yield return true;
                }

                //logger.Log("End pathfinder");

                path.Add(end.ID);
                FoundPaths.Add(new PathfindData(start.ID, end.ID), path);

                yield break;
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

                return -1;
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

            void QueueGoto(Miner miner, GlobalPose pose, int nodeID = -1, float precision = 5)
            {
                string msg = "Goto;";
                msg += pose.pos.pos.EncodeString() + ";";

                GlobalRot rot = pose.rot;
                msg += rot.forward.EncodeString() + ";";
                msg += rot.up.EncodeString() + ";";

                msg += nodeID + ";";

                msg += precision.ToString() + ";";

                miner.QueueCommand(msg);
            }


            bool QueueGotoWaypoint(Miner miner, int waypointID)
            {
                QueueGoto(miner, GetNode(waypointID).Pose.ToGlobal(WorldMatrix), waypointID, GetNode(waypointID).precision);
                return true;
            }

            Path? TryGetPath(PathNode from, PathNode to, out bool isInProgress)
            {
                PathfindData data = new PathfindData(from.ID, to.ID);
                List<int> path;
                bool found = FoundPaths.TryGetValue(data, out path);

                if (found)
                {
                    if (path != null)
                    {
                        isInProgress = false;
                        return new Path(path);
                    }
                    else
                    {
                        isInProgress = false;
                        logger.Log($"No path found!");
                        return null;
                    }
                }
                else if (!PathfindQueue.Contains(data))
                {
                    PathfindQueue.Add(data);
                    logger.Log($"Attempting to find path from {from.ID} to {to.ID}");
                }

                isInProgress = true;
                return null;
            }

            bool MoveMiner(MoveOrder o)
            {
                if (!prog.controller.IsSafeToMove())
                    return false;

                bool pathIsBeingGenerated;
                Path? p = TryGetPath(GetNode(o.miner.CurrentWaypointID), GetNode(o.nodeID), out pathIsBeingGenerated);
                bool pathPresent = p != null;

                bool ok = pathPresent && IsPathClear(p.Value, o.miner.ID);
                if (ok)
                {
                    o.miner.TargetWaypointID = o.nodeID;
                    o.miner.path = p;

                    string path = "";
                    foreach (int n in p.Value.path)
                        path += $"{n}, ";

                    logger.Log($"Starting move of miner {o.miner.ID} to {o.nodeID} with path: " + path);
                }
                else if (pathPresent)
                {
                    o.isObstructed = true;
                    o.displayStatus = "OBSTRUCTED";
                }
                else if (!pathIsBeingGenerated)
                    o.displayStatus = "NO PATH";

                return ok;
            }


            IEnumerator<bool> ExecuteOrders()
            {
                MoveOrder[] temp_orders = new MoveOrder[100];

                while (true)
                {
                    //prog.Echo("Executing orders!");
                    OrderQueue.CopyTo(temp_orders);

                    foreach (MoveOrder o in temp_orders)
                    {
                        yield return false;
                        if (o == null)
                            break;

                        if (MoveMiner(o))
                        {
                            OrderQueue.Remove(o);
                            break;
                        }
                    }

                    yield return false;
                }
            }

            public void MoveMinerTo(Miner miner, int waypointID)
            {
                //if (!prog.controller.IsSafeToMove())
                //return false;

                OrderQueue.Add(new MoveOrder(miner, waypointID));
            }

            public void StopMiner(Miner miner)
            {
                MoveOrder[] temp_orders = new MoveOrder[100];
                OrderQueue.CopyTo(temp_orders);

                foreach (MoveOrder o in temp_orders)
                {
                    if (o == null)
                        break;

                    if (o.miner == miner)
                        OrderQueue.Remove(o);
                }


                if(miner.path?.path.Count > 0)
                {
                    miner.TargetWaypointID = miner.path.Value.path[0];
                    miner.path?.path.RemoveRange(1, miner.path.Value.path.Count - 1);
                }
                else
                    miner.TargetWaypointID = miner.CurrentWaypointID;
            }

            bool CheckMinerPath(Miner miner)
            {
                if (!miner.path.HasValue || (bool)miner.path?.isObstructed)
                {
                    if (miner.path.HasValue)
                    {
                        /*logger.Log($"Path of miner {miner.ID} is obstructed!");

                        string path = "";
                        foreach (int nodeID in miner.path?.path)
                            path += nodeID + ", ";
                        logger.Log($"Path is {path}");
                        */
                    }
                    else if (miner.CurrentWaypointID != miner.TargetWaypointID && miner.TargetWaypointID != -999)
                    {
                        bool PathIsBeingGenerated;
                        Path? p = TryGetPath(GetNode(miner.CurrentWaypointID), GetNode(miner.TargetWaypointID), out PathIsBeingGenerated);

                        if (p != null)
                            miner.path = p;
                        //else if (!PathIsBeingGenerated)
                        //logger.Log($"Path for miner {miner.ID} not found!");
                    }



                    return false;
                }
                else if (miner.path?.path.Count == 0)
                    return false;

                return true;
            }

            void ClearReservedNodes(Miner miner)
            {
                if (NodesReservedByMiner.ContainsKey(miner.ID) && NodesReservedByMiner[miner.ID] != null)
                {
                    foreach (int id in NodesReservedByMiner[miner.ID])
                        ReservedNodes.Remove(id);

                    NodesReservedByMiner[miner.ID].Clear();
                }
                else
                {
                    NodesReservedByMiner.Add(miner.ID, new List<int>());
                }
            }

            void AddReservedNode(Miner miner, int NodeID)
            {
                if (ReservedNodes.ContainsKey(NodeID))
                    ReservedNodes[NodeID] = miner.ID;
                else
                    ReservedNodes.Add(NodeID, miner.ID);

                if (!NodesReservedByMiner.ContainsKey(miner.ID))
                    NodesReservedByMiner.Add(miner.ID, new List<int>());

                NodesReservedByMiner[miner.ID].Add(NodeID);
            }

            void ReserveRampForMiner(Miner miner, int NodeID)
            {
                foreach (int id in GetNode(NodeID).Children)
                    AddReservedNode(miner, id);
            }

            void ReserveNodeForMiner(Miner miner, int NodeID)
            {
                AddReservedNode(miner, NodeID);

                if (GetNode(NodeID).IsRamp)
                    ReserveRampForMiner(miner, NodeID);

                if (GetNode(GetNode(NodeID).ParentID) != null && GetNode(GetNode(NodeID).ParentID).IsRamp)
                    ReserveRampForMiner(miner, GetNode(NodeID).ParentID);
            }

            bool HasCommandIssuedToMoveToWaypoint(Miner miner, int nodeID)
            {
                foreach (string cmd in miner.CommandQueue)
                {
                    string[] cmdStringSplit = cmd.Split(';');

                    if (cmdStringSplit[0] != "Goto")
                        continue;

                    int sentNodeID = int.Parse(cmdStringSplit[6]);

                    if (sentNodeID == nodeID)
                        return true;
                }

                return false;
            }
            public void CheckMove(Miner miner)
            {
                if (miner.CurrentWaypointID == miner.TargetWaypointID)
                    return;

                if (!CheckMinerPath(miner))
                    return;

                float dist_tgt = miner.path?.path.Count() > 1 ? PATH_NODE_DIST_TGT : FINAL_DIST_TGT;
                int NextNodeID = (int)miner.path?.path[0];
                int? NextNextNodeID = miner.path?.path.Count > 1 ? miner.path?.path[1] : null;

                if (miner.Docked)
                    return;

                if (!miner.busy && !HasCommandIssuedToMoveToWaypoint(miner, NextNodeID) && CanMinerMoveToNode(NextNodeID, miner.ID))
                {
                    QueueGotoWaypoint(miner, NextNodeID);
                    logger.Log($"Moving {miner.ID} to {NextNodeID}");

                    ClearReservedNodes(miner);
                    ReserveNodeForMiner(miner, miner.CurrentWaypointID);
                    ReserveNodeForMiner(miner, NextNodeID);

                    if (NextNextNodeID != null && CanMinerMoveToNode((int)NextNextNodeID, miner.ID))
                        ReserveNodeForMiner(miner, (int)NextNextNodeID);
                    return;
                }
                else if (!miner.busy)
                {
                    //logger.Log($"Miner {miner.ID}'s path is blocked.");
                    return;
                }
                if (Vector3.Distance(miner.Pos.pos, GetNode(NextNodeID).Pose.pos.ToGlobal(WorldMatrix).pos) < GetNode(NextNodeID).precision && CanMinerMoveToNode(NextNodeID, miner.ID))
                {
                    logger.Log($"Miner {miner.ID} arrived at {NextNodeID}");

                    miner.CurrentWaypointID = NextNodeID;
                    miner.path?.path.RemoveAt(0);

                    if (miner.path?.path.Count == 0)
                    {
                        miner.path = null;
                        ClearReservedNodes(miner);
                        ReserveNodeForMiner(miner, miner.CurrentWaypointID);
                    }
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

            public bool CanMinerMoveToNode(int NodeID, long minerID)
            {
                foreach (Miner miner in prog.controller.Miners)
                {
                    if (miner.ID == minerID)
                        continue;

                    if (miner.CurrentWaypointID == NodeID || miner.TargetWaypointID == NodeID)
                        return false;
                }

                if (ReservedNodes.ContainsKey(NodeID) && ReservedNodes[NodeID] != minerID)
                    return false;

                return true;
            }

            public bool NodeHasTraffic(int NodeID, long minerID = -1, List<int> path = null)
            {
                foreach (MoveOrder o in OrderQueue)
                {
                    if (!o.isObstructed && o.nodeID == NodeID && o.miner.ID != minerID)
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
                            if (GetNode(nID).IsRamp)
                                continue; //If node is a ramp area ignore it

                            if (nID == NodeID)
                                return true;
                        }
                }

                return false;
            }

            public bool NodeIsOccupied(int NodeID, long minerID = -1, bool checkRampAreaOccupancy = true, bool callingFromRampAreaCheck = false)
            {
                if (prog.controller.GetById(minerID).CurrentWaypointID == NodeID)
                    return false;

                if (checkRampAreaOccupancy && GetNode(NodeID).IsRamp)
                {
                    foreach (int childNodeID in GetNode(NodeID).Children)
                    {
                        if (NodeID == prog.controller.GetById(minerID).CurrentWaypointID || GetNode(NodeID).Children.Contains(prog.controller.GetById(minerID).CurrentWaypointID)) //Miner is already in ramp area
                            break;

                        if (NodeIsOccupied(childNodeID, minerID, false, true))
                        {
                            logger.Log($"Node {NodeID} is occupied because it is a ramp area and one of it's child nodes is occupied");
                            return true;
                        }
                    }
                }

                if (checkRampAreaOccupancy && GetNode(GetNode(NodeID).ParentID) != null && GetNode(GetNode(NodeID).ParentID).IsRamp)
                {
                    foreach (int childNodeID in GetNode(GetNode(NodeID).ParentID).Children)
                    {
                        if (GetNode(NodeID).ParentID == prog.controller.GetById(minerID).CurrentWaypointID || GetNode(GetNode(NodeID).ParentID).Children.Contains(prog.controller.GetById(minerID).CurrentWaypointID)) //Miner is already in ramp area
                            break;

                        if (NodeIsOccupied(childNodeID, minerID, false, true))
                        {
                            logger.Log($"Node {NodeID} is occupied because it is a child of a ramp area and one of the ramp area's child nodes is occupied");
                            return true;
                        }
                    }
                }


                foreach (Miner miner in prog.controller.Miners)
                {
                    if (miner.ID == minerID)
                        continue;

                    if (miner.CurrentWaypointID == NodeID)
                    {
                        logger.Log($"Node {NodeID} is occupied because a miner is there");
                        return true;
                    }

                    if (miner.path.HasValue && miner.path.Value.path != null && miner.path.Value.path.Count > 0)
                    {
                        if (miner.path?.path[0] == NodeID)
                        {
                            logger.Log($"Node {NodeID} is occupied because a miner is about to move there");
                            return true;
                        }
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

                    /*if (NodeHasTraffic(id, minerID))
                        return false;*/

                    foreach (Miner miner in prog.controller.Miners)
                    {
                        if (miner.ID == minerID)
                            continue;

                        if (miner.CurrentWaypointID == id && miner.CurrentWaypointID == miner.TargetWaypointID)
                            return false;

                        if (miner.path.HasValue && miner.path.Value.path != null && miner.path.Value.path.Count > 0)
                        {
                            foreach (int nID in miner.path.Value.path)
                            {
                                if (GetNode(nID).IsRamp)
                                    continue; //If node is a ramp area ignore it

                                if (nID == id) //Ensure that the paths won't block each other
                                {
                                    if (!CanPass(miner.path.Value.path, path.path, nID, id))
                                    {
                                        //logger.Log($"Miner {minerID} will block miner {miner.ID} if it starts to move.");
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }

                return true;
            }

            bool CanPass(List<int> path1, List<int> path2, int id1, int id2)
            {
                int path1index = path1.IndexOf(id1);
                int path2index = path2.IndexOf(id2);
                if (path1.Count > path1index && path2index > 0)
                {
                    if (path1[path1index + 1] == path2[path2index - 1])
                        return false;
                }
                else if (path1index > 0 && path2.Count > path2index)
                {
                    if (path1[path1index - 1] == path2[path2index + 1])
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

        public struct PathfindData
        {
            public int from;
            public int to;
            public PathfindData(int f, int t)
            {
                from = f;
                to = t;
            }
        }

        public struct ReservedNodeData
        {
            public int NodeID;
            public int MinerID;

            public ReservedNodeData(int NodeToReserve, int ReservedBy)
            {
                NodeID = NodeToReserve;
                MinerID = ReservedBy;
            }
        }
    }
}
