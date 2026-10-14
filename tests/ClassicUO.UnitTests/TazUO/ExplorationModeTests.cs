using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ExplorationModeTests
    {
        [Theory]
        [InlineData(-1, 0, -1, 1)]
        [InlineData(1, 0, 1, -1)]
        [InlineData(0, -1, -1, -1)]
        [InlineData(0, 1, 1, 1)]
        public void Screen_direction_maps_to_the_correct_isometric_tile_axes(int x, int y, int expectedX, int expectedY)
        {
            var origin = new Vector2(100, 100);
            Vector2 moved = ExplorationMode.Pan(origin, new Vector2(x, y), 0.1f, false, new Point(1000, 1000));
            Assert.Equal(expectedX, System.Math.Sign(moved.X - origin.X));
            Assert.Equal(expectedY, System.Math.Sign(moved.Y - origin.Y));
        }
        [Fact]
        public void Diagonal_pan_is_not_faster_and_shift_triples_speed()
        {
            var origin = new Vector2(100, 100);
            var bounds = new Point(1000, 1000);
            float straight = Vector2.Distance(origin, ExplorationMode.Pan(origin, Vector2.UnitX, 0.1f, false, bounds));
            float diagonal = Vector2.Distance(origin, ExplorationMode.Pan(origin, Vector2.One, 0.1f, false, bounds));
            float fast = Vector2.Distance(origin, ExplorationMode.Pan(origin, Vector2.UnitX, 0.1f, true, bounds));
            Assert.Equal(straight, diagonal, 4);
            Assert.Equal(straight * 3f, fast, 4);
        }
        [Theory]
        [InlineData(-20, 500, 0, 500)]
        [InlineData(7000, 5000, 6143, 4095)]
        [InlineData(100, -2, 100, 0)]
        public void Camera_position_stays_inside_map_boundaries(float x, float y, float expectedX, float expectedY) =>
            Assert.Equal(new Vector2(expectedX, expectedY), ExplorationMode.ClampPosition(new Vector2(x, y), new Point(6144, 4096)));
        [Fact]
        public void Long_frame_after_focus_return_does_not_jump_across_the_map()
        {
            var origin = new Vector2(100, 100);
            var bounds = new Point(1000, 1000);
            Assert.Equal(ExplorationMode.Pan(origin, Vector2.UnitX, 0.1f, true, bounds),
                ExplorationMode.Pan(origin, Vector2.UnitX, 10f, true, bounds));
            Assert.Equal(origin, ExplorationMode.Pan(origin, Vector2.UnitX, -1f, true, bounds));
        }
        [Theory]
        [InlineData(100, 200, 0)]
        [InlineData(6143, 4095, 100)]
        [InlineData(0, 0, -20)]
        public void Render_origin_centers_distant_terrain_without_a_player_reference(int x, int y, int z)
        {
            var center = new Point(1920, 1080);
            Point offset = ExplorationMode.DrawOffset(new Vector2(x, y), center, z);
            Assert.Equal(center.X, (x - y) * 22 - offset.X);
            Assert.Equal(center.Y, (x + y) * 22 - (z << 2) - offset.Y);
        }
        [Fact]
        public void Releasing_one_direction_alias_keeps_the_other_held_and_focus_reset_clears_both()
        {
            var active = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.Active), BindingFlags.Static | BindingFlags.NonPublic);
            var keys = (bool[])typeof(ExplorationMode).GetField("_keys", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previousKeys = (bool[])keys.Clone();
            bool previousActive = ExplorationMode.Active;
            bool previousSteering = ExplorationMode.MouseSteering;
            try
            {
                active.SetValue(null, true);
                keys[ExplorationMode.KeyIndex(SDL.SDL_Keycode.SDLK_W)] = true;
                keys[ExplorationMode.KeyIndex(SDL.SDL_Keycode.SDLK_UP)] = true;
                Assert.True(ExplorationMode.KeyUp(SDL.SDL_Keycode.SDLK_W));
                Assert.False(keys[ExplorationMode.KeyIndex(SDL.SDL_Keycode.SDLK_W)]);
                Assert.True(keys[ExplorationMode.KeyIndex(SDL.SDL_Keycode.SDLK_UP)]);
                ExplorationMode.ResetKeys();
                Assert.All(keys, held => Assert.False(held));
            }
            finally { previousKeys.CopyTo(keys, 0); active.SetValue(null, previousActive); ExplorationMode.SetMouseSteering(previousSteering); }
        }
        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(16, 0, 0, 0)]
        [InlineData(100, 0, 1, 0)]
        [InlineData(-100, 0, -1, 0)]
        [InlineData(0, -100, 0, -1)]
        [InlineData(0, 100, 0, 1)]
        public void Mouse_steering_uses_viewport_center_and_has_a_stop_zone(int x, int y, int expectedX, int expectedY)
        {
            var bounds = new Rectangle(120, 40, 800, 600);
            Vector2 direction = ExplorationMode.MouseDirection(new Point(520 + x, 340 + y), bounds);
            Assert.Equal(new Vector2(expectedX, expectedY), direction);
        }
        [Fact]
        public void Moving_mouse_further_away_does_not_make_the_bird_faster()
        {
            var bounds = new Rectangle(0, 0, 3840, 2160);
            Vector2 near = ExplorationMode.MouseDirection(new Point(2000, 1160), bounds);
            Vector2 far = ExplorationMode.MouseDirection(new Point(2720, 1880), bounds);
            Assert.Equal(near.X, far.X, 5);
            Assert.Equal(near.Y, far.Y, 5);
        }
        [Theory]
        [InlineData(0.0166667f)]
        [InlineData(0.0333333f)]
        [InlineData(0.1f)]
        public void Takeoff_rises_to_cruising_height_without_overshoot(float seconds)
        {
            float altitude = 0;
            for (int frame = 0; frame < 150; ++frame)
            {
                float next = ExplorationMode.Rise(altitude, seconds);
                Assert.InRange(next, altitude, 64f);
                altitude = next;
            }
            Assert.Equal(64f, altitude);
            Assert.Equal(altitude, ExplorationMode.Rise(altitude, -1));
            Assert.Equal(ExplorationMode.Rise(0, 0.1f), ExplorationMode.Rise(0, 10));
        }
        [Theory]
        [InlineData(90f, 1, 0.1f, false, 93.2f)]
        [InlineData(90f, -1, 0.1f, false, 86.8f)]
        [InlineData(90f, 0, 0.1f, false, 90f)]
        [InlineData(90f, 1, 0.1f, true, 99.6f)]
        [InlineData(254f, 1, 0.1f, false, 255f)]
        [InlineData(-127f, -1, 0.1f, false, -128f)]
        [InlineData(90f, 1, 10f, false, 93.2f)]
        [InlineData(90f, -1, -1f, false, 90f)]
        public void Vertical_controls_raise_descend_or_hold_height_with_bounded_frame_steps(float z, int direction, float seconds, bool fast, float expected)
        {
            Assert.Equal(expected, ExplorationMode.ChangeHeight(z, direction, seconds, fast), 3);
        }
        [Fact]
        public void Terrain_height_changes_do_not_shift_the_flight_camera()
        {
            var flightZ = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.FlightZ), BindingFlags.Static | BindingFlags.NonPublic);
            var groundZ = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.GroundZ), BindingFlags.Static | BindingFlags.NonPublic);
            float previousFlightZ = ExplorationMode.FlightZ;
            sbyte previousGroundZ = ExplorationMode.GroundZ;
            try
            {
                flightZ.SetValue(null, 90f);
                var position = new Vector2(100, 200);
                var center = new Point(400, 300);
                Point offset = ExplorationMode.DrawOffset(position, center, ExplorationMode.FlightZ);
                foreach (sbyte ground in new sbyte[] { -128, -20, 0, 60, 127 })
                {
                    groundZ.SetValue(null, ground);
                    Assert.Equal(offset, ExplorationMode.DrawOffset(position, center, ExplorationMode.FlightZ));
                    Assert.Equal(center, ExplorationMode.DrawOffset(position, offset, ExplorationMode.FlightZ));
                    Assert.Equal(90f - ground, ExplorationMode.Altitude);
                }
            }
            finally { flightZ.SetValue(null, previousFlightZ); groundZ.SetValue(null, previousGroundZ); }
        }
        [Theory]
        [InlineData(SDL.SDL_Keycode.SDLK_Q)]
        [InlineData(SDL.SDL_Keycode.SDLK_E)]
        public void Vertical_keys_release_and_focus_reset_clears_them(SDL.SDL_Keycode key)
        {
            var active = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.Active), BindingFlags.Static | BindingFlags.NonPublic);
            var keys = (bool[])typeof(ExplorationMode).GetField("_keys", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previousKeys = (bool[])keys.Clone();
            bool previousActive = ExplorationMode.Active, previousSteering = ExplorationMode.MouseSteering;
            try
            {
                active.SetValue(null, true);
                int index = ExplorationMode.KeyIndex(key);
                Assert.InRange(index, 8, 9);
                keys[index] = true;
                Assert.True(ExplorationMode.KeyUp(key));
                Assert.False(keys[index]);
                keys[index] = true;
                ExplorationMode.ResetKeys();
                Assert.False(keys[index]);
            }
            finally { previousKeys.CopyTo(keys, 0); active.SetValue(null, previousActive); ExplorationMode.SetMouseSteering(previousSteering); }
        }
        [Theory]
        [InlineData(0f, -20)]
        [InlineData(0.5f, 0)]
        [InlineData(32.3f, 60)]
        [InlineData(64f, 127)]
        public void Airborne_avatar_stays_with_camera_at_distant_coordinates(float altitude, int groundZ)
        {
            var position = new Vector2(5020.25f, 3040.75f);
            var center = new Point(1920, 1080);
            Point offset = ExplorationMode.DrawOffset(position, center, groundZ + altitude);
            Assert.Equal(center.X, (int)System.Math.Round((position.X - position.Y) * 22f) - offset.X);
            int groundY = (int)System.Math.Round((position.X + position.Y) * 22f) - offset.Y - (groundZ << 2);
            Assert.Equal(center.Y, groundY - (int)System.Math.Round(altitude * 4f));
            Assert.True(groundY >= center.Y);
        }
        [Fact]
        public void Focus_reset_ends_mouse_flight_and_inactive_mode_cannot_start_it()
        {
            var active = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.Active), BindingFlags.Static | BindingFlags.NonPublic);
            var keys = (bool[])typeof(ExplorationMode).GetField("_keys", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previousKeys = (bool[])keys.Clone();
            bool previousActive = ExplorationMode.Active, previousSteering = ExplorationMode.MouseSteering;
            try
            {
                active.SetValue(null, false);
                ExplorationMode.SetMouseSteering(true);
                Assert.False(ExplorationMode.MouseSteering);
                active.SetValue(null, true);
                ExplorationMode.SetMouseSteering(true);
                Assert.True(ExplorationMode.MouseSteering);
                ExplorationMode.ResetKeys();
                Assert.False(ExplorationMode.MouseSteering);
            }
            finally { previousKeys.CopyTo(keys, 0); active.SetValue(null, previousActive); ExplorationMode.SetMouseSteering(previousSteering); }
        }
        [Fact]
        public void Exploration_blocks_both_walking_paths_before_they_touch_player_or_network_state()
        {
            var active = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.Active), BindingFlags.Static | BindingFlags.NonPublic);
            bool previousActive = ExplorationMode.Active;
            try
            {
                active.SetValue(null, true);
                // No skills/assets/network setup: the guard must return before any of it is needed.
                var player = (PlayerMobile)FormatterServices.GetUninitializedObject(typeof(PlayerMobile));
                Assert.False(player.Walk(ClassicUO.Game.Data.Direction.North, false));
                Assert.False(player.WalkNotAvoid(ClassicUO.Game.Data.Direction.North, true));
                Assert.Equal((ushort)0, player.X);
                Assert.Equal((ushort)0, player.Y);
            }
            finally { active.SetValue(null, previousActive); }
        }
        [Fact]
        public void Reset_peek_returns_camera_offset_to_zero()
        {
            var camera = new Camera(0.25f, 3f) { Bounds = new Rectangle(0, 0, 800, 600), PeekingToMouse = true };
            camera.Update(true, 0.016f, new Point(799, 300));
            Assert.NotEqual(Vector2.Zero, camera.Offset);
            camera.ResetPeek();
            Assert.Equal(Vector2.Zero, camera.Offset);
            Assert.False(camera.PeekingToMouse);
            Assert.Equal(new Point(400, 300), camera.WorldToScreen(new Point(400, 300)));
        }
        [Fact]
        public void New_macro_is_appended_without_changing_existing_saved_macro_numbers()
        {
            Assert.Equal((int)MacroType.ContextAction + 1, (int)MacroType.ToggleExploration);
            Assert.Equal(0, new MacroObject(MacroType.ToggleExploration, MacroSubType.MSC_NONE).SubMenuType);
        }
    }
}
