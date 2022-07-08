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

#pragma warning disable IDE0044

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        string OPEN_ARGUMENT = "%open";
        string CLOSE_ARGUMENT = "%close";
        string SWITCH_ARGUMENT = "%switch";
        string OPENING_DONE_ARGUMENT = "%opened";
        string CLOSING_DONE_ARGUMENT = "%closed";

        string RESERVE_ARGUMENT = "%reserve";
        string UNRESERVE_ARGUMENT = "%remove_reserve";
        string SWITCH_RESERVE_ARGUMENT = "%switch_reserve";

        string ASSIGN_DOCKNUM_ARGUMENT = "%docknum";

        public int dockNum = 0;
        bool open = false;
        bool changing = false;
        bool available { get { return !changing && open && !isOccupied() && !reserved; } }
        bool reserved = false;

        IMyPistonBase piston;
        List<IMyAirtightHangarDoor> doors;

        IMyShipConnector connector;

        List<IMyInteriorLight> StatusLights;

        IMyTextPanel lcd;

        bool configurationDone = false;


        GridUtils gridUtils = new GridUtils();
        public Program()
        {
            string[] split = Storage.Split(' ');

            if (split.Length >= 2)
            {
                bool.TryParse(split[0], out open);
                int.TryParse(split[1], out dockNum);
                configurationDone = true;
            }

            string PREFIX = $"[SWM Dock {dockNum}]";

            gridUtils.Init(this);
            gridUtils.Prefix = PREFIX;

            AddBlocks();

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            Storage = open.ToString() + " " + dockNum.ToString();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ProcessCmd(argument);

            if (!configurationDone)
            {
                Echo("Configuration hasn't been completed yet! Dock num :" + dockNum);
                return;
            }
            PrintStatus();
            UpdateLights();
            CheckConnectors();

            if (reserved || isOccupied())
                doors.ForEach((d) => { if (d.Status != DoorStatus.Open) d.OpenDoor(); });

            changing = doors.Any((d) => d.Status == DoorStatus.Opening || d.Status == DoorStatus.Closing);
            open = doors.Any((d) => d.Status == DoorStatus.Open || d.Status == DoorStatus.Closing);

            if (reserved && isOccupied())
                reserved = false;

            if(changing && doors.All((d) => d.Status == DoorStatus.Open))
            {
                changing = false;
                open = true;
            }

            piston.Velocity = reserved || isOccupied() ? 2 : -2;

            Me.CustomData = (available ? "AVAILABLE" : "UNAVAILABLE") + "\n" + (reserved ? "RESERVED" : "NOT RESERVED") + "\n" + (open ? "OPEN" : "CLOSED");
        }
        void PrintStatus()
        {
            string status = "";
            status += "#####  SwarmMiner #####\n";
            //status += "#####  MANUAL OVERRIDE #####\n";
            status += "#####  This is dock " + dockNum + "  #####\n\n";
            status += "Connection: Connected\n\n";
            bool isAvailable = CanChangeState() && open;
            status += "Dock is currently " + (!changing ? (open ? (available ? "available" : (reserved ? "reserved" : "busy")) : "closed") : (open ? "closing" : "opening")) + ".\n\n";
            if (!available)
            {
                if (isOccupied())
                {
                    status += "A ship is currently docked.\n";
                }
            }

            if (!changing)
            {
                status += "\nDock " + (CanChangeState() ? "can" : "can't") + " " + (open ? "close" : "open") + " right now.\n";
            }
            Echo(status);

            if (lcd != null)
            {
                lcd.WriteText(status);
            }
            else
            {
                Echo("An LCD Panel is supported, but not present.");
            }
        }
        void UpdateLights()
        {
            if (changing)
            {
                /*
                foreach (IMyLightingBlock warninglight in WarningLights)
                {
                    warninglight.Enabled = true;
                }*/

                foreach (IMyInteriorLight statuslight in StatusLights)
                {
                    statuslight.Enabled = true;
                    statuslight.Color = new Color(255, 192, 0);
                    statuslight.BlinkIntervalSeconds = 1;
                }
            }
            else
            {
                /*
                foreach (IMyLightingBlock warninglight in WarningLights)
                {
                    warninglight.Enabled = false;
                }
                */
                foreach (IMyInteriorLight statuslight in StatusLights)
                {
                    statuslight.Enabled = true;
                    statuslight.Color = open ? (isOccupied() ? new Color(0, 255, 0) : new Color(192, 255, 128)) : new Color(255, 0, 0);

                    statuslight.BlinkIntervalSeconds = reserved ? 1 : 0;
                }

            }
        }

        void ProcessCmd(string argument)
        {
            string[] split = argument.Split(' ');
            if (split.Length == 0) { return; }
            if (split[0] == ASSIGN_DOCKNUM_ARGUMENT)
                int.TryParse(split[1], out dockNum);
            else if (split[0] == OPEN_ARGUMENT)
                Open();
            else if (split[0] == CLOSE_ARGUMENT)
                Close();

            else if (split[0] == OPENING_DONE_ARGUMENT)
                ActionDone(true);
            else if (split[0] == CLOSING_DONE_ARGUMENT)
                ActionDone(false);

            else if (split[0] == SWITCH_ARGUMENT)
                if (open) { Close(); } else { Open(); }

            else if (split[0] == RESERVE_ARGUMENT)
                reserved = available || reserved;
            else if (split[0] == UNRESERVE_ARGUMENT)
                reserved = false;
            else if (split[0] == SWITCH_RESERVE_ARGUMENT)
                reserved = reserved ? false : available;

        }

        void AddBlocks()
        {
            try
            {
                gridUtils.GetMyBlocksWithTag("Status Light", out StatusLights, false);
                Echo("Lights present.");


                connector = gridUtils.GetMyBlock<IMyShipConnector>(false);
                Echo("Connectors present.");

                doors = gridUtils.GetBlockGroupWithTagAsList<IMyAirtightHangarDoor>("[SWM Dock]");
                Echo("Doors present.");

                piston = gridUtils.GetMyBlock<IMyPistonBase>(false);
                Echo("Piston present.");

                try
                {
                    lcd = gridUtils.GetMyBlockWithTag<IMyTextPanel>("[SWM Dock " + dockNum + "]", false);
                }
                catch
                {
                    Echo("An LCD Panel is supported, but not present.");
                }
            }
            catch (MissingBlockException)
            {
                Echo("Missing blocks found!\n");
                configurationDone = false;
            }
        }
        void Open()
        {
            if (!CanChangeState()) { return; }

            doors.ForEach((d) => d.OpenDoor());
        }
        void Close()
        {
            if (!CanChangeState()) { return; }

            doors.ForEach((d) => d.CloseDoor());
        }

        void CheckConnectors()
        {
            if (!isOccupied())
                return;
        }
        void ActionDone(bool _open)
        {
            changing = false;
            open = _open;
        }

        bool CanChangeState()
        {
            if (open && (isOccupied() || reserved)) { return false; }
            return !changing;
        }

        bool isOccupied()
        {
            return connector.Status == MyShipConnectorStatus.Connected;
        }
    }
}
