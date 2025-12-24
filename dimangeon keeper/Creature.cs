using System;
using System.Collections.Generic;
using System.Drawing;

namespace dimangeon_keeper
{
    public enum CreatureState
    {
        Idle,
        GoingToJob,
        Working,
        CarryingToTreasury,

        GoingToRally,
        Rallying,

        GoingToAttack,
        Attacking,

        Dead
    }

    public enum CreatureKind
    {
        Imp,
        Goblin,

        EnemyWarrior,
        BossAntonius
    }

    public class Creature
    {
        public float X;
        public float Y;

        public float Speed = 5f;
        public CreatureState State = CreatureState.Idle;

        public DigJob CurrentJob;
        public float WorkTimer;

        public CreatureKind Kind = CreatureKind.Imp;

        // legacy (оставляем, чтобы ничего не сломать в твоём Game.cs)
        public bool IsGoblin;

        // combat
        public int Hp;
        public int MaxHp;
        public int Damage;
        public float AttackTimer; // копится до удара

        // goblin rally / ai
        public Point RallyCell;
        public bool HasRally;

        // animation
        public Bitmap[] WalkFrames;
        public int FrameIndex;
        public float FrameTimer;
        public float FrameDuration = 0.15f;

        // path
        public List<Point> Path;
        public int PathIndex;

        // В твоём коде это "ApproachCell" для импов,
        // мы будем ещё переиспользовать как "текущая цель" для врагов
        public Point ApproachCell;

        // treasury carry
        public int CarryingGold;
        public Point TreasuryTargetCell;

        public bool IsEnemy =>
            Kind == CreatureKind.EnemyWarrior || Kind == CreatureKind.BossAntonius;

        public void ClearPath()
        {
            Path = null;
            PathIndex = 0;
            ApproachCell = new Point(-1, -1);
        }

        public Creature(float x, float y)
        {
            X = x;
            Y = y;
        }

        public void UpdateAnimation(float dt, bool isWalking)
        {
            if (WalkFrames == null || WalkFrames.Length == 0)
                return;

            if (!isWalking)
            {
                FrameIndex = 0;
                FrameTimer = 0f;
                return;
            }

            int firstWalk = (WalkFrames.Length > 1) ? 1 : 0;
            int lastWalk = WalkFrames.Length - 1;

            if (FrameIndex < firstWalk)
                FrameIndex = firstWalk;

            FrameTimer += dt;
            if (FrameTimer >= FrameDuration)
            {
                FrameTimer -= FrameDuration;
                FrameIndex++;
                if (FrameIndex > lastWalk)
                    FrameIndex = firstWalk;
            }
        }

        public Bitmap GetCurrentFrame()
        {
            if (WalkFrames == null || WalkFrames.Length == 0)
                return null;

            return WalkFrames[FrameIndex];
        }
    }
}
