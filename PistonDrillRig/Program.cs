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
        List<IMyShipDrill> drills;
        List<IMyPistonBase> pistons;

        GridUtils gridUtils = new GridUtils();
        DebugAPI debug;

        Log log;
        Logger commandsLog;

        GlobalVel speed;
        public Program()
        {
            gridUtils.Init(this);
            gridUtils.GetBlocks(out drills, false);
            gridUtils.GetBlocks(out pistons, false);

            debug = new DebugAPI(this);
            log = new Log();
            commandsLog = new Logger(log, "Commands");

            Runtime.UpdateFrequency |= UpdateFrequency.Update1;
            Runtime.UpdateFrequency |= UpdateFrequency.Update10;
            Runtime.UpdateFrequency |= UpdateFrequency.Update100;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
                ProcessArg(argument);

            if ((updateSource & UpdateType.Update10) > 0)
                Display();

            MoveDrill(speed);
        }

        public void Display()
        {
            string logs = log.Display(DateTime.Now - new TimeSpan(0, 0, 10));
            Echo(logs);

            Echo("###   PistonDrillRig v0   ###");
            Echo($"Current speed\t: {speed.ToLocal(drills[0].WorldMatrix).vel.EncodeString()}");
        }

        public void ProcessArg(string arg)
        {
            try
            {
                bool ok = false;
                string[] split = arg.Split(' ');

                if (split[0] == "setVel")
                {
                    speed = new LocalVel(arg.Remove(0, 7).ToVector(out ok) * 100).ToGlobal(drills[0].WorldMatrix);
                }

                if (!ok)
                    throw new ArgumentException();
            }
            catch (ArgumentException)
            {
                commandsLog.Log("Invalid command");
            }
        }

        public void MoveDrill(GlobalVel vel)
        {
            foreach (IMyPistonBase piston in pistons)
            {
                GlobalDir pistonDir = new GlobalDir(piston.WorldMatrix.Forward);
                float pistonVel = Vector3.Dot(vel.vel, pistonDir.forward);

                piston.Velocity = pistonVel;
            }
        }


        class DrillSite
        {
            public GlobalPos pos;
            public GlobalDir dir;
        }
    }
}
