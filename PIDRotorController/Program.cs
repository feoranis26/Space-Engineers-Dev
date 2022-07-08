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
        IMyMotorStator rotor;
        IMyThrust thruster;
        IMyShipController cockpit;

        DebugAPI debug;
        GridUtils g = new GridUtils();

        PidController c = new PidController(25, 0, 0.1, 60, -60);
        public Program()
        {
            g.Init(this);
            rotor = g.GetBlockWithTag<IMyMotorStator>("PID Rotor");
            thruster = g.GetBlockWithTag<IMyThrust>("Vector Thruster");
            cockpit = g.GetBlock<IMyShipController>();

            debug = new DebugAPI(this);

            Runtime.UpdateFrequency |= UpdateFrequency.Update1;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            debug.RemoveDraw();

            if (cockpit.MoveIndicator.Length() <= 0.01)
            {
                rotor.TargetVelocityRPM = 0;
                thruster.ThrustOverridePercentage = 0;
                return;
            }

            Vector3 TargetFacing = new LocalDir(Vector3.Normalize(cockpit.MoveIndicator)).ToGlobal(cockpit.WorldMatrix).forward;
            //TargetFacing is world-oriented not local!
            RotateThrusterTowards(TargetFacing, thruster, rotor);
        }
        public void RotateThrusterTowards(Vector3 target, IMyThrust th, IMyMotorStator rt)
        {
            //target is WORLD ALIGNED NOT LOCAL!!!!
            Echo(Vector3.Dot(rt.WorldMatrix.Up, target).ToString());
            if (Math.Abs(Vector3.Dot(rt.WorldMatrix.Up, target)) > 0.95) //thruster is perpendicular to target so it can't rotate towards it
            {
                rt.TargetVelocityRad = 0;
                return;
            }
            Vector3 CurrentFacing = th.WorldMatrix.Forward;
            Vector3 TorqueVector = Vector3.Cross(CurrentFacing, target);

            float TargetRot = Vector3.Dot(TorqueVector, rotor.WorldMatrix.Up);
            float TargetAlignment = Vector3.Dot(CurrentFacing, target);

            if (TargetAlignment < 0)
                TargetRot = (float)Math.PI * 2 - TargetRot;
            c.ProcessVariable = TargetRot;
            float Correction = (float)c.ControlVariable(Runtime.TimeSinceLastRun);

            rotor.TargetVelocityRad = Correction;
        }
    }
}
