using NUnit.Framework;

namespace ShadowTrace.Tests
{
    public sealed class TraceClassificationTests
    {
        [Test] public void NoInputIsStay()
        { var trace = new TraceClassification(); trace.Sample(0, 6); Assert.AreEqual(ShadowKind.Stay, trace.Classify(0.3f)); }

        [TestCase(1)] [TestCase(-1)] public void ContinuousMovementIsApproach(int direction)
        {
            var trace = new TraceClassification(); trace.Sample(direction, 6);
            Assert.AreEqual(direction, trace.InitialDirection);
            Assert.AreEqual(ShadowKind.Approach, trace.Classify(0.3f));
        }

        [TestCase(0.29f, ShadowKind.Approach)] [TestCase(0.3f, ShadowKind.Follow)]
        public void EndingPauseUsesThreshold(float pause, ShadowKind expected)
        {
            var trace = new TraceClassification(); trace.Sample(1, 1); trace.Sample(0, pause);
            Assert.AreEqual(expected, trace.Classify(0.3f));
        }

        [Test] public void ReversalHasPriorityOverEndingPause()
        {
            var trace = new TraceClassification(); trace.Sample(-1, 1); trace.Sample(0, 1);
            trace.Sample(1, 1); trace.Sample(0, 1);
            Assert.AreEqual(ShadowKind.Avoid, trace.Classify(0.3f));
            Assert.AreEqual(-1, trace.InitialDirection);
        }

        [Test] public void PauseThenResumeIsApproach()
        {
            var trace = new TraceClassification(); trace.Sample(1, 1); trace.Sample(0, 1); trace.Sample(1, 1);
            Assert.AreEqual(ShadowKind.Approach, trace.Classify(0.3f));
        }

        [Test] public void SlotsAreLimitedAndReuseLowestHoleWithoutRenumbering()
        {
            var slots = new ShadowSlots();
            for (int i = 0; i < 8; i++) Assert.AreEqual(i, slots.Allocate());
            Assert.AreEqual(-1, slots.Allocate()); Assert.AreEqual(8, slots.Count);
            slots.Release(3); slots.Release(1); slots.Release(1);
            Assert.AreEqual(6, slots.Count);
            Assert.AreEqual(1, slots.Allocate()); Assert.AreEqual(3, slots.Allocate());
            Assert.AreEqual(8, slots.Count);
        }
    }
}
