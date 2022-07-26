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
        public static class Job
        {
            //TODO Job size config through GUI modules

            public static SystemState State;
            public static bool IsJobRunning = false;

            public static Vector3I Size = new Vector3I(1, 1, 100);
            public static Vector2 HoleSize = new Vector2(8, 8);

            public static List<DrillSite> Holes = new List<DrillSite>();

            public static PathNode Jobsite;

            public static int DrillSiteNodeStartID = 5000;
            public static int DrillSiteNavNodeStartID = 10000;

            public static List<MiningJob> Jobs = new List<MiningJob>();

            public static float MiningSpeed = 1;

            static List<string> jobs_split;
            public static string Save()
            {
                string str = "";
                str += ((Vector3)Size).EncodeString() + ";";
                str += new Vector3(HoleSize.X, HoleSize.Y, 0).EncodeString() + ";";

                foreach (DrillSite s in Holes)
                    str += s.Done ? "Y," : "N,";

                str += ";";
                str += IsJobRunning + ";";
                str += MiningSpeed + ";";
                str += (int)State + ";";

                foreach (MiningJob j in Jobs)
                    str += j.miner.ID + "-" + Holes.IndexOf(j.drillSite) + ",";

                str += ";";
                return str;
            }

            public static void Load(string data)
            {
                List<string> split = data.Split(';').ToList();
                split.Reverse();

                Vector3 size = split.Pop().ToVector();
                Vector3 holeSize = split.Pop().ToVector();

                Size = new Vector3I((int)size.X, (int)size.Y, (int)size.Z);
                HoleSize = new Vector2(holeSize.X, holeSize.Y);

                GenerateHoles();

                List<string> holes_split = split.Pop().Split(',').ToList();
                for (int i = 0; i < Holes.Count; i++)
                {
                    if (holes_split[i] == "Y")
                        Holes[i].Done = true;
                }

                IsJobRunning = bool.Parse(split.Pop());
                MiningSpeed = float.Parse(split.Pop());
                State = (SystemState)int.Parse(split.Pop());

                jobs_split = split.Pop().Split(',').ToList();
            }

            public static void LoadJobs()
            {
                foreach (string job in jobs_split)
                {
                    if (job == "" || job.Length < 3)
                        continue;

                    string[] job_split = job.Split('-');
                    AssignMinerToHole(ProgHelper.prog.controller.GetById(int.Parse(job_split[0])), Holes[int.Parse(job_split[1])]);
                }
            }

            public static void GenerateHoles()
            {
                foreach(MiningJob job in Jobs)
                    job.miner.job = null;

                Jobs.Clear();

                foreach (DrillSite site in Holes)
                {
                    ProgHelper.prog.nav.Nodes.Remove(site.NavNode);
                    ProgHelper.prog.nav.Nodes.Remove(site.Node);
                }

                foreach(DrillSite site in Holes)
                {
                    Jobsite.Children.Remove(site.Node.ID);
                    Jobsite.Children.Remove(site.NavNode.ID);
                }

                Holes.Clear();

                float sizeX = Size.X * HoleSize.X;
                float sizeY = Size.Y * HoleSize.Y;

                for (int y = Size.Y - 1; y >= 0; y--)
                {
                    for (int x = 0; x < Size.X; x++)
                    {
                        float x_pos = x * HoleSize.X - sizeX / 2;
                        float y_pos = y * HoleSize.Y - sizeY / 2;

                        LocalPos site_pos = new LocalPos(new Vector3(x_pos, y_pos, -20) + Jobsite.Pose.pos.pos + new Vector3(HoleSize.X, HoleSize.Y, 0) / 2f);
                        LocalPos site_node_pos = new LocalPos(new Vector3(x_pos, y_pos, -15) + Jobsite.Pose.pos.pos + new Vector3(HoleSize.X, HoleSize.Y, 0) / 2f);
                        LocalPos nav_pos = new LocalPos(new Vector3(x_pos, y_pos, 0) + Jobsite.Pose.pos.pos + new Vector3(HoleSize.X, HoleSize.Y, 0) / 2f);
                        LocalPose site_pose = new LocalPose(site_node_pos, new LocalRot(Vector3.Forward, Vector3.Up));
                        LocalPose nav_pose = new LocalPose(nav_pos, new LocalRot(Vector3.Forward, Vector3.Up));

                        int NavID = DrillSiteNavNodeStartID + Holes.Count;
                        int NodeID = DrillSiteNodeStartID + Holes.Count;

                        List<int> Children = new List<int>();
                        Children.Add(NodeID);

                        PathNode NavNode = new PathNode(nav_pose, NavID, Jobsite.ID, Children, $"Drillsite nav #{Holes.Count}", false);
                        PathNode Node = new PathNode(site_pose, NodeID, NavID, new List<int>(), $"Drillsite #{Holes.Count}", false);
                        DrillSite site = new DrillSite(site_pos, Size.Z, Node, NavNode);

                        NavNode.precision = 2;
                        Node.precision = 2f;

                        Holes.Add(site);

                        ProgHelper.prog.nav.Nodes.Add(NavNode);
                        ProgHelper.prog.nav.Nodes.Add(Node);

                        Jobsite.Children.Add(NavID);
                    }
                }
            }

            public static void Visualize()
            {
                //ProgHelper.prog.debug.DrawAABB(new BoundingBox(new LocalPos(Jobsite.Pose.pos.pos - new Vector3(Size.X * HoleSize.X, Size.Y * HoleSize.Y, 0) / 2).ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos, new LocalPos(Jobsite.Pose.pos.pos + new Vector3(Size.X * HoleSize.X, Size.Y * HoleSize.Y, -15) / 2).ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos), Color.Yellow);

                Quaternion rot = Quaternion.CreateFromForwardUp(ProgHelper.prog.nav.WorldMatrix.Forward, ProgHelper.prog.nav.WorldMatrix.Up);
                Vector3 Center = new LocalPos(Jobsite.Pose.pos.pos - new Vector3(0, 0, 10f)).ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos;
                Vector3 Sides = new Vector3(HoleSize.X * Size.X, HoleSize.Y * Size.Y, 20f) / 2;

                ProgHelper.prog.debug.DrawGPS(ProgHelper.prog.comms.SystemName + " Jobsite", Jobsite.Pose.pos.ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos, Color.Green);
                ProgHelper.prog.debug.DrawOBB(new MyOrientedBoundingBoxD(Center, Sides, rot), Color.Yellow);

                Center = new LocalPos(Jobsite.Pose.pos.pos + new Vector3(0, 0, -20 - Size.Z / 2)).ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos;
                Sides = new Vector3(HoleSize.X * Size.X, HoleSize.Y * Size.Y, Size.Z) / 2;

                ProgHelper.prog.debug.DrawOBB(new MyOrientedBoundingBoxD(Center, Sides, rot), Color.Red);

                Sides = new Vector3(HoleSize.X - 0.5f, HoleSize.Y - 0.5f, Size.Z) / 2;
                foreach (DrillSite site in Holes)
                {
                    if (!site.Done)
                    {
                        Center = new LocalPos(site.Pos.pos - new Vector3(0, 0, Size.Z) / 2).ToGlobal(ProgHelper.prog.nav.WorldMatrix).pos;
                        ProgHelper.prog.debug.DrawOBB(new MyOrientedBoundingBoxD(Center, Sides, rot), Color.Blue);
                    }
                }
            }

            public static void AssignMinerToHole(Miner miner, DrillSite site)
            {
                MiningJob j = new MiningJob(miner, site);

                Jobs.Add(j);
                miner.job = j;
            }

            public static void AssignMiner(Miner miner)
            {
                List<DrillSite> OccupiedHoles = new List<DrillSite>();

                foreach (MiningJob j in Jobs)
                    OccupiedHoles.Add(j.drillSite);

                DrillSite site = null;
                foreach (DrillSite s in Holes)
                {
                    if (s.Done)
                        continue;

                    if (OccupiedHoles.Contains(s))
                        continue;

                    site = s;
                    break;
                }

                if (site != null)
                    AssignMinerToHole(miner, site);
            }

            public static void CheckJobs()
            {
                Jobs.Clear();

                foreach(Miner m in ProgHelper.prog.controller.Miners)
                {
                    if (m.job != null)
                        Jobs.Add(m.job);
                }
            }

            public static bool IsDone()
            {
                foreach(DrillSite site in Holes)
                {
                    if (!site.Done)
                        return false;
                }

                return true;
            }
        }

        public class DrillSite
        {
            public LocalPos Pos;
            public float Depth;

            public PathNode Node;
            public PathNode NavNode;

            public bool Done = false;
            public DrillSite(LocalPos p, float depth, PathNode node, PathNode navNode)
            {
                Pos = p;
                Depth = depth;

                Node = node;
                NavNode = navNode;
            }
        }

        public class MiningJob
        {
            public Miner miner;
            public DrillSite drillSite;

            public MiningJob(Miner m, DrillSite s)
            {
                miner = m;
                drillSite = s;
            }
        }

    }
}
