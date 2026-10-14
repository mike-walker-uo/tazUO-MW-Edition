using System;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDL3;

namespace ClassicUO.Game.Managers
{
    internal static class ExplorationMode
    {
        private static readonly bool[] _keys = new bool[10];
        private static Profile _profile;
        private static SystemChatControl _chat;
        private static bool _chatWasActive;
        private static GameScene _scene;
        private static int _map;
        private static float _zoom, _takeoffZ;
        private static bool _wasAlive, _prepareInput, _takingOff;
        private static byte _birdAnimation, _birdDirection;
        internal static bool MouseSteering { get; private set; }
        internal static bool Active { get; private set; }
        internal static Vector2 Position { get; private set; }
        internal static Point MapSize { get; private set; }
        internal static sbyte GroundZ { get; private set; }
        internal static ushort BirdGraphic { get; private set; }
        internal static ushort BirdHue { get; private set; }
        internal static float FlightZ { get; private set; }
        internal static float Altitude => FlightZ - GroundZ;

        internal static void Command(string[] args)
        {
            string action = args != null && args.Length > 1 ? args[1].ToLowerInvariant() : "toggle";
            if (args?.Length > 2 || !(action == "on" || action == "off" || action == "toggle" || action == "status"))
            { GameActions.Print("Usage: -fly [on|off|toggle|status]", 0x35); return; }
            if (action == "off") Stop();
            else if (action == "on") Start();
            else if (action == "toggle") Toggle();
            else GameActions.Print(Active ? $"Exploration ON: {(int)Position.X}, {(int)Position.Y}, Z {FlightZ:0.0}. Character remains live." : "Exploration OFF.", 0x35);
        }
        internal static void Toggle() { if (Active) Stop(); else Start(); }
        private static void Start()
        {
            if (Active) return;
            var scene = Client.Game?.GetScene<GameScene>();
            if (!World.InGame || scene == null || ProfileManager.CurrentProfile == null)
            { GameActions.Print("Log in to use exploration mode.", 0x35); return; }
            _scene = scene; _profile = ProfileManager.CurrentProfile; _map = World.MapIndex;
            _chat = UIManager.SystemChat; _chatWasActive = _chat?.IsActive == true;
            _zoom = scene.Camera.Zoom; _wasAlive = !World.Player.IsDead;
            MapSize = new Point(MapLoader.Instance.MapBlocksSize[_map, 0] * 8, MapLoader.Instance.MapBlocksSize[_map, 1] * 8);
            Position = ClampPosition(new Vector2(World.Player.X, World.Player.Y), MapSize);
            GroundZ = World.Map.GetTileZ((int)Position.X, (int)Position.Y);
            ResetKeys(); scene.ResetHeldInput(); scene.Camera.ResetPeek(); scene.Camera.Zoom = _zoom;
            Pathfinder.StopAutoWalk();
            if (TargetManager.IsTargeting) TargetManager.CancelTarget();
            BirdGraphic = (ushort)RandomHelper.GetValue(5, 6); // Eagle or small bird.
            BirdHue = (ushort)RandomHelper.GetValue(2, Math.Min(1001, HuesLoader.Instance.HuesCount - 1));
            _birdAnimation = (byte)(Client.Game.Animations.AnimationExists(BirdGraphic, (byte)HighAnimationGroup.Fly)
                ? HighAnimationGroup.Fly : HighAnimationGroup.Walk);
            _birdDirection = (byte)((byte)World.Player.Direction & 7);
            FlightZ = _takeoffZ = World.Player.Z;
            _takingOff = true;
            Active = true; _prepareInput = true;
            _chat?.RefreshInputVisibility();
            scene.UpdateDrawPosition = true; scene.SetPostProcessingSettings();
            UIManager.Add(new ExplorationModeGump());
            GameActions.Print("Exploration ON. Hold right mouse / WASD / arrows to fly, Q rises, E descends, Shift speeds up, wheel zooms, Esc returns. Your character can still be attacked.", 0x35);
        }
        internal static void Stop()
        {
            if (!Active) return;
            Active = false; ResetKeys();
            BirdGraphic = BirdHue = 0;
            FlightZ = 0; _takingOff = false;
            var scene = _scene;
            _scene = null; _profile = null;
            if (_chat != null && !_chat.IsDisposed && ReferenceEquals(_chat, UIManager.SystemChat))
                _chat.RestoreExplorationChat(_chatWasActive);
            _chat = null;
            scene?.ResetHeldInput();
            if (scene != null)
            {
                scene.Camera.ResetPeek(); scene.Camera.Zoom = _zoom; scene.UpdateDrawPosition = true;
                if (World.InGame && ProfileManager.CurrentProfile != null) scene.UpdateMaxDrawZ(true);
                if (ProfileManager.CurrentProfile != null) scene.SetPostProcessingSettings();
            }
            UIManager.GetGump<ExplorationModeGump>()?.Dispose();
        }
        internal static void ResetKeys() { Array.Clear(_keys, 0, _keys.Length); MouseSteering = false; }
        internal static void SetMouseSteering(bool value) => MouseSteering = Active && value;
        internal static int KeyIndex(SDL.SDL_Keycode key)
        {
            switch (key)
            {
                case SDL.SDL_Keycode.SDLK_W: return 0;
                case SDL.SDL_Keycode.SDLK_UP: return 1;
                case SDL.SDL_Keycode.SDLK_A: return 2;
                case SDL.SDL_Keycode.SDLK_LEFT: return 3;
                case SDL.SDL_Keycode.SDLK_S: return 4;
                case SDL.SDL_Keycode.SDLK_DOWN: return 5;
                case SDL.SDL_Keycode.SDLK_D: return 6;
                case SDL.SDL_Keycode.SDLK_RIGHT: return 7;
                case SDL.SDL_Keycode.SDLK_Q: return 8;
                case SDL.SDL_Keycode.SDLK_E: return 9;
                default: return -1;
            }
        }
        internal static bool KeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (!Active) return false;
            if (key == SDL.SDL_Keycode.SDLK_ESCAPE) { Stop(); return true; }
            if ((key == SDL.SDL_Keycode.SDLK_RETURN || key == SDL.SDL_Keycode.SDLK_KP_ENTER)
                && UIManager.SystemChat != null && !UIManager.SystemChat.IsActive
                && UIManager.KeyboardFocusControl == UIManager.SystemChat.TextBoxControl)
            {
                UIManager.SystemChat.BeginExplorationChat();
                ResetKeys(); return true;
            }
            int index = KeyIndex(key);
            if (index < 0 || (mod & (SDL.SDL_Keymod.SDL_KMOD_CTRL | SDL.SDL_Keymod.SDL_KMOD_ALT)) != SDL.SDL_Keymod.SDL_KMOD_NONE || UIManager.SystemChat == null
                || UIManager.SystemChat.IsActive || UIManager.KeyboardFocusControl != UIManager.SystemChat.TextBoxControl)
                return false;
            _keys[index] = true;
            if (index >= 8) _takingOff = false;
            return true;
        }
        internal static bool KeyUp(SDL.SDL_Keycode key)
        {
            int index = KeyIndex(key);
            if (!Active || index < 0 || !_keys[index]) return false;
            _keys[index] = false; return true;
        }
        internal static Vector2 Pan(Vector2 position, Vector2 screenDirection, float seconds, bool fast, Point size)
        {
            if (screenDirection == Vector2.Zero) return ClampPosition(position, size);
            screenDirection.Normalize();
            // Screen-axis input converted to UO's two isometric tile axes.
            var tiles = new Vector2(screenDirection.X + screenDirection.Y, screenDirection.Y - screenDirection.X);
            tiles.Normalize();
            return ClampPosition(position + tiles * (fast ? 24f : 8f) * Math.Max(0f, Math.Min(0.1f, seconds)), size);
        }
        internal static Vector2 ClampPosition(Vector2 position, Point size) => new Vector2(
            Math.Max(0f, Math.Min(Math.Max(0, size.X - 1), position.X)),
            Math.Max(0f, Math.Min(Math.Max(0, size.Y - 1), position.Y)));
        internal static Point DrawOffset(Vector2 position, Point center, float z) => new Point(
            (int)Math.Round((position.X - position.Y) * 22f) - center.X,
            (int)Math.Round((position.X + position.Y) * 22f) - center.Y - (int)Math.Round(z * 4f));
        internal static float Rise(float altitude, float seconds) =>
            Math.Min(64f, altitude + 64f / 1.25f * Math.Max(0f, Math.Min(0.1f, seconds)));
        internal static float ChangeHeight(float z, int direction, float seconds, bool fast) =>
            Math.Max(-128f, Math.Min(255f, z + direction * (fast ? 96f : 32f) * Math.Max(0f, Math.Min(0.1f, seconds))));
        internal static Vector2 MouseDirection(Point mouse, Rectangle bounds)
        {
            var direction = new Vector2(mouse.X - (bounds.X + bounds.Width / 2f),
                mouse.Y - (bounds.Y + bounds.Height / 2f));
            if (direction.LengthSquared() <= 16f * 16f) return Vector2.Zero;
            direction.Normalize();
            return direction;
        }
        internal static void DrawBird(UltimaBatcher2D batcher, Point offset)
        {
            if (!Active) return;
            byte direction = _birdDirection;
            bool flipped = false;
            AnimationsLoader.Instance.GetAnimDirection(ref direction, ref flipped);
            var frames = Client.Game.Animations.GetAnimationFrames(BirdGraphic, _birdAnimation, direction, out _, out _);
            if (frames.Length == 0) return;
            var sprite = frames[(int)((Time.Ticks / 100) % frames.Length)];
            if (sprite.Texture == null) return;
            Point ground = DrawOffset(Position, offset, GroundZ);
            Point bird = DrawOffset(Position, offset, FlightZ);
            var anchor = new Vector2(flipped ? sprite.UV.Width - sprite.Center.X : sprite.Center.X,
                sprite.UV.Height + sprite.Center.Y);
            batcher.SetStencil(DepthStencilState.None); // Always above roofs; never part of world selection.
            if (Altitude >= 0)
                batcher.DrawShadow(sprite.Texture, new Vector2(ground.X, ground.Y) - anchor, sprite.UV, flipped, 0f, 1f, 0.25f, 1f, 1f);
            batcher.Draw(sprite.Texture, new Vector2(bird.X, bird.Y) - anchor, sprite.UV, ShaderHueTranslator.GetHueVector(BirdHue, false, 1f),
                0f, Vector2.Zero, 1f, flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            batcher.SetStencil(null);
        }
        internal static void Update()
        {
            if (!Active) return;
            if (!World.InGame || World.MapIndex != _map || ProfileManager.CurrentProfile != _profile
                || Client.Game.Scene != _scene || _wasAlive && World.Player.IsDead)
            { Stop(); return; }
            _wasAlive = !World.Player.IsDead;
            if (Client.Game.IsActive && _takingOff)
            {
                FlightZ = _takeoffZ + Rise(FlightZ - _takeoffZ, Time.Delta);
                if (FlightZ >= _takeoffZ + 64f) _takingOff = false;
            }
            if (_prepareInput)
            {
                _prepareInput = false;
                UIManager.SystemChat.IsActive = false;
            }
            if (!Client.Game.IsActive || UIManager.SystemChat.IsActive
                || UIManager.KeyboardFocusControl != UIManager.SystemChat.TextBoxControl)
            { ResetKeys(); return; }
            FlightZ = ChangeHeight(FlightZ, (_keys[8] ? 1 : 0) - (_keys[9] ? 1 : 0), Time.Delta, Keyboard.Shift);
            var direction = new Vector2((_keys[6] || _keys[7] ? 1 : 0) - (_keys[2] || _keys[3] ? 1 : 0),
                (_keys[4] || _keys[5] ? 1 : 0) - (_keys[0] || _keys[1] ? 1 : 0));
            if (!Mouse.RButtonPressed) MouseSteering = false;
            if (direction == Vector2.Zero && MouseSteering && UIManager.IsMouseOverWorld)
            {
                var bounds = _scene.Camera.Bounds;
                if (_profile.GlobalScaling && _profile.GameWindowFullSize)
                { bounds.Width = (int)(bounds.Width / _profile.GlobalScale); bounds.Height = (int)(bounds.Height / _profile.GlobalScale); }
                direction = MouseDirection(Mouse.WorldPosition, bounds);
            }
            if (direction != Vector2.Zero)
                _birdDirection = (byte)GameCursor.GetMouseDirection(0, 0, (int)(direction.X * 100), (int)(direction.Y * 100), _birdDirection);
            _scene.Camera.ResetPeek();
            Position = Pan(Position, direction, Time.Delta, Keyboard.Shift, MapSize);
            GroundZ = World.Map.GetTileZ((int)Position.X, (int)Position.Y);
        }
    }
}
