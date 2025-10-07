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
            DI.Init();
        }

        [Test]
        public void GetService_ReturnsRegisteredImplementation()
        {
            var pathGenerator = DI.GetService<IPathGenerator>();

            Assert.IsNotNull(pathGenerator);
            Assert.IsInstanceOf<BiasedRandomWalkGenerator>(pathGenerator);
        }

        [Test]
        public void GetService_ReturnsTransientInstances()
        {
            var spawnerA = DI.GetService<IRoadSpawner>();
            var spawnerB = DI.GetService<IRoadSpawner>();

            Assert.IsNotNull(spawnerA);
            Assert.IsNotNull(spawnerB);
            Assert.AreNotSame(spawnerA, spawnerB);
        }
    }
}
