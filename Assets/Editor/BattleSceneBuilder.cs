using System;
using System.Collections.Generic;
using System.IO;
using Shmup.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Shmup.EditorTools
{
    /// <summary>
    /// Battle 씬을 통째로 다시 만든다 — 플레이스홀더 스프라이트, 탄 프리팹, 카메라, 씬 배선까지.
    /// 씬/프리팹 .unity YAML을 손으로 쓰면 GUID가 깨지기 쉬워서, 생성기를 원본으로 삼는다.
    ///
    /// 에디터:   Tools → Shmup → Rebuild Battle Scene
    /// 헤드리스: Unity.exe -batchmode -projectPath . -executeMethod Shmup.EditorTools.BattleSceneBuilder.Build -quit
    /// </summary>
    public static class BattleSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle.unity";
        const string SpriteDir = "Assets/Art/Sprites";
        const string PrefabDir = "Assets/Prefabs";
        const string BulletPrefabPath = PrefabDir + "/Bullet.prefab";
        const string ShipSpritePath = SpriteDir + "/player_ship.png";
        const string BulletSpritePath = SpriteDir + "/bullet.png";
        const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        // 고정된 기술 결정 (CLAUDE.md) — 아트 원본 해상도라 변경 금지.
        const int AssetsPPU = 16;
        const int RefResolutionX = 384;
        const int RefResolutionY = 224;

        // '.' 투명 / 'O' 외곽선 / 'B' 본체 / 'L' 하이라이트 / 'C' 캐노피
        static readonly string[] ShipPixels =
        {
            "...OO...........",
            "..OLLOO.........",
            ".OLLLBBOO.......",
            ".OLLBBBBBOO.....",
            "OLLBBBCCBBBOO...",
            "OLLBBBCCBBBBBOO.",
            "OLLBBBCCBBBBBOO.",
            "OLLBBBCCBBBOO...",
            ".OLLBBBBBOO.....",
            ".OLLLBBOO.......",
            "..OLLOO.........",
            "...OO..........."
        };

        static readonly Dictionary<char, Color32> ShipPalette = new Dictionary<char, Color32>
        {
            ['O'] = new Color32(0x18, 0x22, 0x40, 0xFF),
            ['B'] = new Color32(0x3C, 0x6E, 0xB4, 0xFF),
            ['L'] = new Color32(0x9C, 0xD4, 0xFF, 0xFF),
            ['C'] = new Color32(0xFF, 0xE0, 0x8C, 0xFF)
        };

        // 'W' 코어 / 'O' 글로우
        static readonly string[] BulletPixels =
        {
            "..OOOO..",
            "OWWWWWWO",
            "..OOOO.."
        };

        static readonly Dictionary<char, Color32> BulletPalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0xFF, 0xFB, 0xD8, 0xFF),
            ['O'] = new Color32(0xFF, 0x9C, 0x28, 0xFF)
        };

        [MenuItem("Tools/Shmup/Rebuild Battle Scene")]
        public static void Build()
        {
            try
            {
                var shipSprite = WritePixelSprite(ShipSpritePath, ShipPixels, ShipPalette);
                var bulletSprite = WritePixelSprite(BulletSpritePath, BulletPixels, BulletPalette);
                var bulletPrefab = WriteBulletPrefab(bulletSprite);

                BuildScene(shipSprite, bulletPrefab);
                RegisterInBuildSettings();

                AssetDatabase.SaveAssets();
                Debug.Log($"[BattleSceneBuilder] 완료 — {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BattleSceneBuilder] 실패: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ── 스프라이트 ────────────────────────────────────────────────────────────

        static Sprite WritePixelSprite(string assetPath, string[] rows, Dictionary<char, Color32> palette)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            for (int y = 0; y < height; y++)
                if (rows[y].Length != width)
                    throw new InvalidOperationException($"{assetPath}: {y}행의 길이가 {width}가 아니다.");

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                // rows[0]이 맨 윗줄, 텍스처는 y=0이 맨 아랫줄이라 뒤집는다.
                string row = rows[height - 1 - y];
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = palette.TryGetValue(row[x], out var c) ? c : clear;
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = AssetsPPU;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) throw new InvalidOperationException($"{assetPath} 스프라이트 로드 실패");
            return sprite;
        }

        // ── 프리팹 ────────────────────────────────────────────────────────────────

        static GameObject WriteBulletPrefab(Sprite sprite)
        {
            Directory.CreateDirectory(PrefabDir);

            var temp = new GameObject("Bullet");
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, BulletPrefabPath);
            UnityEngine.Object.DestroyImmediate(temp);

            if (prefab == null) throw new InvalidOperationException($"{BulletPrefabPath} 저장 실패");
            return prefab;
        }

        // ── 씬 ────────────────────────────────────────────────────────────────────

        static void BuildScene(Sprite shipSprite, GameObject bulletPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();

            var battleRoot = new GameObject("Battle");

            var player = new GameObject("Player");
            player.transform.SetParent(battleRoot.transform, false);
            player.transform.localPosition = new Vector3(-8f, 0f, 0f);
            var playerRenderer = player.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = shipSprite;
            playerRenderer.sortingOrder = 10;

            var bulletRoot = new GameObject("Bullets");
            bulletRoot.transform.SetParent(battleRoot.transform, false);

            var inputReader = battleRoot.AddComponent<PlayerInputReader>();
            var director = battleRoot.AddComponent<BattleDirector>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                Debug.LogWarning($"[BattleSceneBuilder] {InputActionsPath} 를 못 찾았다 — " +
                                 "PlayerInputReader의 Actions를 수동으로 지정해라.");
            SetReference(inputReader, "_actions", actions);

            SetReference(director, "_input", inputReader);
            SetReference(director, "_playerTransform", player.transform);
            SetReference(director, "_bulletPrefab", bulletPrefab);
            SetReference(director, "_bulletRoot", bulletRoot.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = RefResolutionY / 2f / AssetsPPU;   // 7
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x0A, 0x0E, 0x1A, 0xFF);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            go.AddComponent<UniversalAdditionalCameraData>();

            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = AssetsPPU;
            ppc.refResolutionX = RefResolutionX;
            ppc.refResolutionY = RefResolutionY;
            ApplyRetroAA(ppc);
        }

        /// <summary>
        /// Filter Mode = Retro AA (CLAUDE.md 고정 결정). URP 17의 PixelPerfectCamera는
        /// 이 값을 public 프로퍼티로 노출하지 않고 private [SerializeField] m_FilterMode로만
        /// 들고 있어서, SerializedObject로 직접 쓴다. RetroAA는 열거형 0번이자 기본값이지만
        /// 버전이 올라가며 기본값이 바뀔 수 있으니 명시적으로 박아 둔다.
        /// </summary>
        static void ApplyRetroAA(PixelPerfectCamera ppc)
        {
            var so = new SerializedObject(ppc);
            var property = so.FindProperty("m_FilterMode");
            if (property == null)
            {
                Debug.LogWarning("[BattleSceneBuilder] PixelPerfectCamera.m_FilterMode 를 못 찾았다 — " +
                                 "인스펙터에서 Filter Mode를 Retro AA로 직접 설정해라.");
                return;
            }
            property.enumValueIndex = (int)PixelPerfectCamera.PixelPerfectFilterMode.RetroAA;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
