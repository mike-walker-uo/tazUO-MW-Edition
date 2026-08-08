// TazUO addition: compact controls for configured and auto-discovered music.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MusicPlayerGump : Gump
    {
        private const int W = 350;
        private const int H = 310;
        private const int COMPACT_H = 90;
        private const int PLAYLIST_BUTTON_OFFSET = 1000;
        private const int FAVORITE_BUTTON_OFFSET = 3000;
        private static ushort TEXT_HUE => CustomGumpThemeManager.TitleHue;
        private static ushort STATUS_HUE => CustomGumpThemeManager.TextHue;

        private enum Buttons
        {
            Previous = 1,
            Play,
            Pause,
            Stop,
            Next,
            Shuffle,
            Mute,
            Mode,
            VolumeDown,
            VolumeUp,
            Rescan,
            PlaybackLoop,
            PlaybackList,
            Radio,
            ToggleCompact
        }

        private enum MusicIcon
        {
            Previous,
            Play,
            Pause,
            Stop,
            Next,
            Shuffle,
            Speaker,
            Muted,
            Loop,
            List,
            Radio,
            Collapse,
            Expand,
            Heart
        }

        private readonly ThemedGumpBackground _background;
        private readonly Label _trackLabel;
        private readonly Label _statusLabel;
        private readonly Label _radioInfoLabel;
        private readonly Label _playlistLabel;
        private readonly MusicIconButton _muteButton;
        private readonly MusicIconButton _loopModeButton;
        private readonly MusicIconButton _listModeButton;
        private readonly MusicIconButton _shuffleModeButton;
        private readonly MusicIconButton _radioButton;
        private readonly MusicIconButton _compactButton;
        private readonly NiceButton _modeButton;
        private readonly ScrollArea _playlistArea;
        private readonly VBoxContainer _playlistBox;
        private readonly Dictionary<int, NiceButton> _playlistRows = new Dictionary<int, NiceButton>();
        private readonly Dictionary<int, MusicIconButton> _favoriteButtons = new Dictionary<int, MusicIconButton>();
        private int _playlistCount = -1;
        private long _nextRefresh;
        private int _lastX;
        private int _lastY;
        private bool _isCompact;

        public MusicPlayerGump(bool startCompact = false) : base(0, 0)
        {
            Profile profile = ProfileManager.CurrentProfile;
            Point position = profile?.MusicPlayerPosition ?? new Point(200, 40);
            _isCompact = startCompact || (profile?.MusicPlayerCompact ?? false);

            X = _lastX = position.X;
            Y = _lastY = position.Y;
            Width = W;
            Height = _isCompact ? COMPACT_H : H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            SetInScreen();
            _lastX = X;
            _lastY = Y;

            Add(_background = CustomGumpThemeManager.CreateBackground(W, H, 0.82f));
            Add(_trackLabel = new Label("Music: waiting for a track", true, TEXT_HUE, W - 42, font: 1)
            {
                X = 6,
                Y = 4
            });
            Add(_compactButton = new MusicIconButton(328, 3, 16, MusicIcon.Collapse, "Minimize player", Buttons.ToggleCompact));

            AddIconButton(6, 25, 42, MusicIcon.Previous, "Previous track", Buttons.Previous);
            AddIconButton(52, 25, 42, MusicIcon.Play, "Play", Buttons.Play);
            AddIconButton(98, 25, 46, MusicIcon.Pause, "Pause", Buttons.Pause);
            AddIconButton(148, 25, 42, MusicIcon.Stop, "Stop", Buttons.Stop);
            AddIconButton(194, 25, 42, MusicIcon.Next, "Next track", Buttons.Next);
            Add(_shuffleModeButton = new MusicIconButton(240, 25, 58, MusicIcon.Shuffle, "Shuffle mode / next random track", Buttons.Shuffle));
            Add(_muteButton = new MusicIconButton(302, 25, 42, MusicIcon.Speaker, "Mute", Buttons.Mute));

            Add(_modeButton = CreateButton(6, 47, 74, "Mode", Buttons.Mode));
            AddButton(84, 47, 42, "Vol-", Buttons.VolumeDown);
            AddButton(130, 47, 42, "Vol+", Buttons.VolumeUp);
            AddButton(176, 47, 48, "Scan", Buttons.Rescan);
            Add(_loopModeButton = new MusicIconButton(228, 47, 36, MusicIcon.Loop, "Loop current track", Buttons.PlaybackLoop));
            Add(_listModeButton = new MusicIconButton(268, 47, 36, MusicIcon.List, "Play playlist in order", Buttons.PlaybackList));
            Add(_radioButton = new MusicIconButton(308, 47, 36, MusicIcon.Radio, "Ancient FM: connect or resume", Buttons.Radio));

            Add(_statusLabel = new Label(string.Empty, true, STATUS_HUE, W - 12, font: 1)
            {
                X = 6,
                Y = 70
            });

            Add(_radioInfoLabel = new Label(string.Empty, true, STATUS_HUE, W - 12, font: 1)
            {
                X = 6,
                Y = 90
            });

            Add(_playlistLabel = new Label("Custom playlist", true, TEXT_HUE, W - 12, font: 1)
            {
                X = 6,
                Y = 110
            });

            Add(_playlistArea = new ScrollArea(6, 128, W - 12, H - 134, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            });
            _playlistBox = new VBoxContainer(_playlistArea.Width - _playlistArea.ScrollBarWidth() - 2, 0, 0);
            _playlistArea.Add(_playlistBox);

            RebuildPlaylist();
            RefreshLabels();
            ApplyCompactState();
        }

        public override GumpType GumpType => GumpType.None;
        internal bool IsCompact => _isCompact;

        public override void OnButtonClick(int buttonID)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile == null)
            {
                return;
            }

            if (buttonID >= FAVORITE_BUTTON_OFFSET)
            {
                int music = buttonID - FAVORITE_BUTTON_OFFSET;

                if (IsCustomMusic(music))
                {
                    Client.Game.Audio.ToggleFavoriteCustomMusic(music);
                    SaveProfile();
                    RefreshPlaylistSelection();
                }

                return;
            }

            if (buttonID >= PLAYLIST_BUTTON_OFFSET)
            {
                int music = buttonID - PLAYLIST_BUTTON_OFFSET;

                if (IsCustomMusic(music))
                {
                    profile.EnableMusic = true;
                    Client.Game.Audio.PlayCustomMusic(music);
                    Client.Game.Audio.UpdateCurrentMusicVolume();
                    SaveProfile();
                    RefreshLabels();
                }

                return;
            }

            switch ((Buttons)buttonID)
            {
                case Buttons.Previous:
                    Client.Game.Audio.StepCustomMusic(-1);
                    break;
                case Buttons.Play:
                    profile.EnableMusic = true;
                    Client.Game.Audio.PlayCurrentMusic();
                    Client.Game.Audio.UpdateCurrentMusicVolume();
                    break;
                case Buttons.Pause:
                    Client.Game.Audio.PauseCurrentMusic();
                    break;
                case Buttons.Stop:
                    Client.Game.Audio.StopCurrentMusic();
                    break;
                case Buttons.Shuffle:
                    profile.MusicPlaybackMode = 2;
                    Client.Game.Audio.ApplyCustomMusicPlaybackMode();
                    Client.Game.Audio.ShuffleCustomMusic();
                    break;
                case Buttons.Mute:
                    profile.EnableMusic = !profile.EnableMusic;
                    Client.Game.Audio.UpdateCurrentMusicVolume();

                    if (profile.EnableMusic && Client.Game.Audio.CurrentMusicIndex < 0 && !Client.Game.Audio.IsRadioSelected)
                    {
                        Client.Game.Audio.StepCustomMusic(1);
                    }
                    break;
                case Buttons.Next:
                    Client.Game.Audio.StepCustomMusic(1);
                    break;
                case Buttons.Mode:
                    profile.MusicSelectionMode = (byte)((profile.MusicSelectionMode + 1) % 3);
                    Client.Game.Audio.RefreshMusicSelection();
                    break;
                case Buttons.VolumeDown:
                    profile.MusicVolume = Math.Max(0, profile.MusicVolume - 10);
                    Client.Game.Audio.UpdateCurrentMusicVolume();
                    break;
                case Buttons.VolumeUp:
                    profile.MusicVolume = Math.Min(100, profile.MusicVolume + 10);
                    Client.Game.Audio.UpdateCurrentMusicVolume();
                    break;
                case Buttons.Rescan:
                    int added = SoundsLoader.Instance.RescanCustomMusic();
                    GameActions.Print($"Music scan: {SoundsLoader.Instance.LastMusicScanFileCount} MP3s found, {added} added, {SoundsLoader.Instance.CustomMusicIndices.Count} custom tracks total.", 0x35);
                    GameActions.Print($"Scanned: {SoundsLoader.Instance.MusicDirectories}", 0x35);
                    RebuildPlaylist();
                    break;
                case Buttons.PlaybackLoop:
                    profile.MusicPlaybackMode = 0;
                    Client.Game.Audio.ApplyCustomMusicPlaybackMode();
                    break;
                case Buttons.PlaybackList:
                    profile.MusicPlaybackMode = 1;
                    Client.Game.Audio.ApplyCustomMusicPlaybackMode();
                    break;
                case Buttons.Radio:
                    profile.EnableMusic = true;
                    Client.Game.Audio.PlayAncientFm();
                    Client.Game.Audio.UpdateCurrentMusicVolume();
                    break;
                case Buttons.ToggleCompact:
                    _isCompact = !_isCompact;
                    profile.MusicPlayerCompact = _isCompact;
                    ApplyCompactState();
                    break;
                default:
                    base.OnButtonClick(buttonID);
                    return;
            }

            SaveProfile();
            RefreshLabels();
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
            {
                return;
            }

            if (X != _lastX || Y != _lastY)
            {
                _lastX = X;
                _lastY = Y;

                if (ProfileManager.CurrentProfile != null)
                {
                    ProfileManager.CurrentProfile.MusicPlayerPosition = new Point(X, Y);
                }
            }

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 250;

                if (_playlistCount != SoundsLoader.Instance.CustomMusicIndices.Count)
                {
                    RebuildPlaylist();
                }

                RefreshLabels();
            }
        }

        public override void Dispose()
        {
            if (ProfileManager.CurrentProfile != null)
            {
                ProfileManager.CurrentProfile.MusicPlayerPosition = new Point(X, Y);
                SaveProfile();
            }

            base.Dispose();
        }

        private void AddButton(int x, int y, int width, string text, Buttons button)
        {
            Add(CreateButton(x, y, width, text, button));
        }

        private void AddIconButton(int x, int y, int width, MusicIcon icon, string tooltip, Buttons button)
        {
            Add(new MusicIconButton(x, y, width, icon, tooltip, button));
        }

        private static NiceButton CreateButton(int x, int y, int width, string text, Buttons button)
        {
            var result = new NiceButton(x, y, width, 18, ButtonAction.Activate, text, hue: TEXT_HUE, font: 1)
            {
                ButtonParameter = (int)button,
                IsSelectable = false,
                AlwaysShowBackground = true,
                Hue = CustomGumpThemeManager.ButtonHue
            };
            CustomGumpThemeManager.StyleButton(result);
            return result;
        }

        private void RefreshLabels()
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile == null)
            {
                return;
            }

            string track = Client.Game.Audio.CurrentMusicName;
            _trackLabel.Text = string.IsNullOrEmpty(track) ? "Music: waiting for a track" : $"Music: {track}";

            bool radioSelected = Client.Game.Audio.IsRadioSelected;
            string mode = radioSelected
                ? "RADIO"
                : profile.MusicSelectionMode == 0
                    ? "ORIGINAL"
                    : profile.MusicSelectionMode == 2 ? "MIXED" : "NEW";
            string state = profile.EnableMusic ? "ON" : "MUTED";
            string playback = Client.Game.Audio.CurrentMusicPlaybackState;
            _statusLabel.Text = $"{mode} | {state} | {playback} | Vol {profile.MusicVolume}% | {SoundsLoader.Instance.CustomMusicIndices.Count} custom";
            _radioInfoLabel.Text = Client.Game.Audio.CurrentRadioDetails;
            _muteButton.SetIcon(profile.EnableMusic ? MusicIcon.Speaker : MusicIcon.Muted);
            _muteButton.SetTooltip(profile.EnableMusic ? "Mute" : "Unmute");
            _modeButton.SetText(radioSelected ? "Mode" : mode);
            _loopModeButton.IsSelected = !radioSelected && profile.MusicPlaybackMode == 0;
            _listModeButton.IsSelected = !radioSelected && profile.MusicPlaybackMode == 1;
            _shuffleModeButton.IsSelected = !radioSelected && profile.MusicPlaybackMode == 2;
            _radioButton.IsSelected = radioSelected;
            RefreshPlaylistSelection();
        }

        private void ApplyCompactState()
        {
            Height = _isCompact ? COMPACT_H : H;
            _background.Height = Height;
            _radioInfoLabel.IsVisible = !_isCompact;
            _playlistLabel.IsVisible = !_isCompact;
            _playlistArea.IsVisible = !_isCompact;
            _compactButton.SetIcon(_isCompact ? MusicIcon.Expand : MusicIcon.Collapse);
            _compactButton.SetTooltip(_isCompact ? "Expand player" : "Minimize player");
            SetInScreen();
            _lastX = X;
            _lastY = Y;

            if (ProfileManager.CurrentProfile != null)
            {
                ProfileManager.CurrentProfile.MusicPlayerPosition = new Point(X, Y);
            }
        }

        private void RebuildPlaylist()
        {
            IReadOnlyList<int> music = SoundsLoader.Instance.CustomMusicIndices;
            _playlistCount = music.Count;
            _playlistLabel.Text = $"Custom playlist ({music.Count})";
            _playlistRows.Clear();
            _favoriteButtons.Clear();
            _playlistBox.Clear();

            if (music.Count == 0)
            {
                _playlistBox.Add(new Label("No custom MP3 tracks. Click Scan.", true, STATUS_HUE, _playlistBox.Width, font: 1));
                return;
            }

            for (int i = 0; i < music.Count; i++)
            {
                int musicID = music[i];
                string name = GetMusicName(musicID);
                var container = new DataBox(0, 0, _playlistBox.Width, 20);
                var row = new NiceButton(
                    0,
                    0,
                    _playlistBox.Width - 24,
                    20,
                    ButtonAction.Activate,
                    name,
                    align: TEXT_ALIGN_TYPE.TS_LEFT,
                    hue: TEXT_HUE,
                    font: 1)
                {
                    ButtonParameter = PLAYLIST_BUTTON_OFFSET + musicID,
                    Hue = CustomGumpThemeManager.ButtonHue
                };
                CustomGumpThemeManager.StyleButton(row);
                var favorite = new MusicIconButton(
                    _playlistBox.Width - 22,
                    1,
                    20,
                    MusicIcon.Heart,
                    "Favorite this track (4x shuffle weight)",
                    FAVORITE_BUTTON_OFFSET + musicID);

                _playlistRows[musicID] = row;
                _favoriteButtons[musicID] = favorite;
                container.Add(row);
                container.Add(favorite);
                _playlistBox.Add(container);
            }

            RefreshPlaylistSelection();
        }

        private void RefreshPlaylistSelection()
        {
            int current = Client.Game.Audio.CurrentMusicIndex;
            int favoriteCount = 0;

            foreach (KeyValuePair<int, NiceButton> row in _playlistRows)
            {
                row.Value.IsSelected = row.Key == current;
            }

            foreach (KeyValuePair<int, MusicIconButton> favorite in _favoriteButtons)
            {
                bool selected = Client.Game.Audio.IsFavoriteCustomMusic(favorite.Key);
                favorite.Value.IsSelected = selected;
                favorite.Value.SetTooltip(selected
                    ? "Remove favorite weighting"
                    : "Favorite this track (4x shuffle weight)");

                if (selected)
                {
                    favoriteCount++;
                }
            }

            _playlistLabel.Text = $"Custom playlist ({_playlistRows.Count}) | {favoriteCount} favorite{(favoriteCount == 1 ? string.Empty : "s")}";
        }

        private static string GetMusicName(int music)
        {
            if (SoundsLoader.Instance.TryGetMusicData(music, out string name, out _))
            {
                string displayName = Path.GetFileNameWithoutExtension(name);
                return string.IsNullOrWhiteSpace(displayName) ? $"Track {music}" : displayName;
            }

            return $"Track {music}";
        }

        private static bool IsCustomMusic(int music)
        {
            IReadOnlyList<int> tracks = SoundsLoader.Instance.CustomMusicIndices;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] == music)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SaveProfile()
        {
            if (ProfileManager.CurrentProfile != null && !string.IsNullOrEmpty(ProfileManager.ProfilePath))
            {
                ProfileManager.CurrentProfile.Save(ProfileManager.ProfilePath, false);
            }
        }

        private sealed class MusicIconButton : HitBox
        {
            private readonly int _buttonID;
            private MusicIcon _icon;

            public MusicIconButton(int x, int y, int width, MusicIcon icon, string tooltip, Buttons button)
                : this(x, y, width, icon, tooltip, (int)button)
            {
            }

            public MusicIconButton(int x, int y, int width, MusicIcon icon, string tooltip, int buttonID)
                : base(x, y, width, 18, tooltip)
            {
                _icon = icon;
                _buttonID = buttonID;
                Hue = CustomGumpThemeManager.ButtonHue;
            }

            public bool IsSelected { get; set; }

            public void SetIcon(MusicIcon icon)
            {
                _icon = icon;
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left)
                {
                    OnButtonClick(_buttonID);
                }
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                Vector3 background = ShaderHueTranslator.GetHueVector(Hue, false, IsSelected ? 0.55f : 0.25f);
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(Color.White),
                    new Vector2(x, y),
                    new Rectangle(0, 0, Width, Height),
                    background);

                bool result = base.Draw(batcher, x, y);
                Vector3 iconHue = ShaderHueTranslator.GetHueVector(TEXT_HUE, false, 1f);
                int centerX = x + Width / 2;
                int centerY = y + Height / 2;

                switch (_icon)
                {
                    case MusicIcon.Previous:
                        Fill(batcher, centerX - 7, centerY - 5, 2, 10, iconHue);
                        DrawTriangle(batcher, centerX + 2, centerY, false, iconHue);
                        break;
                    case MusicIcon.Play:
                        DrawTriangle(batcher, centerX, centerY, true, iconHue);
                        break;
                    case MusicIcon.Pause:
                        Fill(batcher, centerX - 5, centerY - 5, 3, 10, iconHue);
                        Fill(batcher, centerX + 2, centerY - 5, 3, 10, iconHue);
                        break;
                    case MusicIcon.Stop:
                        Fill(batcher, centerX - 5, centerY - 5, 10, 10, iconHue);
                        break;
                    case MusicIcon.Next:
                        DrawTriangle(batcher, centerX - 2, centerY, true, iconHue);
                        Fill(batcher, centerX + 5, centerY - 5, 2, 10, iconHue);
                        break;
                    case MusicIcon.Shuffle:
                        DrawShuffle(batcher, centerX, centerY, iconHue);
                        break;
                    case MusicIcon.Speaker:
                    case MusicIcon.Muted:
                        DrawSpeaker(batcher, centerX, centerY, _icon == MusicIcon.Muted, iconHue);
                        break;
                    case MusicIcon.Loop:
                        DrawLoop(batcher, centerX, centerY, iconHue);
                        break;
                    case MusicIcon.List:
                        DrawList(batcher, centerX, centerY, iconHue);
                        break;
                    case MusicIcon.Radio:
                        DrawRadio(batcher, centerX, centerY, iconHue);
                        break;
                    case MusicIcon.Collapse:
                        Fill(batcher, centerX - 5, centerY, 10, 2, iconHue);
                        break;
                    case MusicIcon.Expand:
                        Fill(batcher, centerX - 5, centerY, 10, 2, iconHue);
                        Fill(batcher, centerX - 1, centerY - 4, 2, 10, iconHue);
                        break;
                    case MusicIcon.Heart:
                        DrawHeart(batcher, centerX, centerY, IsSelected, iconHue);
                        break;
                }

                return result;
            }

            private static void DrawTriangle(UltimaBatcher2D batcher, int centerX, int centerY, bool pointsRight, Vector3 hue)
            {
                for (int column = 0; column < 7; column++)
                {
                    int height = 13 - column * 2;
                    int x = pointsRight ? centerX - 3 + column : centerX + 3 - column;
                    Fill(batcher, x, centerY - height / 2, 1, height, hue);
                }
            }

            private static void DrawShuffle(UltimaBatcher2D batcher, int centerX, int centerY, Vector3 hue)
            {
                Texture2D texture = SolidColorTextureCache.GetTexture(Color.White);
                Vector2 leftTop = new Vector2(centerX - 9, centerY - 4);
                Vector2 leftBottom = new Vector2(centerX - 9, centerY + 4);
                Vector2 rightTop = new Vector2(centerX + 9, centerY - 4);
                Vector2 rightBottom = new Vector2(centerX + 9, centerY + 4);

                batcher.DrawLine(texture, leftTop, new Vector2(centerX - 3, centerY - 4), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX - 3, centerY - 4), rightBottom, hue, 1.5f);
                batcher.DrawLine(texture, leftBottom, new Vector2(centerX - 3, centerY + 4), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX - 3, centerY + 4), rightTop, hue, 1.5f);
                DrawArrowHead(batcher, texture, rightTop, hue);
                DrawArrowHead(batcher, texture, rightBottom, hue);
            }

            private static void DrawLoop(UltimaBatcher2D batcher, int centerX, int centerY, Vector3 hue)
            {
                Texture2D texture = SolidColorTextureCache.GetTexture(Color.White);
                Vector2 topLeft = new Vector2(centerX - 8, centerY - 4);
                Vector2 topRight = new Vector2(centerX + 8, centerY - 4);
                Vector2 bottomLeft = new Vector2(centerX - 8, centerY + 4);
                Vector2 bottomRight = new Vector2(centerX + 8, centerY + 4);

                batcher.DrawLine(texture, topLeft, topRight, hue, 1.5f);
                batcher.DrawLine(texture, topRight, bottomRight, hue, 1.5f);
                batcher.DrawLine(texture, bottomRight, bottomLeft, hue, 1.5f);
                batcher.DrawLine(texture, bottomLeft, topLeft, hue, 1.5f);
                DrawArrowHead(batcher, texture, topRight, hue);
                batcher.DrawLine(texture, new Vector2(bottomLeft.X + 3, bottomLeft.Y - 3), bottomLeft, hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(bottomLeft.X + 3, bottomLeft.Y + 3), bottomLeft, hue, 1.5f);
            }

            private static void DrawList(UltimaBatcher2D batcher, int centerX, int centerY, Vector3 hue)
            {
                Texture2D texture = SolidColorTextureCache.GetTexture(Color.White);

                for (int row = -1; row <= 1; row++)
                {
                    int y = centerY + row * 5;
                    Fill(batcher, centerX - 9, y - 1, 2, 2, hue);
                    batcher.DrawLine(texture, new Vector2(centerX - 4, y), new Vector2(centerX + 9, y), hue, 1.5f);
                }
            }

            private static void DrawRadio(UltimaBatcher2D batcher, int centerX, int centerY, Vector3 hue)
            {
                Texture2D texture = SolidColorTextureCache.GetTexture(Color.White);
                Fill(batcher, centerX - 1, centerY - 5, 2, 11, hue);
                Fill(batcher, centerX - 4, centerY + 5, 8, 2, hue);
                batcher.DrawLine(texture, new Vector2(centerX - 1, centerY - 5), new Vector2(centerX + 6, centerY - 9), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX - 5, centerY - 4), new Vector2(centerX - 8, centerY), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX - 8, centerY), new Vector2(centerX - 5, centerY + 4), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX + 5, centerY - 4), new Vector2(centerX + 8, centerY), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX + 8, centerY), new Vector2(centerX + 5, centerY + 4), hue, 1.5f);
            }

            private static void DrawArrowHead(UltimaBatcher2D batcher, Texture2D texture, Vector2 tip, Vector3 hue)
            {
                batcher.DrawLine(texture, new Vector2(tip.X - 3, tip.Y - 3), tip, hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(tip.X - 3, tip.Y + 3), tip, hue, 1.5f);
            }

            private static void DrawSpeaker(UltimaBatcher2D batcher, int centerX, int centerY, bool muted, Vector3 hue)
            {
                Texture2D texture = SolidColorTextureCache.GetTexture(Color.White);
                Fill(batcher, centerX - 8, centerY - 3, 3, 6, hue);
                batcher.DrawLine(texture, new Vector2(centerX - 5, centerY - 3), new Vector2(centerX, centerY - 6), hue, 1.5f);
                batcher.DrawLine(texture, new Vector2(centerX - 5, centerY + 3), new Vector2(centerX, centerY + 6), hue, 1.5f);
                Fill(batcher, centerX - 1, centerY - 6, 2, 12, hue);

                if (muted)
                {
                    batcher.DrawLine(texture, new Vector2(centerX + 4, centerY - 4), new Vector2(centerX + 10, centerY + 4), hue, 1.5f);
                    batcher.DrawLine(texture, new Vector2(centerX + 10, centerY - 4), new Vector2(centerX + 4, centerY + 4), hue, 1.5f);
                }
                else
                {
                    batcher.DrawLine(texture, new Vector2(centerX + 4, centerY - 3), new Vector2(centerX + 6, centerY), hue, 1.5f);
                    batcher.DrawLine(texture, new Vector2(centerX + 6, centerY), new Vector2(centerX + 4, centerY + 3), hue, 1.5f);
                    batcher.DrawLine(texture, new Vector2(centerX + 7, centerY - 5), new Vector2(centerX + 10, centerY), hue, 1.5f);
                    batcher.DrawLine(texture, new Vector2(centerX + 10, centerY), new Vector2(centerX + 7, centerY + 5), hue, 1.5f);
                }
            }

            private static void DrawHeart(UltimaBatcher2D batcher, int centerX, int centerY, bool filled, Vector3 hue)
            {
                if (filled)
                {
                    Fill(batcher, centerX - 6, centerY - 4, 5, 3, hue);
                    Fill(batcher, centerX + 1, centerY - 4, 5, 3, hue);
                    Fill(batcher, centerX - 7, centerY - 2, 14, 4, hue);
                    Fill(batcher, centerX - 5, centerY + 2, 10, 2, hue);
                    Fill(batcher, centerX - 3, centerY + 4, 6, 2, hue);
                    Fill(batcher, centerX - 1, centerY + 6, 2, 1, hue);
                    return;
                }

                Fill(batcher, centerX - 5, centerY - 5, 4, 1, hue);
                Fill(batcher, centerX + 1, centerY - 5, 4, 1, hue);
                Fill(batcher, centerX - 7, centerY - 3, 2, 5, hue);
                Fill(batcher, centerX + 5, centerY - 3, 2, 5, hue);
                Fill(batcher, centerX - 5, centerY + 2, 2, 2, hue);
                Fill(batcher, centerX + 3, centerY + 2, 2, 2, hue);
                Fill(batcher, centerX - 3, centerY + 4, 2, 2, hue);
                Fill(batcher, centerX + 1, centerY + 4, 2, 2, hue);
                Fill(batcher, centerX - 1, centerY + 6, 2, 1, hue);
            }

            private static void Fill(UltimaBatcher2D batcher, int x, int y, int width, int height, Vector3 hue)
            {
                batcher.Draw(SolidColorTextureCache.GetTexture(Color.White), new Rectangle(x, y, width, height), hue);
            }
        }
    }
}
