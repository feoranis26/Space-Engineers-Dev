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
    public struct LocalPos
    {
        public Vector3 pos;
        public LocalPos(Vector3 p)
        {
            pos = p;
        }

        public LocalPos(GlobalPos p, Matrix WorldMatrix)
        {
            Vector3 localPos = WorldMatrix.Translation;
            Vector3 diff = p.pos - localPos;

            pos = Vector3.TransformNormal(diff, MatrixD.Transpose(WorldMatrix));
        }

        public GlobalPos ToGlobal(MatrixD worldMatrix)
        {
            return new GlobalPos(Vector3D.Transform(pos, worldMatrix));
        }
    }

    public struct GlobalPos
    {
        public Vector3 pos;
        public GlobalPos(Vector3 p)
        {
            pos = p;
        }

        public GlobalPos(LocalPos p, MatrixD worldMatrix)
        {
            pos = Vector3D.Transform(p.pos, worldMatrix);
        }

        public LocalPos ToLocal(MatrixD WorldMatrix)
        {
            Vector3 localPos = WorldMatrix.Translation;
            Vector3 diff = pos - localPos;

            return new LocalPos(Vector3.TransformNormal(diff, MatrixD.Transpose(WorldMatrix)));
        }
    }

    public struct LocalRot
    {
        public Vector3 forward;
        public Vector3 up;
        public LocalRot(Vector3 f, Vector3 u)
        {
            forward = f;
            up = u;
        }
        public LocalRot(GlobalRot r, MatrixD worldMatrix)
        {
            forward = Vector3D.TransformNormal(r.forward, MatrixD.Transpose(worldMatrix));
            up = Vector3D.TransformNormal(r.up, MatrixD.Transpose(worldMatrix));
        }

        public LocalRot(Quaternion q)
        {
            forward = q.Forward;
            up = q.Up;
        }

        public GlobalRot ToGlobal(MatrixD worldMatrix)
        {
            Vector3 t_forward = Vector3.Normalize(Vector3D.TransformNormal(forward, worldMatrix));
            Vector3 t_up = Vector3.Normalize(Vector3D.TransformNormal(up, worldMatrix));

            return new GlobalRot(t_forward, t_up);
        }

        public Quaternion GetQuaternion()
        {
            return Quaternion.CreateFromForwardUp(forward, up);
        }
    }

    public struct GlobalRot
    {
        public Vector3 forward;
        public Vector3 up;
        public GlobalRot(Vector3 f, Vector3 u)
        {
            forward = Vector3.Normalize(f);
            up = Vector3.Normalize(u);
        }
        public GlobalRot(LocalRot r, MatrixD worldMatrix)
        {
            forward = Vector3D.TransformNormal(r.forward, worldMatrix);
            up = Vector3D.TransformNormal(r.up, worldMatrix);
        }

        public GlobalRot(Quaternion q)
        {
            forward = q.Forward;
            up = q.Up;
        }
        public LocalRot ToLocal(MatrixD worldMatrix)
        {
            Vector3 t_forward = Vector3.Normalize(Vector3D.TransformNormal(forward, MatrixD.Transpose(worldMatrix)));
            Vector3 t_up = Vector3.Normalize(Vector3D.TransformNormal(up, MatrixD.Transpose(worldMatrix)));

            return new LocalRot(t_forward, t_up);
        }

        public Quaternion GetQuaternion()
        {
            return Quaternion.CreateFromForwardUp(forward, up);
        }
    }
    public struct LocalDir
    {
        public Vector3 forward;
        public LocalDir(Vector3 f)
        {
            forward = Vector3.Normalize(f);
        }

        public LocalDir(GlobalDir d, MatrixD worldMatrix)
        {
            forward = Vector3D.TransformNormal(d.forward, MatrixD.Transpose(worldMatrix));
        }
        public GlobalDir ToGlobal(MatrixD worldMatrix)
        {
            return new GlobalDir(Vector3D.TransformNormal(forward, worldMatrix));
        }
    }


    public struct GlobalDir
    {
        public Vector3 forward;
        public GlobalDir(Vector3 f)
        {
            forward = Vector3.Normalize(f);
        }

        public GlobalDir(LocalDir d, MatrixD worldMatrix)
        {
            forward = Vector3D.TransformNormal(d.forward, worldMatrix);
        }

        public LocalDir ToLocal(MatrixD worldMatrix)
        {
            return new LocalDir(Vector3D.TransformNormal(forward, MatrixD.Transpose(worldMatrix)));
        }
    }

    public struct LocalPose
    {
        public LocalPos pos;
        public LocalRot rot;
        public LocalPose(LocalPos p, LocalRot r)
        {
            pos = p;
            rot = r;
        }

        public GlobalPose ToGlobal(MatrixD WorldMatrix)
        {
            return new GlobalPose(pos.ToGlobal(WorldMatrix), rot.ToGlobal(WorldMatrix));
        }
    }

    public struct GlobalPose
    {
        public GlobalPos pos;
        public GlobalRot rot;
        public GlobalPose(GlobalPos p, GlobalRot r)
        {
            pos = p;
            rot = r;
        }
    }

    public struct GlobalVel
    {
        public Vector3 vel;

        public GlobalVel(Vector3 v)
        {
            vel = v;
        }
        public GlobalVel(LocalVel d, MatrixD worldMatrix)
        {
            vel = Vector3D.Transform(d.vel, worldMatrix);
        }
        public LocalVel ToLocal(MatrixD worldMatrix)
        {
            return new LocalVel(Vector3D.TransformNormal(vel, MatrixD.Transpose(worldMatrix)));
        }
    }

    public struct LocalVel
    {
        public Vector3 vel;
        public LocalVel(Vector3 f)
        {
            vel = f;
        }

        public LocalVel(GlobalVel d, MatrixD worldMatrix)
        {
            vel = Vector3D.Transform(d.vel, MatrixD.Transpose(worldMatrix));
        }

        public GlobalVel ToGlobal(MatrixD worldMatrix)
        {
            return new GlobalVel(Vector3D.TransformNormal(vel, worldMatrix));
        }
    }
}
