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
    partial class Program
    {
        public partial class Motion
        {
            public List<IMyThrust> Thrusters = new List<IMyThrust>();
            public List<IMyGyro> gyros = new List<IMyGyro>();

            public IMyRemoteControl rc;

            MyBlockOrientation OrientrationRef;

            public Program program;
            public Orientation orientation;
            public GridUtils gridUtils;


            public float GYRO_GAIN = 3, GYRO_MAX_ANGULAR_VELOCITY = 2f, SPEED_LIMIT = 15f, ACC_LIMIT = 5f;

            PidController xPID;
            PidController yPID;
            PidController zPID;

            long prevTime;

            public DebugAPI debug;
            public void Init(Program p, Orientation o, GridUtils g)
            {
                program = p;
                orientation = o;
                gridUtils = g;

                GroupThrusters();
                SetReference();

                xPID = new PidController(12, 0, 15, 25, -25);
                yPID = new PidController(12, 0, 15, 25, -25);
                zPID = new PidController(12, 0, 15, 25, -25);

                prevTime = DateTime.Now.Millisecond;

                debug = new DebugAPI(program);
            }
            void GroupThrusters()
            {
                gridUtils.GetBlocks(out Thrusters);
                gridUtils.GetBlocks(out gyros);
            }
            void SetReference()
            {
                IMyRemoteControl reference = gridUtils.GetBlock<IMyRemoteControl>();

                if (reference != null)
                {
                    OrientrationRef = reference.Orientation;
                    rc = reference;
                }
            }

            public void AddThrust(GlobalVel Vel)
            {
                try
                {
                    Vector3 Direction = Vel.vel;

                    float TotalThrustPower = 0;

                    foreach (IMyThrust thruster in Thrusters)
                    {
                        Matrix mTH = thruster.WorldMatrix;
                        GlobalDir ThrusterDirection = new LocalDir(Vector3.Forward).ToGlobal(mTH);
                        TotalThrustPower += Math.Max(Vector3.Dot(Direction, -ThrusterDirection.forward), 0);
                    }

                    Direction *= rc.CalculateShipMass().PhysicalMass; //Account for ship mass
                    Direction *= Vel.vel.Length() / TotalThrustPower; //Account for multiple thrusters

                    foreach (IMyThrust thruster in Thrusters)
                    {
                        Matrix mTH = thruster.WorldMatrix;
                        GlobalDir ThrusterDirection = new LocalDir(Vector3.Forward).ToGlobal(mTH);
                        float power = Vector3.Dot(Direction, -ThrusterDirection.forward);
                        //float align = Math.Abs(Vector3.Multiply(axes, new LocalDir(Vector3.Forward).ToGlobal(mTH).ToLocal(rc.WorldMatrix).forward).Length());

                        //debug.DrawLine(thruster.WorldMatrix.Translation, ThrusterDirection.forward * 5 + mTH.Translation, Color.Green, 0.1f);
                        //debug.DrawLine(thruster.WorldMatrix.Translation, mTH.Translation + Vector3.Forward * 5, Color.Orange, 0.1f);
                        //debug.DrawLine(thruster.WorldMatrix.Translation, new LocalPos(Vector3.Forward * 5 * alignment).ToGlobal(mTH).pos, Color.Yellow, 0.2f);

                        //if (align > 0.9)
                        thruster.ThrustOverride = power;

                        thruster.Enabled = true;
                    }


                }
                catch (NullReferenceException)
                {
                    throw new MissingBlockException();
                }
            }

            public void EnableManualControl()
            {
                foreach (IMyThrust thruster in Thrusters)
                {
                    thruster.ThrustOverride = 0;
                    thruster.Enabled = true;
                }
                foreach (IMyGyro gyro in gyros)
                {
                    gyro.GyroOverride = false;
                }
            }

            public float RotateTo(GlobalRot r)
            {
                r.forward = -r.forward;
                r.up = -r.up;
                Vector3 forward = r.forward;
                Vector3 up = r.up;
                //debug.DrawLine(rc.WorldMatrix.Translation, new LocalDir(Vector3.Forward).ToGlobal(rc.WorldMatrix).forward + rc.WorldMatrix.Translation, Color.Pink, 0.1f);
                //debug.DrawLine(rc.WorldMatrix.Translation, new LocalDir(Vector3.Up).ToGlobal(rc.WorldMatrix).forward + rc.WorldMatrix.Translation, Color.Pink, 0.1f);

                Vector3 correction = Vector3.Cross(rc.WorldMatrix.Forward, forward);

                Vector3 globalVector = new GlobalDir(correction).ToLocal(rc.WorldMatrix).forward * correction.Length();// //new Vector3(pitch * 5, yaw * 5, 0);

                Matrix mRC;
                rc.Orientation.GetMatrix(out mRC);
                //mRC.TransposeRotationInPlace();
                globalVector = new LocalDir(globalVector).ToGlobal(mRC).forward * globalVector.Length();

                float alignment = Vector3.Dot(-forward, new LocalDir(Vector3.Forward).ToGlobal(rc.WorldMatrix).forward);

                if(alignment < -0.9)
                    globalVector.Y = (float)Math.PI * 2 - globalVector.Y;

                float roll = (float)Math.Asin(Vector3D.Dot(rc.WorldMatrix.Right, up));


                if (Vector3D.Dot(rc.WorldMatrix.Up, up) < 0)
                    roll = (float)Math.PI * 2 - roll;

                globalVector.Z = alignment > 0.9 ? roll : globalVector.Z;

                globalVector.Multiply(GYRO_GAIN);

                foreach (IMyGyro gyro in gyros)
                {
                    Matrix mGY;
                    gyro.Orientation.GetMatrix(out mGY);
                    Vector3 localVector = new GlobalDir(globalVector).ToLocal(mGY).forward * globalVector.Length(); ///*Vector3.Transform(globalVector, matrix);*/globalVector; //TODO Gyro orientation
                    
                    gyro.Pitch = Math.Max(Math.Min(localVector.X, GYRO_MAX_ANGULAR_VELOCITY), -GYRO_MAX_ANGULAR_VELOCITY);
                    gyro.Yaw = Math.Max(Math.Min(localVector.Y, GYRO_MAX_ANGULAR_VELOCITY), -GYRO_MAX_ANGULAR_VELOCITY);
                    gyro.Roll = Math.Max(Math.Min(localVector.Z, GYRO_MAX_ANGULAR_VELOCITY), -GYRO_MAX_ANGULAR_VELOCITY);
                    gyro.GyroOverride = true;
                }
                return 0;
            }
            public float RotateTo(GlobalDir r)
            {
                //debug.DrawLine(rc.WorldMatrix.Translation, rc.WorldMatrix.Translation + r.forward * 10, Color.Pink, 0.05f);

                Vector3 forward = r.forward;
                double yaw = Math.Asin(Vector3D.Dot(rc.WorldMatrix.Right, forward)); // yaw angle					
                double pitch = Math.Asin(Vector3D.Dot(rc.WorldMatrix.Up, forward)); // pitch angle

                double y_abs = Math.Abs(yaw);
                double p_abs = Math.Abs(pitch);

                if (Vector3D.Dot(forward, rc.WorldMatrix.Forward) < 0)
                {
                   // yaw = (yaw > 0) ? (Math.PI - yaw) : -Math.PI - yaw;
                   // pitch = (pitch > 0) ? (Math.PI - pitch) : -Math.PI - pitch;
                }

                double pitch_sign = pitch / p_abs; // +1 or -1 depending on the sign of the pitch
                double yaw_sign = yaw / y_abs;

                float angleRemaining = (float)Math.Sqrt(sq(yaw) + sq(pitch));
                if (angleRemaining > 0.025)
                {
                    Vector3 globalVector = new Vector3(pitch, yaw, 0);
                    foreach (IMyGyro gyro in gyros)
                    {
                        MatrixD mworld = gyro.WorldMatrix;
                        //mworld.TransposeRotationInPlace();
                        Vector3 localVector = new GlobalDir(globalVector).ToLocal(mworld).forward;

                        float power = MathHelper.Clamp(angleRemaining * GYRO_GAIN, -GYRO_MAX_ANGULAR_VELOCITY, GYRO_MAX_ANGULAR_VELOCITY);
                        gyro.Pitch = (float)MathHelper.Clamp((-localVector.X * power), -GYRO_MAX_ANGULAR_VELOCITY, GYRO_MAX_ANGULAR_VELOCITY);
                        gyro.Yaw = (float)MathHelper.Clamp((localVector.Y * power), -GYRO_MAX_ANGULAR_VELOCITY, GYRO_MAX_ANGULAR_VELOCITY);
                        //gyro.Roll = (float)MathHelper.Clamp(((-localVector.Z) * GYRO_GAIN), -GYRO_MAX_ANGULAR_VELOCITY, GYRO_MAX_ANGULAR_VELOCITY);
                        gyro.GyroOverride = true;
                    }
                }

                return angleRemaining;
            }

            double sq(double n)
            {
                return Math.Pow(n, 2);
            }
            public void AccelerateTo(GlobalVel vel)
            {
                if (vel.vel.Length() > SPEED_LIMIT)
                    vel = new GlobalVel(Vector3.ClampToSphere(vel.vel, SPEED_LIMIT));

                Vector3 velocity = rc.GetShipVelocities().LinearVelocity;

                //debug.DrawLine(rc.WorldMatrix.Translation, rc.WorldMatrix.Translation + velocity * 10, Color.Pink, 0.05f);
                //debug.DrawLine(rc.WorldMatrix.Translation, rc.WorldMatrix.Translation + vel.vel * 10, Color.Red, 0.05f);

                Vector3 Diff = vel.vel - velocity;
                AddThrust(new GlobalVel(Diff));
            }


            public void AccelerateTo(LocalVel vel)
            {
                AccelerateTo(new GlobalVel(vel, rc.WorldMatrix));
            }

            public void MoveTo(GlobalPos position, out Vector3 diff, GlobalVel? extraSpeed = null)
            {
                Vector3 CurrentPos = rc.GetPosition();
                Vector3 Diff = position.pos - CurrentPos;

                diff = Diff;

                if (Diff.Length() == 0)
                    return;

                float factor = Diff.Length() < SPEED_LIMIT ? Diff.Length() : SPEED_LIMIT;
                Diff = Vector3.Normalize(Diff) * factor;

                xPID.ProcessVariable = -Diff.X;
                yPID.ProcessVariable = -Diff.Y;
                zPID.ProcessVariable = -Diff.Z;

                TimeSpan dT = new TimeSpan(DateTime.Now.Ticks - prevTime);
                double xSpd = xPID.ControlVariable(dT);
                double ySpd = yPID.ControlVariable(dT);
                double zSpd = zPID.ControlVariable(dT);

                Vector3 extra = Vector3.Zero;
                if (extraSpeed.HasValue)
                    extra = extraSpeed.Value.vel;

                GlobalVel acc = new GlobalVel(new Vector3(xSpd, ySpd, zSpd) + extra);

                AccelerateTo(acc);

                prevTime = DateTime.Now.Ticks;
            }

            public float MoveTo(GlobalPos position)
            {
                Vector3 diff = new Vector3();
                MoveTo(position, out diff);
                return diff.Length();
            }

            public void MoveForwardTo(GlobalPos pos, GlobalRot endRotation)
            {
                float n;
                MoveForwardTo(pos, endRotation, out n, out n);
            }

            public void MoveForwardTo(GlobalPos pos, GlobalRot endRotation, out float remainingDistance, out float remainingAngle)
            {
                Vector3 CurrentPos = rc.GetPosition();
                if (Vector3.Distance(CurrentPos, pos.pos) > 10)
                {
                    GlobalRot rot = new GlobalRot(-(CurrentPos - pos.pos), Vector3.Up);
                    remainingAngle = RotateTo(rot);
                }
                else
                {
                    remainingAngle = RotateTo(endRotation);
                }
                remainingDistance = MoveTo(pos);
            }
            public float MoveAxis(float speed, GlobalPos pos, GlobalDir axis)
            {
                Vector3 axis_pos = pos.pos + axis.forward * Vector3.Dot((rc.GetPosition() - pos.pos), axis.forward) + axis.forward * 0.01f;
                //debug.DrawPoint(axis_pos, Color.Purple, 1);
                Vector3 _;
                MoveTo(new GlobalPos(axis_pos), out _, new GlobalVel(axis.forward * speed));

                return Vector3.Dot((rc.GetPosition() - pos.pos), axis.forward);
            }

            public GlobalPos NodeMoveStart;

            public void MoveNode(GlobalPos position, out Vector3 diff)
            {
                GlobalPos currentPos = new GlobalPos(rc.GetPosition());
                diff = position.pos - currentPos.pos;

                if (diff.Length() > 10)
                {
                    if (NodeMoveStart.pos == Vector3.Zero)
                        NodeMoveStart = currentPos;

                    GlobalDir axis = new GlobalDir(-(NodeMoveStart.pos - position.pos));

                    MoveAxis(10, position, axis);
                }
                else
                {
                    NodeMoveStart = new GlobalPos(Vector3.Zero);
                    MoveTo(position);
                }
            }

            public float MoveNode(GlobalPos position)
            {
                Vector3 _;
                MoveNode(position, out _);
                return _.Length();
            }
        }
    }
}
