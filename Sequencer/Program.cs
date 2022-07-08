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
        Dictionary<string, BlockData> Blocks = new Dictionary<string, BlockData>();
        Dictionary<string, Step> Steps = new Dictionary<string, Step>();
        Dictionary<string, Action> Actions = new Dictionary<string, Action>();

        GridUtils gridUtils = new GridUtils();

        IEnumerator<bool> runAction;

        public Program()
        {
            gridUtils.Init(this);

            ParseConfig(Me.CustomData);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                string[] split = argument.Split(' ');
                if (split[0] == "run")
                {
                    runAction = RunAction(split[1]);
                }
            }
            if (runAction != null) //An action is being executed!
            {
                if (!runAction.MoveNext())
                {
                    runAction.Dispose();
                    runAction = null;
                }
            }
        }

        public IEnumerator<bool> RunAction(string actionName)
        {
            Action action = Actions[actionName];

            if (action == null)
                throw new Exception($"No action with {actionName} found.");

            foreach (Step step in action.steps)
            {
                while (!ExecuteStep(step))
                    yield return true;
            }
            yield return false;

        }

        public bool ExecuteStep(Step step)
        {
            foreach (BlockAction action in step.Actions)
            {
                BlockData block = action.Block;

                if (block.Name == "")
                {
                    throw new MissingBlockException($"Error while executing \"{step.Name}\": Block with name \"{block.Name}\" not found.");
                }

                if (block.Type == BlockType.Piston)
                {
                    foreach (IMyTerminalBlock blk in block.Blocks)
                    {
                        IMyPistonBase piston = (IMyPistonBase)blk;

                        piston.Velocity = action.Value.Value;
                    }
                }
                else if (block.Type == BlockType.ToggleOnOff)
                {
                    foreach (IMyTerminalBlock blk in block.Blocks)
                    {
                        if (blk is IMyShipMergeBlock)
                            ((IMyShipMergeBlock)blk).Enabled = action.BoolValue.Value;
                        else if (blk is IMyAirtightHangarDoor)
                        {
                            if (action.BoolValue.Value)
                                ((IMyAirtightHangarDoor)blk).OpenDoor();
                            else
                                ((IMyAirtightHangarDoor)blk).CloseDoor();
                        }
                    }
                }
                else if (block.Type == BlockType.Rotor)
                {
                    if(action.BoolValue != null)
                    {
                        foreach (IMyTerminalBlock blk in block.Blocks)
                        {
                            if (action.BoolValue.Value)
                                ((IMyMotorStator)blk).Attach();
                            else
                                ((IMyMotorStator)blk).Detach();
                        }
                    }
                }
            }

            BlockData WaitBlock = step.Wait.Block;

            if (WaitBlock.TerminalName == "")
            {
                throw new MissingBlockException($"Error while executing \"{step.Name}\": Block with name \"{WaitBlock.TerminalName}\" not found.");
            }

            if (step.WaitForGreaterThan)
            {
                if (WaitBlock.Type == BlockType.Piston)
                    return ((IMyPistonBase)WaitBlock.Blocks[0]).CurrentPosition > step.Wait.Value;
                else if (WaitBlock.Type == BlockType.Rotor)
                    return ((IMyMotorStator)WaitBlock.Blocks[0]).Angle > step.Wait.Value;
                else if (WaitBlock.Type == BlockType.ToggleOnOff)
                {
                    if (WaitBlock.Blocks[0] is IMyShipMergeBlock)
                        return ((IMyShipMergeBlock)WaitBlock.Blocks[0]).IsConnected;
                    else if (WaitBlock.Blocks[0] is IMyAirtightHangarDoor)
                        return ((IMyAirtightHangarDoor)WaitBlock.Blocks[0]).Status == DoorStatus.Closed;
                }

                return false;
            }
            else
            {
                if (WaitBlock.Type == BlockType.Piston)
                    return ((IMyPistonBase)WaitBlock.Blocks[0]).CurrentPosition < step.Wait.Value;
                else if (WaitBlock.Type == BlockType.Rotor)
                    return ((IMyMotorStator)WaitBlock.Blocks[0]).Angle < step.Wait.Value;
                else if (WaitBlock.Type == BlockType.ToggleOnOff)
                {
                    if (WaitBlock.Blocks[0] is IMyShipMergeBlock)
                        return !((IMyShipMergeBlock)WaitBlock.Blocks[0]).IsConnected;
                    else if (WaitBlock.Blocks[0] is IMyAirtightHangarDoor)
                        return ((IMyAirtightHangarDoor)WaitBlock.Blocks[0]).Status == DoorStatus.Open;
                }

                return false;
            }
        }

        #region ConfigParse
        void ParseConfig(string data)
        {
            MyIni p = new MyIni();
            MyIniParseResult res;
            if (!p.TryParse(data, out res))
            {
                throw new ArgumentException("Failed to parse!");
            }

            ParseBlocks(p);

            List<string> sections = new List<string>();
            p.GetSections(sections);

            foreach (string section_name in sections)
            {
                if (section_name.Split(' ')[0] == "Step")
                    ParseStep(section_name, p);
                else if (section_name.Split(' ')[0] == "Action")
                    ParseAction(section_name, p);
            }
        }
        void ParseBlocks(MyIni ini)
        {
            List<MyIniKey> blocks = new List<MyIniKey>();
            ini.GetKeys("Blocks", blocks);

            foreach (MyIniKey blockNameKey in blocks)
            {
                string blockName = blockNameKey.Name;
                string blockTerminalName = ini.Get("Blocks", blockName).ToString();
                Echo(blockName);

                List<IMyTerminalBlock> blks;

                gridUtils.GetBlocksWithTag( blockTerminalName, out blks);

                BlockData data = new BlockData(blockName, blockTerminalName, blks[0] is IMyPistonBase ? BlockType.Piston : (blks[0] is IMyShipMergeBlock || blks[0] is IMyAirtightHangarDoor ? BlockType.ToggleOnOff : BlockType.Rotor), blks);
                Blocks.Add(blockName, data);
            }
        }
        void ParseStep(string section_name, MyIni ini)
        {
            Step step = new Step();
            step.Name = section_name.Split(' ')[1];

            List<MyIniKey> Keys = new List<MyIniKey>();
            ini.GetKeys(section_name, Keys);

            foreach (MyIniKey blockNameKey in Keys)
            {


                if (blockNameKey.Name == "WaitType" || blockNameKey.Name == "Wait")
                    continue;

                string blockName = blockNameKey.Name;
                BlockData block;
                try
                {
                    block = Blocks[blockName];
                }
                catch (KeyNotFoundException)
                {
                    throw new MissingBlockException($"Error while parsing \"{section_name}\": Block with name \"{blockName}\" not found.");
                }
                float? blockValue = null;
                bool? blockBoolValue = null;

                string key = ini.Get(section_name, blockNameKey.Name).ToString();
                if (block.Type != BlockType.ToggleOnOff)
                {
                    if (block.Type == BlockType.Rotor && key == "Detach" || key == "Attach")
                        blockBoolValue = key == "Attach";
                    else
                        blockValue = ini.Get(section_name, blockNameKey.Name).ToSingle();
                }
                else
                    blockBoolValue = ini.Get(section_name, blockNameKey.Name).ToBoolean();

                step.Actions.Add(new BlockAction(block, blockValue, blockBoolValue));
            }

            string waitData = ini.Get(section_name, "Wait").ToString();

            if (waitData == "")
                throw new Exception($"Error while parsing \"{section_name}\": No wait condition specified.");

            string[] split = waitData.Split(':');

            BlockData waitBlock;
            try
            {
                waitBlock = Blocks[split[0]];
            }
            catch (KeyNotFoundException)
            {
                throw new MissingBlockException($"Error while parsing wait data for \"{section_name}\": Block with name \"{split[1]}\" not found.");
            }

            float waitValue = 0;

            if (!(waitBlock.Blocks[0] is IMyShipMergeBlock || waitBlock.Blocks[0] is IMyAirtightHangarDoor) && !float.TryParse(split[2], out waitValue))
                throw new Exception($"Error while parsing \"{section_name}\": Wait condition illegal.");

            step.Wait = new BlockAction(waitBlock, waitValue);

            if (step.Wait.Block.Name == "")
                throw new Exception($"Error while parsing \"{section_name}\": No wait condition specified.");

            string waitType = split[1];
            if (waitType == "")
                throw new Exception($"Error while parsing \"{section_name}\": No WaitType specified.");
            else if (waitType == "GreaterThan" || waitType == "Locked")
                step.WaitForGreaterThan = true;
            else if (waitType == "LessThan" || waitType == "Unlocked")
                step.WaitForGreaterThan = false;
            else
                throw new Exception($"Error while parsing \"{section_name}\": Invalid WaitType \"{waitType}\". Expected \"LessThan\", \"GreaterThan\", \"Locked\" or \"Unlocked\"");

            Steps.Add(step.Name, step);
        }
        void ParseAction(string section_name, MyIni ini)
        {
            Action action = new Action();
            action.Name = section_name.Split(' ')[1];

            string[] stepNames = ini.Get(section_name, "Steps").ToString().Split(',');

            foreach (string stepName in stepNames)
            {
                Step step;
                try
                {
                    step = Steps[stepName];
                }
                catch (KeyNotFoundException)
                {
                    throw new Exception($"Error while parsing \"{section_name}\": Step with name \"{stepName}\" not found.");
                }

                action.steps.Add(step);
            }
            Actions.Add(action.Name, action);
        }
        #endregion
        #region Data

        public struct BlockData
        {
            public string Name;
            public string TerminalName;
            public BlockType Type;
            public List<IMyTerminalBlock> Blocks;

            public BlockData(string name, string terminalName, BlockType type, List<IMyTerminalBlock> blocks)
            {
                Name = name;
                TerminalName = terminalName;
                Type = type;
                Blocks = blocks;
            }
        }

        public struct BlockAction
        {
            public BlockData Block;
            public float? Value;
            public bool? BoolValue;
            public BlockAction(BlockData block, float? value, bool? boolValue = false)
            {
                Block = block;
                Value = value;
                BoolValue = boolValue;
            }
        }

        public enum BlockType
        {
            Piston = 0,
            Rotor = 1,
            ToggleOnOff = 3,
        }

        public class Step
        {
            public string Name;
            public List<BlockAction> Actions = new List<BlockAction>();
            public BlockAction Wait;
            public bool WaitForGreaterThan;
        }

        public class Action
        {
            public string Name;
            public List<Step> steps = new List<Step>();
        }
        #endregion
    }
}
