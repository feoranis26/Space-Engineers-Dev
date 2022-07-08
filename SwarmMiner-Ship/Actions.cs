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
        public class Actions
        {
            //TODO Movement path for miners when moving into holes
            //TODO Disable dampeners when docked
            //TODO Bitmask state enums
            //TODO Job size visualization

            public Program prog;
            public Motion motion;
            public Comms comms;


            List<IMyShipDrill> drills;
            List<IMyCargoContainer> containers;
            List<IMyGasTank> tanks;
            List<IMyBatteryBlock> batteries;
            List<IMyShipConnector> connectors;
            //List<IMyShipConnector> ejectors;

            List<MyDetectedEntityInfo> detected = new List<MyDetectedEntityInfo>();

            IMySensorBlock OreSensor;

            public bool busy = false;

            public bool Docked = false;

            public GlobalPose? PoseTarget;
            public GlobalRot? RotTarget;
            public GlobalPos? PosTarget;

            public float posDiff;
            public float rotDiff;

            public IEnumerator<bool> CurrentCommand;

            public float precision = 5;
            public bool Aborted = false;

            public void Init(Program p, Motion m, Comms c)
            {
                prog = p;
                motion = m;
                comms = c;

                p.gridUtils.GetBlocks(out drills);
                p.gridUtils.GetBlocks(out containers);
                p.gridUtils.GetMyBlocksWithTag("[Connector]", out connectors);
                //p.gridUtils.GetMyBlocksWithTag("[Ejector]", out ejectors);
                try
                {
                    p.gridUtils.GetBlocks(out tanks);
                }
                catch (MissingBlockException)
                {
                    tanks = new List<IMyGasTank>();
                }
                p.gridUtils.GetBlocks(out batteries);


                try
                {
                    OreSensor = p.gridUtils.GetMyBlockWithTag<IMySensorBlock>("[Ore Sensor]");
                }
                catch (MissingBlockException)
                {
                    p.Echo("No ore safety sensor detected. Speed limit will ALWAYS be 1 m/s when mining.");
                }

                motion.EnableManualControl();

                foreach (IMyShipDrill drill in drills)
                    drill.Enabled = false;
            }

            public void Tick(UpdateType update)
            {
                if (prog.state == MinerState.Unknown)
                {
                    motion.EnableManualControl();
                    return;
                }

                Docked = CheckDock();

                if (!Docked)
                {
                    if (PoseTarget.HasValue)
                    {
                        motion.MoveForwardTo(PoseTarget.Value.pos, PoseTarget.Value.rot, out posDiff, out rotDiff);
                    }
                    else
                    {
                        if (PosTarget.HasValue)
                            posDiff = motion.MoveNode(PosTarget.Value);

                        if (RotTarget.HasValue)
                            rotDiff = motion.RotateTo(RotTarget.Value);
                    }
                }
                else
                {
                    motion.EnableManualControl();
                }

                RunCurrentCommand();

                if ((update & UpdateType.Update10) > 0)
                {
                    CheckResources();
                }
            }

            public void RunCurrentCommand()
            {
                if (CurrentCommand != null)
                {
                    busy = CurrentCommand.MoveNext();

                    if (!busy)
                    {
                        CurrentCommand.Dispose();
                        CurrentCommand = null;
                    }
                }
                else
                    busy = false;
            }

            public void Abort()
            {
                //CurrentCommand = null;
                //motion.EnableManualControl();
                Aborted = true;
            }


            public void GotoCMD(string cmd)
            {
                List<string> split = cmd.Split(';').ToList();
                split.Reverse();

                split.Pop();
                split.Pop();
                split.Pop();

                GlobalPos pos = new GlobalPos(split.Pop().ToVector());

                Vector3 f = split.Pop().ToVector();
                Vector3 u = split.Pop().ToVector();
                GlobalRot rot = new GlobalRot(f, u);

                split.Pop();

                precision = float.Parse(split.Pop());

                CurrentCommand = Goto(pos, rot);
                busy = true;
            }

            public void MineCMD(string cmd)
            {
                List<string> split = cmd.Split(';').ToList();
                split.Reverse();

                split.Pop();
                split.Pop();
                split.Pop();

                GlobalPos pos = new GlobalPos(split.Pop().ToVector());

                Vector3 f = split.Pop().ToVector();
                Vector3 u = split.Pop().ToVector();
                GlobalRot rot = new GlobalRot(f, u);

                float depth = float.Parse(split.Pop());

                float speed = float.Parse(split.Pop());
                float fast_speed = float.Parse(split.Pop());
                float return_speed = float.Parse(split.Pop());

                CurrentCommand = Mine(pos, rot, depth, speed, fast_speed, return_speed);
                busy = true;
            }

            public IEnumerator<bool> Goto(GlobalPos target, GlobalRot rot)
            {
                PosTarget = target;
                RotTarget = rot;
                motion.NodeMoveStart = new LocalPos(new Vector3(0, 0, 0.01f)).ToGlobal(motion.rc.WorldMatrix);

                posDiff = 10;
                rotDiff = 10;

                while (posDiff > precision || rotDiff > 0.1)
                {
                    prog.Echo("posDiff: " + posDiff);
                    prog.Echo("rotDiff: " + rotDiff);
                    yield return true;
                }

                comms.CMDEnd();
                yield return false;
            }

            public IEnumerator<bool> Mine(GlobalPos start, GlobalRot rot, float depth, float speed, float fast_speed, float return_speed)
            {
                GlobalPos? temp_tgt = PosTarget;
                GlobalRot? temp_rot = RotTarget;
                PosTarget = null;
                RotTarget = null;

                Aborted = false;

                foreach (IMyShipDrill drill in drills)
                    drill.Enabled = true;

                float distToGo = depth;
                while (distToGo > 1 && !Aborted)
                {
                    float temp_spd = OreSensor.IsActive ? speed : fast_speed;

                    prog.Echo("MINING");

                    distToGo = depth - motion.MoveAxis(temp_spd, start, new GlobalDir(rot.forward));
                    motion.RotateTo(rot);
                    yield return true;
                }

                comms.CMDEnd(Aborted);

                foreach (IMyShipDrill drill in drills)
                    drill.Enabled = false;

                distToGo = depth;
                while (distToGo > 1)
                {
                    distToGo = motion.MoveAxis(Math.Max(-return_speed * 5, -5), start, new GlobalDir(rot.forward));
                    motion.RotateTo(rot);
                    yield return true;
                }

                motion.EnableManualControl();

                PosTarget = temp_tgt;
                RotTarget = temp_rot;
                yield return false;
            }

            public void Dock()
            {
                foreach (IMyShipConnector con in connectors)
                    if (con.Status != MyShipConnectorStatus.Connectable)
                        return;

                foreach (IMyShipConnector con in connectors)
                    con.Connect();

                foreach (IMyShipConnector con in connectors)
                    if (con.Status != MyShipConnectorStatus.Connected)
                        return;

                foreach (IMyBatteryBlock bat in batteries)
                    bat.ChargeMode = ChargeMode.Recharge;

                foreach (IMyGasTank tank in tanks)
                    tank.Stockpile = true;

                Docked = true;
            }

            public void Undock()
            {
                prog.Echo("Undocking!");
                foreach (IMyBatteryBlock bat in batteries)
                    bat.ChargeMode = ChargeMode.Auto;

                foreach (IMyGasTank tank in tanks)
                    tank.Stockpile = false;

                foreach (IMyShipConnector con in connectors)
                    con.Disconnect();
                Docked = false;
            }

            public bool CheckDock()
            {
                foreach (IMyShipConnector con in connectors)
                    if (con.Status == MyShipConnectorStatus.Connected)
                        return true;

                return false;
            }

            public void CheckResources()
            {
                bool areAllInventoriesFull = true;
                bool allInventoriesEmpty = true;
                bool hasStone = false;
                foreach (IMyCargoContainer container in containers)
                {
                    if (container.GetInventory().CurrentVolume < container.GetInventory().MaxVolume - 1)
                        areAllInventoriesFull = false;

                    List<MyInventoryItem> items = new List<MyInventoryItem>();
                    if (container.GetInventory().FindItem(new MyItemType("MyObjectBuilder_Ore", "Stone")) != null)
                        hasStone = true;

                    if (container.GetInventory().CurrentVolume > 1)
                        allInventoriesEmpty = false;
                }

                bool batteriesEmpty = true;
                bool batteriesFull = true;
                foreach (IMyBatteryBlock battery in batteries)
                {
                    if (battery.CurrentStoredPower < battery.MaxStoredPower - 0.5)
                        batteriesFull = false;

                    if (battery.CurrentStoredPower > 0.5)
                        batteriesEmpty = false;
                }

                bool tanksEmpty = true;
                bool tanksFull = true;
                foreach (IMyGasTank tank in tanks)
                {
                    if (tank.FilledRatio < 0.9)
                        tanksFull = false;

                    if (tank.FilledRatio > 0.3)
                        tanksEmpty = false;
                }

                if (prog.state == MinerState.H2Low && tanksFull)
                    prog.state = MinerState.Ready;

                if (prog.state == MinerState.OutOfCharge && batteriesFull)
                    prog.state = MinerState.Ready;

                if (prog.state == MinerState.CargoFull && allInventoriesEmpty)
                    prog.state = MinerState.Ready;

                if (prog.state == MinerState.Ejecting && !hasStone)
                    prog.state = MinerState.Ready;

                //if ((prog.state == MinerState.Active || prog.state == MinerState.Ready) && areAllInventoriesFull && hasStone)
                //    prog.state = MinerState.Ejecting;

                if ((prog.state == MinerState.Active || prog.state == MinerState.Ready) && areAllInventoriesFull)
                    prog.state = MinerState.CargoFull;

                if ((prog.state == MinerState.Active || prog.state == MinerState.Ready) && batteriesEmpty)
                    prog.state = MinerState.OutOfCharge;

                if ((prog.state == MinerState.Active || prog.state == MinerState.Ready) && tanksEmpty && tanks.Count > 0)
                    prog.state = MinerState.H2Low;
            }
        }
    }
}
