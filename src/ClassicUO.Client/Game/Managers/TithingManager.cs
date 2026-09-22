// TazUO MW Edition addition.
using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    internal static class TithingManager
    {
        private const long MIN_REQUEST_INTERVAL_MS = 5000;
        private const long STALE_AFTER_MS = 30000;
        private const long POST_CAST_REFRESH_DELAY_MS = 1000;
        private const uint LOW_TITHING_THRESHOLD = 1000;

        private static long _lastRequestTime = -MIN_REQUEST_INTERVAL_MS;
        private static long _postCastRefreshTime;
        private static bool _skillsReceived;
        private static bool _lowWarningShown;

        internal static string DisplayValue =>
            World.Player?.TithingPointsReceived == true
                ? World.Player.TithingPoints.ToString()
                : "—";

        internal static void Reset()
        {
            _lastRequestTime = -MIN_REQUEST_INTERVAL_MS;
            _postCastRefreshTime = 0;
            _skillsReceived = false;
            _lowWarningShown = false;
        }

        internal static void RequestRefreshIfStale()
        {
            PlayerMobile player = World.Player;
            long now = Time.Ticks;

            if (player == null
                || player.TithingPointsReceived
                    && now - player.TithingPointsLastUpdate < STALE_AFTER_MS)
                return;

            RequestRefresh(false);
        }

        internal static void RequestRefresh(bool force)
        {
            PlayerMobile player = World.Player;
            AsyncNetClient socket = NetClient.Socket;
            long now = Time.Ticks;

            if (!World.InGame || player == null || socket == null || !socket.IsConnected)
                return;

            if (!force
                && player.TithingPointsReceived
                && now - player.TithingPointsLastUpdate < STALE_AFTER_MS)
                return;

            if (now - _lastRequestTime < MIN_REQUEST_INTERVAL_MS)
                return;

            _lastRequestTime = now;
            if (player.HitsRequest < HitsRequestStatus.Pending)
                player.HitsRequest = HitsRequestStatus.Pending;
            socket.Send_StatusRequest(player.Serial);
        }

        internal static void NotifySpellCast(int spellIndex)
        {
            if (spellIndex >= 201 && spellIndex <= 210)
                _postCastRefreshTime = (long)Time.Ticks + POST_CAST_REFRESH_DELAY_MS;
        }

        internal static void Update()
        {
            if (_postCastRefreshTime == 0 || Time.Ticks < _postCastRefreshTime)
                return;

            if ((long)Time.Ticks - _lastRequestTime < MIN_REQUEST_INTERVAL_MS)
                return;

            _postCastRefreshTime = 0;
            RequestRefresh(true);
        }

        internal static void OnStatusReceived(uint points)
        {
            PlayerMobile player = World.Player;
            if (player == null)
                return;

            player.TithingPoints = points;
            player.TithingPointsReceived = true;
            player.TithingPointsLastUpdate = Time.Ticks;
            SpellbookGump.RefreshTithingDisplays();
            EvaluateLowTithingWarning();
        }

        internal static void OnSkillsUpdated()
        {
            _skillsReceived = true;
            EvaluateLowTithingWarning();
        }

        private static void EvaluateLowTithingWarning()
        {
            PlayerMobile player = World.Player;
            bool shouldWarn = player?.TithingPointsReceived == true
                && _skillsReceived
                && player.TithingPoints < LOW_TITHING_THRESHOLD
                && HasRealChivalry(player);

            if (!shouldWarn)
            {
                _lowWarningShown = false;
                return;
            }

            if (_lowWarningShown)
                return;

            _lowWarningShown = true;
            string warning = $"Warning: Only {player.TithingPoints} tithing points remaining.";
            GameActions.Print(
                warning,
                0x0021,
                MessageType.System
            );
            ToastManager.Show(warning, 0x0021, 4000, "tithing-warning",
                AlertCategory.Supplies, AlertSeverity.Warning);
        }

        private static bool HasRealChivalry(PlayerMobile player)
        {
            foreach (Skill skill in player.Skills)
            {
                if (skill != null
                    && string.Equals(skill.Name, "Chivalry", StringComparison.OrdinalIgnoreCase))
                    return skill.BaseFixed > 0;
            }

            return false;
        }
    }
}
