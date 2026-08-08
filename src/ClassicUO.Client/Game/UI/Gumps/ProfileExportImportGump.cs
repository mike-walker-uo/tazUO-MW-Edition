#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Small one-shot dialog: export the current Profile to a JSON file
    /// (in the profile folder under exports/), or import a JSON file picked
    /// via FileSelector. Identity fields (Username/Server/Character) are
    /// preserved across import.
    /// </summary>
    internal class ProfileExportImportGump : Gump
    {
        private const int WIDTH = 360;
        private const int HEIGHT = 160;

        private readonly Label _statusLabel;

        public ProfileExportImportGump() : base(0, 0)
        {
            X = 200;
            Y = 200;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            var background = new AlphaBlendControl(0.85f) { Width = WIDTH, Height = HEIGHT };
            CustomGumpThemeManager.ApplyDataSurface(background, 0.85f);
            Add(background);

            Add(new Label("Profile Export / Import", true, 0x0481, font: 1) { X = 8, Y = 6 });

            Add(new Label(
                "Export saves the current profile as JSON.\n" +
                "Import replaces it (current profile auto-backed up to .bak1).",
                true, 0x03B2, font: 1, maxwidth: WIDTH - 16)
            {
                X = 8,
                Y = 24
            });

            var btnExport = new NiceButton(8, 78, 110, 22, ButtonAction.Activate, "Export") { ButtonParameter = 1, IsSelectable = false };
            var btnImport = new NiceButton(126, 78, 110, 22, ButtonAction.Activate, "Import...") { ButtonParameter = 2, IsSelectable = false };
            var btnOpenFolder = new NiceButton(244, 78, 110, 22, ButtonAction.Activate, "Open Folder") { ButtonParameter = 3, IsSelectable = false };
            Add(btnExport);
            Add(btnImport);
            Add(btnOpenFolder);

            _statusLabel = new Label("", true, 0x03B2, font: 1, maxwidth: WIDTH - 16) { X = 8, Y = 110 };
            Add(_statusLabel);
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1: DoExport(); break;
                case 2: DoImport(); break;
                case 3: OpenExportFolder(); break;
                default: base.OnButtonClick(buttonID); break;
            }
        }

        private static string ExportFolder()
        {
            string baseDir = string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Profiles", "exports")
                : Path.Combine(ProfileManager.ProfilePath, "exports");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        private void DoExport()
        {
            try
            {
                var profile = ProfileManager.CurrentProfile;
                if (profile == null)
                {
                    SetStatus("No active profile.", 0x0021);
                    return;
                }

                string folder = ExportFolder();
                string filename = string.Format("profile_{0:yyyy-MM-dd_HHmm}.json", DateTime.Now);
                string path = Path.Combine(folder, filename);

                ConfigurationResolver.Save(profile, path, ProfileJsonContext.DefaultToUse);
                SetStatus("Exported to " + path, 0x0044);
                GameActions.Print("Profile exported: " + path, 0x0044);
            }
            catch (Exception ex)
            {
                SetStatus("Export failed: " + ex.Message, 0x0021);
            }
        }

        private void DoImport()
        {
            UIManager.Add(new FileSelector(
                FileSelectorType.File,
                ExportFolder(),
                new[] { ".json" },
                onFileSelected: (chosenPath) =>
                {
                    if (string.IsNullOrEmpty(chosenPath)) return;
                    try
                    {
                        // Validate by parsing the imported file (fail early if it's not a Profile).
                        var imported = ConfigurationResolver.Load<Profile>(chosenPath, ProfileJsonContext.DefaultToUse);
                        if (imported == null)
                        {
                            SetStatus("Failed to read JSON.", 0x0021);
                            return;
                        }

                        if (string.IsNullOrEmpty(ProfileManager.ProfilePath))
                        {
                            SetStatus("No active profile path; can't import without an active character.", 0x0021);
                            return;
                        }

                        // Save current state first (creates .bak1).
                        if (ProfileManager.CurrentProfile != null)
                        {
                            ConfigurationResolver.Save(
                                ProfileManager.CurrentProfile,
                                Path.Combine(ProfileManager.ProfilePath, "profile.json"),
                                ProfileJsonContext.DefaultToUse);
                        }

                        // Overwrite active profile.json with the imported file's content.
                        // ProfileManager.CurrentProfile setter is private, so we go through disk.
                        // Identity fields (Username/Server/Character) are [JsonIgnore], so they
                        // get re-applied when the profile is loaded fresh next session.
                        string activePath = Path.Combine(ProfileManager.ProfilePath, "profile.json");
                        if (!ProfileDataStore.WriteAllText("profile.json", File.ReadAllText(chosenPath)))
                        {
                            SetStatus("Import failed while replacing profile.json.", 0x0021);
                            return;
                        }

                        SetStatus("Imported. Restart client to activate the new profile.", 0x0044);
                        GameActions.Print("Profile imported to: " + activePath, 0x0044);
                    }
                    catch (Exception ex)
                    {
                        SetStatus("Import failed: " + ex.Message, 0x0021);
                    }
                },
                title: "Import Profile JSON"));
        }

        private static void OpenExportFolder()
        {
            try
            {
                string folder = ExportFolder();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                GameActions.Print("Could not open folder: " + ex.Message, 0x0021);
            }
        }

        private void SetStatus(string text, ushort hue)
        {
            _statusLabel.Text = text;
            _statusLabel.Hue = hue;
        }
    }
}
