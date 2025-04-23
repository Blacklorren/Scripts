using System;
using System.Collections.Generic;
using UnityEngine;

namespace HandballManager.Data
{
    [Serializable]
    public class MatchSnapshotDto
    {
        public List<PlayerSnapshotDto> Players = new List<PlayerSnapshotDto>();
        public Vector3 BallPosition;
        public Vector3 BallVelocity;
        public Vector3 BallAngularVelocity;
        public int BallLastTouchedByTeamId;
        public int? BallLastTouchedByPlayerId;
        public bool BallIsLoose;
        public bool BallIsInFlight;
        public bool BallIsRolling;
        public int? BallHolderPlayerId;
        public int? BallPasserPlayerId;
        public int? BallIntendedTargetPlayerId;
        public Vector3 BallPassOrigin;
        public int? BallLastShooterPlayerId;
    }
}
