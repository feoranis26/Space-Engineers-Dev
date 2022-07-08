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
        //TODO Vector math utility class
        //TODO LocalCoordinateSlave LocalCoordinateMaster WoorldCoordinate structs

        //TODO Refactor

        public Motion motion = new Motion();
        public GridUtils gridUtils = new GridUtils();
        public Orientation orientation = new Orientation();
        public Comms comms = new Comms();
        public Actions actions = new Actions();

        public MinerState state = MinerState.Unknown;

        public Vector3 MotionTarget = Vector3.Zero;

        List<IMyLightingBlock> Lights;

        DebugAPI debug;

        public Program()
        {
            debug = new DebugAPI(this);
            gridUtils.Init(this);
            gridUtils.Prefix = "[SWM]";

            motion.Init(this, orientation, gridUtils);
            orientation.Init(this, gridUtils);
            actions.Init(this, motion, comms);
            comms.Init(this, IGC, actions);

            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100 | UpdateFrequency.Update1;

            try
            {
                Lights = gridUtils.GetMyBlockGroupWithTagAsList<IMyLightingBlock>("[Warning Lights]");
            }
            catch (MissingBlockException) {
                Echo("No lights found!");
            }
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                string[] sep = argument.Split(';');

                if (sep[0] == "Store")
                {
                    Storage = sep[1];
                }
            }
            comms.Tick(updateSource);

            if ((updateSource & UpdateType.Update10) > 0)
                UpdateLights();

            if (comms.connectionState != ConnectionState.Connected)
            {
                motion.EnableManualControl();
                return;
            }

            debug.RemoveDraw();

            Echo("Run time     : " + Runtime.LastRunTimeMs + " ms");
            Echo("Connected to :" + comms.SystemID);
            Echo("Session ID   : " + comms.hb.SessionID);
            Echo("Miner ID   : " + comms.ID);
            Echo("Current Position: " + new GlobalPos(orientation.rc.WorldMatrix.Translation).ToLocal(comms.systemWorldMatrix).pos.EncodeString());

            Echo(comms.connectionState.ToString());

            Echo("State         : " + state.ToString());
            Echo("Cnction  state: " + comms.connectionState.ToString());
            Echo("Busy          : " + actions.busy);
            Echo("Current cmd   : " + comms.currentCommand);

            Echo("Rot tgt       : " + actions.RotTarget?.forward.EncodeString());
            Echo("Pos tgt       : " + actions.PosTarget?.pos.EncodeString());


            actions.Tick(updateSource);
        }

        bool light_blink_temp = false;

        void UpdateLights()
        {
            if (Lights == null)
                return;

            switch (comms.connectionState)
            {
                case ConnectionState.NotConnected:
                    UpdateLights(Color.Red, 0.5f);
                    return;
                case ConnectionState.Connecting:
                    UpdateLights(Color.Yellow, 0.5f);
                    return;
            }

            switch (state)
            {
                case MinerState.Ready:
                    UpdateLights(Color.Yellow, 0);
                    break;
                case MinerState.NotConnected:
                    UpdateLights(Color.Red, 1);
                    return;
                case MinerState.Stopped:
                    UpdateLights(Color.Red, 0);
                    return;

                case MinerState.CargoFull:
                case MinerState.H2Low:
                case MinerState.OutOfCharge:
                    UpdateLights(Color.Yellow, 0.5f);
                    return;

                case MinerState.Ejecting:
                    UpdateLights(Color.YellowGreen, 1f);
                    break;

                case MinerState.Active:
                    UpdateLights(Color.Green, 1f);
                    break;

                default:
                    if (light_blink_temp)
                        UpdateLights(Color.Red, 0f);
                    else
                        UpdateLights(Color.Yellow, 0f);
                    light_blink_temp = !light_blink_temp;

                    break;
            }

            if (actions.busy && actions.CurrentCommand != null)
            {
                if (light_blink_temp)
                    UpdateLights(Color.Green, 0f);
                else
                    UpdateLights(Color.Yellow, 0f);

                light_blink_temp = !light_blink_temp;
            }
        }

        void UpdateLights(Color color, float blinkInterval)
        {
            foreach (IMyLightingBlock light in Lights)
            {
                light.Color = color;
                light.BlinkIntervalSeconds = blinkInterval;
                light.BlinkLength = 50f;
            }
        }
    }
}
