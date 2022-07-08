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
        public class GridUtils
        {
            IMyGridTerminalSystem GridTerminalSystem;

            public MyGridProgram program;

            public string Prefix;
            public void Init(MyGridProgram prog)
            {
                program = prog;
                GridTerminalSystem = prog.GridTerminalSystem;
            }
            public bool GetBlocks<T>(out List<T> Blocks, bool onThisGrid = true) where T : class, IMyTerminalBlock
            {
                List<T> blocks = new List<T>();
                GridTerminalSystem.GetBlocksOfType(blocks, (block) => !onThisGrid || IsBlockOnThisGrid(block));


                if (blocks.Count > 0)
                {
                    Blocks = blocks;
                    return true;
                }
                throw new MissingBlockException();
            }
            public bool GetMyBlocks<T>(out List<T> Blocks, bool onThisGrid = true) where T : class, IMyTerminalBlock
            {                List<T> blocks = new List<T>();
                GridTerminalSystem.GetBlocksOfType(blocks, (block) => IsBlockMine(block, onThisGrid));


                if (blocks.Count > 0)
                {
                    Blocks = blocks;
                    return true;
                }
                throw new MissingBlockException();
            }

            public bool GetMyBlocksWithTag<T>(string Tag, out List<T> Blocks, bool onThisGrid = true) where T : class, IMyTerminalBlock
            {
                List<T> blocks = new List<T>();
                GridTerminalSystem.GetBlocksOfType(blocks, (block) => IsBlockMine(block, onThisGrid) && block.CustomName.Contains(Tag));


                if (blocks.Count > 0)
                {
                    Blocks = blocks;
                    return true;
                }
                program.Echo(Tag);
                throw new MissingBlockException();
                
            }

            public bool GetBlocksWithTag<T>(string Tag, out List<T> Blocks) where T : class, IMyTerminalBlock
            {
                List<T> blocks = new List<T>();
                GridTerminalSystem.GetBlocksOfType(blocks, (block) => block.CustomName.Contains(Tag));


                if (blocks.Count > 0)
                {
                    Blocks = blocks;
                    return true;
                }
                program.Echo(Tag);
                throw new MissingBlockException();

            }

            public bool IsBlockMine(IMyTerminalBlock Block, bool onThisGrid = true)
            {
                return Block.CustomName.Contains(Prefix) && (Block.CubeGrid == program.Me.CubeGrid || !onThisGrid);
            }

            public bool IsBlockOnThisGrid(IMyTerminalBlock Block)
            {
                return Block.CubeGrid == program.Me.CubeGrid;
            }
            public T GetBlock<T>() where T : class, IMyTerminalBlock
            {
                List<T> blocks;
                if (GetBlocks(out blocks))
                {
                    return blocks[0];
                }
                throw new MissingBlockException();
            }

            public T GetMyBlock<T>(bool onThisGrid = true) where T : class, IMyTerminalBlock
            {
                List<T> blocks;
                if (GetMyBlocks(out blocks, onThisGrid))
                {
                    return blocks[0];
                }
                throw new MissingBlockException();
            }

            public T GetMyBlockWithTag<T>(string Tag, bool onThisGrid = true) where T : class, IMyTerminalBlock
            {
                List<T> blocks;
                if (GetMyBlocksWithTag<T>(Tag, out blocks, onThisGrid))
                {
                    return blocks[0];
                }
                program.Echo(Tag);
                throw new MissingBlockException();
            }

            public T GetBlockWithTag<T>(string Tag) where T : class, IMyTerminalBlock
            {
                List<T> blocks;
                if (GetBlocksWithTag<T>(Tag, out blocks))
                {
                    return blocks[0];
                }
                program.Echo(Tag);
                throw new MissingBlockException();
            }

            public IMyBlockGroup GetBlockGroupWithTag(string Tag, bool mine = false)
            {
                program.Echo("Searching for group with tag: " + Tag);
                List<IMyBlockGroup> group = new List<IMyBlockGroup>();
                GridTerminalSystem.GetBlockGroups(group, (block) => (!mine || IsGroupMine(block)) && block.Name.Contains(Tag));


                if (group.Count > 0)
                {
                    return group[0];
                }
                throw new MissingBlockException();
            }

            public List<IMyBlockGroup> GetBlockGroupsWithTag(string Tag, bool mine = true)
            {
                program.Echo("Searching for group with tag: " + Tag);
                List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
                GridTerminalSystem.GetBlockGroups(groups, (block) => (!mine || IsGroupMine(block)) && block.Name.Contains(Tag));


                if (groups.Count > 0)
                {
                    return groups;
                }
                throw new MissingBlockException();
            }

            public List<T> GetBlockGroupWithTagAsList<T> (string Tag) where T : class, IMyTerminalBlock
            {
                IMyBlockGroup group = GetBlockGroupWithTag(Tag, false);

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                List<T> list = new List<T>();
                group.GetBlocks(blocks);

                if (blocks.Count > 0)
                {
                    foreach(T block in blocks)
                    {
                        list.Add(block);
                    }
                }
                return list;
            }

            public List<T> GetMyBlockGroupWithTagAsList<T>(string Tag) where T : class, IMyTerminalBlock
            {
                IMyBlockGroup group = GetBlockGroupWithTag(Tag);

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                List<T> list = new List<T>();
                group.GetBlocks(blocks);

                if (blocks.Count > 0)
                {
                    foreach (T block in blocks)
                    {
                        if(block.CubeGrid == program.Me.CubeGrid)
                            list.Add(block);
                    }
                }
                return list;
            }

            public List<T> GetBlockGroupsWithTagAsList<T>(string Tag) where T : class, IMyTerminalBlock
            {
                List<IMyBlockGroup> groups = GetBlockGroupsWithTag(Tag);

                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                List<T> list = new List<T>();

                foreach (IMyBlockGroup group in groups)
                {
                    group.GetBlocks(blocks);

                    if (blocks.Count > 0)
                    {
                        foreach (T block in blocks)
                        {
                            list.Add(block);
                        }
                    }
                }
                return list;
            }

            public bool IsGroupMine(IMyBlockGroup Group)
            {
                return Group.Name.Contains(Prefix);
            }
        }

        public class MissingBlockException : Exception
        {
            public MissingBlockException()
            {
            }

            public MissingBlockException(string message)
                : base(message)
            {
            }

            public MissingBlockException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}
