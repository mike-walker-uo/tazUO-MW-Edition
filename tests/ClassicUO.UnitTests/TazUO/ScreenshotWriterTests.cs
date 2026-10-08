using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;
using ZLibNative;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ScreenshotWriterTests
    {
        [Fact]
        public void PNG_preserves_RGB_orientation_and_makes_backbuffer_alpha_opaque()
        {
            using var output = new MemoryStream();
            ScreenshotWriter.WritePng(output, new[] { new Color(255, 0, 0, 0), new Color(0, 255, 0, 90),
                new Color(0, 0, 255, 255), new Color(12, 34, 56, 0) }, 2, 2);
            byte[] png = output.ToArray();
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, Sub(png, 0, 8));
            var chunks = ReadChunks(png);
            Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, new List<string>(chunks.Keys));
            Assert.Equal(2u, Number(chunks["IHDR"], 0)); Assert.Equal(2u, Number(chunks["IHDR"], 4));
            Assert.Equal(8, chunks["IHDR"][8]); Assert.Equal(6, chunks["IHDR"][9]);
            using var compressed = new MemoryStream(chunks["IDAT"]);
            using var inflate = new ZLIBStream(compressed, CompressionMode.Decompress);
            using var raw = new MemoryStream(); inflate.CopyTo(raw);
            Assert.Equal(new byte[] { 0, 255, 0, 0, 255, 0, 255, 0, 255,
                0, 0, 0, 255, 255, 12, 34, 56, 255 }, raw.ToArray());
        }

        [Fact]
        public void Dimensions_are_checked_before_writing_invalid_PNG_data()
        {
            using var output = new MemoryStream();
            Assert.Throws<ArgumentException>(() => ScreenshotWriter.WritePng(output, new Color[1], 2, 2));
            Assert.Throws<ArgumentException>(() => ScreenshotWriter.WritePng(output, Array.Empty<Color>(), 0, 0));
            Assert.Equal(0, output.Length);
        }

        [Fact]
        public void Worker_finishes_on_shutdown_and_dispatches_completion_on_main_queue()
        {
            string directory = Path.Combine(Path.GetTempPath(), "tazuo-screenshot-" + Guid.NewGuid().ToString("N"));
            var previous = MainThreadQueue.QueuedActions.ToArray();
            while (MainThreadQueue.QueuedActions.TryDequeue(out _)) { }
            bool called = false; Exception error = null;
            try
            {
                Assert.True(ScreenshotWriter.TryReserve()); Assert.False(ScreenshotWriter.TryReserve());
                string file = Path.Combine(directory, "shot.png");
                ScreenshotWriter.Save(file, new[] { Color.CornflowerBlue }, 1, 1, e => { called = true; error = e; });
                ScreenshotWriter.FinishPendingSave();
                Assert.True(File.Exists(file)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
                Assert.False(called);
                Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out Action completed)); completed();
                Assert.True(called); Assert.Null(error);
                Assert.True(ScreenshotWriter.TryReserve());
            }
            finally
            {
                ScreenshotWriter.FinishPendingSave(); ScreenshotWriter.Release();
                while (MainThreadQueue.QueuedActions.TryDequeue(out _)) { }
                foreach (Action action in previous) MainThreadQueue.QueuedActions.Enqueue(action);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Existing_screenshot_is_preserved_on_output_collision_and_reservation_is_released()
        {
            string directory = Path.Combine(Path.GetTempPath(), "tazuo-screenshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "shot.png"); File.WriteAllText(file, "previous");
            var previous = MainThreadQueue.QueuedActions.ToArray();
            while (MainThreadQueue.QueuedActions.TryDequeue(out _)) { }
            Exception error = null;
            try
            {
                Assert.True(ScreenshotWriter.TryReserve());
                ScreenshotWriter.Save(file, new[] { Color.White }, 1, 1, e => error = e);
                ScreenshotWriter.FinishPendingSave();
                Assert.Equal("previous", File.ReadAllText(file)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
                Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out Action completed)); completed();
                Assert.IsAssignableFrom<IOException>(error); Assert.True(ScreenshotWriter.TryReserve());
            }
            finally
            {
                ScreenshotWriter.FinishPendingSave(); ScreenshotWriter.Release();
                while (MainThreadQueue.QueuedActions.TryDequeue(out _)) { }
                foreach (Action action in previous) MainThreadQueue.QueuedActions.Enqueue(action);
                Directory.Delete(directory, true);
            }
        }

        private static Dictionary<string, byte[]> ReadChunks(byte[] png)
        {
            var result = new Dictionary<string, byte[]>();
            for (int offset = 8; offset < png.Length;)
            {
                int length = (int)Number(png, offset);
                string type = Encoding.ASCII.GetString(png, offset + 4, 4);
                uint crc = uint.MaxValue;
                for (int i = offset + 4; i < offset + 8 + length; i++)
                {
                    crc ^= png[i];
                    for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xEDB88320u);
                }
                Assert.Equal(~crc, Number(png, offset + 8 + length));
                result.Add(type, Sub(png, offset + 8, length)); offset += length + 12;
            }
            return result;
        }
        private static uint Number(byte[] bytes, int offset) => (uint)bytes[offset] << 24 | (uint)bytes[offset + 1] << 16 | (uint)bytes[offset + 2] << 8 | bytes[offset + 3];
        private static byte[] Sub(byte[] bytes, int offset, int count) { var result = new byte[count]; Array.Copy(bytes, offset, result, 0, count); return result; }
    }
}
