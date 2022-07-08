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
        public Motion motion = new Motion();
        public Orientation orientation = new Orientation();
        public GridUtils gridUtils = new GridUtils();
        public DebugAPI debug;

        GlobalPos target;
        GlobalPos start;
        GlobalRot endRot;
        public Program()
        {
            debug = new DebugAPI(this);
            gridUtils.Init(this);
            gridUtils.Prefix = "[SWM]";

            orientation.Init(this, gridUtils);
            motion.Init(this, orientation, gridUtils);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            target = new GlobalPos(new LocalPos(new Vector3(0, 0, -200)), motion.rc.WorldMatrix);
            start = new GlobalPos(motion.rc.GetPosition());
            endRot = new LocalRot(Vector3.Forward, Vector3.Up).ToGlobal(motion.rc.WorldMatrix); 
            
            motion.EnableManualControl();
        }
        public void Main(string argument, UpdateType updateSource)
        {
            if(argument == "STOP")
            {
                motion.EnableManualControl();

                Runtime.UpdateFrequency = UpdateFrequency.None;
                return;
            }
            debug.RemoveDraw();
            //debug.DrawLine(motion.rc.WorldMatrix.Translation, motion.rc.WorldMatrix.Translation + new Vector3(0, 0, -50), Color.Blue, 0.05f);
            debug.DrawLine(start.pos, target.pos, Color.Blue, 0.05f);
            Vector3 velocity = motion.rc.GetShipVelocities().LinearVelocity;
            debug.DrawLine(motion.rc.WorldMatrix.Translation, motion.rc.WorldMatrix.Translation + velocity, Color.Pink, 0.05f);

            debug.DrawPoint(target.pos, Color.Green, 0.25f);
            //motion.AccelerateTo(new GlobalVel(new Vector3(0, 0, -1)));
            //motion.Accelerate(new LocalVel(new Vector3(0, 0, -1)));
            

            //motion.AddThrust(new GlobalVel(Vector3.Forward));

            //motion.MoveTo(start);
            //motion.MoveForwardTo(start, endRot);

            //motion.RotateTo(endRot);
            //motion.MoveAxis(1, start, new GlobalDir(endRot.forward));

            //motion.RotateTo();
        }
    }
}
