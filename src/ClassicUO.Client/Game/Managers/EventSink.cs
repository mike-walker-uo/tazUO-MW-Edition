using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;
using System;

namespace ClassicUO.Game.Managers
{
    public class EventSink
    {
        private static void InvokeSafely<T>(EventHandler<T> handlers, object sender, T args, string eventName)
        {
            if (handlers == null) return;

            foreach (EventHandler<T> handler in handlers.GetInvocationList())
            {
                string owner = handler.Method.DeclaringType?.FullName ?? "Unknown";
                string feature = $"Event:{eventName}:{owner}";
                if (FeatureDiagnostics.IsDisabled(feature)) continue;

                try { handler(sender, args); }
                catch (Exception ex) { FeatureDiagnostics.RecordFailure(feature, ex); }
            }
        }

        /// <summary>
        /// Invoked when the player is created
        /// </summary>
        public static event EventHandler<EventArgs> OnPlayerCreated;
        public static void InvokeOnPlayerCreated() => InvokeSafely(OnPlayerCreated, null, EventArgs.Empty, nameof(OnPlayerCreated));
        
        /// <summary>
        /// Invoked when an item is added to the client, sender is the Item
        /// </summary>
        public static event EventHandler<EventArgs> OnItemCreated;
        public static void InvokeOnItemCreated(Item sender) => InvokeSafely(OnItemCreated, sender, EventArgs.Empty, nameof(OnItemCreated));

        /// <summary>
        /// Invoked when an item is already in the client but has been updated, sender is the Item
        /// </summary>
        public static event EventHandler<EventArgs> OnItemUpdated;
        public static void InvokeOnItemUpdated(Item sender) => InvokeSafely(OnItemUpdated, sender, EventArgs.Empty, nameof(OnItemUpdated));

        /// <summary>
        /// Invoked when a corpse is added to the client, sender is the corpse Item
        /// </summary>
        public static event EventHandler<EventArgs> OnCorpseCreated;
        public static void InvokeOnCorpseCreated(object sender) => InvokeSafely(OnCorpseCreated, sender, EventArgs.Empty, nameof(OnCorpseCreated));

        /// <summary>
        /// Invoked when the player is connected to a server
        /// </summary>
        public static event EventHandler<EventArgs> OnConnected;
        public static void InvokeOnConnected(object sender) => InvokeSafely(OnConnected, sender, EventArgs.Empty, nameof(OnConnected));

        /// <summary>
        /// Invoked when the player is connected to a server
        /// </summary>
        public static event EventHandler<EventArgs> OnDisconnected;
        public static void InvokeOnDisconnected(object sender) => InvokeSafely(OnDisconnected, sender, EventArgs.Empty, nameof(OnDisconnected));

        /// <summary>
        /// Invoked when any message is received from the server after client processing
        /// </summary>
        public static event EventHandler<MessageEventArgs> MessageReceived;
        public static void InvokeMessageReceived(object sender, MessageEventArgs e) => InvokeSafely(MessageReceived, sender, e, nameof(MessageReceived));

        /// <summary>
        /// Invoked when any message is received from the server *before* client processing
        /// </summary>
        public static event EventHandler<MessageEventArgs> RawMessageReceived;
        public static void InvokeRawMessageReceived(object sender, MessageEventArgs e) => InvokeSafely(RawMessageReceived, sender, e, nameof(RawMessageReceived));

        /// <summary>
        /// Not currently used. May be removed later or put into use, not sure right now
        /// </summary>
        public static event EventHandler<MessageEventArgs> ClilocMessageReceived;
        public static void InvokeClilocMessageReceived(object sender, MessageEventArgs e) => InvokeSafely(ClilocMessageReceived, sender, e, nameof(ClilocMessageReceived));

        /// <summary>
        /// Invoked anytime a message is added to the journal
        /// </summary>
        public static event EventHandler<JournalEntry> JournalEntryAdded;
        public static void InvokeJournalEntryAdded(object sender, JournalEntry e) => InvokeSafely(JournalEntryAdded, sender, e, nameof(JournalEntryAdded));

        /// <summary>
        /// Invoked anytime we receive object property list data (Tooltip text for items)
        /// </summary>
        public static event EventHandler<OPLEventArgs> OPLOnReceive;
        public static void InvokeOPLOnReceive(object sender, OPLEventArgs e) => InvokeSafely(OPLOnReceive, sender, e, nameof(OPLOnReceive));

        /// <summary>
        /// Invoked when a buff is "added" to a player
        /// </summary>
        public static event EventHandler<BuffEventArgs> OnBuffAdded;
        public static void InvokeOnBuffAdded(object sender, BuffEventArgs e) => InvokeSafely(OnBuffAdded, sender, e, nameof(OnBuffAdded));

        /// <summary>
        /// Invoked when a buff is "removed" to a player (Called before removal)
        /// </summary>
        public static event EventHandler<BuffEventArgs> OnBuffRemoved;
        public static void InvokeOnBuffRemoved(object sender, BuffEventArgs e) => InvokeSafely(OnBuffRemoved, sender, e, nameof(OnBuffRemoved));

        /// <summary>
        /// Invoked when the players position is changed
        /// </summary>
        public static event EventHandler<PositionChangedArgs> OnPositionChanged;
        public static void InvokeOnPositionChanged(object sender, PositionChangedArgs e) => InvokeSafely(OnPositionChanged, sender, e, nameof(OnPositionChanged));

        /// <summary>
        /// Invoked when any entity in game receives damage, not neccesarily the player.
        /// </summary>
        public static event EventHandler<int> OnEntityDamage;
        public static void InvokeOnEntityDamage(object sender, int e) => InvokeSafely(OnEntityDamage, sender, e, nameof(OnEntityDamage));

        /// <summary>
        /// Invoked when a container is opened. Sender is the Item, serial is the item serial.
        /// </summary>
        public static event EventHandler<uint> OnOpenContainer;
        public static void InvokeOnOpenContainer(Item sender, uint serial) => InvokeSafely(OnOpenContainer, sender, serial, nameof(OnOpenContainer));

        /// <summary>
        /// Invoked when the player receives a death packet from the server
        /// </summary>
        public static event EventHandler<uint> OnPlayerDeath;
        public static void InvokeOnPlayerDeath(object sender, uint serial) => InvokeSafely(OnPlayerDeath, sender, serial, nameof(OnPlayerDeath));

        /// <summary>
        /// Invoked when the player or server tells the client to path find
        /// Vector is X, Y, Z and Distance
        /// </summary>
        public static event EventHandler<Vector4> OnPathFinding;
        public static void InvokeOnPathFinding(object sender, Vector4 e) => InvokeSafely(OnPathFinding, sender, e, nameof(OnPathFinding));

        /// <summary>
        /// Invoked when the server asks the client to generate some weather
        /// </summary>
        public static event EventHandler<WeatherEventArgs> OnSetWeather;
        public static void InvokeOnSetWeather(object sender, WeatherEventArgs e) => InvokeSafely(OnSetWeather, sender, e, nameof(OnSetWeather));

        /// <summary>
        /// Invoked when a stat of the player is changed(min or max). Currently only Hits is set up.
        /// </summary>
        public static event EventHandler<PlayerStatChangedArgs> OnPlayerStatChange;
        public static void InvokeOnPlayerStatChange(object sender, PlayerStatChangedArgs e) => InvokeSafely(OnPlayerStatChange, sender, e, nameof(OnPlayerStatChange));

        /// <summary>
        /// This  occurs *before* any TazUO tooltip processing occurs allowing you to modify it before processing happens
        /// </summary>
        public static PreProcessTooltipDelegate PreProcessTooltip;
        public delegate void PreProcessTooltipDelegate(ref ItemPropertiesData e);
        public static void InvokePreProcessTooltip(ref ItemPropertiesData e)
        {
            if (PreProcessTooltip == null) return;
            foreach (PreProcessTooltipDelegate handler in PreProcessTooltip.GetInvocationList())
            {
                string feature = "Event:PreProcessTooltip:" + (handler.Method.DeclaringType?.FullName ?? "Unknown");
                if (FeatureDiagnostics.IsDisabled(feature)) continue;
                try { handler(ref e); }
                catch (Exception ex) { FeatureDiagnostics.RecordFailure(feature, ex); }
            }
        }

        /// <summary>
        /// This event occurs *after* TazUO tooltip processing, this is the final string before being rendered into a tooltip window
        /// </summary>
        public static PostProcessTooltipDelegate PostProcessTooltip;
        public delegate void PostProcessTooltipDelegate(ref string e);
        public static void InvokePostProcessTooltip(ref string e)
        {
            if (PostProcessTooltip == null) return;
            foreach (PostProcessTooltipDelegate handler in PostProcessTooltip.GetInvocationList())
            {
                string feature = "Event:PostProcessTooltip:" + (handler.Method.DeclaringType?.FullName ?? "Unknown");
                if (FeatureDiagnostics.IsDisabled(feature)) continue;
                try { handler(ref e); }
                catch (Exception ex) { FeatureDiagnostics.RecordFailure(feature, ex); }
            }
        }

        /// <summary>
        /// Called when the visual spell manager detects a spell being cast.
        /// </summary>
        public static event EventHandler<int> SpellCastBegin;
        public static void InvokeSpellCastBegin(int spell) => InvokeSafely(SpellCastBegin, null, spell, nameof(SpellCastBegin));
    }

    public class OPLEventArgs : EventArgs
    {
        public readonly uint Serial;
        public readonly string Name;
        public readonly string Data;

        public OPLEventArgs(uint serial, string name, string data)
        {
            Serial = serial;
            Name = name;
            Data = data;
        }
    }

    public class BuffEventArgs : EventArgs
    {
        public BuffEventArgs(BuffIcon buff)
        {
            Buff = buff;
        }

        public BuffIcon Buff { get; }
    }

    public class PositionChangedArgs : EventArgs
    {
        public PositionChangedArgs(Vector3 newlocation)
        {
            Newlocation = newlocation;
        }

        public Vector3 Newlocation { get; }
    }

    public class WeatherEventArgs : EventArgs
    {
        public WeatherEventArgs(WeatherType type, byte count, byte temp)
        {
            Type = type;
            Count = count;
            Temp = temp;
        }

        public WeatherType Type { get; }
        public byte Count { get; }
        public byte Temp { get; }
    }

    public class PlayerStatChangedArgs : EventArgs
    {
        public PlayerStatChangedArgs(PlayerStat stat, int oldValue, int newValue)
        {
            Stat = stat;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public PlayerStat Stat { get; }
        public int OldValue { get; }
        public int NewValue { get; }

        public enum PlayerStat
        {
            Hits,
            HitsMax,
            Mana,
            ManaMax,
            Stamina,
            StaminaMax
        }
    }
}
