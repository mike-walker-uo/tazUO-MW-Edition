using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ProfileGumpSaveTests
    {
        public ProfileGumpSaveTests() => TestLogging.EnsureInitialized();

        [Fact]
        public void Missing_parent_preserves_the_open_child_as_the_save_root()
        {
            Item child = Item.Create(0x40007701);
            child.Container = 0x40007702;
            Assert.Same(child, Profile.GetGumpSaveRoot(child));
        }

        [Fact]
        public void Parent_cycle_terminates_and_valid_chain_still_finds_root()
        {
            Item child = Item.Create(0x40007701);
            Item parent = Item.Create(0x40007702);
            World.Items.Add(child.Serial, child);
            World.Items.Add(parent.Serial, parent);
            try
            {
                child.Container = parent.Serial;
                Assert.Same(parent, Profile.GetGumpSaveRoot(child));
                parent.Container = child.Serial;
                Assert.Same(child, Profile.GetGumpSaveRoot(child));
            }
            finally
            {
                World.Items.Remove(child.Serial);
                World.Items.Remove(parent.Serial);
            }
        }

        [Fact]
        public void Closed_child_with_a_cyclic_sibling_link_does_not_block_traversal()
        {
            Item root = Item.Create(0x40007701);
            Item child = Item.Create(0x40007702);
            root.Opened = true;
            root.Items = child;
            child.Next = child;
            using var stream = new MemoryStream();
            using var xml = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
            var visited = new HashSet<uint>();
            typeof(Profile).GetMethod("SaveItemsGumpRecursive", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { root, xml, new LinkedList<Gump>(), visited });
            Assert.Contains(root.Serial, visited);
            Assert.Contains(child.Serial, visited);
        }

        [Fact]
        public void Failed_serialization_keeps_previous_xml_and_cleans_temporary_file()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tazuo-gumps-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "gumps.xml");
            const string previous = "<gumps />";
            File.WriteAllText(path, previous);
            var gump = new FailingGump();
            UIManager.Gumps.AddLast(gump);
            try
            {
                Assert.False(new Profile().SaveGumps(dir));
                Assert.Equal(previous, File.ReadAllText(path));
                Assert.False(File.Exists(path + ".tmp"));
            }
            finally
            {
                UIManager.Gumps.Remove(gump);
                Directory.Delete(dir, true);
            }
        }

        private sealed class FailingGump : Gump
        {
            public FailingGump() : base(0, 1) { }
            public override void Save(XmlTextWriter writer)
            {
                writer.WriteAttributeString("partial", "yes");
                throw new InvalidOperationException("injected serialization failure");
            }
        }
    }
}
