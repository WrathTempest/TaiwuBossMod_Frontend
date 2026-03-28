using Config;
using GameData.Domains.Character;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;
using TaiwuBossMod_Frontend.Utils;

namespace TaiwuBossMod_Frontend.Utils
{
    public static class DataFileHandler
    {
        private static string pluginDir = TaiwuBossMod_FrontendPlugin.pluginDir;
        // 🔧 Change this if you want a different folder name
        public static string FolderName = "ModConfig";

        // 🔧 Base directory (auto = plugin location)
        private static string BasePath => TaiwuBossMod_FrontendPlugin.pluginDir;

        private static string FullFolderPath => Path.Combine(BasePath, FolderName);

        public static List<T> GetList<T>(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue(typeof(T).Name, out var obj))
                return obj as List<T>;

            return new List<T>();
        }

        public static List<object> CreateObjectsFromJson(Type itemType, object baseItem, string json)
        {
            var result = new List<object>();

            try
            {
                // 🔧 Deserialize into Dictionary list (raw data)
                var rawList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

                if (rawList == null)
                    return result;

                foreach (var dict in rawList)
                {
                    // 🔁 Clone base item
                    var newItem = Clone(baseItem);

                    // 🧠 Apply values
                    ApplyValues(newItem, dict);

                    result.Add(newItem);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreateObjectsFromJson] ERROR:\n{ex}");
            }

            return result;
        }

        public static object Clone(object obj)
        {
            var method = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(obj, null);
        }

        public static void ApplyValues(object target, Dictionary<string, object> data)
        {
            var type = target.GetType();

            foreach (var kvp in data)
            {
                try
                {
                    string name = kvp.Key;
                    object value = kvp.Value;

                    // 🔍 Try field first
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (field != null)
                    {
                        object converted = ConvertValue(value, field.FieldType);
                        field.SetValue(target, converted);
                        continue;
                    }

                    // 🔍 Try property
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (prop != null && prop.CanWrite)
                    {
                        object converted = ConvertValue(value, prop.PropertyType);
                        prop.SetValue(target, converted, null);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ApplyValues] Failed on {kvp.Key}: {ex.Message}");
                }
            }
        }

        public static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            try
            {
                // 🔁 Handle JToken (Newtonsoft internal)
                if (value is Newtonsoft.Json.Linq.JToken token)
                    value = token.ToObject(targetType);

                // ✅ Direct assign
                if (targetType.IsAssignableFrom(value.GetType()))
                    return value;

                // 🔢 Enum
                if (targetType.IsEnum)
                    return Enum.ToObject(targetType, Convert.ToInt32(value));

                // 📦 Arrays
                if (targetType.IsArray && value is Newtonsoft.Json.Linq.JArray jArray)
                {
                    var elementType = targetType.GetElementType();
                    var array = Array.CreateInstance(elementType, jArray.Count);

                    for (int i = 0; i < jArray.Count; i++)
                    {
                        var elem = ConvertValue(jArray[i], elementType);
                        array.SetValue(elem, i);
                    }

                    return array;
                }

                // 📚 List<T>
                if (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var list = (System.Collections.IList)Activator.CreateInstance(targetType);

                    var jArray2 = value as Newtonsoft.Json.Linq.JArray;

                    if (jArray2 != null)
                    {
                        foreach (var item in jArray2)
                        {
                            list.Add(ConvertValue(item, elementType));
                        }
                    }

                    return list;
                }

                // 🔢 Primitive conversion
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        public static void LoadAndMergeAll()
        {
            var files = Directory.GetFiles(FullFolderPath, "*.json");

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                foreach (var config in ConfigCollection.Items)
                {
                    var configType = config.GetType();
                    var expectedItemName = configType.Name + "Item";

                    if (expectedItemName != fileName)
                        continue;

                    // 🔍 Get _dataArray
                    var field = configType.GetField("_dataArray", BindingFlags.NonPublic | BindingFlags.Instance);
                    var list = field?.GetValue(config) as System.Collections.IList;

                    if (list == null || list.Count == 0)
                        continue;

                    // 🔧 Get item type
                    var itemType = list.GetType().GetGenericArguments()[0];

                    // 🔁 Use first item as base
                    var baseItem = list[0];

                    // 📖 Read JSON
                    string json = File.ReadAllText(file);

                    var newItems = CreateObjectsFromJson(itemType, baseItem, json);

                    // 🔄 Merge
                    ConfigHandler.MergeListsByTemplateId(list, newItems, itemType);

                    Debug.LogWarning($"[LoadAndMergeAll] Merged {fileName}");
                }
            }
        }

        public static Dictionary<string, object> LoadAllJsons()
        {
            var result = new Dictionary<string, object>();

            try
            {
                EnsureFolder();

                var files = Directory.GetFiles(FullFolderPath, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);

                        // 🔍 Find matching type in loaded assemblies
                        Type targetType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a =>
                            {
                                try { return a.GetTypes(); }
                                catch { return new Type[0]; }
                            })
                            .FirstOrDefault(t => t.Name == fileName);

                        if (targetType == null)
                        {
                            LogError("LoadAllJsonAsLists", new Exception($"Type not found for {fileName}"));
                            continue;
                        }

                        // 🔧 Create List<T> dynamically
                        Type listType = typeof(List<>).MakeGenericType(targetType);

                        string json = File.ReadAllText(file);
                        var data = JsonConvert.DeserializeObject(json, listType);

                        if (data == null)
                            continue;

                        result[fileName] = data;
                    }
                    catch (Exception innerEx)
                    {
                        LogError($"LoadAllJsonAsLists ({file})", innerEx);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("LoadAllJsonAsLists (FATAL)", ex);
            }

            return result;
        }

        // ✅ Ensure folder exists
        private static void EnsureFolder()
        {

            if (!Directory.Exists(FullFolderPath))
                Directory.CreateDirectory(FullFolderPath);
        }

        // ✅ Build file path based on TYPE NAME
        private static string GetFilePath(Type type)
        {
            return Path.Combine(FullFolderPath, $"{type.Name}.json");
        }

        // =========================================================
        // 🔹 DUMP
        // =========================================================
        public static void Dump<T>(IEnumerable<T> data)
        {
            try
            {
                EnsureFolder();

                var path = GetFilePath(typeof(T));

                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                LogError("Dump", ex);
            }
        }

        // Overload if you want to manually specify name (rarely needed)
        public static void Dump<T>(string fileName, IEnumerable<T> data)
        {
            try
            {
                EnsureFolder();

                if (fileName != typeof(T).Name)
                    throw new Exception($"Filename must match type name: {typeof(T).Name}");

                var path = Path.Combine(FullFolderPath, $"{fileName}.json");

                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                LogError("Dump (custom name)", ex);
            }
        }

        // =========================================================
        // 🔹 LOAD
        // =========================================================
        public static List<T> Load<T>()
        {
            try
            {
                EnsureFolder();

                var type = typeof(T);
                var path = GetFilePath(type);

                // ❌ No file = return empty list (safe for merging)
                if (!File.Exists(path))
                    return new List<T>();

                string json = File.ReadAllText(path);

                var data = JsonConvert.DeserializeObject<List<T>>(json);

                // Null safety
                return data ?? new List<T>();
            }
            catch (Exception ex)
            {
                LogError("Load", ex);
                return new List<T>();
            }
        }

        // =========================================================
        // 🔹 VALIDATION (optional strict check)
        // =========================================================
        public static bool ValidateFileName<T>(string filePath)
        {
            var expected = typeof(T).Name + ".json";
            var actual = Path.GetFileName(filePath);

            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // 🔹 LOGGING
        // =========================================================
        private static void LogError(string context, Exception ex)
        {
            try
            {
                EnsureFolder();

                string path = Path.Combine(FullFolderPath, "error.log");

                File.AppendAllText(path,
                    $"[{DateTime.Now}] {context} ERROR:\n{ex}\n\n");
            }
            catch
            {
                // swallow completely (never crash game)
            }
        }

        public static void DumpConfig<TConfig, TItem>() where TConfig : class
        {
            try
            {
                Debug.LogWarning($"Loading Config Files for {typeof(TConfig).Name}...");

                // 🔍 Get instance from ConfigCollection
                var instance = ConfigCollection.Items.OfType<TConfig>().FirstOrDefault();

                if (instance == null)
                {
                    Debug.LogError($"[DumpConfig] Could not find instance of {typeof(TConfig).Name}");
                    return;
                }

                // 🔍 Get _dataArray via reflection
                var dataArray = Helpers.GetPrivateField<List<TItem>>(instance, "_dataArray");

                if (dataArray == null)
                {
                    Debug.LogError($"[DumpConfig] _dataArray is null for {typeof(TConfig).Name}");
                    return;
                }

                Debug.LogWarning($"Plugin Directory: {pluginDir}");
                Debug.LogWarning($"Dumping {dataArray.Count} entries of {typeof(TItem).Name}");

                // 💾 Dump
                DataFileHandler.Dump<TItem>(dataArray);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DumpConfig] ERROR:\n{ex}");
            }
        }

        public static void DumpAllConfigs()
        {
            try
            {
                Debug.LogWarning($"[DumpAllConfigs] Starting dump...");

                foreach (var config in ConfigCollection.Items)
                {
                    try
                    {
                        if (config == null)
                            continue;

                        Type configType = config.GetType();
                        string configName = configType.Name;
                        string itemTypeName = configName + "Item";

                        Debug.LogWarning($"[DumpAllConfigs] Processing {configName}...");

                        // 🔍 Find _dataArray field
                        var field = configType.GetField("_dataArray", BindingFlags.NonPublic | BindingFlags.Instance);

                        if (field == null)
                        {
                            Debug.LogWarning($"[DumpAllConfigs] Skipped {configName} (no _dataArray)");
                            continue;
                        }

                        var data = field.GetValue(config);

                        if (data == null)
                        {
                            Debug.LogWarning($"[DumpAllConfigs] Skipped {configName} (_dataArray is null)");
                            continue;
                        }

                        // 🔍 Try to resolve item type (optional, for naming consistency)
                        Type itemType = configType.Assembly.GetTypes()
                            .FirstOrDefault(t => t.Name == itemTypeName);

                        string fileName = itemType != null ? itemType.Name : configName;

                        // 📁 Build path
                        string folder = Path.Combine(pluginDir, "GameDataDump");
                        Directory.CreateDirectory(folder);

                        string path = Path.Combine(folder, fileName + ".json");

                        // 💾 Serialize
                        string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                        File.WriteAllText(path, json);

                        // 📊 Count (if it's a collection)
                        int count = 0;
                        if (data is System.Collections.ICollection collection)
                            count = collection.Count;

                        Debug.LogWarning($"[DumpAllConfigs] Dumped {fileName} ({count} entries)");
                    }
                    catch (Exception innerEx)
                    {
                        Debug.LogError($"[DumpAllConfigs] ERROR processing config:\n{innerEx}");
                    }
                }

                Debug.LogWarning($"[DumpAllConfigs] Dump complete.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DumpAllConfigs] FATAL ERROR:\n{ex}");
            }
        }
    }
}