using Config;
using Config.Common;
using HarmonyLib;
using System;
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
    internal class ConfigHandler
    {
        public static T Get<T>() where T : class, IConfigData
        {
            foreach (var item in ConfigCollection.Items)
            {
                if (item is T t)
                    return t;
            }
            return null;
        }
        public static void MergeListsByTemplateId(object originalListObj, object incomingListObj, Type itemType)
        {
            var originalList = originalListObj as System.Collections.IList;
            var incomingList = incomingListObj as System.Collections.IList;

            if (originalList == null || incomingList == null)
                return;

            // 🔍 Get TemplateId field
            var field = itemType.GetField("TemplateId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                throw new Exception($"Type {itemType.Name} has no TemplateId field");

            // 🔧 Build lookup
            var lookup = new Dictionary<object, int>();

            for (int i = 0; i < originalList.Count; i++)
            {
                var id = field.GetValue(originalList[i]);
                //UnityEngine.Debug.LogWarning($"Original Template ID: {id}");
                if (id != null && !lookup.ContainsKey(id))
                    lookup[id] = i;
            }

            int added = 0;
            int replaced = 0;

            // 🔄 Merge
            foreach (var item in incomingList)
            {
                var id = field.GetValue(item);
                //UnityEngine.Debug.LogWarning($"ModConfig Template ID: {id}");
                if (id == null)
                    continue;

                if (lookup.TryGetValue(id, out int index))
                {
                    originalList[index] = item;
                    replaced++;
                }
                else
                {
                    originalList.Add(item);
                    added++;
                }
            }

            UnityEngine.Debug.LogWarning($"[Merge] {itemType.Name} → Added: {added}, Replaced: {replaced}");
        }
        public static void MergeAllConfigs(Dictionary<string, object> loadedData)
        {
            try
            {
                UnityEngine.Debug.LogWarning("[MergeAllConfigs] Starting merge...");

                foreach (var config in ConfigCollection.Items)
                {
                    try
                    {
                        if (config == null)
                            continue;

                        Type configType = config.GetType();
                        string configName = configType.Name;
                        string itemTypeName = configName + "Item";

                        // 🔍 Check if we have matching JSON data
                        if (!loadedData.TryGetValue(itemTypeName, out var jsonListObj))
                            continue;

                        // 🔍 Get _dataArray field
                        var field = configType.GetField("_dataArray", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (field == null)
                        {
                            UnityEngine.Debug.LogWarning($"[MergeAllConfigs] {configName} has no _dataArray");
                            continue;
                        }

                        var originalList = field.GetValue(config);
                        if (originalList == null)
                            continue;

                        // 🔧 Get list type (List<T>)
                        var listType = originalList.GetType();

                        if (!listType.IsGenericType)
                        {
                            UnityEngine.Debug.LogWarning($"[MergeAllConfigs] {configName} _dataArray is not generic");
                            continue;
                        }

                        Type itemType = listType.GetGenericArguments()[0];

                        // 🔍 Validate type match
                        if (itemType.Name != itemTypeName)
                        {
                            UnityEngine.Debug.LogWarning($"[MergeAllConfigs] Type mismatch: {itemType.Name} != {itemTypeName}");
                            continue;
                        }

                        // 🔄 Merge using reflection
                        MergeListsByTemplateId(originalList, jsonListObj, itemType);

                        // 📊 Logging
                        int count = ((System.Collections.ICollection)originalList).Count;
                        UnityEngine.Debug.LogWarning($"[MergeAllConfigs] Merged {itemTypeName} → New Count: {count}");
                    }
                    catch (Exception innerEx)
                    {
                        UnityEngine.Debug.LogError($"[MergeAllConfigs] ERROR processing config:\n{innerEx}");
                    }
                }

                UnityEngine.Debug.LogWarning("[MergeAllConfigs] Merge complete.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MergeAllConfigs] FATAL ERROR:\n{ex}");
            }
        }
        public static void MergeByTemplateId<T>(List<T> original, List<T> incoming)
        {
            if (original == null || incoming == null)
                return;

            var type = typeof(T);

            // 🔍 Find TemplateId field
            var field = type.GetField("TemplateId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                throw new Exception($"Type {type.Name} does not contain a TemplateId field");

            // 🔧 Build lookup for original list
            var lookup = new Dictionary<object, int>();

            for (int i = 0; i < original.Count; i++)
            {
                var id = field.GetValue(original[i]);
                if (id != null && !lookup.ContainsKey(id))
                    lookup[id] = i;
            }

            // 🔄 Merge incoming
            foreach (var item in incoming)
            {
                var id = field.GetValue(item);

                if (id == null)
                    continue;

                if (lookup.TryGetValue(id, out int index))
                {
                    // 🔁 Overwrite existing
                    original[index] = item;
                }
                else
                {
                    // ➕ Add new
                    original.Add(item);
                }
            }
        }

    }
}
