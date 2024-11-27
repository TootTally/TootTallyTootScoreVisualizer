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
            if (Application.version.CompareTo("1.19A") < 0)
            {
                Plugin.LogInfo("Please install Trombone Champ 1.19A version or later.");
                return;
            }

            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "TootScoreVisualizer", true, "Enables text popup customization when playing notes.");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            TSVName = config.Bind("Generic", nameof(TSVName), "Default", "Enter the name of your config here. Do not put the .xml extension.");

            config.SettingChanged += Config_SettingChanged;

            string sourceFolderPath = Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), "TootScoreVisualizer");
            string targetFolderPath = Path.Combine(Paths.BepInExRootPath, "TootScoreVisualizer");
            FileHelper.TryMigrateFolder(sourceFolderPath, targetFolderPath, true);

            settingPage = TootTallySettingsManager.AddNewPage(SETTINGS_PAGE_NAME, "TootScoreVisualizer", 40, new UnityEngine.Color(.1f, .1f, .1f, .1f));
            var fileNames = new List<string>();
            if (Directory.Exists(targetFolderPath))
            {
                var filePaths = Directory.GetFiles(targetFolderPath);
                filePaths.ToList().ForEach(d => fileNames.Add(Path.GetFileNameWithoutExtension(d)));
            }
            settingPage.AddDropdown("TSVDropdown", TSVName, fileNames.ToArray());
            ResolvePresets();

            TootTallySettings.Plugin.TryAddThunderstoreIconToPageButton(Instance.Info.Location, Name, settingPage);

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
            public static void OnLoadControllerLoadGameplayAsyncPostfix(GameController __instance)
            {
                ResolvePresets();
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.doScoreText))]
            [HarmonyPrefix]

            public static void OnGameControllerGetScoreAveragePrefix(GameController __instance)
            {
                noteScoreAverage = __instance.notescoreaverage;
            }

            private static int _lastNoteEffectIndex;

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPrefix]
            public static void OnGameControllerAnimateOutNotePrefix(GameController __instance)
            {
                _lastNoteEffectIndex = __instance.noteparticles_index;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPostfix]
            public static void OnGameControllerAnimateOutNotePostfix(GameController __instance, ref noteendeffect[] ___allnoteendeffects)
            {
                if (!TSVConfig.configLoaded) return;
                Threshold threshold = TSVConfig.GetScoreThreshold(noteScoreAverage);

                noteendeffect currentEffect = ___allnoteendeffects[_lastNoteEffectIndex];
                currentEffect.combotext_txt_front.horizontalOverflow = currentEffect.combotext_txt_shadow.horizontalOverflow = HorizontalWrapMode.Overflow;
                currentEffect.combotext_txt_front.verticalOverflow = currentEffect.combotext_txt_shadow.verticalOverflow = VerticalWrapMode.Overflow;
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
            // Token: 0x04000E4F RID: 3663
            public GameObject noteeffect_obj;

            // Token: 0x04000E50 RID: 3664
            public RectTransform noteeffect_rect;

            // Token: 0x04000E51 RID: 3665
            public GameObject burst_obj;

            // Token: 0x04000E52 RID: 3666
            public Image burst_img;

            // Token: 0x04000E53 RID: 3667
            public CanvasGroup burst_canvasg;

            // Token: 0x04000E54 RID: 3668
            public ParticleSystem burst_particles;

            // Token: 0x04000E55 RID: 3669
            public GameObject drops_obj;

            // Token: 0x04000E56 RID: 3670
            public CanvasGroup drops_canvasg;

            // Token: 0x04000E57 RID: 3671
            public GameObject combotext_obj;

            // Token: 0x04000E58 RID: 3672
            public RectTransform combotext_rect;

            // Token: 0x04000E59 RID: 3673
            public Text combotext_txt_front;

            // Token: 0x04000E5A RID: 3674
            public Text combotext_txt_shadow;
        }

        public ConfigEntry<string> TSVName { get; set; }
    }
}