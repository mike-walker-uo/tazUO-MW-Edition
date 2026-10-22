using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI;
using ClassicUO.IO;

namespace ClassicUO.Game.Managers
{
    internal static class PetBuffMonitor
    {
        internal const uint CorrelationMs = 350;
        private const uint ResolveMs = CorrelationMs * 2;
        private const uint RetainCuesMs = CorrelationMs * 4 + 400;
        internal const uint RecentIconMs = 10000; // Observation window, NOT an effect duration.
        internal const uint ObservationHistoryMs = 30000;
        internal enum Evidence { Observed, Server, Flag }

        internal sealed class Status
        {
            internal BuffIconType Kind;
            internal Evidence Source;
            internal uint SeenAt;
            internal ushort Seconds;
            internal string Name
            {
                get
                {
                    if (Source == Evidence.Observed && Kind == BuffIconType.Clumsy) return "Clumsy / Feeblemind / Weaken";
                    if (Source == Evidence.Observed && Kind == BuffIconType.Curse) return "Curse / Mass Curse";
                    switch (Kind)
                    {
                        case BuffIconType.CorpseSkin: return "Corpse Skin";
                        case BuffIconType.EvilOmen: return "Evil Omen";
                        case BuffIconType.Mindrot: return "Mind Rot";
                        case BuffIconType.MassCurse: return "Mass Curse";
                        case BuffIconType.FeebleMind: return "Feeblemind";
                        default: return Kind.ToString();
                    }
                }
            }
            internal bool ShowIcon(uint now) => Source != Evidence.Observed || unchecked(now - SeenAt) < RecentIconMs;
            internal bool Expired(uint now) => Source == Evidence.Observed
                ? unchecked(now - SeenAt) >= ObservationHistoryMs
                : Source == Evidence.Server && Seconds != 0 && unchecked(now - SeenAt) >= Seconds * 1000u;
            internal string Detail(uint now)
            {
                if (Source == Evidence.Flag) return "Confirmed by current mobile status flags";
                if (Source == Evidence.Server) return Seconds == 0 ? "Server-reported; duration unknown"
                    : "Server-reported; " + Math.Max(0, Seconds - (int)(unchecked(now - SeenAt) / 1000)) + "s remaining";
                return "Inferred; seen " + unchecked(now - SeenAt) / 1000 + "s ago; duration unknown";
            }
        }

        internal sealed class Pet
        {
            internal uint Serial;
            internal string Name;
            internal int X, Y, Z;
            internal bool Seen;
            internal readonly SortedDictionary<BuffIconType, Status> Statuses = new SortedDictionary<BuffIconType, Status>();
        }

        internal struct Visual
        {
            internal uint Serial, SeenAt, Hue;
            internal byte Packet, Speed, Duration;
            internal GraphicEffectType Type;
            internal ushort Graphic, Particle;
            internal int X, Y, Z;
            internal bool Processed;
        }

        private struct Sound
        {
            internal uint SeenAt;
            internal ushort Id;
            internal int X, Y, Z;
        }

        private readonly struct Signature
        {
            internal readonly BuffIconType Kind;
            internal readonly ushort Graphic, Particle, Sound;
            internal readonly uint Hue;
            internal readonly byte Speed, Duration;
            internal readonly bool Shared;
            internal Signature(BuffIconType kind, ushort graphic, uint hue, ushort particle, byte speed, byte duration,
                ushort sound = 0, bool shared = false)
            { Kind = kind; Graphic = graphic; Hue = hue; Particle = particle; Speed = speed; Duration = duration; Sound = sound; Shared = shared; }
            internal bool Matches(Visual v) => v.Graphic == Graphic && v.Hue == Hue && v.Speed == Speed && v.Duration == Duration
                && (v.Particle == 0 || v.Particle == Particle);
        }

        // Status application cues from ServUO. Deliberately excludes damage spells, poison attempts,
        // and Evasion's successful-parry animation (none establish a lasting status).
        private static readonly Signature[] Signatures =
        {
            new Signature(BuffIconType.Bleed, 0x377A, 31, 9950, 244, 25),
            new Signature(BuffIconType.CorpseSkin, 0x373A, 67, 9913, 1, 15),
            new Signature(BuffIconType.EvilOmen, 0x3728, 1150, 9912, 1, 13),
            new Signature(BuffIconType.EvilOmen, 0x3779, 67, 9502, 1, 15),
            new Signature(BuffIconType.Mindrot, 0x373A, 15, 9903, 1, 17),
            new Signature(BuffIconType.Strangle, 0x36CB, 67, 9911, 1, 9),
            new Signature(BuffIconType.Strangle, 0x374A, 1108, 9502, 1, 17),
            new Signature(BuffIconType.Curse, 0x374A, 0, 5028, 10, 15, 0x1E1, true),
            new Signature(BuffIconType.Clumsy, 0x3779, 0, 5002, 10, 15, 0x1DF, true),
            new Signature(BuffIconType.Bless, 0x373A, 0, 5018, 10, 15, 0x1EA, true),
            new Signature(BuffIconType.Agility, 0x375A, 0, 5010, 10, 15, 0x1E7, true),
            new Signature(BuffIconType.Cunning, 0x375A, 0, 5011, 10, 15, 0x1EB, true),
            new Signature(BuffIconType.Strength, 0x375A, 0, 5017, 10, 15, 0x1EE, true)
        };

        internal sealed class Tracker
        {
            internal readonly SortedDictionary<uint, Pet> Pets = new SortedDictionary<uint, Pet>();
            internal int Revision;
            private readonly List<Visual> _visuals = new List<Visual>(64);
            private readonly List<Sound> _sounds = new List<Sound>(32);
            private uint _overflowAt;
            private bool _overflow;
            private readonly List<uint> _removePets = new List<uint>();
            private readonly List<BuffIconType> _removeStatuses = new List<BuffIconType>();

            internal void Clear()
            { Pets.Clear(); _visuals.Clear(); _sounds.Clear(); _overflow = false; Revision++; }

            internal void BeginPets() { foreach (Pet pet in Pets.Values) pet.Seen = false; }
            internal void UpdatePet(uint serial, string name, int x, int y, int z, bool poisoned, bool paralyzed, uint now)
            {
                if (!Pets.TryGetValue(serial, out Pet pet))
                { Pets[serial] = pet = new Pet { Serial = serial }; Revision++; }
                if (pet.Name != name) { pet.Name = name; Revision++; }
                pet.X = x; pet.Y = y; pet.Z = z; pet.Seen = true;
                Flag(pet, BuffIconType.Poison, poisoned, now);
                Flag(pet, BuffIconType.Paralyze, paralyzed, now);
            }
            internal void EndPets()
            {
                _removePets.Clear();
                foreach (Pet pet in Pets.Values) if (!pet.Seen) _removePets.Add(pet.Serial);
                foreach (uint serial in _removePets) { Pets.Remove(serial); Revision++; }
                // No observations from a previous visibility/ownership interval may attach on return.
                if (_removePets.Count > 0) { _visuals.Clear(); _sounds.Clear(); }
            }

            private void Flag(Pet pet, BuffIconType kind, bool active, uint now)
            {
                pet.Statuses.TryGetValue(kind, out Status current);
                if (!active)
                {
                    if (current?.Source == Evidence.Flag) { pet.Statuses.Remove(kind); Revision++; }
                    return;
                }
                if (current?.Source != Evidence.Flag) Put(pet, kind, Evidence.Flag, now);
            }

            private void Put(Pet pet, BuffIconType kind, Evidence source, uint now, ushort seconds = 0)
            {
                if (source == Evidence.Observed && pet.Statuses.TryGetValue(kind, out Status current)
                    && current.Source != Evidence.Observed) return;
                if (source == Evidence.Observed && pet.Statuses.TryGetValue(kind, out Status previous)
                    && unchecked((int)(now - previous.SeenAt)) <= 0) return;
                if (pet.Statuses.Count >= 64 && !pet.Statuses.ContainsKey(kind)) return;
                pet.Statuses[kind] = new Status { Kind = kind, Source = source, SeenAt = now, Seconds = seconds };
                Revision++;
            }

            internal void Buff(uint serial, ushort icon, ushort action, ushort seconds, uint now)
            {
                if (!Pets.TryGetValue(serial, out Pet pet) || !Enum.IsDefined(typeof(BuffIconType), (BuffIconType)icon)) return;
                var kind = (BuffIconType)icon;
                if (action == 0)
                {
                    if (pet.Statuses.Remove(kind)) Revision++;
                    // An older delayed correlation must not resurrect a server-removed status.
                    // Retain these cues as competitors for other stacked pets until they age out.
                    for (int i = 0; i < _visuals.Count; i++)
                        if (_visuals[i].Serial == serial)
                        { Visual visual = _visuals[i]; visual.Processed = true; _visuals[i] = visual; }
                }
                else if (action == 1 || action == 2) Put(pet, kind, Evidence.Server, now, seconds);
            }

            internal void ReadBuff(ref StackDataReader original, uint now)
            {
                StackDataReader p = original;
                if (p.Remaining < 8) return;
                uint serial = p.ReadUInt32BE(); ushort icon = p.ReadUInt16BE(), action = p.ReadUInt16BE(), seconds = 0;
                if (action != 0)
                {
                    if ((action != 1 && action != 2) || p.Remaining < 25) return;
                    p.Skip(12); seconds = p.ReadUInt16BE();
                }
                Buff(serial, icon, action, seconds, now);
            }

            internal void Effect(Visual v)
            {
                if (v.Type != GraphicEffectType.FixedFrom || v.Serial == 0 || v.Packet == 0x70) return;
                bool tracked = Pets.TryGetValue(v.Serial, out Pet pet);
                foreach (Signature signature in Signatures)
                {
                    if (!signature.Matches(v)) continue;
                    // Curse's particle ID is also used by bard debuffs: sound is required even on C7.
                    if (!signature.Shared || v.Particle != 0 && signature.Kind != BuffIconType.Curse)
                    {
                        if (tracked) Put(pet, signature.Kind, Evidence.Observed, v.SeenAt);
                        if (signature.Shared) Queue(v, tracked); // A known C7 source still competes with nearby C0 cues.
                        return;
                    }
                    Queue(v, tracked); return;
                }
            }

            private void Queue(Visual v, bool tracked)
            {
                if (!tracked && !AtPetPosition(v.X, v.Y, v.Z) && !AtPendingPosition(v.X, v.Y, v.Z)) return;
                if (_overflow && unchecked(v.SeenAt - _overflowAt) < RetainCuesMs) return;
                if (_visuals.Count == 64) { Overflow(v.SeenAt); return; }
                _visuals.Add(v);
            }

            internal void Hear(ushort id, int x, int y, int z, uint now)
            {
                bool relevant = false;
                foreach (Signature signature in Signatures) if (signature.Sound == id && id != 0) { relevant = true; break; }
                if (!relevant) return; // Sound alone never creates a status.
                if (!AtPetPosition(x, y, z) && !AtPendingPosition(x, y, z)) return;
                if (_overflow && unchecked(now - _overflowAt) < RetainCuesMs) return;
                if (_sounds.Count == 32) { Overflow(now); return; }
                _sounds.Add(new Sound { Id = id, X = x, Y = y, Z = z, SeenAt = now });
            }

            private void Overflow(uint now)
            {
                // Dropping only one cue could erase a competitor and falsely attribute its sound.
                _visuals.Clear(); _sounds.Clear(); _overflowAt = now; _overflow = true;
            }

            private bool AtPetPosition(int x, int y, int z)
            {
                foreach (Pet pet in Pets.Values) if (pet.X == x && pet.Y == y && pet.Z == z) return true;
                return false;
            }
            private bool AtPendingPosition(int x, int y, int z)
            {
                foreach (Visual visual in _visuals) if (visual.X == x && visual.Y == y && visual.Z == z) return true;
                return false;
            }
            private static bool CloseInTime(uint a, uint b) => Math.Min(unchecked(a - b), unchecked(b - a)) <= CorrelationMs;
            private static BuffIconType? Pair(Visual v, Sound s)
            {
                if (v.X != s.X || v.Y != s.Y || v.Z != s.Z || !CloseInTime(v.SeenAt, s.SeenAt)) return null;
                foreach (Signature signature in Signatures)
                    if (signature.Shared && signature.Sound == s.Id && signature.Matches(v)) return signature.Kind;
                return null;
            }
            private uint UniqueSource(Sound sound)
            {
                uint serial = 0;
                foreach (Visual v in _visuals)
                {
                    if (!Pair(v, sound).HasValue) continue;
                    if (serial != 0 && serial != v.Serial) return 0;
                    serial = v.Serial;
                }
                return serial;
            }

            internal void Advance(uint now)
            {
                if (_overflow && unchecked(now - _overflowAt) >= RetainCuesMs) _overflow = false;
                // A sound may follow this visual by 350ms, with a competing visual 350ms later still.
                // Wait for both sides of that window so stacked pets/NPCs cannot win by packet order.
                foreach (Sound sound in _sounds)
                {
                    uint serial = UniqueSource(sound); // Resolve once per sound, not once per candidate pair.
                    if (serial == 0 || !Pets.TryGetValue(serial, out Pet pet)) continue;
                    foreach (Visual visual in _visuals)
                    {
                        if (visual.Serial != serial || visual.Processed || unchecked(now - visual.SeenAt) < ResolveMs) continue;
                        BuffIconType? kind = Pair(visual, sound);
                        if (kind.HasValue) Put(pet, kind.Value, Evidence.Observed, visual.SeenAt);
                    }
                }
                for (int i = 0; i < _visuals.Count; i++)
                    if (unchecked(now - _visuals[i].SeenAt) >= ResolveMs)
                    { Visual visual = _visuals[i]; visual.Processed = true; _visuals[i] = visual; }
                // Retain processed competitors until later overlapping cues have also been resolved.
                for (int i = _visuals.Count - 1; i >= 0; i--)
                    if (unchecked(now - _visuals[i].SeenAt) > RetainCuesMs) _visuals.RemoveAt(i);
                for (int i = _sounds.Count - 1; i >= 0; i--)
                    if (unchecked(now - _sounds[i].SeenAt) > RetainCuesMs) _sounds.RemoveAt(i);
                foreach (Pet pet in Pets.Values)
                {
                    _removeStatuses.Clear();
                    foreach (Status status in pet.Statuses.Values) if (status.Expired(now)) _removeStatuses.Add(status.Kind);
                    foreach (BuffIconType kind in _removeStatuses) { pet.Statuses.Remove(kind); Revision++; }
                }
            }
        }

        internal static readonly Tracker Current = new Tracker();
        private static Profile _profile;
        private static uint _player;
        private static int _map = -1;
        internal static bool Enabled => ProfileManager.CurrentProfile?.PetBuffMonitorEnabled == true && World.InGame;

        internal static void SetEnabled(bool enabled)
        {
            if (ProfileManager.CurrentProfile == null) return;
            ProfileManager.CurrentProfile.PetBuffMonitorEnabled = enabled;
            Reset();
            if (enabled) { MobileCache.Rebuild(); Tick(); }
        }
        internal static void Reset()
        { Current.Clear(); _profile = null; _player = 0; _map = -1; }
        private static bool Ready()
        {
            if (!Enabled) return false;
            if (!ReferenceEquals(_profile, ProfileManager.CurrentProfile) || _player != World.Player.Serial || _map != World.Map.Index)
            {
                Current.Clear(); _profile = ProfileManager.CurrentProfile; _player = World.Player.Serial; _map = World.Map.Index;
            }
            return true;
        }
        internal static void Tick()
        {
            if (!Ready()) return;
            uint now = Time.Ticks;
            Current.BeginPets();
            foreach (var pet in MobileCache.Pets)
            {
                if (pet == null || pet.IsDestroyed || pet.IsDead || pet.Distance > World.ClientViewRange
                    || !MobileCache.ShouldCachePet(pet == World.Player, pet.IsDead, pet.IsRenamable, pet.NotorietyFlag)) continue;
                Current.UpdatePet(pet.Serial, pet.Name ?? "Pet", pet.X, pet.Y, pet.Z, pet.IsPoisoned, pet.IsParalyzed, now);
            }
            Current.EndPets(); Current.Advance(now);
        }

        internal static void CaptureBuff(ref StackDataReader original)
        {
            if (Ready()) Current.ReadBuff(ref original, Time.Ticks);
        }
        internal static void CaptureEffect(ref StackDataReader original)
        {
            if (Ready() && Current.Pets.Count != 0 && TryReadVisual(ref original, Time.Ticks, out Visual visual)) Current.Effect(visual);
        }
        internal static bool TryReadVisual(ref StackDataReader original, uint now, out Visual v)
        {
            v = default;
            if (original.Remaining < 35) return false;
            StackDataReader p = original;
            byte packet = p[0];
            if (packet != 0xC0 && packet != 0xC7 || packet == 0xC7 && p.Remaining < 48) return false;
            v = new Visual { Packet = packet, SeenAt = now, Type = (GraphicEffectType)p.ReadUInt8() };
            v.Serial = p.ReadUInt32BE(); p.Skip(4); v.Graphic = p.ReadUInt16BE();
            v.X = p.ReadUInt16BE(); v.Y = p.ReadUInt16BE(); v.Z = p.ReadInt8(); p.Skip(5);
            v.Speed = p.ReadUInt8(); v.Duration = p.ReadUInt8(); p.Skip(4);
            v.Hue = p.ReadUInt32BE(); p.Skip(4);
            if (packet == 0xC7) v.Particle = p.ReadUInt16BE();
            return true;
        }
        internal static void CaptureSound(ushort id, int x, int y, int z)
        { if (Ready() && Current.Pets.Count != 0) Current.Hear(id, x, y, z, Time.Ticks); }

        internal static ushort IconGraphic(BuffIconType kind)
        {
            int value = (ushort)kind;
            int index = value >= 0x466 ? value - (0x466 - 125) : value - 0x3E9;
            ushort[] table = BuffTable.Table;
            return table != null && index >= 0 && index < table.Length ? table[index] : (ushort)0;
        }
    }
}
