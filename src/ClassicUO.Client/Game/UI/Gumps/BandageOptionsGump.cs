#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Two-column compact options window for auto-bandage settings.
    /// Open via `-bandageopts`.
    /// </summary>
    internal class BandageOptionsGump : Gump
    {
        // Reference held so the External-Target label can be refreshed in
        // Update() — `Pick` runs async and the label cannot be set inline.
        private Label _targetNameLbl;
        private uint _lastTargetSerial;
        // Two columns; each ~280px wide.
        private const int COL_W = 280;
        private const int W = COL_W * 2 + 36;       // 596
        private const int PAD = 12;
        private const int INDENT_L = 22;
        private const int INDENT_R = COL_W + 24 + INDENT_L;
        private const int HEADER_X_L = 46;
        private const int HEADER_X_R = COL_W + 24 + HEADER_X_L;
        private const int ICON_X_L   = PAD;
        private const int ICON_X_R   = COL_W + 24 + PAD;
        private const int ROW_H = 20;
        private const int SECTION_GAP = 8;
        private const int DIVIDER_GAP = 4;

        private static ushort TITLE_HUE => CustomGumpThemeManager.TitleHue;
        private static ushort LABEL_HUE => CustomGumpThemeManager.TextHue;
        private static ushort GROUP_HUE => CustomGumpThemeManager.GroupHue;
        private static ushort DIM_HUE => CustomGumpThemeManager.DimHue;

        private const ushort ICON_BANDAGE = 0x0E21;
        private const ushort ICON_PLAYER  = 0x2106;
        private const ushort ICON_PET     = 0x0E81;
        private const ushort ICON_EXT     = 0x097B;
        private const ushort ICON_TIMER   = 0x09D7;
        private const ushort ICON_WARN    = 0x09F5;

        private const ushort CB_OFF = 0x00D2;
        private const ushort CB_ON  = 0x00D3;

        public BandageOptionsGump() : base(0, 0)
        {
            X = 150; Y = 80;
            Width = W; Height = 100;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            ThemedGumpBackground bg = CustomGumpThemeManager.CreateBackground(W, 100, 0.85f);
            Add(bg);

            // ==== Title row (full width)
            int y0 = PAD;
            Add(new StaticPic(ICON_BANDAGE, 0) { X = PAD, Y = y0 - 2 });
            Add(new Label("Bandage Options", true, TITLE_HUE, font: 1) { X = HEADER_X_L, Y = y0 });
            y0 += 22;
            FullDivider(ref y0);

            // ==== Two-column area starts directly after the title divider
            int yL = y0;
            int yR = y0;

            // -- LEFT column: PRIORITY → PLAYER → PET --
            HeaderAt(HEADER_X_L, ICON_X_L, "PRIORITY", null, ref yL);
            AddPriorityRadios(INDENT_L, ref yL);
            ColDivider(PAD, COL_W, ref yL);

            HeaderAt(HEADER_X_L, ICON_X_L, "PLAYER", ICON_PLAYER, ref yL, "(Healing)");
            AddCbAt(INDENT_L, "Auto bandage", AutoBandageManager.Enabled, b => AutoBandageManager.SetEnabled(b), ref yL,
                "Automatically bandage yourself when your HP% drops under the threshold. Starts enabled when Healing skill >= 40 unless manually overridden.");
            AddCbAt(INDENT_L, "Block on poisoned", AutoBandageManager.BlockOnPoisoned, b => AutoBandageManager.BlockOnPoisoned = b, ref yL,
                "Refuse to fire a bandage on the player while poisoned. On many shards bandages cure poison — leave OFF if your shard does.");
            AddCbAt(INDENT_L, "Block on mortal strike", AutoBandageManager.BlockOnMortal, b => AutoBandageManager.BlockOnMortal = b, ref yL,
                "Refuse to fire a bandage while you are under the mortal-strike effect (yellow HP bar). Mortal blocks healing on most shards.");
            AddCbAt(INDENT_L, "Block when dead", AutoBandageManager.BlockOnDead, b => AutoBandageManager.BlockOnDead = b, ref yL,
                "Refuse to fire while the player is dead (ghost form). Should normally stay ON.");
            AddThresholdRow(INDENT_L, COL_W + INDENT_L - 24, "Threshold", AutoBandageManager.ThresholdPercent, v => AutoBandageManager.SetThreshold(v), ref yL,
                "HP% under which auto-bandage fires for the player. Default 90.");
            ColDivider(PAD, COL_W, ref yL);

            HeaderAt(HEADER_X_L, ICON_X_L, "PET", ICON_PET, ref yL, "(Veterinary)");
            AddCbAt(INDENT_L, "Auto pet bandage", PetBandageManager.Enabled, b => PetBandageManager.SetEnabled(b), ref yL,
                "Automatically bandage the lowest-HP renamable pet within 2 tiles. Starts enabled when Veterinary >= 40 unless manually overridden.");
            AddCbAt(INDENT_L, "Block on pet poisoned", PetBandageManager.BlockOnPoisoned, b => PetBandageManager.BlockOnPoisoned = b, ref yL,
                "Skip pets that are currently poisoned.");
            AddCbAt(INDENT_L, "Block on pet mortal", PetBandageManager.BlockOnMortal, b => PetBandageManager.BlockOnMortal = b, ref yL,
                "Skip pets under mortal-strike (yellow HP bar).");
            AddCbAt(INDENT_L, "Block when pet dead", PetBandageManager.BlockOnDead, b => PetBandageManager.BlockOnDead = b, ref yL,
                "Skip dead pets. Leave OFF to let Veterinary bandages resurrect bonded pets.");
            AddThresholdRow(INDENT_L, COL_W + INDENT_L - 24, "Threshold", PetBandageManager.ThresholdPct, v => PetBandageManager.SetThreshold(v), ref yL,
                "HP% under which a pet gets bandaged. Default 90.");

            // -- RIGHT column --
            // Multi-pet moves to top of right column to balance heights.
            HeaderAt(HEADER_X_R, ICON_X_R, "MULTI-PET", null, ref yR);
            AddMultiPetRadios(INDENT_R, ref yR);
            ColDivider(COL_W + 24 + PAD, COL_W, ref yR);

            HeaderAt(HEADER_X_R, ICON_X_R, "EXTERNAL", ICON_EXT, ref yR, "(picked)");
            AddTargetRow(INDENT_R, COL_W + INDENT_R - 24, ref yR);
            AddCbAt(INDENT_R, "Auto external bandage", ExternalBandageManager.Enabled, b => ExternalBandageManager.SetEnabled(b), ref yR,
                "Automatically bandage the externally-picked target (set via Pick).");
            AddCbAt(INDENT_R, "Block on poisoned", ExternalBandageManager.BlockOnPoisoned, b => ExternalBandageManager.BlockOnPoisoned = b, ref yR,
                "Skip when the external target is poisoned.");
            AddCbAt(INDENT_R, "Block on mortal strike", ExternalBandageManager.BlockOnMortal, b => ExternalBandageManager.BlockOnMortal = b, ref yR,
                "Skip when the external target is under mortal-strike.");
            AddCbAt(INDENT_R, "Block when dead", ExternalBandageManager.BlockOnDead, b => ExternalBandageManager.BlockOnDead = b, ref yR,
                "Skip when the external target is dead.");
            AddThresholdRow(INDENT_R, COL_W + INDENT_R - 24, "Threshold", ExternalBandageManager.ThresholdPct, v => ExternalBandageManager.SetThreshold(v), ref yR,
                "HP% under which the external target gets bandaged.");
            ColDivider(COL_W + 24 + PAD, COL_W, ref yR);

            HeaderAt(HEADER_X_R, ICON_X_R, "LOW-STOCK", ICON_WARN, ref yR);
            AddCbAt(INDENT_R, "Warn when bandages low", BandageStockWarner.Enabled, b => BandageStockWarner.SetEnabled(b), ref yR,
                "Toast + chime when total bandage count in pack drops below the threshold. Re-arms when count climbs back above threshold + 10.");
            AddThresholdRow(INDENT_R, COL_W + INDENT_R - 24, "Threshold (count)", BandageStockWarner.Threshold, v => BandageStockWarner.SetThreshold(v), ref yR,
                "Total bandage count under which the alarm fires. Default 100.");
            yR += 4;
            var anchorBtn = new NiceButton(INDENT_R, yR, COL_W - 40, 18, ButtonAction.Activate, "Move toast position", font: 1) { IsSelectable = false };
            CustomGumpThemeManager.StyleButton(anchorBtn);
            anchorBtn.SetTooltip("Open a drag handle to anchor toast notifications somewhere else on the screen.");
            anchorBtn.MouseUp += (s, e) =>
            {
                var existing = UIManager.GetGump<ToastAnchorGump>();
                if (existing != null && !existing.IsDisposed) existing.Dispose();
                else UIManager.Add(new ToastAnchorGump());
            };
            Add(anchorBtn);
            yR += ROW_H;
            ColDivider(COL_W + 24 + PAD, COL_W, ref yR);

            HeaderAt(HEADER_X_R, ICON_X_R, "CYCLE", ICON_TIMER, ref yR);
            AddCycleRow(INDENT_R, COL_W + INDENT_R - 24, ref yR);

            // Final size: take taller column.
            int finalH = Math.Max(yL, yR) + PAD;
            bg.Height = finalH;
            Height = finalH;
        }

        public override GumpType GumpType => GumpType.None;

        // ---------- helpers (positional) ----------

        private void HeaderAt(int labelX, int iconX, string text, ushort? icon, ref int y, string suffix = null)
        {
            if (icon.HasValue) Add(new StaticPic(icon.Value, 0) { X = iconX, Y = y - 2 });
            Add(new Label(text, true, GROUP_HUE, font: 1) { X = labelX, Y = y });
            if (!string.IsNullOrEmpty(suffix))
                Add(new Label(suffix, true, DIM_HUE, font: 1) { X = labelX + 78, Y = y });
            y += 18;
        }

        private void SubLabelAt(int x, string text, ref int y)
        {
            Add(new Label(text, true, LABEL_HUE, font: 1) { X = x, Y = y });
            y += 16;
        }

        private void FullDivider(ref int y)
        {
            y += DIVIDER_GAP;
            Add(new AlphaBlendControl(0.35f) { Width = W - 2 * PAD, Height = 1, X = PAD, Y = y });
            y += SECTION_GAP;
        }

        private void ColDivider(int x, int width, ref int y)
        {
            y += DIVIDER_GAP;
            Add(new AlphaBlendControl(0.35f) { Width = width, Height = 1, X = x, Y = y });
            y += SECTION_GAP;
        }

        private Checkbox MakeCb(string text, bool initial, Action<bool> onChange)
        {
            var cb = new Checkbox(CB_OFF, CB_ON, text, font: 1, color: CustomGumpThemeManager.TextHue) { IsChecked = initial };
            cb.ValueChanged += (s, e) => onChange(cb.IsChecked);
            return cb;
        }

        private void AddCbAt(int x, string text, bool isOn, Action<bool> onChange, ref int y, string tooltip = null)
        {
            var cb = MakeCb(text, isOn, b => { onChange(b); BandageSettings.MarkDirty(); });
            cb.X = x;
            cb.Y = y;
            if (!string.IsNullOrEmpty(tooltip)) cb.SetTooltip(tooltip);
            Add(cb);
            y += ROW_H;
        }

        private void AddThresholdRow(int labelX, int inputRightX, string label, int initial, Action<int> onSet, ref int y, string tooltip = null)
        {
            y += 4;
            var lbl = new Label(label, true, LABEL_HUE, font: 1) { X = labelX, Y = y };
            Add(lbl);
            // max_char_count is interpreted as max VALUE when NumbersOnly is true (see StbTextBox.cs:928).
            var input = new StbTextBox(1, 99, 30, hue: CustomGumpThemeManager.TextHue)
            {
                X = inputRightX - 45, Y = y - 1, Width = 45, Height = 18,
                Multiline = false, NumbersOnly = true,
            };
            input.SetText(initial.ToString());
            input.TextChanged += (s, e) =>
            {
                if (int.TryParse(input.Text, out int pct)) onSet(pct);
            };
            Add(input);
            if (!string.IsNullOrEmpty(tooltip))
            {
                lbl.SetTooltip(tooltip);
                input.SetTooltip(tooltip);
            }
            y += ROW_H;
        }

        private void AddCycleRow(int labelX, int inputRightX, ref int y)
        {
            y += 4;
            var lbl = new Label("Bandage timer (ms)", true, LABEL_HUE, font: 1) { X = labelX, Y = y };
            lbl.SetTooltip("Bandage cycle in milliseconds. Shared between AutoBandage and PetBandage. Set to your shard's bandage timing + small safety margin (UO Alive = 2000ms → 2200).");
            Add(lbl);
            var input = new StbTextBox(1, 20000, 60, hue: CustomGumpThemeManager.TextHue)
            {
                X = inputRightX - 55, Y = y - 1, Width = 55, Height = 18,
                Multiline = false, NumbersOnly = true,
            };
            input.SetTooltip("Bandage cycle in milliseconds (200-20000).");
            input.SetText(AutoBandageManager.CycleMs.ToString());
            input.TextChanged += (s, e) =>
            {
                if (long.TryParse(input.Text, out long ms))
                {
                    if (ms < 200) ms = 200;
                    if (ms > 20000) ms = 20000;
                    AutoBandageManager.CycleMs = ms;
                    PetBandageManager.CycleMs  = ms;
                }
            };
            Add(input);
            y += ROW_H;
        }

        private void AddTargetRow(int labelX, int rightX, ref int y)
        {
            var nameLbl = new Label($"Target: {ExternalBandageManager.TargetName()}", true, LABEL_HUE, font: 1) { X = labelX, Y = y };
            _targetNameLbl = nameLbl;
            _lastTargetSerial = ExternalBandageManager.TargetSerial;
            Add(nameLbl);
            var pickBtn = new NiceButton(rightX - 78, y, 32, 16, ButtonAction.Activate, "Pick", font: 1) { IsSelectable = false };
            pickBtn.SetTooltip("Open a target cursor and click any mob to bind it as the external bandage target.");
            pickBtn.MouseUp += (s, e) =>
            {
                ExternalBandageManager.PickTarget();
                nameLbl.Text = $"Target: {ExternalBandageManager.TargetName()}";
            };
            Add(pickBtn);
            var clearBtn = new NiceButton(rightX - 40, y, 38, 16, ButtonAction.Activate, "Clear", font: 1) { IsSelectable = false };
            clearBtn.SetTooltip("Forget the currently bound external bandage target.");
            clearBtn.MouseUp += (s, e) =>
            {
                ExternalBandageManager.Clear();
                nameLbl.Text = $"Target: {ExternalBandageManager.TargetName()}";
            };
            Add(clearBtn);
            y += ROW_H;
        }

        private void AddPriorityRadios(int x, ref int y)
        {
            var opts = new (string label, BandageScheduler.Pref p, string tip)[]
            {
                ("Prefer PLAYER",    BandageScheduler.Pref.Player,
                    "When player + pet + external all need healing at the same instant, bandage the PLAYER first."),
                ("Prefer PET",       BandageScheduler.Pref.Pet,
                    "When player + pet + external all need healing, bandage the PET first."),
                ("Prefer EXTERNAL",  BandageScheduler.Pref.External,
                    "When player + pet + external all need healing, bandage the EXTERNAL target first."),
            };
            RenderRadioGroup(x, opts, () => BandageScheduler.Priority, p => BandageScheduler.Priority = p, ref y);
        }

        private void AddMultiPetRadios(int x, ref int y)
        {
            var opts = new (string label, PetBandageManager.MultiPetMode m, string tip)[]
            {
                ("Always weakest",          PetBandageManager.MultiPetMode.AlwaysWeakest,
                    "Each cycle re-pick the absolute lowest-HP pet. May rapidly switch between pets."),
                ("Focus weakest -> 90%",    PetBandageManager.MultiPetMode.FocusUntil90,
                    "Lock onto the first wounded pet and keep bandaging IT until its HP climbs to 90%, then move on."),
                ("Focus weakest -> 50%",    PetBandageManager.MultiPetMode.FocusUntil50,
                    "Same as above but only locks until 50% HP — switches to the next-weakest sooner."),
            };
            RenderRadioGroup(x, opts, () => PetBandageManager.Mode, m => PetBandageManager.Mode = m, ref y);
        }

        private void RenderRadioGroup<T>(
            int x,
            (string label, T value, string tip)[] opts,
            Func<T> getCurrent,
            Action<T> setCurrent,
            ref int y)
        {
            var cmp = EqualityComparer<T>.Default;
            var boxes = new List<Checkbox>(opts.Length);
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                var cb = MakeCb(opts[i].label, cmp.Equals(getCurrent(), opts[i].value), b =>
                {
                    if (!b)
                    {
                        if (cmp.Equals(getCurrent(), opts[idx].value)) boxes[idx].IsChecked = true;
                        return;
                    }
                    setCurrent(opts[idx].value);
                    for (int j = 0; j < boxes.Count; j++)
                        if (j != idx) boxes[j].IsChecked = false;
                });
                cb.X = x;
                cb.Y = y;
                if (!string.IsNullOrEmpty(opts[i].tip)) cb.SetTooltip(opts[i].tip);
                boxes.Add(cb);
                Add(cb);
                y += ROW_H;
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed || _targetNameLbl == null) return;
            // Poll the external target serial — Pick is async so the label
            // can't be set inline. Refresh whenever the serial changes.
            uint cur = ExternalBandageManager.TargetSerial;
            if (cur != _lastTargetSerial)
            {
                _lastTargetSerial = cur;
                _targetNameLbl.Text = $"Target: {ExternalBandageManager.TargetName()}";
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            return true;
        }
    }
}
