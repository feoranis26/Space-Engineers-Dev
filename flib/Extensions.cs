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
    // This template is intended for extension classes. For most purposes you're going to want a normal
    // utility class.
    // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods
    static class Extensions
    {
        public static Vector3 ToVector(this string data)
        {
            bool _;
            return ToVector(data, out _);
        }

        public static Vector3 ToVector(this string data, out bool successful)
        {
            string[] split = data.Split(' ');

            float x;
            float y;
            float z;
            if (float.TryParse(split[0], out x) && float.TryParse(split[1], out y) && float.TryParse(split[2], out z))
            {
                successful = true;
                return new Vector3(x / 100, y / 100, z / 100);
            }

            successful = false;
            return Vector3.Zero;
        }

        public static string EncodeString(this Vector3 data)
        {
            return Math.Floor(data.X * 100) + " " + Math.Floor(data.Y * 100) + " " + Math.Floor(data.Z * 100);
        }

        public static string EncodeString(this Matrix matrix)
        {
            return matrix.M11 + "," + matrix.M12 + "," + matrix.M13 + "," + matrix.M14 + "," +
                matrix.M21 + "," + matrix.M22 + "," + matrix.M23 + "," + matrix.M24 + "," +
                matrix.M31 + "," + matrix.M32 + "," + matrix.M33 + "," + matrix.M34 + "," +
                matrix.M41 + "," + matrix.M42 + "," + matrix.M43 + "," + matrix.M44;
        }

        public static Matrix ToMatrix(this string str)
        {
            string[] s = str.Split(',');
            return new Matrix(
                float.Parse(s[0]), float.Parse(s[1]), float.Parse(s[2]), float.Parse(s[3]),
                float.Parse(s[4]), float.Parse(s[5]), float.Parse(s[6]), float.Parse(s[7]),
                float.Parse(s[8]), float.Parse(s[9]), float.Parse(s[10]), float.Parse(s[11]),
                float.Parse(s[12]), float.Parse(s[13]), float.Parse(s[14]), float.Parse(s[15])
            );
        }
    }
}
