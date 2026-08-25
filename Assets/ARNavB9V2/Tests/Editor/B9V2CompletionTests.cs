using ARNavB9V2.Indoor;
using NUnit.Framework;

namespace ARNavB9V2.Tests
{
    public sealed class B9V2CompletionTests
    {
        [Test]
        public void StepDetector_WhenPeakReleasedAndIntervalPassed_DetectsNextStep()
        {
            var detector = new B9StepDetector(0.1f, 0.04f, 0.25f);

            Assert.IsFalse(detector.Process(0.03f, 0f));
            Assert.IsTrue(detector.Process(0.12f, 0.3f));
            Assert.IsFalse(detector.Process(0.14f, 0.4f));
            Assert.IsFalse(detector.Process(0.02f, 0.5f));
            Assert.IsTrue(detector.Process(0.11f, 0.6f));
        }

        [Test]
        public void StepDetector_WhenPeakNeverReleases_DoesNotDoubleCount()
        {
            var detector = new B9StepDetector(0.1f, 0.04f, 0.2f);

            Assert.IsTrue(detector.Process(0.15f, 0.3f));
            Assert.IsFalse(detector.Process(0.13f, 0.6f));
            Assert.IsFalse(detector.Process(0.12f, 1f));
        }
    }
}
