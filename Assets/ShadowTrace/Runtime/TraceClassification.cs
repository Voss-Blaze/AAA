namespace ShadowTrace
{
    public enum ShadowKind { Stay, Approach, Avoid, Follow }
    public enum TracePhase { Idle, Preparing, Recording }

    /// <summary>Classifies intent, rather than displacement; jumping does not change type.</summary>
    public sealed class TraceClassification
    {
        public int InitialDirection { get; private set; }
        public bool Reversed { get; private set; }
        public float StillTime { get; private set; }
        private int lastDirection;

        public void Sample(int direction, float deltaTime)
        {
            direction = direction > 0 ? 1 : direction < 0 ? -1 : 0;
            if (direction == 0) { StillTime += deltaTime; return; }
            StillTime = 0;
            if (InitialDirection == 0) InitialDirection = direction;
            if (lastDirection != 0 && lastDirection != direction) Reversed = true;
            lastDirection = direction;
        }

        public ShadowKind Classify(float stopThreshold)
        {
            if (InitialDirection == 0) return ShadowKind.Stay;
            if (Reversed) return ShadowKind.Avoid;
            return StillTime + 0.00001f >= stopThreshold ? ShadowKind.Follow : ShadowKind.Approach;
        }
    }

    /// <summary>Stable slots: deleting one never renumbers the others.</summary>
    public sealed class ShadowSlots
    {
        private readonly bool[] occupied;
        public int Capacity => occupied.Length;
        public int Count { get; private set; }
        public ShadowSlots(int capacity = 8) { occupied = new bool[capacity]; }
        public int Allocate()
        {
            for (int i = 0; i < occupied.Length; i++)
                if (!occupied[i]) { occupied[i] = true; Count++; return i; }
            return -1;
        }
        public void Release(int slot)
        {
            if (slot < 0 || slot >= occupied.Length || !occupied[slot]) return;
            occupied[slot] = false;
            Count--;
        }
    }
}
