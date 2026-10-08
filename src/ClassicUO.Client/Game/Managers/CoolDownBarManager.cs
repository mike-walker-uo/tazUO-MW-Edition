using ClassicUO.Configuration;
using ClassicUO.Game.UI.Gumps;
using System;

namespace ClassicUO.Game.Managers
{
    public class CoolDownBarManager
    {
        private const int MAX_COOLDOWN_BARS = 15;
        private static CoolDownBar[] coolDownBars = new CoolDownBar[MAX_COOLDOWN_BARS];

        public CoolDownBarManager()
        {
            EventSink.MessageReceived += MessageManager_MessageReceived;
        }

        private void MessageManager_MessageReceived(object sender, MessageEventArgs e)
        {
            var profile = ProfileManager.CurrentProfile;
            int count = profile?.CoolDownConditionCount ?? 0;

            if (World.Player == null || count <= 0 || string.IsNullOrEmpty(e?.Text))
            {
                return;
            }

            profile.EnsureCooldownRuleIds();
            for (int i = 0; i < count; i++)
            {
                switch (profile.Condition_Type[i])
                {
                    default:
                    case 0:
                        break;
                    case 1: //self
                        if (e.Parent != null && e.Parent.Serial != World.Player.Serial)
                            continue;
                        break;
                    case 2:
                        if (e.Parent != null && e.Parent.Serial == World.Player.Serial)
                            continue;
                        break;
                }

                if (e.Text.Contains(profile.Condition_Trigger[i]))
                {
                    AddCoolDownBar(
                        TimeSpan.FromSeconds(profile.Condition_Duration[i]),
                        profile.Condition_Label[i],
                        profile.Condition_Hue[i],
                        profile.Condition_ReplaceIfExists.Count > i
                            ? profile.Condition_ReplaceIfExists[i]
                            : false,
                        i < profile.Condition_KeepExisting.Count && profile.Condition_KeepExisting[i],
                        ProfileManager.ProfilePath + "\n" + profile.Condition_Ids[i]
                    );
                }
            }
        }

        internal static bool MatchesRule(string activeKey, string activeLabel, string key, string label) =>
            key != null ? string.Equals(activeKey, key, StringComparison.Ordinal)
                : activeKey == null && string.Equals(activeLabel, label, StringComparison.Ordinal);

        public static void AddCoolDownBar(TimeSpan _duration, string _name, ushort _hue, bool replace, bool keepExisting = false, string ruleKey = null)
        {
            if (keepExisting)
                foreach (var bar in coolDownBars)
                    if (bar != null && !bar.IsDisposed && MatchesRule(bar.RuleKey, bar.textLabel.Text, ruleKey, _name))
                        return;

            if (replace)
                for (int i = 0; i < coolDownBars.Length; i++)
                {
                    if (coolDownBars[i] != null && !coolDownBars[i].IsDisposed && coolDownBars[i].textLabel.Text == _name)
                    {
                        coolDownBars[i].Dispose();
                        coolDownBars[i] = new CoolDownBar(_duration, _name, _hue, CoolDownBar.DEFAULT_X, CoolDownBar.DEFAULT_Y + (i * (CoolDownBar.COOL_DOWN_HEIGHT + 5)));
                        coolDownBars[i].RuleKey = ruleKey;
                        UIManager.Add(coolDownBars[i]);
                        return;
                    }
                }
            for (int i = 0; i < coolDownBars.Length; i++)
            {
                if (coolDownBars[i] == null || coolDownBars[i].IsDisposed)
                {
                    coolDownBars[i] = new CoolDownBar(_duration, _name, _hue, CoolDownBar.DEFAULT_X, CoolDownBar.DEFAULT_Y + (i * (CoolDownBar.COOL_DOWN_HEIGHT + 5)));
                    coolDownBars[i].RuleKey = ruleKey;
                    UIManager.Add(coolDownBars[i]);
                    return;
                }
            }
        }
    }
}
