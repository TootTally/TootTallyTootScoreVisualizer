using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TootTallyCore.Utils.Assets;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyModules;
using TootTallySettings;
using UnityEngine;
using UnityEngine.UI;
using static Rewired.InputMapper;

namespace TootTallyTootScoreVisualizer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        public const string CONFIGS_FOLDER_NAME = "/TootScoreVisualizer/";
        private const string CONFIG_NAME = "TootScoreVisualizer.cfg";
        private const string SETTINGS_PAGE_NAME = "TootScoreVisualizer";
        public static string currentLoadedConfigName;
        public static bool isTextInitialized;

        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "TootScoreVisualizer"; set => Name = value; }

        public static TootTallySettingPage settingPage;

        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "TootScoreVisualizer", true, "Enables text popup customization when playing notes.");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            AssetManager.LoadSingleAsset(Path.Combine(Path.GetDirectoryName(Instance.Info.Location), "icon.png"), "TootScoreVisualizerIcon.png");

            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            TSVName = config.Bind("Generic", nameof(TSVName), "Default", "Enter the name of your config here. Do not put the .xml extension.");

            config.SettingChanged += Config_SettingChanged;

            string sourceFolderPath = Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), "TootScoreVisualizer");
            string targetFolderPath = Path.Combine(Paths.BepInExRootPath, "TootScoreVisualizer");
            FileHelper.TryMigrateFolder(sourceFolderPath, targetFolderPath, true);

            settingPage = TootTallySettingsManager.AddNewPage(SETTINGS_PAGE_NAME, "TootScoreVisualizer", 40, new UnityEngine.Color(.1f, .1f, .1f, .1f));
            settingPage.AddImageToPageButton("TootScoreVisualizerIcon.png");
            var fileNames = new List<string>();
            if (Directory.Exists(targetFolderPath))
            {
                var filePaths = Directory.GetFiles(targetFolderPath);
                filePaths.ToList().ForEach(d => fileNames.Add(Path.GetFileNameWithoutExtension(d)));
            }
            settingPage.AddDropdown("TSVDropdown", TSVName, fileNames.ToArray());
            ResolvePresets();

            _harmony.PatchAll(typeof(TSVPatches));
            LogInfo($"Module loaded!");
        }
        private void Config_SettingChanged(object sender, SettingChangedEventArgs e)
        {
            ResolvePresets();
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public static class TSVPatches
        {
            public static int noteParticles_index;
            public static float noteScoreAverage;

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OnLoadControllerLoadGameplayAsyncPostfix()
            {
                ResolvePresets();
                isTextInitialized = false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.getScoreAverage))]
            [HarmonyPrefix]

            public static void OnGameControllerGetScoreAveragePrefix(GameController __instance)
            {
                noteScoreAverage = __instance.notescoreaverage;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPrefix]

            public static void OnGameControllerAnimateOutNotePrefix(GameController __instance, ref noteendeffect[] ___allnoteendeffects)
            {
                if (!TSVConfig.configLoaded) return;
                if (!isTextInitialized)
                {
                    foreach (noteendeffect noteendeffect in ___allnoteendeffects)
                    {
                        noteendeffect.combotext_txt_front.supportRichText = noteendeffect.combotext_txt_shadow.supportRichText = true;
                        noteendeffect.combotext_txt_front.horizontalOverflow = noteendeffect.combotext_txt_shadow.horizontalOverflow = HorizontalWrapMode.Overflow;
                        noteendeffect.combotext_txt_front.verticalOverflow = noteendeffect.combotext_txt_shadow.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                    isTextInitialized = true;
                }


                noteParticles_index = __instance.noteparticles_index;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPostfix]
            public static void OnGameControllerAnimateOutNotePostfix(GameController __instance, ref noteendeffect[] ___allnoteendeffects)
            {
                if (!TSVConfig.configLoaded) return;
                Threshold threshold = TSVConfig.GetScoreThreshold(noteScoreAverage);

                noteendeffect currentEffect = ___allnoteendeffects[noteParticles_index];
                currentEffect.combotext_txt_front.text = threshold.GetFormattedText(noteScoreAverage, __instance.multiplier);
                currentEffect.combotext_txt_shadow.text = threshold.GetFormattedTextNoColor(noteScoreAverage, __instance.multiplier);
                currentEffect.combotext_txt_front.color = threshold.color;
            }
        }

        public static void ResolvePresets()
        {
            if (Plugin.currentLoadedConfigName != Plugin.Instance.TSVName.Value)
            {
                Plugin.LogInfo("Config file changed, loading new config");
                TSVConfig.LoadConfig(Plugin.Instance.TSVName.Value);
            }
        }

        //Yoinked that from basegame using DNSpy
        public struct noteendeffect
        {
            // Token: 0x040007C3 RID: 1987
            public GameObject noteeffect_obj;

            // Token: 0x040007C4 RID: 1988
            public RectTransform noteeffect_rect;

            // Token: 0x040007C5 RID: 1989
            public GameObject burst_obj;

            // Token: 0x040007C6 RID: 1990
            public Image burst_img;

            // Token: 0x040007C7 RID: 1991
            public CanvasGroup burst_canvasg;

            // Token: 0x040007C8 RID: 1992
            public GameObject drops_obj;

            // Token: 0x040007C9 RID: 1993
            public CanvasGroup drops_canvasg;

            // Token: 0x040007CA RID: 1994
            public GameObject combotext_obj;

            // Token: 0x040007CB RID: 1995
            public RectTransform combotext_rect;

            // Token: 0x040007CC RID: 1996
            public Text combotext_txt_front;

            // Token: 0x040007CD RID: 1997
            public Text combotext_txt_shadow;
        }

        public ConfigEntry<string> TSVName { get; set; }
    }
}