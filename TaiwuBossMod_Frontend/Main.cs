using Config;
using GameData.Domains.Mod;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaiwuBossMod;
using TaiwuBossMod_Frontend.Utils;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuBossMod_Frontend
{
    [PluginConfig("TaiwuBossMod", "Izayoixx", "1.0.0")]
    public class TaiwuBossMod_FrontendPlugin : TaiwuRemakePlugin
    {
        private const string MyGUID = "com.Taba.TaiwuBossMod_Frontend";
        //private const string PluginName = "TaiwuBossMod_Frontend";
        //private const string VersionString = "1.0.0";
        public static string pluginDir;
        public static Dictionary<string, object> ModConfigData;

        private Harmony harmony;

        public override void Initialize()
        {
            Debug.LogWarning($"Loading FrontEnd Plugin!");
            this.harmony = Harmony.CreateAndPatchAll(typeof(TaiwuBossMod_FrontendPlugin), null);
            pluginDir = ModManager.GetModInfo(base.ModIdStr).DirectoryName;
            GameObject helperGO = new GameObject("ConfigMergeHelper");
            ConfigMergeHelper helper = helperGO.AddComponent<ConfigMergeHelper>();
            helper.StartMerge();

        }
        public override void Dispose()
        {
            bool flag = this.harmony != null;
            if (flag)
            {
                this.harmony.UnpatchSelf();
            }
        }
               

    }
}
