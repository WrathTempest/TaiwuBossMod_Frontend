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
    internal class Helpers
    {
        public static void LogFields(object obj)
        {
            if (obj == null)
            {
                UnityEngine.Debug.LogWarning("Object is null");
                return;
            }

            Type type = obj.GetType();
            UnityEngine.Debug.LogWarning($"--- Fields of {type.Name} ---");

            // Get all instance fields (public + private)
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                object value = field.GetValue(obj);

                // Optional: handle arrays / lists for nicer logging
                if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    string arrayStr = "[";
                    foreach (var item in enumerable)
                    {
                        arrayStr += item + ",";
                    }
                    arrayStr = arrayStr.TrimEnd(',') + "]";
                    UnityEngine.Debug.LogWarning($"{field.Name} = {arrayStr}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"{field.Name} = {value}");
                }
            }
            UnityEngine.Debug.LogWarning($"--- End of {type.Name} ---");
        }
        public static T GetPrivateField<T>(object instance, string fieldName)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));

            // Use AccessTools to get the field
            return AccessTools.FieldRefAccess<T>(instance.GetType(), fieldName)(instance);
        }
        public static void SetPrivateField<T>(object instance, string fieldName, T newValue)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(fieldName)) throw new ArgumentNullException(nameof(fieldName));

            Type type = instance.GetType();
            FieldInfo field = null;

            while (type != null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (field != null)
                    break;

                type = type.BaseType;
            }

            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, fieldName);

            field.SetValue(instance, newValue);
        }

        public static T GetPrivateProperty<T>(object instance, string propertyName)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = instance.GetType();

            // Try property first
            var prop = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (prop != null)
            {
                return (T)prop.GetValue(instance, null);
            }

            // Fallback: try getter method directly (get_PropertyName)
            var getter = type.GetMethod(
                "get_" + propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (getter != null)
            {
                return (T)getter.Invoke(instance, null);
            }

            throw new MissingMemberException(type.FullName, propertyName);
        }

        public static void SetPrivateProperty<T>(object instance, string propertyName, T value)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = instance.GetType();

            var prop = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (prop != null)
            {
                prop.SetValue(instance, value, null);
                return;
            }

            var setter = type.GetMethod(
                "set_" + propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (setter != null)
            {
                setter.Invoke(instance, new object[] { value });
                return;
            }

            throw new MissingMemberException(type.FullName, propertyName);
        }

        public static string GetRealCaller(int skipFrames = 1)
        {
            var stackTrace = new StackTrace(skipFrames, true);
            foreach (var frame in stackTrace.GetFrames())
            {
                MethodBase method = frame.GetMethod();
                if (method == null) continue;

                // Skip Harmony-generated dynamic methods
                if (method.Name.Contains("DMD<")) continue;

                // Skip helper class itself
                if (method.DeclaringType == typeof(Helpers)) continue;

                return $"{method.DeclaringType.FullName}.{method.Name}";
            }

            return "UnknownCaller";
        }

        /// <summary>
        /// Logs a traced call for debugging.
        /// </summary>
        public static void LogCaller(string message = "", int skipFrames = 1)
        {
            string caller = GetRealCaller(skipFrames + 1); // +1 for hero.battleDataBehaviour.battleData method
            //Main.Log.LogInfo($"{message} Called by: {caller}");
        }

        public static object Call(
        object instance,
        string methodName,
        params object[] args)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();

            MethodInfo method = AccessTools.Method(type, methodName);

            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);

            return method.Invoke(instance, args);
        }

        public static object CallStatic(
            Type type,
            string methodName,
            params object[] args)
        {
            MethodInfo method = AccessTools.Method(type, methodName);

            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);

            return method.Invoke(null, args);
        }

        public static void DumpSprite(Sprite sprite, string fileName)
        {
            if (sprite == null)
                return;

            Texture2D source = sprite.texture;
            Rect rect = sprite.rect;

            Texture2D readableTex;

            // If texture is readable we can use it directly
            if (source.isReadable)
            {
                readableTex = source;
            }
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.sRGB
                );

                Graphics.Blit(source, rt);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                readableTex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readableTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readableTex.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }

            // Crop sprite from atlas
            Texture2D tex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false, false);

            Color[] pixels = readableTex.GetPixels(
                (int)rect.x,
                (int)rect.y,
                (int)rect.width,
                (int)rect.height
            );

            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();


            string dumpDir = System.IO.Path.Combine(TaiwuBossMod_FrontendPlugin.pluginDir, "Image", "Dump");

            Directory.CreateDirectory(dumpDir);

            string path = System.IO.Path.Combine(dumpDir, fileName + ".png");

            File.WriteAllBytes(path, png);
        }

        public static Dictionary<string, Sprite> LoadReplacementSprites()
        {
            Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
            string replaceDir = Path.Combine(TaiwuBossMod_FrontendPlugin.pluginDir, "Image", "Replace");

            if (!Directory.Exists(replaceDir))
                return sprites;

            string[] files = Directory.GetFiles(replaceDir, "*.png");

            foreach (string file in files)
            {
                byte[] data = File.ReadAllBytes(file);

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(data);

                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                string key = System.IO.Path.GetFileNameWithoutExtension(file);

                sprites[key] = sprite;
            }

            return sprites;
        }

        public static Dictionary<string, TMP_SpriteAsset> LoadAllSpriteAssets(string folderPath)
        {
            var dict = new Dictionary<string, TMP_SpriteAsset>();

            if (!Directory.Exists(folderPath))
                return dict;

            string[] files = Directory.GetFiles(folderPath, "*.png");

            foreach (string file in files)
            {
                string key = Path.GetFileNameWithoutExtension(file);

                var asset = CreateSpriteAsset(file, key);

                if (asset != null && !dict.ContainsKey(key))
                    dict.Add(key, asset);
            }

            return dict;
        }

        private static TMP_SpriteAsset CreateSpriteAsset(string filePath, string spriteName)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(fileData);

                // Create TMP Sprite Asset
                TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                spriteAsset.spriteSheet = texture;

                // Create sprite
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );

                // Create glyph
                TMP_SpriteGlyph glyph = new TMP_SpriteGlyph
                {
                    index = 0,
                    sprite = sprite,
                    glyphRect = new GlyphRect(0, 0, texture.width, texture.height),
                    metrics = new GlyphMetrics(
                        texture.width,
                        texture.height,
                        0,
                        texture.height,
                        texture.width
                    ),
                    scale = 1.0f
                };

                // Add glyph (IMPORTANT: use Add, not assignment)
                spriteAsset.spriteGlyphTable.Add(glyph);

                // Create character
                TMP_SpriteCharacter character = new TMP_SpriteCharacter(0, glyph)
                {
                    name = spriteName
                };

                // Add character
                spriteAsset.spriteCharacterTable.Add(character);

                // Finalize
                spriteAsset.UpdateLookupTables();

                return spriteAsset;
            }
            catch
            {
                return null;
            }
        }
    }
}
