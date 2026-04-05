using Config;
using Config.Common;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.Windows;

namespace TaiwuBossMod_Frontend.Utils
{
    internal class ConfigMergeHelper : MonoBehaviour
    {
        public float DelaySeconds = 0.05f; // adjust as needed

        // Call this to start the delayed merge
        public void StartMerge()
        {
            StartCoroutine(DelayedMergeCoroutine());
        }

        private IEnumerator DelayedMergeCoroutine()
        {
            // Wait a few frames or seconds for the game to finish initializing
            if (DelaySeconds > 0f)
                yield return new WaitForSeconds(DelaySeconds);
            else
                yield return null; // wait 1 frame

            // Call your merge logic
            LoadConfigData();

            UnityEngine.Debug.Log("[ConfigMergeHelper] Merge complete.");

            // Optional: destroy this helper GameObject after done
            Destroy(this.gameObject);
        }

        public static void InitializeModConfig()
        {
            TaiwuBossMod_FrontendPlugin.ModConfigData = DataFileHandler.LoadAllJsons();
        }

        public static void LoadConfigData()
        {
            InitializeModConfig();
            DataFileHandler.LoadAndMergeAll();
        }

    }
}
