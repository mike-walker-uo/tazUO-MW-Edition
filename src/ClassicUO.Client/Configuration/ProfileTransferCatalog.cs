using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ClassicUO.Configuration
{
    internal enum ProfileTransferCategory
    {
        General, Audio, Video, Interface, Chat, Combat, Containers, Maps, Agents, Macros, Skills, SkillButtons, SpellIcons, InfoBar, GridLayout, GumpLayout, Enhancements, Other
    }

    internal static class ProfileTransferCatalog
    {
        internal static readonly string[] Labels =
        {
            "General & input",
            "Sound & music",
            "Graphics & weather",
            "Interface, themes & fonts",
            "Speech & journal",
            "Combat, spells & cooldowns",
            "Containers & loot",
            "Maps & travel",
            "Vendors, agents & tools",
            "Macros & hotkeys",
            "Skill groups",
            "Skill buttons",
            "Spell icons",
            "Info bar",
            "Grid container layouts",
            "Gump positions & sizes",
            "Enhancements & feature toggles",
            "Other profile options",
        };
        internal static readonly ProfileTransferCategory[] All =
            (ProfileTransferCategory[])Enum.GetValues(typeof(ProfileTransferCategory));

        internal static readonly Dictionary<ProfileTransferCategory, string[]> Files = new Dictionary<ProfileTransferCategory, string[]>
        {
            { ProfileTransferCategory.Video, "bodyscales.tsv bodyhues.tsv".Split(' ') },
            { ProfileTransferCategory.Interface, "nameoverhead.xml".Split(' ') },
            { ProfileTransferCategory.Chat, "journal_filters.json mutes.tsv keywords.tsv".Split(' ') },
            { ProfileTransferCategory.Combat, "bandage.tsv SpellVisualRange.json spell_ability_effects.tsv autohit.tsv".Split(' ') },
            { ProfileTransferCategory.Containers, "AutoLoot.json trash.serial item_finder_searches.tsv".Split(' ') },
            { ProfileTransferCategory.Maps, "TileMarkers.json checkpoints.tsv".Split(' ') },
            { ProfileTransferCategory.Agents, "dress_configs.json BuyAgentConfig.json SellAgentConfig.json OrganizerConfig.json RestockAgent.json equipment_guru_goals.tsv equipment_guru_rules.tsv loadouts.tsv pins.tsv AlertCenter.json".Split(' ') },
            { ProfileTransferCategory.Macros, "macros.xml aliases.tsv snippets.tsv pinned_commands.tsv SpellBar.json SpellBarSettings.json".Split(' ') },
            { ProfileTransferCategory.Skills, "skillsgroups.xml".Split(' ') },
            { ProfileTransferCategory.SkillButtons, new[] { "gumps.xml" } },
            { ProfileTransferCategory.SpellIcons, new[] { "gumps.xml" } },
            { ProfileTransferCategory.InfoBar, "infobar.xml".Split(' ') },
            { ProfileTransferCategory.GridLayout, "grid_containers.json GridContainers.xml".Split(' ') },
            { ProfileTransferCategory.GumpLayout, "gumps.xml".Split(' ') },
            { ProfileTransferCategory.Enhancements, "tazuo_features.tsv toast.tsv guild_hues.tsv".Split(' ') },
            { ProfileTransferCategory.Other, "ignore_list.xml".Split(' ') },
        };

        private static readonly Dictionary<string, ProfileTransferCategory> Members = new Dictionary<string, ProfileTransferCategory>();
        internal static readonly Dictionary<string, ProfileTransferCategory> JsonCategories;

        static ProfileTransferCatalog()
        {
            Add(ProfileTransferCategory.General,
                "AlwaysRun AlwaysRunUnlessHidden AuraUnderFeetType AutoAvoidObstacules AutoFollowDistance AutoOpenCorpseRange AutoOpenCorpses "+
                "AutoOpenDoors AutoOpenQueuedActions CBBlackBGToggled CloseAllAnchoredGumpsInGroupWithRightClick CloseHealthBarType "+
                "ClutterPickerShortcut CompactTransferProgress ControllerEnabled ControllerMouseSensativity CorpseOpenOptions CustomBarsToggled "+
                "DisableArrowBtn DisableAutoFollowAlt DisableAutoMove DisableCtrlQWBtn DisableTabBtn DragSelectAsAnchor DragSelectFilter "+
                "DragSelectFilterModifiers DragSelectModifierKey DragSelect_NameplateModifier EnableDragSelect EnablePathfind FollowingMode "+
                "FollowingTarget GroundDropPreview HideScreenshotStoredInMessage HighlightGameObjects HighlightMobilesByInvul "+
                "HighlightMobilesByParalize HighlightMobilesByPoisoned HoldAltToMoveGumps HoldDownKeyAltToCloseAnchored HoldShiftForContext "+
                "HoldShiftToSplitStack IgnoreStaminaCheck InvulnerableHue MobileHPShowWhen MobileHPType NoColorObjectsOutOfRange ParalyzedHue "+
                "PartyAura PartyAuraHue PartyInviteGump PathfindSingleClick PoisonHue SallosEasyGrab SaveHealthbars ShowHouseContent ShowMobilesHP "+
                "ShowNewCorpseNameIncoming ShowNewMobileNameIncoming ShowSkillsChangedDeltaValue ShowSkillsChangedMessage ShowStatsChangedMessage "+
                "ShowTargetRangeIndicator SkipEmptyCorpse SmoothDoors StandardSkillsGump StatusLayout TextFading TurnDelay UseObjectsFading "+
                "UseOldStatusGump UseShiftToPathfind UseSmoothBoatMovement UseWASDInsteadArrowKeys");
            Add(ProfileTransferCategory.Audio,
                "CombatMusicCrossfadeMs EnableCombatMusic EnableFootstepsSound EnableMusic EnableSound FavoriteMusicTracks MusicPlaybackMode "+
                "MusicPlayerCompact MusicSelectionMode MusicVolume ReproduceSoundsInBackground SoundVolume");
            Add(ProfileTransferCategory.Video,
                "AmbienceOverlayEnabled AmbientWeatherEnabled AnimatedWaterEffect AuraOnMouse BlizzardIntensity BloodDecalsEnabled "+
                "CircleOfTransparencyRadius CircleOfTransparencyType ClassicEffectsOnly DefaultScale DirtMaterialIntensity DrawRoofs "+
                "DungeonMaterialIntensity DynamicAmbienceIntensity DynamicShadowIntensity DynamicShadowLength EnableBlackWhiteEffect "+
                "EnableCaveBorder EnableDeathScreen EnableMousewheelScaleZoom EnablePostProcessingEffects EnhancedObjectShadows "+
                "EnhancedWaterEnabled EnhancedWaterStyle FieldsType FootstepGraphicsEnabled FootstepVisualDefaultsVersion ForceHouseTransparency "+
                "ForcedHouseTransparency ForcedTransparencyHouseTileHue FreezingRainTemperature GameWindowFullSize GameWindowLock "+
                "GrassMaterialIntensity HeavySnowIntensity HiddenBodyAlpha HiddenBodyHue HiddenLayers HideLayersForSelf HideVegetation LightLevel "+
                "LightLevelType MagnifierSmooth MagnifierZoom MineMaterialIntensity PlayerConstantAlpha PostProcessingType ReduceFPSWhenInactive "+
                "ReduceWeatherMotion RestoreScaleAfterUnpressCtrl SandMaterialIntensity SceneryQuality ShadowMode ShadowsEnabled ShadowsStatics "+
                "SnowMaterialIntensity TempestIntensity TerrainShadowsLevel TreeToStumps UseAlternativeLights UseCircleOfTransparency "+
                "UseColoredLights UseCustomLightLevel UseDarkNights VisualSilence WaterAtmosphereEnabled WaterIntensityDefaultsVersion "+
                "WaterMaterialIntensity WaterStyleDefaultsVersion WindowBorderless");
            Add(ProfileTransferCategory.Interface,
                "BoostGumpOpacityOnHover BuffBarOpacity ChatFont CounterBarAbbreviatedAmount CounterBarCellSize CounterBarColumns "+
                "CounterBarDisplayAbbreviatedAmount CounterBarEnabled CounterBarHighlightAmount CounterBarHighlightOnAmount "+
                "CounterBarHighlightOnUse CounterBarRows CounterGumpLocked CustomGumpOpacity CustomGumpTheme DurabilityGumpOpacity "+
                "EnableAlphaScrollingOnGumps FavoriteGumpThemes GameWindowSideChatFont GameWindowSideChatFontSize GlobalScale GlobalScaling "+
                "GumpHoverOpacityPercent GumpThemeOverrides HideHudGumpFlags InterfaceScale InterfaceScaling JournalOpacity "+
                "LastGumpThemeRotationDay MainWindowBackgroundHue ModernPaperDollDurabilityHue ModernPaperDollHue ModernPaperDoll_DurabilityPercent "+
                "ModernPaperdollAnchorEnabled NameOverheadToggled NamePlateAvoidOverlap NamePlateBorderOpacity NamePlateFont NamePlateFontSize "+
                "NamePlateHealthBar NamePlateHealthBarOpacity NamePlateHideAtFullHealth NamePlateHideAtFullHealthInWarmode NamePlateOpacity "+
                "OptionsFont OptionsFontSize OverheadChatFont OverheadChatFontSize OverrideAllFonts OverrideAllFontsIsUnicode PaperdollLocked "+
                "PaperdollOpacity PaperdollScale PaperdollSkin ReducedThemeDecoration RotateGumpThemesDaily SelectedJournalFontSize "+
                "SelectedTTFJournalFont SelectedToolTipFont SelectedToolTipFontSize SlayerBarOpacity TextBorderSize ToolTipBGHue "+
                "ToolTipOverride_Layer ToolTipOverride_MaxVal1 ToolTipOverride_MaxVal2 ToolTipOverride_MinVal1 ToolTipOverride_MinVal2 "+
                "ToolTipOverride_NewFormat ToolTipOverride_SearchText TooltipBackgroundOpacity TooltipDelayBeforeDisplay TooltipDisplayZoom "+
                "TooltipFont TooltipHeaderFormat TooltipTextHue TopbarGumpIsDisabled TopbarGumpIsMinimized UseModernPaperdoll UseTooltip");
            Add(ProfileTransferCategory.Chat,
                "ActivateChatAdditionalButtons ActivateChatAfterEnter ActivateChatShiftEnterSupport AllyMessageHue AltJournalBackgroundHue "+
                "AutoOpenGlobalChat AutoOpenGuildChat AutoOpenNearbySpeech ChatMentionGuildTag ChatMentionWords ChatMentionsEnabled ChatMessageHue "+
                "DisableSystemChat DisplayPartyChatOverhead EmoteHue ExcludeOwnSpeechHistory ExcludePetSpeechHistory ForceUnicodeJournal "+
                "GlobalChatChannelColors GlobalChatIncludeGuild GlobalChatIncludeParty GlobalChatLightMode GuildMessageHue HideChatGradient "+
                "HideJournalBorder HideJournalSystemPrefix HideJournalTimestamp IgnoreAllianceMessages IgnoreGuildMessages JournalAnchorEnabled "+
                "JournalDarkMode JournalLocked JournalStyle JournalTabs LastJournalTab MaxJournalEntries NativeGlobalChatPreferenceVersion "+
                "OverheadChatWidth PartyMessageHue ResizeJournalSize SaveJournalToFile ScaleSpeechDelay ShowJournalClient ShowJournalGuildAlly "+
                "ShowJournalObjects ShowJournalSystem SpeechDelay SpeechHue UseNativeGlobalChatReplacement WhisperHue YellHue");
            Add(ProfileTransferCategory.Combat,
                "BandageAgentCheckForBuff BandageAgentCheckHidden BandageAgentCheckInvul BandageAgentCheckPoisoned BandageAgentDelay "+
                "BandageAgentGraphic BandageAgentHPPercentage BandageAgentUseNewPacket BandageSelfOld BeneficHue BossHealthBarOrnate "+
                "BossHealthBarWidth BuffBarTime CanAttackHue CastSpellsByOneClick CombatOverlayDefaultsVersion Condition_Duration Condition_Hue "+
                "Condition_Ids Condition_KeepExisting Condition_Label Condition_ReplaceIfExists Condition_Trigger Condition_Type "+
                "CoolDownConditionCount CriminalHue DamageHueAlly DamageHueLastAttck DamageHueOther DamageHuePet DamageHueSelf DamageNumberHue "+
                "DamageNumberSize DisplayRadius DisplayRadiusDistance DisplayRadiusHue EnableBandageAgent EnableSpellIndicators "+
                "EnabledBeneficialCriminalActionQuery EnabledCriminalActionQuery EnabledSpellFormat EnabledSpellHue EnemyHue FastSpellsAssign "+
                "FriendHue HarmfulHue HoldDownKeyTab ImprovedBuffBarHue InnocentHue MurdererHue NetherBlastVortexStyle NeutralHue "+
                "OverheadEffectSizePreset ShowDPS SlayerBarCollapsed SmartCastAutoTarget SpellDisplayFormat UseImprovedBuffBar");
            Add(ProfileTransferCategory.Containers,
                "AltGridContainerBackgroundHue AutoLootHumanCorpses BackpackSections BackpackStyle ConfigurableNegatives ConfigurableProperties "+
                "ConfigurableRarities ConfigurableResistances ConfigurableSlayers ConfigurableSuperSlayers ContainerOpacity ContainerSections "+
                "ContainersScale CorpseContainerOpacity CorpseSections CorpseSingleClickLoot DisableTargetingGridContainers "+
                "DoubleClickToLootInsideContainers EnableAutoLoot EnableAutoLootProgressBar EnableGridContainerAnchor GrabBagSerial GridBorderAlpha "+
                "GridBorderHue GridContainerScaleItems GridContainerSearchMode GridContainersDefaultToOldStyleView GridContainersScale "+
                "GridEnableContPreview GridHighlightSetup GridHighlightSize GridHighlight_AcceptExtraProperties GridHighlight_CorpseOnly "+
                "GridHighlight_ExcludeNegatives GridHighlight_Hue GridHighlight_IsOptionalProperties GridHighlight_Name GridHighlight_PropMinVal "+
                "GridHighlight_PropNames GridHighlight_RequiredRarities GridLootType Grid_BorderStyle Grid_DefaultColumns Grid_DefaultRows "+
                "Grid_HideBorder Grid_UseContainerHue HighlightContainerWhenSelected HueContainerGumps MoveMultiObjectDelay "+
                "NearbyLootConcealsContainerOnOpen NearbyLootGumpHeight NearbyLootOpensHumanCorpses OverrideContainerLocation "+
                "OverrideContainerLocationSetting RelativeDragAndDropItems ScaleItemsInsideContainers SetFavoriteMoveBagSerial "+
                "UseGridLayoutContainerGumps UseLargeContainerGumps UseModernShopGump");
            Add(ProfileTransferCategory.Maps,
                "PathPreviewHue WorldExplorerCompactColumns WorldExplorerCompactHeight WorldExplorerCompactWidth WorldExplorerLocations "+
                "WorldExplorerMinimized WorldExplorerPins WorldExplorerPreferredSources WorldExplorerTheme WorldExplorerTravelMethod "+
                "WorldExplorerTravelStyle WorldMapAllowPositionalTarget WorldMapFlipMap WorldMapFont WorldMapFreeView WorldMapHiddenMarkerFiles "+
                "WorldMapHiddenZoneFiles WorldMapLocked WorldMapMarkerNamesAtAnyZoom WorldMapShowCoordinates WorldMapShowCorpse "+
                "WorldMapShowGridIfZoomed WorldMapShowGroupBar WorldMapShowGroupName WorldMapShowMarkers WorldMapShowMarkersNames "+
                "WorldMapShowMobiles WorldMapShowMouseCoordinates WorldMapShowMultis WorldMapShowParty WorldMapShowPlayerBar WorldMapShowPlayerName "+
                "WorldMapTopMost WorldMapZoomIndex");
            Add(ProfileTransferCategory.Agents,
                "AutomationEnabled BuyAgentEnabled BuyAgentMaxItems BuyAgentMaxUniques EnableRepairBenchAutomation EnableScavenger "+
                "IllustratedVendorGumps SellAgentEnabled SellAgentMaxItems SellAgentMaxUniques VendorGumpHeight VendorLedger "+
                "VendorListingFeePercent VendorListingTemplates VendorRepriceFeePercent VendorSearchScansEnabled VendorWatchItems");
            Add(ProfileTransferCategory.Macros,
                "CounterBarShowHotkeys DisableDefaultHotkeys DisableHotkeys SpellBar_ShowHotkeys");
            Add(ProfileTransferCategory.SpellIcons, "SpellIconScale SpellIcon_DisplayHotkey SpellIcon_HotkeyHue");
            Add(ProfileTransferCategory.InfoBar,
                "InfoBarFont InfoBarFontSize InfoBarHighlightType InfoBarLocked InfoBarSize ShowInfoBar");
            Add(ProfileTransferCategory.GumpLayout,
                "AlertCenterPosition BackpackGridPosition BackpackGridSize BossHealthBarPosition ContainerDefaultPosition CoolDownX CoolDownY "+
                "DragSelectStartX DragSelectStartY EquipmentGuruPosition GameWindowPosition GameWindowSize GridLootPosition "+
                "GumpThemeSelectorPosition ItemFinderPosition JournalPosition LastCorpseContainerPosition LastTargetHealthBarPos "+
                "ModernPaperdollPosition MusicPlayerPosition OverrideContainerLocationPosition PaperdollPosition PaperdollToolsPosition "+
                "PlayerOffset RestockAgentPosition SkillProgressBarPosition StatusGumpPosition TopbarGumpPosition UseLastMovedCooldownPosition "+
                "WindowClientBounds WorldExplorerPosition WorldExplorerTravelPosition WorldMapHeight WorldMapPosition WorldMapWidth");
            Add(ProfileTransferCategory.Enhancements,
                "VisualEnhancements");
            Add(ProfileTransferCategory.General, "ForceResyncOnHang SavedMountSerial");
            Add(ProfileTransferCategory.Interface,
                "AdvancedSkillsGumpHeight AutoOpenXmlGumps LastActiveNameOverheadOption OpenModernPaperdollAtMinimizeLoc PerfHudCollapsed PerfHudTiny " +
                "EnableTitleBarStats TitleBarStatsMode TitleBarUpdateInterval ForceCenterAlignTooltipMobiles ForceTooltipsOnOldClients LeftAlignToolTips DisplaySkillBarOnChange SkillBarFormat");
            Add(ProfileTransferCategory.Combat,
                "CloseHealthBarIfAnchored DisableGrayEnemies EnableHealthIndicator HealthIndicatorWidth HealthLineSizeMultiplier " +
                "OpenHealthBarForLastAttack ShowHealthIndicatorBelow ShowTargetIndicator UseOneHPBarForLastAttack");
            Add(ProfileTransferCategory.Chat, "DisableMouseInteractionOverheadText OverridePartyAndGuildHue");
            Add(ProfileTransferCategory.Maps, "SOSGumpID");
            Add(ProfileTransferCategory.GumpLayout, "BackPackLocked StatusGumpLocked");
            Add(ProfileTransferCategory.GumpLayout, "SlayerBarPosition SlayerBarDetached SlayerBarHorizontal SlayerBarLocked");
            Add(ProfileTransferCategory.Interface, "SlayerBarDefaultHue SlayerBarSlayerHues ProtectedItemHue");
            // Unknown/new options remain transferable through Other, or Select all.
            JsonCategories = typeof(Profile).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() == null
                    && p.Name != nameof(Profile.ProtectedItems)) // Item identities belong to the destination character.
                .ToDictionary(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                    ?? ProfileJsonContext.DefaultToUse.Options.PropertyNamingPolicy.ConvertName(p.Name),
                    p => Members.TryGetValue(p.Name, out var category) ? category : ProfileTransferCategory.Other);
        }

        private static void Add(ProfileTransferCategory category, string names)
        {
            foreach (string name in names.Split(' ')) Members.Add(name, category);
        }
    }
}
