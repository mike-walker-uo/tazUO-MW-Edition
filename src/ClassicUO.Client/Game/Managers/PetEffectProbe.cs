using System;
using System.Collections.Generic;
using System.Text;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.IO;

namespace ClassicUO.Game.Managers
{
    // Passive, window-scoped experiment. Animation lifetimes are never used as status timers.
    internal static class PetEffectProbe
    {
        internal sealed class Entry
        {
            internal uint SeenAt;
            internal string Text;
        }

        internal sealed class Buff
        {
            internal ushort Icon, Seconds;
            internal uint SeenAt;
            internal string Name => Enum.IsDefined(typeof(BuffIconType), (BuffIconType)Icon)
                ? ((BuffIconType)Icon).ToString() : "Unknown 0x" + Icon.ToString("X4");

            internal string Describe(uint now)
            {
                if (Seconds == 0) return Name + " - server reported; duration unknown";
                int remaining = Seconds - (int)(unchecked(now - SeenAt) / 1000);
                return Name + (remaining > 0 ? " - server timer: " + remaining + "s"
                    : " - server timer elapsed; awaiting removal/update");
            }
        }

        internal sealed class Session
        {
            internal const int HistoryLimit = 128;
            internal uint Serial;
            internal string Name = "No pet selected";
            internal string Feedback = "Choose Target pet, then click your pet. No Animal Lore is used.";
            internal bool Paused;
            internal int Revision;
            internal readonly SortedDictionary<ushort, Buff> Buffs = new SortedDictionary<ushort, Buff>();
            internal readonly List<Entry> Candidates = new List<Entry>();
            internal readonly List<Entry> History = new List<Entry>();

            internal void Clear()
            {
                Buffs.Clear(); Candidates.Clear(); History.Clear(); Revision++;
                Feedback = "Cleared. Existing effects cannot be recovered without new packets.";
            }

            internal void Pause(uint now)
            {
                Paused = !Paused;
                // A missed removal would otherwise leave a false confirmed status on screen.
                Buffs.Clear(); Candidates.Clear();
                Feedback = Paused ? "Paused; current status is unknown." : "Resumed; waiting for new packets.";
                Add(now, "Capture " + (Paused ? "paused" : "resumed") + "; status cleared due to observation gap.");
            }

            internal void Add(uint now, string text)
            {
                if (History.Count == HistoryLimit) History.RemoveAt(0);
                History.Add(new Entry { SeenAt = now, Text = text }); Revision++;
            }

            internal void Candidate(uint now, string text)
            {
                Entry entry = Candidates.Find(e => e.Text == text);
                if (entry == null)
                {
                    if (Candidates.Count == 16) Candidates.RemoveAt(0);
                    Candidates.Add(entry = new Entry { Text = text });
                }
                entry.SeenAt = now;
            }

            internal void ReadBuff(ref StackDataReader original, uint now)
            {
                if (Paused || Serial == 0) return;
                StackDataReader p = original; // Never consume or rewrite the real packet.
                if (p.Remaining < 8 || p.ReadUInt32BE() != Serial) return;
                ushort icon = p.ReadUInt16BE(), action = p.ReadUInt16BE();
                if (action == 0)
                {
                    Buffs.Remove(icon);
                    Add(now, "0xDF pet serial: removed buff 0x" + icon.ToString("X4"));
                    return;
                }
                if ((action != 1 && action != 2) || p.Remaining < 25)
                {
                    Add(now, "0xDF pet serial: unsupported/truncated buff, icon=0x" + icon.ToString("X4") + ", action=" + action);
                    return;
                }
                p.Skip(12);
                ushort seconds = p.ReadUInt16BE();
                p.Skip(3);
                uint title = p.ReadUInt32BE(), description = p.ReadUInt32BE();
                if (!Buffs.ContainsKey(icon) && Buffs.Count == 64) Buffs.Remove(FirstBuff());
                var buff = new Buff { Icon = icon, Seconds = seconds, SeenAt = now };
                Buffs[icon] = buff;
                Add(now, "0xDF pet serial: " + buff.Name + ", action=" + action + ", server duration=" + seconds
                    + "s, cliloc=" + title + "/" + description);
            }

            private ushort FirstBuff()
            {
                foreach (ushort icon in Buffs.Keys) return icon;
                return 0;
            }

            internal void ReadEffect(ref StackDataReader original, uint now, int petX, int petY, int petZ)
            {
                if (Paused || Serial == 0) return;
                StackDataReader p = original;
                if (p.Length == 0 || p.Remaining < 27) return;
                byte packet = p[0];
                int required = packet == 0x70 ? 27 : packet == 0xC0 ? 35 : packet == 0xC7 ? 48 : 0;
                if (required == 0 || p.Remaining < required) return;
                var type = (GraphicEffectType)p.ReadUInt8();
                if (type > GraphicEffectType.FixedFrom) return;
                uint source = p.ReadUInt32BE(), target = p.ReadUInt32BE();
                ushort graphic = p.ReadUInt16BE();
                int x = p.ReadUInt16BE(), y = p.ReadUInt16BE(), z = p.ReadInt8();
                int tx = p.ReadUInt16BE(), ty = p.ReadUInt16BE(), tz = p.ReadInt8();
                byte speed = p.ReadUInt8(), duration = p.ReadUInt8();
                p.Skip(4); // unused word, fixed direction, explosion flag
                uint hue = 0, effectSerial = 0;
                ushort particle = 0;
                byte layer = 0;
                if (packet != 0x70) { hue = p.ReadUInt32BE(); p.Skip(4); }
                if (packet == 0xC7)
                {
                    particle = p.ReadUInt16BE(); p.Skip(4);
                    effectSerial = p.ReadUInt32BE(); layer = p.ReadUInt8();
                }
                bool attached = type == GraphicEffectType.FixedFrom && source == Serial;
                bool involved = source == Serial || target == Serial || effectSerial == Serial;
                bool nearby = Near(x, y, z, petX, petY, petZ)
                    || Near(tx, ty, tz, petX, petY, petZ);
                if (!involved && !nearby) return;
                string candidate = attached ? MatchEffect(graphic, particle, hue) : null;
                if (candidate != null) Candidate(now, candidate);
                string attribution = attached ? "attached to pet" : involved ? "pet serial involved; application unknown" : "position only; ambiguous";
                Add(now, "0x" + packet.ToString("X2") + " " + attribution + ": " + type
                    + ", gfx=0x" + graphic.ToString("X4") + ", hue=" + hue + ", particle=" + particle + ", layer=" + layer
                    + ", src=0x" + source.ToString("X8") + ", dst=0x" + target.ToString("X8") + ", effect=0x" + effectSerial.ToString("X8")
                    + ", xyz=" + x + "/" + y + "/" + z + " -> " + tx + "/" + ty + "/" + tz
                    + ", anim speed/duration=" + speed + "/" + duration
                    + (candidate == null ? "; no status match" : "; candidate: " + candidate));
            }

            internal void Sound(ushort sound, int x, int y, int z, uint now, int petX, int petY, int petZ)
            {
                if (Paused || Serial == 0 || !Near(x, y, z, petX, petY, petZ)) return;
                Add(now, "0x54 position only; ambiguous: sound=0x" + sound.ToString("X4")
                    + ", xyz=" + x + "/" + y + "/" + z + "; " + MatchSound(sound));
            }

            internal string Report(uint now)
            {
                var text = new StringBuilder("Pet effects PoC - reference signatures, unverified on UOAlive\n");
                text.AppendLine(Name + " [0x" + Serial.ToString("X8") + "] " + (Paused ? "PAUSED" : "capturing"));
                text.AppendLine("Only 0xDF addressed to the pet confirms status. Visual/sound cues do not confirm current status or duration.");
                foreach (Buff buff in Buffs.Values) text.AppendLine("SERVER: " + buff.Describe(now));
                foreach (Entry entry in Candidates) text.AppendLine("CANDIDATE " + Age(entry, now) + ": " + entry.Text);
                foreach (Entry entry in History) text.AppendLine(Age(entry, now) + ": " + entry.Text);
                return text.ToString();
            }
        }

        private static Profile _profile;
        private static uint _player;
        internal static Session Current { get; private set; }

        internal static Session Start()
        {
            End();
            _profile = ProfileManager.CurrentProfile; _player = World.Player?.Serial ?? 0;
            return Current = new Session();
        }

        internal static void End()
        {
            Current = null; _profile = null; _player = 0;
            if (TargetManager.TargetingState == CursorTarget.PetEffectProbe) TargetManager.CancelTarget();
        }

        internal static void Target(Entity entity)
        {
            Session session = Current;
            if (session == null) return;
            var pet = entity as Mobile;
            if (pet == null || pet == World.Player || !pet.IsRenamable
                || pet.NotorietyFlag == NotorietyFlag.Enemy || pet.NotorietyFlag == NotorietyFlag.Invulnerable)
            {
                session.Feedback = "Target your visible, renamable pet. Ownership cannot be verified by this client.";
                session.Revision++;
                return;
            }
            session.Clear(); session.Serial = pet.Serial; session.Name = pet.Name ?? "Pet";
            session.Feedback = "Capturing this pet only. No packet history from before selection is available.";
        }

        private static Session Active()
        {
            Session session = Current;
            return session != null && !session.Paused && session.Serial != 0 && World.InGame
                && World.Player.Serial == _player && ReferenceEquals(_profile, ProfileManager.CurrentProfile) ? session : null;
        }

        internal static void CaptureBuff(ref StackDataReader p)
        {
            Active()?.ReadBuff(ref p, Time.Ticks);
        }

        internal static void CaptureEffect(ref StackDataReader p)
        {
            Session session = Active();
            Mobile pet = session == null ? null : World.Mobiles.Get(session.Serial);
            if (pet != null) session.ReadEffect(ref p, Time.Ticks, pet.X, pet.Y, pet.Z);
        }

        internal static void CaptureSound(ushort sound, int x, int y, int z)
        {
            Session session = Active();
            Mobile pet = session == null ? null : World.Mobiles.Get(session.Serial);
            if (pet != null) session.Sound(sound, x, y, z, Time.Ticks, pet.X, pet.Y, pet.Z);
        }

        internal static string Age(Entry entry, uint now) => (unchecked(now - entry.SeenAt) / 1000) + "s ago";

        private static bool Near(int x, int y, int z, int petX, int petY, int petZ)
            => Math.Abs(x - petX) <= 1 && Math.Abs(y - petY) <= 1 && Math.Abs(z - petZ) <= 16;

        // Exact attached particle signatures from ServUO spell sources, not verified shard mappings.
        // Shared signatures deliberately remain ambiguous. Legacy graphics without particles stay raw.
        internal static string MatchEffect(ushort graphic, ushort particle, uint hue)
        {
            if (graphic == 0x3779 && particle == 5002 && hue == 0) return "Debuff: Clumsy / Feeblemind / Weaken (shared cue)";
            if (graphic == 0x374A && particle == 5028 && hue == 0) return "Debuff: Curse / Mass Curse (shared cue)";
            if (graphic == 0x373A && particle == 9913 && hue == 67) return "Debuff: Corpse Skin";
            if (graphic == 0x3728 && particle == 9912 && hue == 1150
                || graphic == 0x3779 && particle == 9502 && hue == 67) return "Debuff: Evil Omen";
            if (graphic == 0x373A && particle == 9903 && hue == 15) return "Debuff: Mind Rot";
            if (graphic == 0x36CB && particle == 9911 && hue == 67) return "Debuff: Strangle";
            if (graphic == 0x373A && particle == 5018 && hue == 0) return "Buff: Bless";
            if (graphic == 0x375A && particle == 5010 && hue == 0) return "Buff: Agility";
            if (graphic == 0x375A && particle == 5011 && hue == 0) return "Buff: Cunning";
            if (graphic == 0x375A && particle == 5017 && hue == 0) return "Buff: Strength";
            return null;
        }

        private static string MatchSound(ushort sound)
        {
            switch (sound)
            {
                case 0x1DF: return "possible Clumsy / Feeblemind / Weaken cue";
                case 0x1E1: return "possible Curse / Mass Curse cue";
                case 0x1BB: return "possible Corpse Skin cue";
                case 0xFC: return "possible Evil Omen cue";
                case 0x1FB: case 0x258: return "possible Mind Rot cue";
                case 0x22F: return "possible Strangle cue";
                case 0x1EA: return "possible Bless cue";
                case 0x1E7: return "possible Agility cue";
                case 0x1EB: return "possible Cunning cue";
                case 0x1EE: return "possible Strength cue";
                default: return "unmapped sound";
            }
        }
    }
}
