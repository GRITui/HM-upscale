using System.IO;
using HM.Core;
using NUnit.Framework;

namespace HM.Tests
{
    public class SaveSystemTests
    {
        string dir;
        SaveSystem sys;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "hm-save-" + System.Guid.NewGuid().ToString("N"));
            sys = new SaveSystem(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void RoundTripPreservesData()
        {
            sys.Save("a", new SaveData { totalMinutes = 1234.5, gold = 99, playerX = 1f, playerY = 2f, playerZ = 3f });
            Assert.AreEqual(SaveLoadResult.Ok, sys.TryLoad("a", out var d));
            Assert.AreEqual(1234.5, d.totalMinutes, 1e-9);
            Assert.AreEqual(99, d.gold);
            Assert.AreEqual(3f, d.playerZ);
            Assert.AreEqual(SaveSystem.CurrentVersion, d.version);
        }

        [Test]
        public void MissingSlotIsNotFound() =>
            Assert.AreEqual(SaveLoadResult.NotFound, sys.TryLoad("nope", out _));

        [Test]
        public void GarbageIsCorrupt()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(sys.PathFor("bad"), "{ not json");
            Assert.AreEqual(SaveLoadResult.Corrupt, sys.TryLoad("bad", out var d));
            Assert.IsNull(d);
        }

        [Test]
        public void NewerVersionIsRejected()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(sys.PathFor("future"), "{\"version\":" + (SaveSystem.CurrentVersion + 1) + "}");
            Assert.AreEqual(SaveLoadResult.NewerVersion, sys.TryLoad("future", out _));
        }

        [Test]
        public void OverwriteLeavesNoTempFile()
        {
            sys.Save("a", new SaveData { gold = 1 });
            sys.Save("a", new SaveData { gold = 2 });
            Assert.AreEqual(SaveLoadResult.Ok, sys.TryLoad("a", out var d));
            Assert.AreEqual(2, d.gold);
            Assert.IsFalse(File.Exists(sys.PathFor("a") + ".tmp"));
            Assert.AreEqual(1, Directory.GetFiles(dir).Length);
        }

        [Test]
        public void InterruptedWriteDoesNotAffectExistingSave()
        {
            sys.Save("a", new SaveData { gold = 7 });
            // Simulate a crash that left a truncated temp file behind.
            File.WriteAllText(sys.PathFor("a") + ".tmp", "{ trunc");
            Assert.AreEqual(SaveLoadResult.Ok, sys.TryLoad("a", out var d));
            Assert.AreEqual(7, d.gold);
            sys.Save("a", new SaveData { gold = 8 });
            sys.TryLoad("a", out d);
            Assert.AreEqual(8, d.gold);
        }

        [Test]
        public void DeleteRemovesSlot()
        {
            sys.Save("a", new SaveData());
            sys.Delete("a");
            Assert.IsFalse(sys.Exists("a"));
        }
    }
}
