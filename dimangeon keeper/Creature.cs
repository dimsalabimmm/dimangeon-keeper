using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dimangeon_keeper
{
    public enum CreatureState
    {
        Idle,
        GoingToJob,
        Working
    }

    public class Creature
    {
        public float X;
        public float Y;

        public float Speed = 4f; //скок тайлов в секунду бежит
        public CreatureState State = CreatureState.Idle;

        public DigJob CurrentJob;
        public float WorkTimer;

        public Bitmap[] WalkFrames;
        public int FrameIndex;
        public float FrameTimer;
        public float FrameDuration = 0.15f;

        public List<Point> Path;
        public int PathIndex;
        public Point ApproachCell;

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

        public void UpdateAnimation(float dt, bool IsWalking)
        {
            if (!IsWalking)
            {
                FrameIndex = 0;
                FrameTimer = 0f;
                return;

            }
            FrameTimer += dt;
            if (FrameTimer > FrameDuration)
            {
                FrameTimer -= FrameDuration;
                FrameIndex++;
                if (WalkFrames != null && FrameIndex >= WalkFrames.Length)
                {
                    FrameIndex = 0;
                }
            }
        }
        public Bitmap GetCurrentFrame()
        {
            if (WalkFrames == null || WalkFrames.Length == 0)
            {
                return null;
            }
            return WalkFrames[FrameIndex];
        }
        public void Update(float dt)
        {

        }
    }
}
