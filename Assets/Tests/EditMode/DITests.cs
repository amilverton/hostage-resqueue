using Core;
using NUnit.Framework;
using Roads;

namespace RoadsTests
{
    [TestFixture]
    public class DITests
    {
        [SetUp]
        public void Setup()
        {
            Di.Init();
        }

        [Test]
        public void GetService_ReturnsRegisteredImplementation()
        {
            var pathGenerator = Di.GetService<IPathGenerator>();

            Assert.IsNotNull(pathGenerator);
            Assert.IsInstanceOf<BiasedRandomWalkGenerator>(pathGenerator);
        }

        [Test]
        public void GetService_ReturnsTransientInstances()
        {
            var spawnerA = Di.GetService<IRoadSpawner>();
            var spawnerB = Di.GetService<IRoadSpawner>();

            Assert.IsNotNull(spawnerA);
            Assert.IsNotNull(spawnerB);
            Assert.AreNotSame(spawnerA, spawnerB);
        }
    }
}
