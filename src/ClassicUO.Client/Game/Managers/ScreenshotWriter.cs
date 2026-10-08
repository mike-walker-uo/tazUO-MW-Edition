using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using ZLibNative;

namespace ClassicUO.Game.Managers
{
    internal static class ScreenshotWriter
    {
        // Reserve before readback/allocation: at most one CPU snapshot is retained.
        private static int _pending;
        private static Task _saveTask;
        private static readonly uint[] CrcTable = MakeCrcTable();
        internal static void FinishPendingSave() => _saveTask?.GetAwaiter().GetResult();
        internal static bool TryReserve() => Interlocked.CompareExchange(ref _pending, 1, 0) == 0;
        internal static void Release() => Interlocked.Exchange(ref _pending, 0);

        internal static void Save(string path, Color[] pixels, int width, int height, Action<Exception> completed)
        {
            _saveTask = Task.Run(() =>
            {
                Exception error = null;
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                        WritePng(file, pixels, width, height);
                    File.Move(temporary, path);
                }
                catch (Exception ex) { error = ex; }
                finally
                {
                    if (File.Exists(temporary))
                        try { File.Delete(temporary); } catch (Exception cleanupError) { if (error == null) error = cleanupError; }
                    Release();
                }
                PostCompletion(completed, error);
            });
        }

        // Separate closure: a queued completion never retains the large pixel snapshot.
        private static void PostCompletion(Action<Exception> completed, Exception error) =>
            MainThreadQueue.EnqueueAction(() => completed(error));

        // CPU-only PNG encoding. GPU readback belongs to the calling frame thread.
        internal static void WritePng(Stream output, Color[] pixels, int width, int height)
        {
            if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
                throw new ArgumentException("Invalid screenshot dimensions.");

            output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var header = new byte[13];
            PutUInt(header, 0, (uint)width);
            PutUInt(header, 4, (uint)height);
            header[8] = 8;
            header[9] = 6; // RGBA, full opacity even when the backbuffer alpha is zero.
            WriteChunk(output, "IHDR", header);

            using var compressed = new MemoryStream();
            using (var deflate = new ZLIBStream(compressed, CompressionLevel.Fastest, true))
            {
                var row = new byte[checked(width * 4 + 1)];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = pixels[y * width + x];
                        int offset = x * 4 + 1;
                        row[offset] = color.R;
                        row[offset + 1] = color.G;
                        row[offset + 2] = color.B;
                        row[offset + 3] = 255;
                    }
                    deflate.Write(row, 0, row.Length);
                }
            }
            WriteChunk(output, "IDAT", compressed.GetBuffer(), checked((int)compressed.Length));
            WriteChunk(output, "IEND", Array.Empty<byte>());
        }

        private static void PutUInt(byte[] data, int offset, uint value)
        {
            for (int i = 0; i < 4; i++) data[offset + i] = (byte)(value >> (24 - i * 8));
        }

        private static void WriteChunk(Stream output, string name, byte[] data, int length = -1)
        {
            if (length < 0) length = data.Length;
            var number = new byte[4];
            PutUInt(number, 0, (uint)length);
            output.Write(number, 0, 4);
            byte[] type = Encoding.ASCII.GetBytes(name);
            output.Write(type, 0, type.Length);
            output.Write(data, 0, length);
            uint crc = uint.MaxValue;
            foreach (byte value in type) crc = UpdateCrc(crc, value);
            for (int i = 0; i < length; i++) crc = UpdateCrc(crc, data[i]);
            PutUInt(number, 0, ~crc);
            output.Write(number, 0, 4);
        }

        private static uint UpdateCrc(uint crc, byte value) => (crc >> 8) ^ CrcTable[(crc ^ value) & 255];

        private static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value >> 1) ^ ((value & 1) == 0 ? 0u : 0xEDB88320u);
                table[i] = value;
            }
            return table;
        }
    }
}
