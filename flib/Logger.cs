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
        public class Log
        {
            List<LogEntry> entries = new List<LogEntry>();
            string displayFormat = "[{loggerName}]: {message} \n";
            public string Display(DateTime? since = null, List<Logger> filter = null)
            {
                string display = "";
                foreach (LogEntry entry in entries)
                {
                    if (since.HasValue && entry.entryTime < since.Value)
                        continue;

                    if (filter != null && !filter.Contains(entry.logger))
                        continue;

                    string log = displayFormat;
                    log = log.Replace("{loggerName}", entry.logger.Name);
                    log = log.Replace("{message}", entry.message);
                    log = log.Replace("{time}", entry.entryTime.ToShortTimeString());

                    display += log;
                }

                return display;
            }

            public void AddLog(LogEntry e)
            {
                entries.Insert(0, e);
            }
        }
        public class Logger
        {
            Log log;
            public string Name;

            public Logger(Log l, string n)
            {
                log = l;
                Name = n;
            }

            public void Log(string message)
            {
                LogEntry entry = new LogEntry(message, this);
                log.AddLog(entry);
            }
        }

        public struct LogEntry
        {
            public string message;
            public DateTime entryTime;
            public Logger logger;

            public LogEntry(string msg, Logger l)
            {
                message = msg;
                logger = l;
                entryTime = DateTime.Now;
            }
        }
    }
}
