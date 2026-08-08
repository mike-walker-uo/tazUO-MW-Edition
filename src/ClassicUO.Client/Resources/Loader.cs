using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicUO.Resources
{
    partial class Loader
    {
        [EmbedResourceCSharp.FileEmbed("cuologo.png")]
        public static partial ReadOnlySpan<byte> GetCuoLogo();

        [EmbedResourceCSharp.FileEmbed("game-background.png")]
        public static partial ReadOnlySpan<byte> GetBackgroundImage();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurface();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface-waves.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurfaceWaves();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface-choppy.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurfaceChoppy();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface-swell.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurfaceSwell();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface-storm.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurfaceStorm();

        [EmbedResourceCSharp.FileEmbed("Resources/water-surface-moonlit.png")]
        public static partial ReadOnlySpan<byte> GetWaterSurfaceMoonlit();

        [EmbedResourceCSharp.FileEmbed("Resources/word-of-death-skull.png")]
        public static partial ReadOnlySpan<byte> GetWordOfDeathSkull();
    }
}
