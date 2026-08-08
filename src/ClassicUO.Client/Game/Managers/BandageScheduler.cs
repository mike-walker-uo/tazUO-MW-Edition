#region license
// TazUO addition.
#endregion

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Shared bandage cooldown tracker for the local player.
    /// ServUO keys BandageContext on the HEALER, not the patient — starting
    /// a second bandage cancels the first. This scheduler is the single
    /// source of truth both AutoBandageManager and PetBandageManager
    /// consult before firing.
    /// </summary>
    public static class BandageScheduler
    {
        /// <summary>Which manager wins when multiple need a bandage at once.</summary>
        public enum Pref { Player, Pet, External }
        private static Pref _priority = Pref.Player;
        public static Pref Priority
        {
            get => _priority;
            set { if (_priority != value) { _priority = value; BandageSettings.MarkDirty(); } }
        }

        // Legacy convenience — true when Player has priority.
        public static bool PreferPlayer
        {
            get => Priority == Pref.Player;
            set => Priority = value ? Pref.Player : Pref.Pet;
        }

        private static long _nextEligibleAt;
        private static uint _currentPatient;

        public static bool IsBusy => Time.Ticks < _nextEligibleAt;
        public static uint CurrentPatient => _currentPatient;
        public static long NextEligibleAt => _nextEligibleAt;

        public static void MarkFired(uint patientSerial, long cycleMs)
        {
            _currentPatient = patientSerial;
            _nextEligibleAt = (long)Time.Ticks + cycleMs;
        }

        public static void Reset()
        {
            _nextEligibleAt = 0;
            _currentPatient = 0;
        }

        public static void ResetForProfile()
        {
            _priority = Pref.Player;
            Reset();
        }
    }
}
