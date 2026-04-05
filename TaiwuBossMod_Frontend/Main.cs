using Config;
using GameData.Domains.Character;
using GameData.Domains.Character.Display;
using GameData.Domains.Item.Display;
using GameData.Domains.Mod;
using GameData.Utilities;
using HarmonyLib;
using Newtonsoft.Json;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaiwuBossMod;
using TaiwuBossMod_Backend;
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
        public static int BossID = 14;

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
        public override void OnModSettingUpdate()
        {
            ModManager.GetSetting(base.ModIdStr, "BossID", ref BossID);
            if (BossID >= 14 || BossID == 10)
            {
                BossID = -1;
            }
            //DumpSectIds();
        }
        public override void Dispose()
        {
            bool flag = this.harmony != null;
            if (flag)
            {
                this.harmony.UnpatchSelf();
            }
        }

        [HarmonyPatch(typeof(UI_Combat), "InitSkeleton")]
        [HarmonyPrefix]
        public static void ChangeSkeleton(UI_Combat __instance, SkeletonAnimation skeleton, bool isAlly, int charId, bool isMainCharacter)
        {
            Debug.LogWarning($"In InitSkeleton Patch!");
            if (!isMainCharacter) return;
            if (SingletonObject.getInstance<BasicGameData>().TaiwuCharId != charId) return;
            if (!IsBossEnabled())
            {
                RefreshCharId2BossId();
            }
            else
            {
                var dict = Helpers.GetPrivateProperty<IReadOnlyDictionary<int, CharacterDisplayData>>(__instance, "_charDisplayDataDict");
                short charTemplateId = dict[charId].TemplateId;
                AppendTemplateId2BossID(charTemplateId);
            }
            
        }

        [HarmonyPatch(typeof(UI_Combat), "TryGetBossConfig")]
        [HarmonyPostfix]
        public static void BossPreload(UI_Combat __instance, int charId, ref bool __result)
        {
            if (!IsBossEnabled())
            {
                RefreshCharId2BossId();
            }
            else
            {
                if (SingletonObject.getInstance<BasicGameData>().TaiwuCharId != charId) return;
                // Debug.LogWarning($"Attempting to append taiwu to bossdict");
                var dict = Helpers.GetPrivateProperty<IReadOnlyDictionary<int, CharacterDisplayData>>(__instance, "_charDisplayDataDict");
                short charTemplateId = dict[SingletonObject.getInstance<BasicGameData>().TaiwuCharId].TemplateId;
                AppendTemplateId2BossID(charTemplateId);
                __result = true;
            }
            
        }

        [HarmonyPatch(typeof(SpineAnimationUtils), "UpdateSkeleton", new Type[] {typeof(SkeletonAnimation), typeof(CharacterDisplayData), typeof(List<ItemDisplayData>), typeof(List<sbyte>) })]
        [HarmonyPostfix]
        public static void ChangeSkeletonSize(SkeletonAnimation skeletonAnimation, CharacterDisplayData charData)
        {
            if (charData.TitleIds == null) return;
            if (!charData.TitleIds.Contains(43)) return;
            Debug.LogWarning($"Increasing Player Scale!");
            if (IsBoss(charData.TemplateId) && IsBossEnabled())
            {
                skeletonAnimation.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            }
            else
            {
                skeletonAnimation.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
            }
            
        }

        [HarmonyPatch(typeof(NameCenter), "FormatName")]
        [HarmonyPostfix]
        public static void GetName( ref string __result)
        {
            if (__result.Contains("QinXue") || __result.Contains("Qin Xue"))
            __result = "The Heavenly Demon, <color=#FF2000>Noir</color>";
        }

        public static bool IsBossEnabled()
        {
            if (BossID < 0 || BossID >= Boss.Instance.Count || Boss.Instance[BossID] == null) return false;
            return true;
        }

        public static bool IsBoss(short templateId)
        {
            if (UI_Combat.CharId2BossId.ContainsKey(templateId)) return true;
            return false;
        }

        public static void RefreshCharId2BossId()
        {
            //reset
            UI_Combat.CharId2BossId.Clear();
            sbyte bossId = 0;
            while ((int)bossId < Boss.Instance.Count)
            {
                short[] charIdList = Boss.Instance[bossId].CharacterIdList;
                foreach (short charId in charIdList)
                {
                    UI_Combat.CharId2BossId[charId] = bossId;
                }
                bossId += 1;
            }
        }

        public static void AppendTemplateId2BossID(short templateId)
        {
            //reset
            if (UI_Combat.CharId2BossId.ContainsKey(templateId) && UI_Combat.CharId2BossId[templateId] == BossID) return;
            UI_Combat.CharId2BossId.Clear();
            sbyte bossId = 0;
            while ((int)bossId < Boss.Instance.Count)
            {
                short[] charIdList = Boss.Instance[bossId].CharacterIdList;
                foreach (short charId in charIdList)
                {
                    UI_Combat.CharId2BossId[charId] = bossId;
                }
                bossId += 1;
            }
            UI_Combat.CharId2BossId[templateId] = (sbyte)BossID;
            foreach (short id in Boss.Instance[BossID].CharacterIdList)
            {
                CharacterItem charConfig = Character.Instance[id];
                if (charConfig.SpecialCombatSkeleton != -1)
                {
                    CharacterItem taiwuConfig = Character.Instance[templateId];
                    Helpers.SetPrivateField<sbyte>(taiwuConfig, "SpecialCombatSkeleton", charConfig.SpecialCombatSkeleton);
                    break;
                }
            }
        }


    }
}
