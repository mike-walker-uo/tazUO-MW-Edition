using System;
using System.Runtime.Serialization;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class EnemyRadarTests : IDisposable
    {
        private readonly PlayerMobile _player = World.Player;
        private readonly Point _rangeCenter = World.RangeSize;
        private readonly bool _neutral = EnemyRadar.IncludeNeutral;
        private readonly int _style = EnemyRadar.Style, _size = EnemyRadar.Size,
            _opacity = EnemyRadar.Opacity, _range = EnemyRadar.Range;

        public EnemyRadarTests()
        {
            var player = (PlayerMobile)FormatterServices.GetUninitializedObject(typeof(PlayerMobile));
            player.Serial = 1;
            player.X = 100; player.Y = 100;
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, player);
            World.RangeSize = new Point(100, 100);
            EnemyRadar.IncludeNeutral = true;
        }

        public void Dispose()
        {
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _player);
            World.RangeSize = _rangeCenter;
            EnemyRadar.IncludeNeutral = _neutral;
            EnemyRadar.Style = _style; EnemyRadar.Size = _size;
            EnemyRadar.Opacity = _opacity; EnemyRadar.Range = _range;
        }

        private static Mobile Contact()
        {
            var m = new Mobile(2);
            m.X = 124; m.Y = 100;
            m.NotorietyFlag = NotorietyFlag.Enemy;
            return m;
        }

        [Fact]
        public void Uses_client_range_boundary_and_rejects_stale_or_friendly_contacts()
        {
            Mobile m = Contact();
            Assert.True(EnemyRadar.Eligible(m, 24));
            Assert.False(EnemyRadar.Eligible(m, 18));
            Assert.False(EnemyRadar.Eligible(World.Player, 24));
            Assert.False(EnemyRadar.Eligible(null, 24));
            m.InParty = true; Assert.False(EnemyRadar.Eligible(m, 24)); m.InParty = false;
            m.IsRenamable = true; Assert.False(EnemyRadar.Eligible(m, 24)); m.IsRenamable = false;
            m.Flags = Flags.Hidden; Assert.False(EnemyRadar.Eligible(m, 24)); m.Flags = 0;
            m.IsDead = true; Assert.False(EnemyRadar.Eligible(m, 24)); m.IsDead = false;
            typeof(GameObject).GetProperty(nameof(GameObject.IsDestroyed)).SetValue(m, true);
            Assert.False(EnemyRadar.Eligible(m, 24));
        }

        [Theory]
        [InlineData(NotorietyFlag.Innocent, true, false)]
        [InlineData(NotorietyFlag.Ally, true, false)]
        [InlineData(NotorietyFlag.Invulnerable, true, false)]
        [InlineData(NotorietyFlag.Unknown, true, false)]
        [InlineData(NotorietyFlag.Gray, true, true)]
        [InlineData(NotorietyFlag.Gray, false, false)]
        [InlineData(NotorietyFlag.Enemy, false, true)]
        [InlineData(NotorietyFlag.Murderer, false, true)]
        [InlineData(NotorietyFlag.Criminal, false, true)]
        public void Neutral_option_does_not_include_friends(NotorietyFlag flag, bool neutral, bool expected) =>
            Assert.Equal(expected, EnemyRadar.IsHostile(flag, neutral));

        [Fact]
        public void Full_square_range_fits_ring_without_clipping_diagonal_contacts()
        {
            Assert.Equal(Vector2.Zero, EnemyRadar.Project(0, 0, 24));
            Assert.Equal(Vector2.Zero, EnemyRadar.Project(1, 1, 0));
            for (int x = -24; x <= 24; x++)
                for (int y = -24; y <= 24; y++)
                {
                    Vector2 p = EnemyRadar.Project(x, y, 24);
                    Assert.InRange(p.Length(), 0f, 1.00001f);
                    Assert.InRange(Math.Abs(p.Length() - Math.Max(Math.Abs(x), Math.Abs(y)) / 24f), 0f, 0.00001f);
                }
            Assert.Equal(new Vector2(0, 1), EnemyRadar.Project(24, 24, 24));
            Assert.Equal(new Vector2(1, 0), EnemyRadar.Project(24, -24, 24));
        }

        [Fact]
        public void Subtile_movement_is_continuous_and_beams_fade_completely()
        {
            Vector2 before = EnemyRadar.Project(5, 0, 24);
            Vector2 during = EnemyRadar.Project(5.5f, 0, 24);
            Vector2 after = EnemyRadar.Project(6, 0, 24);
            Assert.InRange(during.Length(), before.Length(), after.Length());
            Assert.InRange(Vector2.Distance(before, during), 0f, 0.022f);
            Assert.Equal(1f, EnemyRadar.BeamFade(0));
            Assert.Equal(0f, EnemyRadar.BeamFade(1));
            Assert.Equal(0f, EnemyRadar.BeamFade(2));
            Assert.True(EnemyRadar.BeamFade(0.25f) > EnemyRadar.BeamFade(0.75f));
        }

        [Fact]
        public void Loaded_settings_stay_within_supported_drawing_limits()
        {
            EnemyRadar.Style = 999; EnemyRadar.Size = int.MaxValue;
            EnemyRadar.Opacity = int.MaxValue; EnemyRadar.Range = int.MaxValue;
            Assert.Equal(2, EnemyRadar.Style); Assert.Equal(400, EnemyRadar.Size);
            Assert.Equal(100, EnemyRadar.Opacity); Assert.Equal(Constants.MAX_VIEW_RANGE, EnemyRadar.Range);
            EnemyRadar.Style = -1; EnemyRadar.Size = -1; EnemyRadar.Opacity = -1; EnemyRadar.Range = -1;
            Assert.Equal(0, EnemyRadar.Style); Assert.Equal(100, EnemyRadar.Size);
            Assert.Equal(5, EnemyRadar.Opacity); Assert.Equal(1, EnemyRadar.Range);
        }
    }
}
