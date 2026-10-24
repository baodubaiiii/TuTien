using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TuTien.Client.Editor
{
    public static partial class DemoBuilder
    {
        private static TMP_FontAsset font;
        private static Sprite square;
        private static readonly Color Ink = new Color(0.15f, 0.22f, 0.19f);
        private static readonly Color Paper = new Color(0.94f, 0.92f, 0.85f);
        private static readonly Color Jade = new Color(0.16f, 0.4f, 0.33f);

        [MenuItem("Tu Tien/Build Demo")]
        public static void Build()
        {
            string[] paths = { "Assets/Scenes", "Assets/Resources/Prefabs", "Assets/Resources/Fonts", "Assets/Prefabs/World", "Assets/Art" };
            foreach (string path in paths)
            {
                Directory.CreateDirectory(path);
            }

            AssetDatabase.Refresh();
            string essentials = "Library/PackageCache/com.unity.textmeshpro@3.0.6/Package Resources/TMP Essential Resources.unitypackage";
            if (!AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                AssetDatabase.ImportPackage(essentials, false);
            }
            AssetDatabase.Refresh();
            CreateFont();
            CreateSquare();
            CreateRoot();
            CreateItemSlot();
            CreateStatRow();
            CreateCharacterPreview();
            CreateInventory();
            CreateWorldPrefabs();
            BuildLoading();
            BuildLogin();
            BuildSelect();
            BuildCreate();
            BuildWorld();
            string[] scenes = { "LoadingScene", "LoginScene", "CharacterSelectScene", "CharacterCreateScene", "WorldScene" };
            EditorBuildSettings.scenes = scenes.Select(name => new EditorBuildSettingsScene("Assets/Scenes/" + name + ".unity", true)).ToArray();
            PlayerSettings.companyName = "TuTien";
            PlayerSettings.productName = "TuTienClient";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            BuildBundles();
            EditorSceneManager.OpenScene("Assets/Scenes/LoadingScene.unity");
            Debug.Log("Tu Tien demo scenes, prefabs and CDN bundle built.");
        }

        private static void CreateFont()
        {
            string path = "Assets/Resources/Fonts/FreemanSDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font && font.material && font.atlasTexture)
            {
                return;
            }

            if (font)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Freeman-Regular.ttf");
            font = TMP_FontAsset.CreateFontAsset(source);
            font.name = "FreemanSDF";
            font.isMultiAtlasTexturesEnabled = true;
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ĐđÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸàáâãèéêìíòóôõùúăđĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ%+-/:().,·", out _);
            Material material = font.material;
            Texture2D[] textures = font.atlasTextures;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(material, font);
            foreach (Texture2D atlas in textures)
            {
                if (atlas)
                {
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
            font.material = material;
            font.atlasTextures = textures;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        private static void CreateSquare()
        {
            string path = "Assets/Art/Square.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(16, 16);
                texture.SetPixels(Enumerable.Repeat(Color.white, 256).ToArray());
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            square = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateRoot()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ServerSettings>("Assets/Resources/ServerSettings.asset");
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<ServerSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Resources/ServerSettings.asset");
            }
            var root = new GameObject("GameRoot");
            root.AddComponent<GameClient>().Settings = settings;
            root.AddComponent<DemoSmoke>();
            SavePrefab(root, "Assets/Resources/Prefabs/GameRoot.prefab");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var value = new GameObject(name, typeof(RectTransform));
            var rect = value.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float sizeFont = 26, Color? color = null)
        {
            size.y = Mathf.Max(size.y, sizeFont * 1.6f);
            var rect = Rect(name, parent, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = sizeFont;
            label.color = color ?? Ink;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static Camera NewScene(string name, out RectTransform canvas)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f, 0.91f, 0.87f);
            camera.transform.position = new Vector3(0, 0, -10);
            canvas = Rect("Canvas", null, Vector2.zero, new Vector2(1600, 900));
            var renderCanvas = canvas.gameObject.AddComponent<Canvas>();
            renderCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            renderCanvas.worldCamera = camera;
            renderCanvas.planeDistance = 1;
            renderCanvas.sortingOrder = 10000;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return camera;
        }

        private static void SaveScene(string name)
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/" + name + ".unity");
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static SpriteRenderer WorldSprite(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var root = new GameObject(name, typeof(SpriteRenderer));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = scale;
            var sprite = root.GetComponent<SpriteRenderer>();
            sprite.sprite = square;
            sprite.color = color;
            return sprite;
        }

        private static void CreateWorldPrefabs()
        {
            var mapObject = new GameObject("MapRoot");
            var map = mapObject.AddComponent<MapView>();
            map.Sprite = square;
            map.Font = font;
            map.Floor = WorldSprite("Floor", mapObject.transform, new Vector3(8, -4.5f), new Vector3(16, 9), Jade).transform;
            map.Floor.GetComponent<SpriteRenderer>().sortingOrder = -100;
            map.Obstacles = new GameObject("Terrain").transform;
            map.Obstacles.SetParent(mapObject.transform);
            SavePrefab(mapObject, "Assets/Prefabs/World/MapRoot.prefab");
            foreach (string name in new[] { "Player", "RemotePlayer", "Npc", "Monster" })
            {
                var actor = new GameObject(name, typeof(CharacterView), typeof(CircleCollider2D));
                actor.GetComponent<CircleCollider2D>().radius = 0.4f;
                var view = actor.GetComponent<CharacterView>();
                view.Selection = WorldSprite("Selection", actor.transform, new Vector3(0, 0.25f), new Vector3(0.75f, 0.85f), new Color(1, 0.85f, 0.3f, 0.65f));
                view.Selection.enabled = false;
                WorldSprite("Shadow", actor.transform, new Vector3(0, -0.1f), new Vector3(0.65f, 0.12f), new Color(0, 0, 0, 0.5f));
                view.Body = WorldSprite("Body", actor.transform, new Vector3(0, 0.22f), new Vector3(0.42f, 0.55f), Jade);
                WorldSprite("Head", actor.transform, new Vector3(0, 0.65f), new Vector3(0.27f, 0.27f), new Color(0.9f, 0.77f, 0.58f));
                view.Health = WorldSprite("Health", actor.transform, new Vector3(0, 0.98f), new Vector3(0.6f, 0.045f), new Color(0.3f, 0.9f, 0.4f)).transform;
                var text = new GameObject("Name", typeof(TextMeshPro));
                text.transform.SetParent(actor.transform, false);
                text.transform.localPosition = new Vector3(0, 1.23f, 0);
                view.Name = text.GetComponent<TextMeshPro>();
                view.Name.font = font;
                view.Name.fontSize = 2;
                view.Name.alignment = TextAlignmentOptions.Center;
                view.Name.color = Color.white;
                view.Name.rectTransform.sizeDelta = new Vector2(4, 0.4f);
                SavePrefab(actor, "Assets/Prefabs/World/" + name + ".prefab");
            }
            var item = new GameObject("ItemMap", typeof(ItemMapView), typeof(CircleCollider2D));
            item.GetComponent<CircleCollider2D>().radius = 0.4f;
            WorldSprite("Icon", item.transform, Vector3.zero, new Vector3(0.28f, 0.28f), new Color(0.91f, 0.76f, 0.34f)).sortingOrder = 1000;
            var itemName = new GameObject("Name", typeof(TextMeshPro));
            itemName.transform.SetParent(item.transform, false);
            itemName.transform.localPosition = new Vector3(0, 0.5f, 0);
            var itemText = itemName.GetComponent<TextMeshPro>();
            itemText.font = font;
            itemText.fontSize = 1.7f;
            itemText.alignment = TextAlignmentOptions.Center;
            itemText.rectTransform.sizeDelta = new Vector2(4, 0.6f);
            itemText.GetComponent<MeshRenderer>().sortingOrder = 3000;
            item.GetComponent<ItemMapView>().Name = itemText;
            SavePrefab(item, "Assets/Prefabs/World/ItemMap.prefab");
            var effect = new GameObject("FloatingText", typeof(TextMeshPro), typeof(FloatingText));
            var label = effect.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 3;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(5, 0.6f);
            label.sortingOrder = 5000;
            effect.GetComponent<FloatingText>().Text = label;
            SavePrefab(effect, "Assets/Resources/Prefabs/FloatingText.prefab");
        }

        [MenuItem("Tu Tien/Build CDN Bundles")]
        public static void BuildBundles()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string output = Path.Combine(root, "content/published/unity");
            Directory.CreateDirectory(output);
            string[] names = { "MapRoot", "Player", "RemotePlayer", "Npc", "Monster", "ItemMap" };
            var build = new AssetBundleBuild
            {
                assetBundleName = "world",
                assetNames = names.Select(name => "Assets/Prefabs/World/" + name + ".prefab").ToArray(),
                addressableNames = names
            };
            var result = BuildPipeline.BuildAssetBundles(output, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (!result)
            {
                throw new IOException("Unity bundle build failed.");
            }

            byte[] bytes = File.ReadAllBytes(Path.Combine(output, "world"));
            string hash;
            using (var sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }

            File.WriteAllText(Path.Combine(output, "manifest.json"), new JObject
            {
                ["version"] = "unity-demo-1",
                ["platform"] = "StandaloneWindows64",
                ["files"] = new JArray(new JObject { ["key"] = "world", ["path"] = "world", ["sha256"] = hash, ["size"] = bytes.LongLength })
            }.ToString());
        }

        [MenuItem("Tu Tien/Build Windows Demo")]
        public static void BuildPlayer()
        {
            Build();
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Build/TuTienClient.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new IOException("Unity player build failed.");
            }
        }
    }
}
