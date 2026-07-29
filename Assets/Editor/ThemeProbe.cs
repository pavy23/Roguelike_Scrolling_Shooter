using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shmup.EditorTools
{
    /// <summary>임시 진단 도구: 시드별 테마 순열/보스/세그먼트 배열을 파일로 덤프한다.</summary>
    public static class ThemeProbe
    {
        public static string Run(string outPath)
        {
            var data = Shmup.Core.Content.GameDataParser.Parse(
                Resources.Load<TextAsset>("GameData/enemies").text,
                Resources.Load<TextAsset>("GameData/weapons").text,
                Resources.Load<TextAsset>("GameData/waves").text,
                Resources.Load<TextAsset>("GameData/rewards").text,
                Resources.Load<TextAsset>("GameData/ships").text,
                Resources.Load<TextAsset>("GameData/scoring").text);
            var gen = new Shmup.Core.Generation.SegmentStageGenerator(data.StageGeneration);

            var sb = new StringBuilder();
            ulong[] seeds = { 1UL, 2UL, 7UL, 42UL, 1234UL, 99999UL, 555UL, 20260729UL };
            var themeOrders = new HashSet<string>();
            foreach (var seed in seeds)
            {
                var order = new List<string>();
                sb.Append("seed ").Append(seed).Append('\n');
                for (int stage = 1; stage <= 5; stage++)
                {
                    var plan = gen.Generate(seed, stage, stage);
                    order.Add(plan.ThemeId);
                    var ids = new List<string>();
                    for (int i = 0; i < plan.Segments.Count; i++)
                        ids.Add(plan.Segments[i].SegmentId);
                    sb.Append("  s").Append(stage).Append(' ')
                      .Append(plan.ThemeId).Append(" / ").Append(plan.BossId)
                      .Append("  [").Append(string.Join(", ", ids)).Append("]\n");
                }
                themeOrders.Add(string.Join(">", order));
            }
            sb.Append("\ndistinct theme orders across ").Append(seeds.Length)
              .Append(" seeds: ").Append(themeOrders.Count).Append('\n');
            foreach (var o in themeOrders) sb.Append("  ").Append(o).Append('\n');

            System.IO.File.WriteAllText(outPath, sb.ToString());
            return "written " + outPath;
        }
    }
}
