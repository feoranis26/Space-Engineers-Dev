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
    partial class Program : MyGridProgram
    {
        //TODO Refactor

        //TODO GUI Display modules
        //TODO Main control logic independent of single miners
        //TODO Job state control

        //TODO Haulers to return ore into mothership

        MinerComms comms = new MinerComms();
        SwarmController controller = new SwarmController();
        CMDProcessor cmdProcessor = new CMDProcessor();
        Navigation nav = new Navigation();
        GridUtils gridUtils = new GridUtils();
        DebugAPI debug;

        public string display = "";

        public bool drawDebug = true;
        public Log log = new Log();
        public Program()
        {
            ProgHelper.prog = this;
            gridUtils.Init(this);
            gridUtils.Prefix = "[SWM]";

            nav.Init(this, gridUtils);
            comms.Init(this, IGC, controller);
            controller.Init(comms, nav, this);
            cmdProcessor.Init(comms, controller);

            Job.Jobsite = nav.GetNode(nav.Jobsite);
            Job.GenerateHoles();

            Load();

            Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;

            debug = new DebugAPI(this);
        }

        public void Load()
        {
            Echo("LOADING: " + Storage);

            if (Storage == "")
                return;

            string[] split = Storage.Split('}');
            Job.Load(split[0]);
            controller.Load(split[1]);
            nav.Load(split[2]);
            Job.LoadJobs();
        }

        public void Save()
        {
            Echo("SAVING!");

            Storage = "";
            Storage += Job.Save() + "}";
            Storage += controller.Save() + "}";
            Storage += nav.Save() + "}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo("### - SwarmMiner - ###");
            Echo("### - CONTROLLER - ###");
            Echo("\n");
            Echo("Run time          : " + Runtime.LastRunTimeMs + " ms");
            Echo("Session ID        : " + comms.hb.SessionID);
            Echo("Connected miners  : " + controller.ConnectedMiners);
            Echo("Registered miners : " + controller.Miners.Count);
            Echo("Busy miners       : " + controller.BusyMiners);

            Echo("Is safe to move   : " + (controller.IsSafeToMove() ? "YES" : "NO"));
            Echo("State             : " + Job.State);

            comms.Tick(updateSource);
            controller.Tick(updateSource);

            Echo("\n\n");
            Echo("Miner positions   : ");
            foreach (Miner miner in controller.Miners)
                Echo($"Miner {miner.ID}: NODE {miner.CurrentWaypointID}, {nav.GetNode(miner.CurrentWaypointID).DisplayName}");

            Echo("\n\n");
            Echo("Miner cmds        : ");
            foreach (Miner miner in controller.Miners)
                Echo($"Miner {miner.ID}: Q/E: {miner.CommandQueue.Count}/{miner.currentCommandNum}");

            Echo("\n\n");
            Echo("Miner states      : ");
            foreach (Miner miner in controller.Miners)
                Echo($"Miner {miner.ID}: S/P: {miner.State}/{miner.prevState}");

            Echo("\n\n");
            Echo("Miner targets     : ");
            foreach (Miner miner in controller.Miners)
                Echo($"Miner {miner.ID}: {miner.TargetWaypointID}");

            Echo("\n\n");
            foreach (MoveOrder o in nav.OrderQueue)
                Echo($"Move order #{o.miner.ID}: from {o.miner.CurrentWaypointID} to {o.nodeID}, status: {o.displayStatus}.");

            Echo("\n\n");
            foreach (PathfindData dat in nav.PathfindQueue)
                Echo($"Pathfind queue: from {dat.from} to {dat.to}");

            Echo("\n\n");
            string reservedNodes = "";
            foreach (int nodeID in nav.ReservedNodes.Keys)
                reservedNodes += $" {nodeID} for {nav.ReservedNodes[nodeID]}, ";

            Echo($"Reserved nodes: {reservedNodes}");

            string logs = log.Display(DateTime.Now - new TimeSpan(0, 0, 10));
            Echo(logs);

            if ((updateSource & UpdateType.Update10) > 0)
                nav.Tick();


            if ((updateSource & UpdateType.Update10) > 0)
            {
                debug.RemoveDraw();

                if (drawDebug)
                    nav.DrawVisuals();

                Job.Visualize();
            }

            if ((updateSource & UpdateType.Update100) > 0)
            {
                Job.CheckJobs();
            }

            if (argument != "")
            {
                cmdProcessor.Process(argument);
            }
        }
    }
}
