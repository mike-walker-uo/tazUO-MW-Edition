using System;
using System.Globalization;
using System.Text;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    internal enum ClientActionKind { Spell, Skill, PrimaryAbility, SecondaryAbility, Macro, Loadout, CounterCell, ClientCommand }

    // One descriptor shared by action-bar cells and saved shortcut macros.
    internal sealed class ClientAction
    {
        public ClientActionKind Kind { get; }
        public int Id { get; }
        public string Name { get; }
        public string ShortcutName { get; }
        public ClientAction(ClientActionKind kind, int id, string name)
        {
            Kind = kind;
            Id = id;
            Name = name ?? string.Empty;
            ShortcutName = "Shortcut: " + Kind + " " +
                (Kind == ClientActionKind.Macro || Kind == ClientActionKind.Loadout || Kind == ClientActionKind.ClientCommand ? Name : Id.ToString(CultureInfo.InvariantCulture));
        }

        public string Serialize() => ((int)Kind).ToString(CultureInfo.InvariantCulture) + ":" +
            Id.ToString(CultureInfo.InvariantCulture) + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Name));

        public static bool TryParse(string text, out ClientAction action)
        {
            action = null;
            string[] parts = (text ?? string.Empty).Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int kind) ||
                !Enum.IsDefined(typeof(ClientActionKind), kind) || !int.TryParse(parts[1], out int id) || id < 0)
                return false;
            try
            {
                action = new ClientAction((ClientActionKind)kind, id, Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])));
                return true;
            }
            catch (FormatException) { return false; }
        }

        internal bool SameTarget(ClientAction other) => other != null && Kind == other.Kind &&
            (Kind == ClientActionKind.Macro || Kind == ClientActionKind.Loadout || Kind == ClientActionKind.ClientCommand
                ? string.Equals(Name, other.Name, StringComparison.Ordinal) : Id == other.Id);

        public void Execute()
        {
            if (!World.InGame || World.Player == null) return;
            switch (Kind)
            {
                case ClientActionKind.Spell: GameActions.CastSpell(Id); break;
                case ClientActionKind.Skill:
                    if (Id < World.Player.Skills.Length && World.Player.Skills[Id]?.IsClickable == true)
                        GameActions.UseSkill(Id);
                    break;
                case ClientActionKind.PrimaryAbility: GameActions.UsePrimaryAbility(); break;
                case ClientActionKind.SecondaryAbility: GameActions.UseSecondaryAbility(); break;
                case ClientActionKind.Macro:
                    MacroManager manager = MacroManager.TryGetMacroManager();
                    Macro macro = manager?.FindMacro(Name);
                    if (macro != null && !(macro.Items is MacroObjectString first && first.Code == MacroType.ContextAction))
                        manager.SetMacroToExecute((MacroObject)macro.Items);
                    break;
                case ClientActionKind.Loadout: QuickLoadoutManager.LoadSet(Name); break;
                case ClientActionKind.ClientCommand:
                    string[] parts = Name.Split(' ');
                    CommandManager.Execute(parts[0], parts);
                    break;
                case ClientActionKind.CounterCell: CounterBarGump.CurrentCounterBarGump?.GetCounterItem(Id)?.Use(); break;
            }
        }
    }
}
