using System;
using System.IO;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 세이브 파일 안전 쓰기/읽기 (퍼블리셔 심사 지적: 원본 삭제 후 이동 방식이라
    /// 쓰기 중 종료 시 진행이 소실될 수 있었다).
    ///
    /// 쓰기: tmp에 기록 → 기존 파일을 bak으로 보존 → tmp를 본 파일로 교체.
    /// 읽기: 본 파일이 손상/부재면 bak으로 폴백. 어느 쪽도 못 읽으면 null.
    /// </summary>
    public static class SafeFile
    {
        public static void Write(string path, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string temp = path + ".tmp";
                string backup = path + ".bak";
                File.WriteAllText(temp, content);

                // 이전 본 파일을 백업으로 승격 (교체 실패 시 복구 지점)
                if (File.Exists(path))
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SafeFile] 쓰기 실패({e.GetType().Name}) {path}: {e.Message}");
            }
        }

        /// <summary>본 파일 → 백업 순서로 읽는다. validate가 false면 다음 후보로 넘어간다.</summary>
        public static string Read(string path, Func<string, bool> validate = null)
        {
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    string text = File.ReadAllText(candidate);
                    if (validate != null && !validate(text))
                    {
                        Debug.LogWarning($"[SafeFile] 검증 실패 — 다음 후보로: {candidate}");
                        continue;
                    }
                    return text;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SafeFile] 읽기 실패({e.GetType().Name}) {candidate}: {e.Message}");
                }
            }
            return null;
        }

        public static void Delete(string path)
        {
            foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                try
                {
                    if (File.Exists(candidate)) File.Delete(candidate);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SafeFile] 삭제 실패({e.GetType().Name}) {candidate}: {e.Message}");
                }
            }
        }
    }
}
