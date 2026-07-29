using System;
using System.Collections.Generic;
using System.IO;
using Shmup.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using UnityEditor.U2D;

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
        // 2026-07-28 사람 승인으로 384×224 → 640×360 상향 (ROADMAP.md M0).
        const int AssetsPPU = 16;
        const int RefResolutionX = 640;
        const int RefResolutionY = 360;

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

        // 적 16×12: 밝은 회백색으로 그려 두고 BattleDirector가 종류별로 틴트한다.
        const string EnemySpritePath = SpriteDir + "/enemy.png";
        const string EnemyPrefabPath = PrefabDir + "/Enemy.prefab";

        static readonly string[] EnemyPixels =
        {
            "....OOOOOOOO....",
            "..OOBBBBBBBBOO..",
            ".OBBBBLLLLBBBBO.",
            "OBBBLLBBBBLLBBBO",
            "OBBLLBBBBBBLLBBO",
            "OBBLBBBOOBBBLBBO",
            "OBBLBBBOOBBBLBBO",
            "OBBLLBBBBBBLLBBO",
            "OBBBLLBBBBLLBBBO",
            ".OBBBBLLLLBBBBO.",
            "..OOBBBBBBBBOO..",
            "....OOOOOOOO...."
        };

        static readonly Dictionary<char, Color32> EnemyPalette = new Dictionary<char, Color32>
        {
            ['O'] = new Color32(0x40, 0x40, 0x48, 0xFF),
            ['B'] = new Color32(0xD8, 0xD8, 0xE0, 0xFF),
            ['L'] = new Color32(0xFF, 0xFF, 0xFF, 0xFF)
        };

        // 적탄 6×6 오렌지 구체 (플레이스홀더 — REQ-007 적탄 뷰)
        const string EnemyShotSpritePath = SpriteDir + "/enemy_shot.png";

        static readonly string[] EnemyShotPixels =
        {
            ".OWWO.",
            "OWCCWO",
            "WCCCCW",
            "WCCCCW",
            "OWCCWO",
            ".OWWO."
        };

        static readonly Dictionary<char, Color32> EnemyShotPalette = new Dictionary<char, Color32>
        {
            ['O'] = new Color32(0xB4, 0x2C, 0x14, 0xFF),
            ['W'] = new Color32(0xFF, 0x78, 0x20, 0xFF),
            ['C'] = new Color32(0xFF, 0xE0, 0xA0, 0xFF)
        };

        // 파워업 캡슐 10×8 (그라디우스 오렌지 캡슐 오마주)
        const string CapsuleSpritePath = SpriteDir + "/capsule.png";
        const string CapsulePrefabPath = PrefabDir + "/Capsule.prefab";

        static readonly string[] CapsulePixels =
        {
            "..OOOOOO..",
            ".OWWWWWWO.",
            "OWWCCCCWWO",
            "OWCCWWCCWO",
            "OWCCWWCCWO",
            "OWWCCCCWWO",
            ".OWWWWWWO.",
            "..OOOOOO.."
        };

        static readonly Dictionary<char, Color32> CapsulePalette = new Dictionary<char, Color32>
        {
            ['O'] = new Color32(0x8C, 0x3A, 0x0A, 0xFF),
            ['W'] = new Color32(0xFF, 0x9C, 0x28, 0xFF),
            ['C'] = new Color32(0xFF, 0xE8, 0xB0, 0xFF)
        };

        // 폭발 12×12 스타버스트 (런타임에 확대+페이드)
        const string ExplosionSpritePath = SpriteDir + "/explosion.png";
        const string ExplosionPrefabPath = PrefabDir + "/Explosion.prefab";

        static readonly string[] ExplosionPixels =
        {
            ".....WW.....",
            "..O..WW..O..",
            ".OO.WYYW.OO.",
            "..OWYYYYWO..",
            "..WYYCCYYW..",
            "WWYYCCCCYYWW",
            "WWYYCCCCYYWW",
            "..WYYCCYYW..",
            "..OWYYYYWO..",
            ".OO.WYYW.OO.",
            "..O..WW..O..",
            ".....WW....."
        };

        static readonly Dictionary<char, Color32> ExplosionPalette = new Dictionary<char, Color32>
        {
            ['C'] = new Color32(0xFF, 0xFF, 0xE0, 0xFF),
            ['Y'] = new Color32(0xFF, 0xC8, 0x3C, 0xFF),
            ['W'] = new Color32(0xFF, 0x78, 0x20, 0xFF),
            ['O'] = new Color32(0xB4, 0x3C, 0x14, 0xFF)
        };

        // 미사일 10×4 (전방 하강탄 — BulletKind.Missile 뷰)
        const string MissileSpritePath = SpriteDir + "/missile.png";

        static readonly string[] MissilePixels =
        {
            "..OOOOOOO.",
            "OWWWWWWWFO",
            "..OOOOOOO.",
            ".....F...."
        };

        static readonly Dictionary<char, Color32> MissilePalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0xC8, 0xD0, 0xDC, 0xFF),
            ['O'] = new Color32(0x50, 0x5C, 0x70, 0xFF),
            ['F'] = new Color32(0xFF, 0xA0, 0x30, 0xFF)
        };

        // 옵션 8×8 주황 구체 (그라디우스 오마주)
        const string OptionSpritePath = SpriteDir + "/option.png";
        const string OptionPrefabPath = PrefabDir + "/Option.prefab";

        static readonly string[] OptionPixels =
        {
            "..OOOO..",
            ".OWWWWO.",
            "OWWCCWWO",
            "OWCCCCWO",
            "OWCCCCWO",
            "OWWCCWWO",
            ".OWWWWO.",
            "..OOOO.."
        };

        static readonly Dictionary<char, Color32> OptionPalette = new Dictionary<char, Color32>
        {
            ['O'] = new Color32(0x8C, 0x2A, 0x0A, 0xFF),
            ['W'] = new Color32(0xFF, 0x78, 0x20, 0xFF),
            ['C'] = new Color32(0xFF, 0xD0, 0x80, 0xFF)
        };

        // 실드 링 20×20 (플레이어 자식, 알파는 런타임 조절)
        const string ShieldSpritePath = SpriteDir + "/shield.png";

        static string[] BuildShieldPixels()
        {
            const int size = 20;
            const float outer = 9.5f, inner = 7.5f;
            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                var row = new char[size];
                for (int x = 0; x < size; x++)
                {
                    float dx = x - (size - 1) / 2f, dy = y - (size - 1) / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    row[x] = d <= outer && d >= inner ? 'W' : '.';
                }
                rows[y] = new string(row);
            }
            return rows;
        }

        static readonly Dictionary<char, Color32> ShieldPalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0x6A, 0xC8, 0xFF, 0xFF)
        };

        // 피격 플래시용 흰색 타일 (풀스크린으로 스케일해 알파만 조절)
        const string WhiteSpritePath = SpriteDir + "/px_white.png";
        static readonly string[] WhitePixels = { "WW", "WW" };
        static readonly Dictionary<char, Color32> WhitePalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0xFF, 0xFF, 0xFF, 0xFF)
        };

        // HUD 슬롯 글자 아이콘 5×5 (S/M/O/B) — 프레임 안 중앙, 정적
        static readonly string[][] HudIconPixels =
        {
            new[] { ".WWW", "W...", ".WW.", "...W", "WWW." },            // S (MainShot)
            new[] { "W...W", "WW.WW", "W.W.W", "W...W", "W...W" },       // M (Missile)
            new[] { ".WWW.", "W...W", "W...W", "W...W", ".WWW." },       // O (Option)
            new[] { "WWW.", "W..W", "WWW.", "W..W", "WWW." }             // B (Barrier/Shield)
        };

        // HUD 슬롯 프레임 22×12: 'W' 테두리(런타임 틴트로 상태 표시), 'D' 반투명 내부
        const string HudSlotSpritePath = SpriteDir + "/hud_slot.png";
        const string HudPipSpritePath = SpriteDir + "/hud_pip.png";

        static readonly string[] HudSlotPixels = BuildHudSlotPixels();
        static readonly string[] HudPipPixels = { "WWW", "WWW", "WWW" };

        static readonly Dictionary<char, Color32> HudPalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            ['D'] = new Color32(0x10, 0x18, 0x28, 0xB4)
        };

        static string[] BuildHudSlotPixels()
        {
            const int width = 22, height = 12;
            var rows = new string[height];
            rows[0] = rows[height - 1] = new string('W', width);
            for (int y = 1; y < height - 1; y++)
                rows[y] = "W" + new string('D', width - 2) + "W";
            return rows;
        }

        // 패럴랙스 스타필드 타일 (화면 크기 1장, 레이어 루트가 2장을 이어 붙여 래핑)
        const string StarsFarSpritePath = SpriteDir + "/stars_far.png";
        const string StarsNearSpritePath = SpriteDir + "/stars_near.png";

        /// <summary>
        /// 고정 시드로 별을 뿌린 타일 픽셀을 만든다. 에디터 생성 시점에만 도는 코드라
        /// System.Random을 써도 되지만(AGENTS.md §4는 Core 규칙), 재생성 때마다 배경이
        /// 바뀌지 않도록 시드는 상수로 박는다.
        /// </summary>
        static string[] BuildStarPixels(int seed, int starCount, bool bigStars)
        {
            const int width = RefResolutionX, height = RefResolutionY;
            var grid = new char[height][];
            for (int y = 0; y < height; y++)
                grid[y] = new string('.', width).ToCharArray();

            var random = new System.Random(seed);
            for (int i = 0; i < starCount; i++)
            {
                int x = random.Next(width);
                int y = random.Next(height);
                char shade = random.Next(3) == 0 ? 'W' : 'G';
                grid[y][x] = shade;
                if (bigStars && random.Next(5) == 0 && x + 1 < width && y + 1 < height)
                {
                    grid[y][x + 1] = shade;
                    grid[y + 1][x] = shade;
                }
            }

            var rows = new string[height];
            for (int y = 0; y < height; y++) rows[y] = new string(grid[y]);
            return rows;
        }

        static readonly Dictionary<char, Color32> StarsFarPalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0x6A, 0x74, 0x94, 0xFF),
            ['G'] = new Color32(0x3A, 0x44, 0x64, 0xFF)
        };

        static readonly Dictionary<char, Color32> StarsNearPalette = new Dictionary<char, Color32>
        {
            ['W'] = new Color32(0xE0, 0xEC, 0xFF, 0xFF),
            ['G'] = new Color32(0x8C, 0xA4, 0xD4, 0xFF)
        };

        [MenuItem("Tools/Shmup/Rebuild Battle Scene")]
        public static void Build()
        {
            try
            {
                CopyGameDataToResources();
                EnsureSpriteAtlas();

                var shipSprite = WriteExternalOrPixelSprite(ShipSpritePath, "player_ship.png", ShipPixels, ShipPalette);
                var bulletSprite = WritePixelSprite(BulletSpritePath, BulletPixels, BulletPalette);
                var hudSlotSprite = WritePixelSprite(HudSlotSpritePath, HudSlotPixels, HudPalette);
                var hudPipSprite = WritePixelSprite(HudPipSpritePath, HudPipPixels, HudPalette);
                // 별 개수는 384×224 시절 110/45를 새 캔버스 면적비(×2.68)로 환산한 값 — 밀도 유지.
                var starsFarSprite = WritePixelSprite(StarsFarSpritePath, BuildStarPixels(9001, 295, false), StarsFarPalette);
                var starsNearSprite = WritePixelSprite(StarsNearSpritePath, BuildStarPixels(4242, 120, true), StarsNearPalette);
                var bulletPrefab = WriteBulletPrefab(bulletSprite);
                var enemySprite = WriteExternalOrPixelSprite(EnemySpritePath, "enemy_zako.png", EnemyPixels, EnemyPalette);
                var enemyPrefab = WriteSpritePrefab(EnemyPrefabPath, "Enemy", enemySprite, 8);
                var capsuleSprite = WriteExternalOrPixelSprite(CapsuleSpritePath, "capsule.png", CapsulePixels, CapsulePalette);
                var capsulePrefab = WriteSpritePrefab(CapsulePrefabPath, "Capsule", capsuleSprite, 7);
                var explosionFrames = LoadExplosionFrames();
                var explosionSprite = explosionFrames.Length > 0
                    ? explosionFrames[0]
                    : WritePixelSprite(ExplosionSpritePath, ExplosionPixels, ExplosionPalette);
                var explosionPrefab = WriteSpritePrefab(ExplosionPrefabPath, "Explosion", explosionSprite, 20);
                var whiteSprite = WritePixelSprite(WhiteSpritePath, WhitePixels, WhitePalette);
                var missileSprite = WritePixelSprite(MissileSpritePath, MissilePixels, MissilePalette);
                var enemyShotSprite = WritePixelSprite(EnemyShotSpritePath, EnemyShotPixels, EnemyShotPalette);
                var optionSprite = WritePixelSprite(OptionSpritePath, OptionPixels, OptionPalette);
                var optionPrefab = WriteSpritePrefab(OptionPrefabPath, "Option", optionSprite, 9);
                var shieldSprite = WritePixelSprite(ShieldSpritePath, BuildShieldPixels(), ShieldPalette);

                BuildScene(shipSprite, bulletPrefab, enemyPrefab, capsulePrefab, explosionPrefab,
                           whiteSprite, missileSprite, optionPrefab, shieldSprite,
                           hudSlotSprite, hudPipSprite, starsFarSprite, starsNearSprite,
                           explosionFrames, enemyShotSprite);
                BuildTitleScene(starsFarSprite, starsNearSprite);
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

        // ── GameData ─────────────────────────────────────────────────────────────

        /// <summary>
        /// GameData/*.json(저장소 원본, GROK 소유)을 Resources로 복사해 빌드에 포함시킨다.
        /// 런타임은 이 사본을 읽고, 파싱은 Core(GameDataParser)가 한다. 원본 수정 후에는
        /// 씬 재생성을 다시 돌려야 사본이 갱신된다.
        /// </summary>
        /// <summary>
        /// Assets/Art/Sprites 전체를 하나의 SpriteAtlas로 묶는다 (드로우콜 절감).
        /// 픽셀 아트 규격: 회전 금지, 풀렉트 패킹, 포인트 필터, 무압축.
        /// 이미 있으면 재생성하지 않는다 — 폴더 참조라 새 스프라이트는 자동 포함된다.
        /// </summary>
        static void EnsureSpriteAtlas()
        {
            const string atlasPath = "Assets/Art/GameSprites.spriteatlas";
            if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath) != null) return;

            var atlas = new SpriteAtlas();
            var packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 2;
            atlas.SetPackingSettings(packing);

            var texture = atlas.GetTextureSettings();
            texture.filterMode = FilterMode.Point;
            texture.generateMipMaps = false;
            atlas.SetTextureSettings(texture);

            var platform = atlas.GetPlatformSettings("DefaultTexturePlatform");
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 2048;
            atlas.SetPlatformSettings(platform);

            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SpriteDir);
            if (folder != null)
                atlas.Add(new[] { folder });
            AssetDatabase.CreateAsset(atlas, atlasPath);
            Debug.Log($"[BattleSceneBuilder] SpriteAtlas 생성: {atlasPath} (폴더 {SpriteDir} 참조)");
        }

        static void CopyGameDataToResources()
        {
            const string targetDir = "Assets/Resources/GameData";
            string sourceDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "GameData"));
            Directory.CreateDirectory(targetDir);

            foreach (string source in Directory.GetFiles(sourceDir, "*.json"))
            {
                string target = $"{targetDir}/{Path.GetFileName(source)}";
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[BattleSceneBuilder] GameData → Resources 복사 완료");
        }

        // ── 스프라이트 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 외부 아트 파이프라인 (ART-DIRECTION.md): art-input/<fileName>이 있으면 그 파일이
        /// 절차 생성 플레이스홀더를 대체한다. 없으면 기존 픽셀 배열로 생성.
        /// </summary>
        static Sprite WriteExternalOrPixelSprite(string assetPath, string externalFileName, string[] rows, Dictionary<char, Color32> palette)
        {
            string external = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "art-input", externalFileName));
            if (File.Exists(external))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                File.Copy(external, assetPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ApplySpriteImporter(assetPath);
                var externalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (externalSprite == null) throw new InvalidOperationException($"{assetPath} 외부 스프라이트 로드 실패");
                Debug.Log($"[BattleSceneBuilder] 외부 아트 적용: {externalFileName} → {assetPath}");
                return externalSprite;
            }
            return WritePixelSprite(assetPath, rows, palette);
        }

        /// <summary>art-input의 선택적 외부 스프라이트. 없으면 null (해당 요소는 생략).</summary>
        static Sprite LoadExternalSprite(string externalFileName, string assetName)
        {
            string external = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "art-input", externalFileName));
            if (!File.Exists(external)) return null;

            string assetPath = $"{SpriteDir}/{assetName}.png";
            Directory.CreateDirectory(SpriteDir);
            File.Copy(external, assetPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ApplySpriteImporter(assetPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                Debug.Log($"[BattleSceneBuilder] 외부 아트 적용: {externalFileName} → {assetPath}");
            return sprite;
        }

        /// <summary>art-input/fx_explosion_00.png…을 순서대로 임포트한다 (M2 폭발 애니).</summary>
        static Sprite[] LoadExplosionFrames()
        {
            return LoadFrameSequence("fx_explosion_");
        }

        static Sprite[] LoadShipAnimationFrames()
        {
            return LoadFrameSequence("ship_anim_");
        }

        static Sprite[] LoadFrameSequence(string prefix)
        {
            var frames = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                var sprite = LoadExternalSprite($"{prefix}{i:d2}.png", $"{prefix}{i:d2}");
                if (sprite == null) break;
                frames.Add(sprite);
            }
            return frames.ToArray();
        }

        static void ApplySpriteImporter(string assetPath)
        {
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
        }

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
            => WriteSpritePrefab(BulletPrefabPath, "Bullet", sprite, 5);

        static GameObject WriteSpritePrefab(string prefabPath, string name, Sprite sprite, int sortingOrder)
        {
            Directory.CreateDirectory(PrefabDir);

            var temp = new GameObject(name);
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            UnityEngine.Object.DestroyImmediate(temp);

            if (prefab == null) throw new InvalidOperationException($"{prefabPath} 저장 실패");
            return prefab;
        }

        // ── 씬 ────────────────────────────────────────────────────────────────────

        static void BuildScene(Sprite shipSprite, GameObject bulletPrefab, GameObject enemyPrefab, GameObject capsulePrefab, GameObject explosionPrefab, Sprite whiteSprite, Sprite missileSprite, GameObject optionPrefab, Sprite shieldSprite, Sprite hudSlotSprite, Sprite hudPipSprite, Sprite starsFarSprite, Sprite starsNearSprite, Sprite[] explosionFrames, Sprite enemyShotSprite)
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

            var enemyRoot = new GameObject("Enemies");
            enemyRoot.transform.SetParent(battleRoot.transform, false);

            var capsuleRoot = new GameObject("Capsules");
            capsuleRoot.transform.SetParent(battleRoot.transform, false);

            var fxRoot = new GameObject("Fx");
            fxRoot.transform.SetParent(battleRoot.transform, false);

            var optionRoot = new GameObject("Options");
            optionRoot.transform.SetParent(battleRoot.transform, false);

            // 실드 링: 플레이어 자식이라 위치를 따로 동기화할 필요가 없다.
            var shield = new GameObject("Shield");
            shield.transform.SetParent(player.transform, false);
            var shieldRenderer = shield.AddComponent<SpriteRenderer>();
            shieldRenderer.sprite = shieldSprite;
            shieldRenderer.sortingOrder = 11;
            shieldRenderer.color = new Color(0.42f, 0.78f, 1f, 0.4f);
            shieldRenderer.enabled = false;

            // 피격 플래시: 2px 흰 타일을 화면 전체(24×14u)로 늘려 알파만 조절한다.
            var damageFlash = new GameObject("DamageFlash");
            damageFlash.transform.SetParent(battleRoot.transform, false);
            // 타일이 2px이므로 스케일 = 화면 픽셀 / 2
            damageFlash.transform.localScale = new Vector3(RefResolutionX / 2f, RefResolutionY / 2f, 1f);
            var damageFlashRenderer = damageFlash.AddComponent<SpriteRenderer>();
            damageFlashRenderer.sprite = whiteSprite;
            damageFlashRenderer.sortingOrder = 90;
            damageFlashRenderer.color = new Color(1f, 0.2f, 0.2f, 0f);

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
            SetReference(director, "_enemyPrefab", enemyPrefab);
            SetReference(director, "_enemyRoot", enemyRoot.transform);
            SetReference(director, "_capsulePrefab", capsulePrefab);
            SetReference(director, "_capsuleRoot", capsuleRoot.transform);
            SetReference(director, "_explosionPrefab", explosionPrefab);
            SetReference(director, "_fxRoot", fxRoot.transform);
            SetReference(director, "_damageFlash", damageFlashRenderer);
            SetReference(director, "_missileSprite", missileSprite);
            SetReference(director, "_optionPrefab", optionPrefab);
            SetReference(director, "_optionRoot", optionRoot.transform);
            SetReference(director, "_shieldView", shieldRenderer);
            SetReferenceArray(director, "_explosionFrames", explosionFrames);
            SetReference(director, "_enemyShotSprite", enemyShotSprite);

            // 보스 뷰 (REQ-007): 시뮬 BossActive일 때만 director가 렌더러를 켠다.
            var bossSprite = LoadExternalSprite("boss_stage1.png", "boss_stage1");
            var boss = new GameObject("Boss");
            boss.transform.SetParent(battleRoot.transform, false);
            var bossRenderer = boss.AddComponent<SpriteRenderer>();
            bossRenderer.sprite = bossSprite != null ? bossSprite : shipSprite;
            bossRenderer.sortingOrder = 15;
            bossRenderer.enabled = false;
            if (bossSprite == null)
            {
                boss.transform.localScale = Vector3.one * 4f;   // 외부 아트 없을 때 임시 확대
                bossRenderer.color = new Color32(0xC8, 0x50, 0x50, 0xFF);
            }
            SetReference(director, "_bossRenderer", bossRenderer);

            // 보스ID → 스프라이트 매핑 (M3 테마별 보스)
            var bossPrefixes = new List<string>();
            var bossSprites = new List<Sprite>();
            void AddBossSprite(string prefix, Sprite sprite)
            {
                if (sprite == null) return;
                bossPrefixes.Add(prefix);
                bossSprites.Add(sprite);
            }
            AddBossSprite("boss_stage1", bossSprite);
            AddBossSprite("boss_hive", LoadExternalSprite("boss_hive.png", "boss_hive"));
            AddBossSprite("boss_fortress", LoadExternalSprite("boss_fortress.png", "boss_fortress"));
            AddBossSprite("boss_storm", LoadExternalSprite("boss_storm.png", "boss_storm"));
            AddBossSprite("boss_core", LoadExternalSprite("boss_core.png", "boss_core"));
            SetStringArray(director, "_bossSpritePrefixes", bossPrefixes.ToArray());
            SetReferenceArray(director, "_bossSprites", bossSprites.ToArray());

            // 보스 HP 바: 상단 중앙, px_white 스케일 방식. BossActive 동안만 director가 켠다.
            var bossHpRoot = new GameObject("BossHp");
            bossHpRoot.transform.localPosition = new Vector3(0f, 10.4f, 0f);
            var hpBack = new GameObject("Back");
            hpBack.transform.SetParent(bossHpRoot.transform, false);
            var hpBackRenderer = hpBack.AddComponent<SpriteRenderer>();
            hpBackRenderer.sprite = whiteSprite;
            hpBackRenderer.sortingOrder = 98;
            hpBackRenderer.color = new Color32(0x10, 0x14, 0x20, 0xE0);
            hpBack.transform.localScale = new Vector3(16f / (2f / 16f), 3f, 1f);
            var hpFill = new GameObject("Fill");
            hpFill.transform.SetParent(bossHpRoot.transform, false);
            var hpFillRenderer = hpFill.AddComponent<SpriteRenderer>();
            hpFillRenderer.sprite = whiteSprite;
            hpFillRenderer.sortingOrder = 99;
            hpFillRenderer.color = new Color32(0xE8, 0x4A, 0x2A, 0xFF);
            hpFill.transform.localScale = new Vector3(16f / (2f / 16f), 2f, 1f);
            bossHpRoot.SetActive(false);
            SetReference(director, "_bossHpRoot", bossHpRoot);
            SetReference(director, "_bossHpFill", hpFill.transform);

            // 적 종별 전용 스프라이트 (M2 비주얼 다양화). 없는 종류는 기본+틴트 폴백.
            var enemyTypeSprites = new List<Sprite>();
            var enemyTypePrefixes = new List<string>();
            void AddEnemySprite(string prefix, Sprite sprite)
            {
                if (sprite == null) return;
                enemyTypePrefixes.Add(prefix);
                enemyTypeSprites.Add(sprite);
            }
            var enemySpriteDefault = AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath);
            AddEnemySprite("zako_straight", enemySpriteDefault);
            AddEnemySprite("zako_fast", enemySpriteDefault);
            AddEnemySprite("zako_sine", LoadExternalSprite("enemy_scarab.png", "enemy_scarab"));
            AddEnemySprite("turret", LoadExternalSprite("enemy_turret.png", "enemy_turret"));
            AddEnemySprite("zako_tank", LoadExternalSprite("enemy_tank.png", "enemy_tank"));
            AddEnemySprite("elite", LoadExternalSprite("enemy_elite.png", "enemy_elite"));
            AddEnemySprite("spore", LoadExternalSprite("enemy_spore.png", "enemy_spore"));
            AddEnemySprite("lancer", LoadExternalSprite("enemy_lancer.png", "enemy_lancer"));
            AddEnemySprite("sentry", LoadExternalSprite("enemy_sentry.png", "enemy_sentry"));
            AddEnemySprite("interceptor", LoadExternalSprite("enemy_interceptor.png", "enemy_interceptor"));
            AddEnemySprite("wisp", LoadExternalSprite("enemy_wisp.png", "enemy_wisp"));
            AddEnemySprite("guardian", LoadExternalSprite("enemy_guardian.png", "enemy_guardian"));
            AddEnemySprite("mini_destroyer", LoadExternalSprite("enemy_mini_destroyer.png", "enemy_mini_destroyer"));
            AddEnemySprite("mini_horror", LoadExternalSprite("enemy_mini_horror.png", "enemy_mini_horror"));
            AddEnemySprite("mini_walker", LoadExternalSprite("enemy_mini_walker.png", "enemy_mini_walker"));
            AddEnemySprite("mini_crystal", LoadExternalSprite("enemy_mini_crystal.png", "enemy_mini_crystal"));
            AddEnemySprite("scrap_tumbler", LoadExternalSprite("enemy_scrap_tumbler.png", "enemy_scrap_tumbler"));
            AddEnemySprite("brood_spitter", LoadExternalSprite("enemy_brood_spitter.png", "enemy_brood_spitter"));
            AddEnemySprite("mortar_drone", LoadExternalSprite("enemy_mortar_drone.png", "enemy_mortar_drone"));
            AddEnemySprite("echo_wisp", LoadExternalSprite("enemy_echo_wisp.png", "enemy_echo_wisp"));
            AddEnemySprite("rust_skimmer", LoadExternalSprite("enemy_rust_skimmer.png", "enemy_rust_skimmer"));
            AddEnemySprite("junk_roller", LoadExternalSprite("enemy_junk_roller.png", "enemy_junk_roller"));
            AddEnemySprite("void_moth", LoadExternalSprite("enemy_void_moth.png", "enemy_void_moth"));
            AddEnemySprite("shard_prism", LoadExternalSprite("enemy_shard_prism.png", "enemy_shard_prism"));
            AddEnemySprite("sting_hornet", LoadExternalSprite("enemy_sting_hornet.png", "enemy_sting_hornet"));
            AddEnemySprite("pipe_rat", LoadExternalSprite("enemy_pipe_rat.png", "enemy_pipe_rat"));
            AddEnemySprite("phase_disc", LoadExternalSprite("enemy_phase_disc.png", "enemy_phase_disc"));
            AddEnemySprite("rift_blade", LoadExternalSprite("enemy_rift_blade.png", "enemy_rift_blade"));
            SetStringArray(director, "_enemySpritePrefixes", enemyTypePrefixes.ToArray());
            SetReferenceArray(director, "_enemySprites", enemyTypeSprites.ToArray());

            // 아이들 애니메이션 (art-input/anim_<prefix>_XX.png 시퀀스가 있으면)
            var animPrefixes = new List<string>();
            var animCounts = new List<int>();
            var animFlat = new List<Sprite>();
            void AddAnim(string prefix)
            {
                var frames = LoadFrameSequence($"anim_{prefix}_");
                if (frames.Length == 0) return;
                animPrefixes.Add(prefix);
                animCounts.Add(frames.Length);
                animFlat.AddRange(frames);
            }
            foreach (string prefix in new[]
            {
                "zako_straight", "zako_fast", "zako_sine", "turret", "zako_tank", "elite",
                "spore", "lancer", "sentry", "interceptor", "wisp", "guardian",
                "scrap_tumbler", "brood_spitter", "mortar_drone", "echo_wisp",
                "rust_skimmer", "junk_roller", "void_moth", "shard_prism",
                "sting_hornet", "pipe_rat", "phase_disc", "rift_blade",
                "mini_destroyer", "mini_horror", "mini_walker", "mini_crystal",
                "boss_stage1", "boss_hive", "boss_fortress", "boss_storm", "boss_core"
            })
                AddAnim(prefix);
            SetStringArray(director, "_animPrefixes", animPrefixes.ToArray());
            SetIntArray(director, "_animFrameCounts", animCounts.ToArray());
            SetReferenceArray(director, "_animFrames", animFlat.ToArray());

            // 기체 애니메이션 (art-input/ship_anim_XX.png 있으면)
            var shipFrames = LoadShipAnimationFrames();
            if (shipFrames.Length > 0)
            {
                var animator = player.AddComponent<PlayerShipAnimator>();
                SetReference(animator, "_renderer", playerRenderer);
                SetReferenceArray(animator, "_frames", shipFrames);
            }

            // 함선별 스프라이트 (밸런스/스피드/탱커 차별화, 2026-07-29 사람 지시)
            var shipIds = new[] { "starter", "interceptor", "bulwark" };
            var shipSprites = new Sprite[]
            {
                shipSprite,
                LoadExternalSprite("ship_interceptor.png", "ship_interceptor"),
                LoadExternalSprite("ship_bulwark.png", "ship_bulwark")
            };
            SetStringArray(director, "_shipSpriteIds", shipIds);
            SetReferenceArray(director, "_shipSprites", shipSprites);
            SetReference(director, "_laserShotSprite",
                LoadExternalSprite("bullet_laser.png", "bullet_laser"));
            SetReference(director, "_spreadShotSprite",
                LoadExternalSprite("bullet_spread.png", "bullet_spread"));

            // 장애물 (REQ-023): 테마×계열 스프라이트, _themeIds 순서와 정렬
            var obstacleRoot = new GameObject("Obstacles");
            obstacleRoot.transform.SetParent(battleRoot.transform, false);
            var armorSprite = LoadExternalSprite("obstacle_armor_block.png", "obstacle_armor_block");
            var sporeSprite = LoadExternalSprite("obstacle_spore_pillar.png", "obstacle_spore_pillar");
            var crystalSprite = LoadExternalSprite("obstacle_crystal.png", "obstacle_crystal");
            var coreBlockSprite = LoadExternalSprite("obstacle_core_block.png", "obstacle_core_block");
            var obstaclePrefab = WriteSpritePrefab(
                PrefabDir + "/Obstacle.prefab", "Obstacle",
                armorSprite != null ? armorSprite : shipSprite, 9);
            SetReference(director, "_obstacleRoot", obstacleRoot.transform);
            SetReference(director, "_obstaclePrefab", obstaclePrefab);
            SetReferenceArray(director, "_obstacleSolidSprites", new[]
                { armorSprite, armorSprite, armorSprite, armorSprite, coreBlockSprite });
            SetReferenceArray(director, "_obstacleBreakableSprites", new[]
                { crystalSprite, sporeSprite, crystalSprite, crystalSprite, crystalSprite });

            // UI 픽셀 폰트 (Galmuri, OFL — Assets/Fonts/Galmuri-LICENSE-OFL.txt)
            var uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Galmuri9.ttf");
            var uiFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Galmuri11-Bold.ttf");
            if (uiFont == null || uiFontBold == null)
                Debug.LogWarning("[BattleSceneBuilder] Assets/Fonts/Galmuri*.ttf 를 못 찾았다 — UI 텍스트가 비어 보인다.");

            var rewardScreen = battleRoot.AddComponent<RewardScreen>();
            SetReference(rewardScreen, "_director", director);
            SetReference(rewardScreen, "_font", uiFont);
            SetReference(rewardScreen, "_fontBold", uiFontBold);
            var pause = battleRoot.AddComponent<PauseScreen>();
            SetReference(pause, "_font", uiFont);
            SetReference(pause, "_fontBold", uiFontBold);
            SetReference(pause, "_director", director);
            var options = battleRoot.AddComponent<OptionsScreen>();
            SetReference(options, "_input", inputReader);
            SetReference(options, "_font", uiFont);
            SetReference(options, "_fontBold", uiFontBold);
            var gameOver = battleRoot.AddComponent<GameOverScreen>();
            SetReference(gameOver, "_director", director);
            SetReference(gameOver, "_font", uiFont);
            SetReference(gameOver, "_fontBold", uiFontBold);
            var onboarding = battleRoot.AddComponent<OnboardingHints>();
            SetReference(onboarding, "_director", director);
            SetReference(onboarding, "_font", uiFont);

            // 주스 연출 허브 (M4+ 게임 필): 히트스톱·슬로모·화면 흔들림 + 접근성 토글
            var juice = battleRoot.AddComponent<JuiceDirector>();
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
                SetReference(juice, "_cameraTransform", mainCamera.transform);
            SetReference(director, "_juice", juice);
            SetReference(options, "_juice", juice);

            // 머즐 플래시: 기체 코 앞에 고정, PlayerFired 이벤트로 director가 깜빡인다
            var muzzleSprite = LoadExternalSprite("fx_muzzle_00.png", "fx_muzzle_00");
            if (muzzleSprite != null)
            {
                var muzzle = new GameObject("MuzzleFlash");
                muzzle.transform.SetParent(player.transform, false);
                muzzle.transform.localPosition = new Vector3(0.85f, 0f, 0f);
                var muzzleRenderer = muzzle.AddComponent<SpriteRenderer>();
                muzzleRenderer.sprite = muzzleSprite;
                muzzleRenderer.sortingOrder = 21;
                muzzleRenderer.enabled = false;
                SetReference(director, "_muzzleFlash", muzzleRenderer);
            }
            var bossIntro = battleRoot.AddComponent<BossIntro>();
            SetReference(director, "_bossIntro", bossIntro);
            SetReference(bossIntro, "_fontBold", uiFontBold);
            var scoreHud = battleRoot.AddComponent<ScoreHud>();
            SetReference(scoreHud, "_director", director);
            SetReference(scoreHud, "_fontBold", uiFontBold);

            CreateHud(director, hudSlotSprite, hudPipSprite);
            CreateBackground(director, starsFarSprite, starsNearSprite);
            CreateSfx(director);
            CreateBgm(director);

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
            camera.orthographicSize = RefResolutionY / 2f / AssetsPPU;   // 11.25
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

        // ── 배경 ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 2층 패럴랙스 스타필드. 레이어마다 화면 폭 타일 2장을 이어 붙이고
        /// ParallaxBackground가 스크롤 팩터별로 왼쪽으로 밀며 래핑한다.
        /// </summary>
        static readonly float[] StarLayerFactors = { 0.25f, 0.6f };

        static GameObject CreateStarLayers(Sprite farSprite, Sprite nearSprite, out Transform[] layers)
        {
            const float tileWidth = RefResolutionX / (float)AssetsPPU;   // 40u

            var root = new GameObject("Background");
            layers = new Transform[2];
            var sprites = new[] { farSprite, nearSprite };
            var orders = new[] { -100, -90 };

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = new GameObject(i == 0 ? "StarsFar" : "StarsNear");
                layer.transform.SetParent(root.transform, false);
                layers[i] = layer.transform;

                for (int tile = 0; tile < 2; tile++)
                {
                    var tileGo = new GameObject($"Tile{tile}");
                    tileGo.transform.SetParent(layer.transform, false);
                    tileGo.transform.localPosition = new Vector3(tile * tileWidth, 0f, 0f);
                    var renderer = tileGo.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[i];
                    renderer.sortingOrder = orders[i];
                }
            }

            return root;
        }

        /// <summary>
        /// 배틀 배경: 테마 루트를 여러 개 만들고 director가 StageIndex로 로테이션한다 (M3).
        /// 각 루트 = 별 2겹 + 테마 3겹(art-input/&lt;theme&gt;_far/mid/near.png). 팩터가 작을수록 원경,
        /// near 레이어는 게임플레이 위(55)에서 스크롤보다 빠르게 지나가는 전경 실루엣.
        /// </summary>
        static void CreateBackground(BattleDirector director, Sprite farSprite, Sprite nearSprite)
        {
            var themeRoots = new List<GameObject>();
            var themeIds = new List<string>();
            void AddTheme(string themeId, string rootName, string filePrefix)
            {
                var root = CreateThemeRoot(director, rootName, farSprite, nearSprite, filePrefix);
                if (root == null) return;
                themeRoots.Add(root);
                themeIds.Add(themeId);   // waves.json의 theme 값과 일치해야 한다 (테마-보스 바인딩)
            }
            AddTheme("scrapyard", "Background_Scrapyard", "scrap");
            AddTheme("hive", "Background_Hive", "hive");
            AddTheme("fortress", "Background_Fortress", "fort");
            AddTheme("nebula", "Background_Nebula", "nebula");
            AddTheme("core", "Background_Core", "core");

            // 두 번째 이후 테마는 director의 ApplyStageTheme이 켤 때까지 꺼 둔다.
            for (int i = 1; i < themeRoots.Count; i++)
                themeRoots[i].SetActive(false);

            SetReferenceArray(director, "_themeBackgrounds", themeRoots.ToArray());
            SetStringArray(director, "_themeIds", themeIds.ToArray());
        }

        static GameObject CreateThemeRoot(
            BattleDirector director, string rootName, Sprite starsFar, Sprite starsNear, string themePrefix)
        {
            var themeFar = LoadExternalSprite($"{themePrefix}_far.png", $"{themePrefix}_far");
            var themeMid = LoadExternalSprite($"{themePrefix}_mid.png", $"{themePrefix}_mid");
            var themeNear = LoadExternalSprite($"{themePrefix}_near.png", $"{themePrefix}_near");
            // 기본 테마(첫 번째)는 테마 스프라이트가 없어도 별만으로 만든다.
            if (themeFar == null && themeMid == null && themeNear == null
                && themePrefix != "scrap")
                return null;

            var sprites = new List<Sprite>();
            var factors = new List<float>();
            var orders = new List<int>();
            void AddLayer(Sprite sprite, float factor, int order)
            {
                if (sprite == null) return;
                sprites.Add(sprite);
                factors.Add(factor);
                orders.Add(order);
            }

            AddLayer(starsFar, 0.1f, -100);
            AddLayer(themeFar, 0.2f, -96);
            AddLayer(starsNear, 0.45f, -90);
            AddLayer(themeMid, 0.6f, -85);
            AddLayer(themeNear, 1.15f, 55);

            const float tileWidth = RefResolutionX / (float)AssetsPPU;
            var root = new GameObject(rootName);
            var layers = new Transform[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                var layer = new GameObject($"Layer{i}_{sprites[i].name}");
                layer.transform.SetParent(root.transform, false);
                layers[i] = layer.transform;
                for (int tile = 0; tile < 2; tile++)
                {
                    var tileGo = new GameObject($"Tile{tile}");
                    tileGo.transform.SetParent(layer.transform, false);
                    tileGo.transform.localPosition = new Vector3(tile * tileWidth, 0f, 0f);
                    var renderer = tileGo.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[i];
                    renderer.sortingOrder = orders[i];
                }
            }

            var parallax = root.AddComponent<ParallaxBackground>();
            SetReference(parallax, "_director", director);
            SetReferenceArray(parallax, "_layers", layers);
            SetFloatArray(parallax, "_factors", factors.ToArray());
            SetFloat(parallax, "_tileWidth", tileWidth);
            return root;
        }

        // ── SFX ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// SfxPlayer 배선. 채택음(Tools/SfxGen 시드 0)은 Assets/Audio/Sfx에 커밋되어 있다.
        /// 클립이 없으면 경고만 내고 무음으로 둔다 — 씬 재생성이 막히면 안 된다.
        /// </summary>
        const string SfxDir = "Assets/Audio/Sfx";

        static void CreateSfx(BattleDirector director)
        {
            var go = new GameObject("Sfx");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var player = go.AddComponent<SfxPlayer>();
            SetReference(player, "_source", source);
            SetReference(player, "_laser", LoadClip("sfx_laser"));
            SetReference(player, "_hit", LoadClip("sfx_hit"));
            SetReference(player, "_explosion", LoadClip("sfx_explosion"));
            SetReference(player, "_pickup", LoadClip("sfx_pickup"));
            SetReference(player, "_powerup", LoadClip("sfx_powerup"));
            SetReference(director, "_sfx", player);
        }

        /// <summary>테마별 BGM 루프 (Tools/SfxGen/bgmgen.py 테마 프리셋 산출물). 없으면 무음.</summary>
        static void CreateBgm(BattleDirector director)
        {
            string[] themeIds = { "scrapyard", "hive", "fortress", "nebula", "core" };
            var clips = new AudioClip[themeIds.Length];
            AudioClip first = null;
            for (int i = 0; i < themeIds.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    $"Assets/Audio/Bgm/bgm_{themeIds[i]}.wav");
                if (first == null && clips[i] != null) first = clips[i];
            }
            if (first == null)
            {
                Debug.LogWarning("[BattleSceneBuilder] Assets/Audio/Bgm 트랙 없음 — BGM 무음.");
                return;
            }

            var go = new GameObject("Bgm");
            var source = go.AddComponent<AudioSource>();
            source.clip = first;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.45f;
            source.spatialBlend = 0f;

            var player = go.AddComponent<BgmPlayer>();
            SetReference(player, "_director", director);
            SetReference(player, "_source", source);
            SetStringArray(player, "_themeIds", themeIds);
            SetReferenceArray(player, "_clips", clips);
            SetReference(player, "_bossClip",
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bgm/bgm_boss.wav"));
            SetReference(player, "_clearJingle",
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/jingle_clear.wav"));
            SetReference(player, "_gameOverJingle",
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sfx/jingle_gameover.wav"));
        }

        static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{name}.wav");
            if (clip == null)
                Debug.LogWarning($"[BattleSceneBuilder] {SfxDir}/{name}.wav 없음 — 해당 SFX는 무음.");
            return clip;
        }

        // ── 타이틀 씬 ─────────────────────────────────────────────────────────────

        const string TitleScenePath = "Assets/Scenes/Title.unity";

        static void BuildTitleScene(Sprite farSprite, Sprite nearSprite)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            var root = CreateStarLayers(farSprite, nearSprite, out var layers);
            // 키아트 (gpt-image-1.5, 잠정 채택 — 우주 영역이 투명이라 뒤의 패럴랙스 별이 비친다)
            var keyartSprite = LoadExternalSprite("title_keyart.png", "title_keyart");
            if (keyartSprite != null)
            {
                var keyart = new GameObject("TitleKeyArt");
                keyart.transform.SetParent(root.transform, false);
                var keyartRenderer = keyart.AddComponent<SpriteRenderer>();
                keyartRenderer.sprite = keyartSprite;
                keyartRenderer.sortingOrder = 3;   // 별 레이어 위, UI(오버레이 캔버스) 아래
            }

            var titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Galmuri9.ttf");
            var titleFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Galmuri11-Bold.ttf");
            var title = root.AddComponent<TitleScreen>();
            SetReferenceArray(title, "_layers", layers);
            SetFloatArray(title, "_factors", StarLayerFactors);
            SetFloat(title, "_tileWidth", RefResolutionX / (float)AssetsPPU);
            SetReference(title, "_font", titleFont);
            SetReference(title, "_fontBold", titleFontBold);
            var hangar = root.AddComponent<HangarScreen>();   // 함선 해금형 메타 (2026-07-29)
            SetReference(hangar, "_font", titleFont);
            SetReference(hangar, "_fontBold", titleFontBold);
            SetStringArray(hangar, "_shipIds", new[] { "starter", "interceptor", "bulwark" });
            SetReferenceArray(hangar, "_shipSprites", new Sprite[]
            {
                LoadExternalSprite("player_ship.png", "player_ship"),
                LoadExternalSprite("ship_interceptor.png", "ship_interceptor"),
                LoadExternalSprite("ship_bulwark.png", "ship_bulwark")
            });

            // 타이틀 BGM (bgmgen title 프리셋, 시드 0 잠정)
            var titleBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bgm/bgm_title.wav");
            if (titleBgm != null)
            {
                var bgmGo = new GameObject("TitleBgm");
                bgmGo.transform.SetParent(root.transform, false);
                var bgmSource = bgmGo.AddComponent<AudioSource>();
                bgmSource.clip = titleBgm;
                bgmSource.loop = true;
                bgmSource.playOnAwake = true;
                bgmSource.volume = 0.45f;
                bgmSource.spatialBlend = 0f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        static void SetIntArray(UnityEngine.Object target, string fieldName, int[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).intValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetStringArray(UnityEngine.Object target, string fieldName, string[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(UnityEngine.Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloatArray(UnityEngine.Object target, string fieldName, float[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── HUD ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// 파워업 게이지 HUD: 하단 중앙 슬롯 4개 + 슬롯당 레벨 핍 5개, 그리고 DevCheats 오버레이.
        /// 좌표는 640×360 뷰(월드 40×22.5u) 안에서 픽셀(1/16u) 정렬로 배치한다.
        /// </summary>
        static void CreateHud(BattleDirector director, Sprite slotSprite, Sprite pipSprite)
        {
            const float px = 1f / AssetsPPU;
            const float slotSpacing = 24 * px;       // 슬롯 중심 간격 1.5u
            const float frameCenterY = -(RefResolutionY / 2f / AssetsPPU) + 10 * px; // 화면 하단 + 여백 4px + 프레임 절반 6px
            const float pipRowY = frameCenterY + 9 * px;
            const float pipSpacing = 4 * px;

            var hudRoot = new GameObject("Hud");

            var slotFrames = new SpriteRenderer[PowerUpHudView.SlotCount];
            var pips = new SpriteRenderer[PowerUpHudView.SlotCount * PowerUpHudView.MaxPipsPerSlot];

            for (int slot = 0; slot < PowerUpHudView.SlotCount; slot++)
            {
                float x = (slot - (PowerUpHudView.SlotCount - 1) / 2f) * slotSpacing;

                var frame = new GameObject($"Slot{slot}");
                frame.transform.SetParent(hudRoot.transform, false);
                frame.transform.localPosition = new Vector3(x, frameCenterY, 0f);
                var frameRenderer = frame.AddComponent<SpriteRenderer>();
                frameRenderer.sprite = slotSprite;
                frameRenderer.sortingOrder = 100;
                slotFrames[slot] = frameRenderer;

                // 슬롯 글자 아이콘 (S/M/O/B) — 프레임 중앙, 상태와 무관한 정적 표시
                var icon = new GameObject($"Slot{slot}Icon");
                icon.transform.SetParent(frame.transform, false);
                var iconRenderer = icon.AddComponent<SpriteRenderer>();
                iconRenderer.sprite = WritePixelSprite(
                    $"{SpriteDir}/hud_icon_{slot}.png", HudIconPixels[slot], HudPalette);
                iconRenderer.sortingOrder = 102;
                iconRenderer.color = new Color32(0xC8, 0xD4, 0xE8, 0xFF);

                for (int pip = 0; pip < PowerUpHudView.MaxPipsPerSlot; pip++)
                {
                    float pipX = x + (pip - (PowerUpHudView.MaxPipsPerSlot - 1) / 2f) * pipSpacing;

                    var pipGo = new GameObject($"Slot{slot}Pip{pip}");
                    pipGo.transform.SetParent(hudRoot.transform, false);
                    pipGo.transform.localPosition = new Vector3(pipX, pipRowY, 0f);
                    var pipRenderer = pipGo.AddComponent<SpriteRenderer>();
                    pipRenderer.sprite = pipSprite;
                    pipRenderer.sortingOrder = 101;
                    pips[slot * PowerUpHudView.MaxPipsPerSlot + pip] = pipRenderer;
                }
            }

            var hudView = hudRoot.AddComponent<PowerUpHudView>();
            SetReference(hudView, "_director", director);
            SetReferenceArray(hudView, "_slotFrames", slotFrames);
            SetReferenceArray(hudView, "_pips", pips);

            var cheats = hudRoot.AddComponent<DevCheats>();
            SetReference(cheats, "_director", director);
        }

        static void SetReferenceArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} 직렬화 필드를 못 찾았다.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
            // 타이틀이 0번(시작 씬), 전투가 1번. SampleScene은 빌드에서 제외.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
