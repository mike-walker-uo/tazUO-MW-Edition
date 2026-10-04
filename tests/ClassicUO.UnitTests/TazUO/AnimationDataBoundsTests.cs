using System;
using System.IO;
using System.Reflection;
using ClassicUO.Assets;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class AnimationDataBoundsTests
    {
        public AnimationDataBoundsTests() => TestLogging.EnsureInitialized();

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(3, 0, 0)]
        [InlineData(4, 0, 0)]
        [InlineData(71, 1, 0)]
        [InlineData(72, 1, 1)]
        [InlineData(72, 64, 64)]
        [InlineData(72, 65, 0)]
        public unsafe void Animation_requires_complete_record_and_valid_frame_count(int size, int frameCount, int expected)
        {
            string path = Path.Combine(Path.GetTempPath(), "tazuo-anim-audit-" + Guid.NewGuid().ToString("N"));
            UOFileMul file = null;
            try
            {
                byte[] bytes = new byte[size];
                if (size > 69) bytes[69] = (byte)frameCount;
                if (size > 4) bytes[4] = 12;
                File.WriteAllBytes(path, bytes);
                file = new UOFileMul(path);
                var loader = (AnimDataLoader)Activator.CreateInstance(typeof(AnimDataLoader), true);
                typeof(AnimDataLoader).GetField("_file", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, file);

                AnimDataFrame frame = loader.CalculateCurrentGraphic(0);
                Assert.Equal(expected, (int)frame.FrameCount);
                if (expected > 0) Assert.Equal((sbyte)12, frame.FrameData[0]);
                Assert.Equal((byte)0, loader.CalculateCurrentGraphic(1).FrameCount);
            }
            finally
            {
                file?.Dispose();
                File.Delete(path);
            }
        }
    }
}
