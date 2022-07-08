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
        string START_COLLECTION_ARGUMENT = "%start_collection";
        string STOP_COLLECTION_ARGUMENT = "%stop_collection";
        string SWITCH_COLLECTION_ARGUMENT = "%switch_collection";

        string RESERVE_ARGUMENT = "%reserve";
        string UNRESERVE_ARGUMENT = "%remove_reserve";
        string SWITCH_RESERVE_ARGUMENT = "%switch_reserve";

        string ASSIGN_DOCKNUM_ARGUMENT = "%docknum";

        public int dockNum = 0;
        bool open = false;
        bool changing = false;
        bool extracting = false;
        bool isShipEmpty = false;
        bool available { get { return !changing && open && !isOccupied() && !reserved; } }
        bool reserved = false;

        IMyTimerBlock startOpening;
        IMyTimerBlock startClosing;
        IMyTimerBlock airTimer;

        IMyShipConnector ConnectorA;
        IMyShipConnector ConnectorB;

        IMyConveyorSorter Pump;

        List<IMyInteriorLight> StatusLights;
        List<IMyLightingBlock> WarningLights;

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
            if(configurationDone)
                Storage = open.ToString() + " " + dockNum.ToString();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ProcessCmd(argument);

            if (!configurationDone) {
                Echo("Configuration hasn't been completed yet! Dock num :" + dockNum);
                return;
            }
            PrintStatus();
            UpdateLights();
            CheckConnectors();

            if (reserved && isOccupied())
                reserved = false;

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
            if (open || (changing && !open))
            {
                if (airTimer.IsCountingDown)
                {
                    status += "\n\nDock is about to be DEPRESSURIZED.";
                }
                else
                {
                    status += "\n\nDock is currently NOT PRESSURIZED.";
                }
            }
            Echo(status);

            if(lcd != null)
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
            {
                int.TryParse(split[1], out dockNum);
                configurationDone = true;
            }
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
                //gridUtils.GetMyBlocksWithTag("Warning Light", out WarningLights, false);
                Echo("Lights present.");

                startOpening = gridUtils.GetMyBlockWithTag<IMyTimerBlock>("[Timer] [Open]", false);
                startClosing = gridUtils.GetMyBlockWithTag<IMyTimerBlock>("[Timer] [Close]", false);
                airTimer = gridUtils.GetMyBlockWithTag<IMyTimerBlock>("[Timer] [Air]", false);
                Echo("Timers present.");

                ConnectorA = gridUtils.GetMyBlockWithTag<IMyShipConnector>("[Connector A]", false);

                try
                {
                    ConnectorB = gridUtils.GetMyBlockWithTag<IMyShipConnector>("[Connector B]", false);
                }
                catch (MissingBlockException) { }

                Echo("Connectors present.");

                Pump = gridUtils.GetMyBlockWithTag<IMyConveyorSorter>("[Pump]", false);

                try
                {
                    lcd = gridUtils.GetMyBlockWithTag<IMyTextPanel>("[SWM] [Dock " + dockNum + "]", false);
                }
                catch
                {
                    Echo("An LCD Panel is supported, but not present.");
                }
            }
            catch(MissingBlockException)
            {
                Echo("Missing blocks found!\n");
                configurationDone = false;
            }
        }
        void Open()
        {
            if (!CanChangeState()) { return; }
            changing = true;

            startOpening.Trigger();
        }
        void Close()
        {
            if (!CanChangeState()) { return; }
            changing = true;

            startClosing.Trigger();
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
            if ( open && (isOccupied() || reserved)) { return false; }
            return !changing;
        }

        bool isOccupied()
        {
            return ConnectorA.Status == MyShipConnectorStatus.Connected || ConnectorB?.Status == MyShipConnectorStatus.Connected;
        }
    }
}
