using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// 테스트 공용 하네스 (정리 3번, 2026-08-07). 테스트 파일마다 복붙되던
    /// 저장소 루트 탐색(23곳, 6가지 변형)과 리포지토리 GameData 전체 파싱을
    /// 한 곳으로 모은다. **게임 코드에는 일절 관여하지 않는 테스트 전용
    /// 유틸리티다** — 여기 무엇을 넣든 본 게임 동작은 변하지 않는다.
    /// </summary>
    public static class TestKit
    {
        /// <summary>
        /// GameData 폴더를 가진 저장소 루트를 찾는다.
        ///
        /// 시작 후보를 여러 곳 두는 이유: dotnet 러너는 TestDirectory가 맞고,
        /// Unity 내장 NUnit은 WorkDirectory를 안 채우거나 프로젝트 밖을
        /// 가리키는 경우가 있다 (기존 복붙 변형들이 각자 다른 후보를 골랐던
        /// 이유). 전 후보에서 상향 탐색하면 양쪽 러너를 모두 감당한다.
        /// </summary>
        public static string FindRepositoryRoot()
        {
            foreach (string start in new[]
            {
                SafeContext(c => c.WorkDirectory),
                SafeContext(c => c.TestDirectory),
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            })
            {
                if (string.IsNullOrEmpty(start)) continue;
                var current = new DirectoryInfo(start);
                while (current != null)
                {
                    if (Directory.Exists(
                        Path.Combine(current.FullName, "GameData")))
                        return current.FullName;
                    current = current.Parent;
                }
            }
            throw new DirectoryNotFoundException(
                "Could not locate the repository GameData directory.");
        }

        /// <summary>
        /// 리포지토리 GameData 6종을 전부 읽어 파싱한다. 부분 파싱(3~5인자)을
        /// 검증하는 테스트는 이 헬퍼를 쓰지 말고 자기 조합을 유지할 것 —
        /// 로드 집합이 달라지면 GameDataSet 내용도 달라진다.
        /// </summary>
        public static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(FindRepositoryRoot(), "GameData");
            string Read(string name) =>
                File.ReadAllText(Path.Combine(gameData, name));
            return GameDataParser.Parse(
                Read("enemies.json"),
                Read("weapons.json"),
                Read("waves.json"),
                Read("rewards.json"),
                Read("ships.json"),
                Read("scoring.json"));
        }

        static string SafeContext(Func<TestContext, string> pick)
        {
            try
            {
                TestContext context = TestContext.CurrentContext;
                return context != null ? pick(context) : null;
            }
            catch
            {
                // TestContext는 러너 밖(정적 초기화 등)에서 던질 수 있다.
                return null;
            }
        }
    }
}
