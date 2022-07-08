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
        public class Orientation
        {
            public IMyRemoteControl rc;
            public GridUtils gridUtils;

            public MyGridProgram program;

            public void Init(Program prog, GridUtils i)
            {
                program = prog;
                gridUtils = i;

                rc = gridUtils.GetBlock<IMyRemoteControl>();
            }

            public Vector3 Cockpit2ShipRotation(Vector3 Vector)
            {
                Matrix OrientationMatrix;
                rc.Orientation.GetMatrix(out OrientationMatrix);
                Vector3 rotatedUp = Vector3.TransformNormal(Vector, OrientationMatrix);
                return rotatedUp;
            }

            public Quaternion Cockpit2ShipRotation(MyBlockOrientation Orientation, Quaternion quat)
            {
                Vector3 fwd = quat.Forward;
                Vector3 up = quat.Up;

                fwd = Cockpit2ShipRotation(fwd);
                up = Cockpit2ShipRotation(up);

                return Quaternion.CreateFromForwardUp(fwd, up);
            }

            public Quaternion GetShipRotation()
            {
                /*
                IMyShipController controller = GetBlock<IMyShipController>();

                Vector3 f_ship = Cockpit2ShipRotation(controller.Orientation, Vector3I.Forward);
                Vector3 u_ship = Cockpit2ShipRotation(controller.Orientation, Vector3I.Up);

                Vector3 origin =    GetBlock<IMyShipController>().CubeGrid.GridIntegerToWorld(Vector3I.Zero);
                Vector3 forward =   Vector3.Normalize(GetBlock<IMyShipController>().CubeGrid.GridIntegerToWorld((Vector3I)Cockpit2ShipRotation(controller.Orientation, f_ship)) - origin);
                Vector3 up =        Vector3.Normalize(GetBlock<IMyShipController>().CubeGrid.GridIntegerToWorld((Vector3I)Cockpit2ShipRotation(controller.Orientation, u_ship)) - origin);

                Quaternion rotation = Quaternion.CreateFromForwardUp(forward, up);

                return rotation;*/


                return Quaternion.CreateFromForwardUp(rc.WorldMatrix.Forward, rc.WorldMatrix.Up);
            }

            public static Vector3 WorldToLocal(Vector3 worldPos, Matrix WorldMatrix)
            {
                Vector3 localPos = WorldMatrix.Translation;
                Vector3 diff = worldPos - localPos;

                return Vector3.TransformNormal(diff, MatrixD.Transpose(WorldMatrix));
            }
        }
    }
}
