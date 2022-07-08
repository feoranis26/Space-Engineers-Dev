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
        //Arguments
        string INGRESS_ARGUMENT = "%ingress";
        string EGRESS_ARGUMENT = "%egress";

        string OPEN_AUX_ARGUMENT = "%open";
        string CLOSE_AUX_ARGUMENT = "%close";
        string SWITCH_AUX_ARGUMENT = "%switch";

        string SETNAME_ARGUMENT = "!name";

        string REPORT_OPEN_ARGUMENT = "?state";

        //State
        AirlockState state = AirlockState.Unknown;

        List<IMyAirtightHangarDoor> InnerDoors;
        List<IMyAirtightHangarDoor> HullDoors;
        List<IMyAirVent> Vents;

        List<IMyLightingBlock> WarningLights;
        List<IMyLightingBlock> InnerDoorLights;
        List<IMyLightingBlock> HullDoorLights;

        string LockName = "AIRLOCK";

        bool run = true;

        GridInteractions gridInteractions = new GridInteractions();
        public Program()
        {
            gridInteractions.Init(this);
            gridInteractions.Prefix = "[AIRLOCK]";

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            LoadData();
            LoadBlocks();

            if(state != AirlockState.LockedOut || state != AirlockState.LockedIn)
            {
                //state = AirlockState.LockedOut;
                //Ingress();
            }
        }

        public void Save()
        {
            Storage = "";
            Storage += ((int)state).ToString() + " ";
            Storage += LockName + " ";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(state.ToString());

            if (argument != "")
                ProcessCommand(argument);
            else if(run)
                Tick();

            Me.CustomData = state.ToString();
        }

        void LoadData()
        {
            Echo(Storage);
            if(Storage.Length > 0)
            {
                string[] storage = Storage.Split(' ');
                state = (AirlockState)int.Parse(storage[0]);
                LockName = storage[1];
            }
            else if(run)
            {
                state = AirlockState.LockedOut;
                Ingress();
            }
        }

        void LoadBlocks()
        {
            try
            {
                Echo("Loading blocks...");
                HullDoors = gridInteractions.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Door Hull]");
                Echo("Hull doors present.");
                InnerDoors = gridInteractions.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Door Inner]");
                Echo("Inner doors present");

                Vents = gridInteractions.GetBlockGroupWithTagAsList<IMyAirVent>("[" + LockName + " Vents]");
                Echo("Vents present");

                try
                {
                    InnerDoorLights = gridInteractions.GetBlockGroupWithTagAsList<IMyLightingBlock>("[" + LockName + " Warning Inner]");
                    Echo("Inner door warning lights present");
                    HullDoorLights = gridInteractions.GetBlockGroupWithTagAsList<IMyLightingBlock>("[" + LockName + " Warning Hull]");
                    Echo("Hull door warning lights present");
                    WarningLights = gridInteractions.GetBlockGroupWithTagAsList<IMyLightingBlock>("[" + LockName + " Warning All]");
                }
                catch
                {
                    Echo("Not all lights are installed.");
                }
                Echo("Rotating warning lights present");
            }
            catch(MissingBlockException)
            {
                Echo("Missing blocks found!");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                run = false;
            }
        }

        void ProcessCommand(string arg)
        {
            string cmdType;
            string senderID = "";
            string cmd;
            if (arg.Substring(0, 1) == "#")
            {
                cmdType = arg.Split(' ')[1].Substring(0, 1);
                senderID = arg.Split(' ')[0].Substring(1);
                cmd = arg.Substring(senderID.Length + 1);
            }
            else
            {
                cmdType = arg.Split(' ')[0].Substring(0, 1);
                cmd = arg;
            }

            Echo(cmdType);

            ActionResult result = ActionResult.InvalidQuery;
            switch (cmdType)
            {
                case "%":
                    result = ProcessAction(cmd);
                    break;
                case "!":
                    result = ProcessConfig(cmd);
                    break;
                case "?":
                    result = ProcessQuery(cmd);
                    break;
            }

            if(senderID != "")
            {
                IMyProgrammableBlock sender = (IMyProgrammableBlock)GridTerminalSystem.GetBlockWithId(long.Parse(senderID));
                sender.TryRun("#" + Me.GetId() + " " + result.ToString());
            }
        }
        ActionResult ProcessAction(string arg)
        {
            string[] cmds = arg.Split(' ');

            if (cmds.Length == 0)
                return ActionResult.InvalidQuery;

            string cmd = cmds[0];

            Echo(cmd);

            if (cmd == INGRESS_ARGUMENT)
                return Ingress();
            else if (cmd == EGRESS_ARGUMENT)
                return Egress();

            if (cmds.Length < 2)
                return ActionResult.InvalidQuery;

            if (cmd == OPEN_AUX_ARGUMENT)
                return OpenAux(cmds[1]);
            else if (cmd == CLOSE_AUX_ARGUMENT)
                return CloseAux(cmds[1]);
            else if (cmd == SWITCH_AUX_ARGUMENT)
                return SwitchAux(cmds[1]);

            return ActionResult.InvalidQuery;

        }

        ActionResult ProcessConfig(string arg)
        {
            string[] cmds = arg.Split(' ');

            if (cmds.Length == 0)
                return ActionResult.InvalidQuery;

            string cmd = cmds[0];

            if (cmds.Length < 2)
                return ActionResult.InvalidQuery;

            if (cmd == SETNAME_ARGUMENT)
                LockName = cmds[1];

            return ActionResult.InvalidQuery;
        }

        ActionResult ProcessQuery(string cmd)
        {
            return ActionResult.InvalidQuery;
        }

        ActionResult Ingress()
        {
            switch (state)
            {
                case AirlockState.LockedOut:
                    foreach (IMyAirtightHangarDoor door in HullDoors)
                    {
                        door.CloseDoor();
                    }

                    state = AirlockState.HullDoorsClosing;

                    return ActionResult.Started;
                case AirlockState.HullDoorsClosing:

                    foreach (IMyAirtightHangarDoor door in HullDoors)
                    {
                        if(door.Status != DoorStatus.Closed)
                        {
                            return ActionResult.Busy;
                        }
                    }

                    foreach(IMyAirVent vent in Vents)
                    {
                        vent.Depressurize = false;
                    }

                    state = AirlockState.Pressurizing;

                    return ActionResult.Busy;

                case AirlockState.Pressurizing:

                    foreach (IMyAirVent vent in Vents)
                    {
                        if (vent.GetOxygenLevel() < 0.95)
                            return ActionResult.Busy;
                    }
                    state = AirlockState.InnerDoorsOpening;

                    return ActionResult.Busy;
                case AirlockState.InnerDoorsOpening:

                    foreach (IMyAirtightHangarDoor door in InnerDoors)
                    {
                        door.OpenDoor();
                    }

                    foreach (IMyAirtightHangarDoor door in InnerDoors)
                    {
                        if (door.Status != DoorStatus.Open)
                        {
                            return ActionResult.Busy;
                        }
                    }

                    state = AirlockState.LockedIn;
                    return ActionResult.Successful;
            }
            return ActionResult.Busy;
        }

        ActionResult Egress()
        {
            switch (state)
            {
                case AirlockState.LockedIn:
                    foreach (IMyAirtightHangarDoor door in InnerDoors)
                    {
                        door.CloseDoor();
                    }
                    CloseAllAux();

                    state = AirlockState.InnerDoorsClosing;

                    return ActionResult.Started;
                case AirlockState.InnerDoorsClosing:

                    foreach (IMyAirtightHangarDoor door in InnerDoors)
                    {
                        if (door.Status != DoorStatus.Closed)
                        {
                            return ActionResult.Busy;
                        }
                    }
                    if (!CloseAllAux()) { return ActionResult.Busy; }

                    foreach (IMyAirVent vent in Vents)
                    {
                        vent.Depressurize = true;
                    }

                    state = AirlockState.Depressurizing;

                    return ActionResult.Busy;

                case AirlockState.Depressurizing:
                    foreach (IMyAirVent vent in Vents)
                    {
                        if (vent.GetOxygenLevel() > 0)
                            return ActionResult.Busy;
                    }

                    state = AirlockState.HullDoorsOpening;

                    return ActionResult.Busy;
                case AirlockState.HullDoorsOpening:

                    foreach (IMyAirtightHangarDoor door in HullDoors)
                    {
                        door.OpenDoor();
                    }

                    foreach (IMyAirtightHangarDoor door in HullDoors)
                    {
                        if (door.Status != DoorStatus.Open)
                        {
                            return ActionResult.Busy;
                        }
                    }

                    state = AirlockState.LockedOut;

                    return ActionResult.Successful;
            }
            return ActionResult.Busy;
        }

        ActionResult OpenAux(string name)
        {
            if(state != AirlockState.LockedIn) { return ActionResult.NotReady; }
            try
            {
                List<IMyAirtightHangarDoor> auxDoors = gridInteractions.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Aux " + name + "]");
                foreach(IMyAirtightHangarDoor door in auxDoors)
                {
                    door.OpenDoor();
                }
                return ActionResult.Successful;
            }
            catch(MissingBlockException)
            {
                return ActionResult.InvalidQuery;
            }
        }
        ActionResult CloseAux(string name)
        {
            if(state != AirlockState.LockedIn) { return ActionResult.NotReady; }
            try
            {
                List<IMyAirtightHangarDoor> auxDoors = gridInteractions.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Aux " + name + "]");
                foreach (IMyAirtightHangarDoor door in auxDoors)
                {
                    door.CloseDoor();
                }
                return ActionResult.Successful;
            }
            catch (MissingBlockException)
            {
                return ActionResult.InvalidQuery;
            }
        }

        ActionResult SwitchAux(string name)
        {
            if (state != AirlockState.LockedIn) { return ActionResult.NotReady; }
            try
            {
                List<IMyAirtightHangarDoor> auxDoors = gridInteractions.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Aux " + name + "]");
                foreach (IMyAirtightHangarDoor door in auxDoors)
                {
                    if (door.Status == DoorStatus.Open)
                        door.CloseDoor();
                    else if (door.Status == DoorStatus.Closed)
                        door.OpenDoor();
                }
                return ActionResult.Successful;
            }
            catch (MissingBlockException)
            {
                return ActionResult.InvalidQuery;
            }
        }

        void UpdateLights()
        {
            if (state == AirlockState.Unknown)
            {
                foreach (IMyLightingBlock light in WarningLights)
                {
                    light.Enabled = false;
                }
                foreach (IMyLightingBlock light in InnerDoorLights)
                {
                    light.Enabled = false;
                }
                foreach (IMyLightingBlock light in HullDoorLights)
                {
                    light.Enabled = false;
                }
            }
            else if (isIngressInProgress())
            {
                foreach (IMyLightingBlock light in WarningLights)
                {
                    light.Enabled = true;
                }
                foreach (IMyLightingBlock light in InnerDoorLights)
                {
                    light.Enabled = true;
                    light.Color = new Color(0, 255, 0);
                    light.BlinkIntervalSeconds = state == AirlockState.InnerDoorsOpening ? 2 : 0;
                }
                foreach (IMyLightingBlock light in HullDoorLights)
                {
                    light.Enabled = true;
                    light.Color = new Color(255, 0, 0);
                    light.BlinkIntervalSeconds = state == AirlockState.HullDoorsClosing ? 2 : 0;
                }
            }
            else if (isEgressInProgress())
            {
                foreach (IMyLightingBlock light in WarningLights)
                {
                    light.Enabled = true;
                }
                foreach (IMyLightingBlock light in HullDoorLights)
                {
                    light.Enabled = true;
                    light.Color = new Color(0, 255, 0);
                    light.BlinkIntervalSeconds = state == AirlockState.HullDoorsOpening ? 2 : 0;
                }
                foreach (IMyLightingBlock light in InnerDoorLights)
                {
                    light.Enabled = true;
                    light.Color = new Color(255, 0, 0);
                    light.BlinkIntervalSeconds = state == AirlockState.InnerDoorsClosing ? 2 : 0;
                }
            }
            else
            {
                if (state == AirlockState.LockedIn)
                {
                    foreach (IMyLightingBlock light in InnerDoorLights)
                    {
                        light.Enabled = true;
                        light.Color = new Color(0, 255, 0);
                        light.BlinkIntervalSeconds = 0;
                    }
                    foreach (IMyLightingBlock light in HullDoorLights)
                    {
                        light.Enabled = true;
                        light.Color = new Color(255, 0, 0);
                        light.BlinkIntervalSeconds = 0;
                    }
                }
                else
                {
                    foreach (IMyLightingBlock light in InnerDoorLights)
                    {
                        light.Enabled = true;
                        light.Color = new Color(255, 0, 0);
                        light.BlinkIntervalSeconds = 0;
                    }
                    foreach (IMyLightingBlock light in HullDoorLights)
                    {
                        light.Enabled = true;
                        light.Color = new Color(0, 255, 0);
                        light.BlinkIntervalSeconds = 0;
                    }
                }
                foreach (IMyLightingBlock light in WarningLights)
                {
                    light.Enabled = false;
                }
            }
        }

        bool CloseAllAux()
        {
            bool allClosed = true;
            List<IMyAirtightHangarDoor> Doors = gridInteractions.GetBlockGroupsWithTagAsList<IMyAirtightHangarDoor>("[" + LockName + " Aux ");
            foreach(IMyAirtightHangarDoor door in Doors)
            {
                door.CloseDoor();
                allClosed = door.OpenRatio != 0 ? false : allClosed;
            }
            return allClosed;
        }

        void Tick()
        {
            if(isIngressInProgress()) { Ingress(); }
            if(isEgressInProgress()) { Egress(); }

            UpdateLights();
        }

        bool isBusy()
        {
            return state != AirlockState.LockedIn && state != AirlockState.LockedOut;
        }
        bool isIngressInProgress()
        {
            return state == AirlockState.HullDoorsClosing || state == AirlockState.Pressurizing || state == AirlockState.InnerDoorsOpening;
        }
        bool isEgressInProgress()
        {
            return state == AirlockState.InnerDoorsClosing || state == AirlockState.Depressurizing || state == AirlockState.HullDoorsOpening;
        }
        enum AirlockState
        {
            LockedIn = 0,
            LockedOut = 1,
            InnerDoorsOpening = 2,
            InnerDoorsClosing = 3,
            Depressurizing = 4,
            Pressurizing = 5,
            HullDoorsOpening = 6,
            HullDoorsClosing = 7,
            Unknown = -1
        }

        enum ActionResult
        {
            Started = 1,
            Successful = 0,

            Busy = -1,
            NotReady = -2,
            InvalidQuery = -10
        }
    }
}
