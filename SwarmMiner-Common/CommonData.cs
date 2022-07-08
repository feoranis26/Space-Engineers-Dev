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
using System.Text.RegularExpressions;

namespace IngameScript
{
    partial class Program
    {
        public enum SystemState
        {
            Halt = 0,
            Stopped = -1,
            Paused = 2,
            Running = 1,
        }

        public enum ConnectionState
        {
            Connected = 1,
            Connecting = 2,
            NotConnected = -1
        }

        public class PathNode
        {
            public LocalPose Pose;
            public int ID;
            public int ParentID;
            public List<int> Children;
            public float precision = 5;

            public string AirlockPBName;
            public string DockPBName;

            public string DisplayName;

            public PathNode(LocalPose pose, int id, int parent, List<int> children, string displayName)
            {
                Pose = pose;
                ID = id;
                ParentID = parent;
                Children = children;
                DisplayName = displayName;
            }
        }
        public enum MinerState
        {
            Stopped = 0,
            Active = 1,
            Ready = 2,
            CargoFull = 3,
            H2Low = 4,
            OutOfCharge = 5,
            Ejecting = 6,

            Unknown = -1,
            NotConnected = -2
        }

        public struct Path
        {
            public List<int> path;
            public bool isObstructed;
            public Path(List<int> p)
            {
                path = p;
                isObstructed = p == null;
            }
        }
    }
}
