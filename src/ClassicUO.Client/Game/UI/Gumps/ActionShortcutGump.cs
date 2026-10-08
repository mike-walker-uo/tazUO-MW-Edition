using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using SDL3;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ActionShortcutGump : Gump
    {
        private readonly ClientAction _action;
        private readonly MacroManager _manager;
        private readonly Profile _profile;
        private readonly HotkeyBox _box;
        private readonly Label _status;

        internal static void Open(ClientAction action)
        {
            if (!World.InGame || action == null) return;
            UIManager.GetGump<ActionShortcutGump>()?.Dispose();
            UIManager.Add(new ActionShortcutGump(action));
        }

        private ActionShortcutGump(ClientAction action) : base(0, 0)
        {
            _action = action;
            _manager = MacroManager.TryGetMacroManager();
            _profile = ProfileManager.CurrentProfile;
            Width = 390; Height = 185;
            AcceptMouseInput = true;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("Shortcut: " + action.Name, true, CustomGumpThemeManager.TitleHue, 350, font: 1) { X = 18, Y = 18 });
            Add(new Label("Press a key, then confirm. Red button clears binding.", true, CustomGumpThemeManager.TextHue, 350, font: 1) { X = 18, Y = 50 });
            Add(_box = new HotkeyBox { X = 18, Y = 85 });
            Add(_status = new Label("Also check Razor Enhanced for conflicting bindings.", true, CustomGumpThemeManager.TextHue, 350, font: 1) { X = 18, Y = 122 });
            Macro macro = _manager?.FindMacro(action.ShortcutName);
            if (macro != null)
            {
                SDL.SDL_Keymod mod = Modifiers(macro);
                if (macro.ControllerButtons?.Length > 0) _box.SetButtons(macro.ControllerButtons);
                else if (macro.WheelScroll) _box.SetMouseWheel(macro.WheelUp, mod);
                else if (macro.MouseButton != MouseButtonType.None) _box.SetMouseButton(macro.MouseButton, mod);
                else _box.SetKey(macro.Key, mod);
            }
            _box.HotkeyChanged += Save;
            _box.HotkeyCancelled += ClearBinding;
            _box.IsActive = true;
            _box.SetKeyboardFocus();
            CenterXInViewPort(); CenterYInViewPort();
        }

        internal static SDL.SDL_Keymod Modifiers(Macro macro) =>
            (macro.Alt ? SDL.SDL_Keymod.SDL_KMOD_ALT : SDL.SDL_Keymod.SDL_KMOD_NONE) |
            (macro.Ctrl ? SDL.SDL_Keymod.SDL_KMOD_CTRL : SDL.SDL_Keymod.SDL_KMOD_NONE) |
            (macro.Shift ? SDL.SDL_Keymod.SDL_KMOD_SHIFT : SDL.SDL_Keymod.SDL_KMOD_NONE);

        internal static string BindingLabel(Macro macro)
        {
            if (macro == null) return string.Empty;
            if (macro.ControllerButtons?.Length > 0) return Controller.GetButtonNames(macro.ControllerButtons);
            if (macro.WheelScroll) return KeysTranslator.GetMouseWheel(macro.WheelUp, Modifiers(macro));
            if (macro.MouseButton != MouseButtonType.None) return KeysTranslator.GetMouseButton(macro.MouseButton, Modifiers(macro));
            return macro.Key == SDL.SDL_Keycode.SDLK_UNKNOWN ? string.Empty : KeysTranslator.TryGetKey(macro.Key, Modifiers(macro));
        }

        private void Save(object sender, EventArgs e)
        {
            if (_manager == null || ProfileManager.CurrentProfile != _profile || !World.InGame) return;
            bool alt = (_box.Mod & SDL.SDL_Keymod.SDL_KMOD_ALT) != 0;
            bool ctrl = (_box.Mod & SDL.SDL_Keymod.SDL_KMOD_CTRL) != 0;
            bool shift = (_box.Mod & SDL.SDL_Keymod.SDL_KMOD_SHIFT) != 0;
            Macro existing = _manager.FindMacro(_action.ShortcutName);
            Macro conflict = null;
            if (_box.Key != SDL.SDL_Keycode.SDLK_UNKNOWN)
            {
                conflict = _manager.FindMacro(_box.Key, alt, ctrl, shift);
                if (SpellBarManager.HasBinding(_box.Key, _box.Mod))
                { _status.Text = "Key already assigned to the spell bar."; return; }
            }
            else if (_box.MouseButton != MouseButtonType.None) conflict = _manager.FindMacro(_box.MouseButton, alt, ctrl, shift);
            else if (_box.WheelScroll) conflict = _manager.FindMacro(_box.WheelUp, alt, ctrl, shift);
            else if (_box.Buttons?.Length > 0)
            {
                conflict = _manager.GetAllMacros().FirstOrDefault(m => m.ControllerButtons?.Length > 0 && (m.ControllerButtons.All(_box.Buttons.Contains) || _box.Buttons.All(m.ControllerButtons.Contains)));
                if (SpellBarManager.GetControllerButtons().Any(buttons => (buttons.Length > 0 && (buttons.All(_box.Buttons.Contains) || _box.Buttons.All(buttons.Contains)))))
                { _status.Text = "Buttons already assigned to the spell bar."; return; }
            }
            else return;
            if (conflict != null && conflict != existing)
            { _status.Text = "Already assigned: " + conflict.Name; return; }
            // Never overwrite an unrelated macro with the same name.
            if (existing != null && (!(existing.Items is MacroObjectString saved) ||
                saved.Code != MacroType.ContextAction || !ClientAction.TryParse(saved.Text, out ClientAction savedAction) || !_action.SameTarget(savedAction)))
            { _status.Text = "A different macro already uses this shortcut name."; return; }
            Macro macro = existing ?? new Macro(_action.ShortcutName);
            if (existing == null)
            {
                macro.Items = new MacroObjectString(MacroType.ContextAction, MacroSubType.MSC_NONE, _action.Serialize());
                _manager.MoveToBack(macro);
            }
            ((MacroObjectString)macro.Items).Text = _action.Serialize();
            macro.Key = _box.Key; macro.MouseButton = _box.MouseButton;
            macro.WheelScroll = _box.WheelScroll; macro.WheelUp = _box.WheelUp;
            macro.ControllerButtons = _box.Buttons;
            macro.Alt = alt; macro.Ctrl = ctrl; macro.Shift = shift;
            _manager.Save(); Dispose();
        }

        private void ClearBinding(object sender, EventArgs e)
        {
            if (_manager == null || ProfileManager.CurrentProfile != _profile || !World.InGame) return;
            Macro macro = _manager.FindMacro(_action.ShortcutName);
            if (macro?.Items is MacroObjectString saved && saved.Code == MacroType.ContextAction && ClientAction.TryParse(saved.Text, out ClientAction savedAction) && _action.SameTarget(savedAction))
            {
                macro.Key = SDL.SDL_Keycode.SDLK_UNKNOWN; macro.MouseButton = MouseButtonType.None;
                macro.WheelScroll = false; macro.ControllerButtons = null;
                macro.Alt = macro.Ctrl = macro.Shift = false;
                _manager.Save();
            }
            Dispose();
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || ProfileManager.CurrentProfile != _profile) { Dispose(); return; }
            base.Update();
        }
    }
}
