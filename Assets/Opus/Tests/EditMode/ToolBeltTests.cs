using NUnit.Framework;
using Opus.Tools;

namespace Opus.Tests
{
    public class ToolBeltTests
    {
        [TestCase(0, 1, 2, 1)]
        [TestCase(1, 1, 2, 0)]
        [TestCase(0, -1, 2, 1)]
        [TestCase(3, -1, 4, 2)]
        [TestCase(0, -5, 4, 3)]
        public void NextIndexWrapsBothWays(int current, int direction, int count, int expected)
        {
            Assert.AreEqual(expected, ToolBelt.NextIndex(current, direction, count));
        }

        [Test]
        public void NextIndexWithNoToolsIsInvalid()
        {
            Assert.AreEqual(-1, ToolBelt.NextIndex(0, 1, 0));
        }
    }
}
