using System;
using UnityEngine;

namespace HandballManager.Data
{
    [Serializable]
    public class PlayerSnapshotDto
    {
        public int PlayerId;
        public int TeamSimId;
        public string PlayerName;
        public string Nationality;
        public int Age;
        public string AssignedTacticalRole;
        public string PrimaryPosition;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 LookDirection;
        public bool HasBall;
        public float Stamina;
        public float CurrentFatigue;
        public bool IsOnCourt;
        public float SuspensionTimer;
        public bool IsJumping;
        public bool JumpActive;
        public float VerticalPosition;
        public float JumpTimer;
        public float JumpInitialHeight;
        public Vector2 JumpStartVelocity;
        public Vector2? JumpOrigin;
        public bool JumpOriginatedOutsideGoalArea;
        public bool IsStumbling;
        public float StumbleTimer;
        public string CurrentAction;
        public string PlannedAction;
        public Vector2 TargetPosition;
        public int? TargetPlayerId;
        public float ActionTimer;
        public float EffectiveSpeed;
    }
}
