using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 보드 한 줄. 서버가 쓰는 짧은 키를 그대로 받는다 (n=이름, s=점수, sh=기체, g=등급).
    /// JsonUtility는 이름이 정확히 맞는 필드만 채우고 나머지 키는 조용히 버린다 —
    /// 서버가 필드를 더 붙여도 클라이언트가 깨지지 않는다.
    /// </summary>
    [Serializable]
    public sealed class ScoreboardEntry
    {
        public string n;
        public long s;
        public string sh;
        public string g;

        // P1.5 상세 통계. 서버가 값이 없는 항목은 키 자체를 빼므로(JSON.stringify가
        // undefined를 지운다) 구 기록에서는 전부 0으로 남는다 — st는 1부터 시작하는
        // 스테이지 번호라 `st <= 0`이 곧 "이 기록엔 문맥이 없다"는 뜻이다.
        public int st;   // 도달 스테이지 (1부터)
        public int rm;   // 도달 룸 (1부터)
        public int op;   // 옵션 수
        public int lv;   // 게이지 레벨 합
        public int bb;   // 사용한 봄 수 (0 = NO BOMB)
        public int gz;   // 그레이즈 누계
        public int mx;   // 최고 콤보 배율

        /// <summary>
        /// 허용한 피격 수 (REQ-105). **0이 최고 성적**이라 "값이 없음"과 구분해야 한다 —
        /// 이 필드가 없던 시절의 기록에서 0을 그리면 무피격 주행이라는 거짓말이 된다.
        /// 서버가 키 자체를 빼므로 <see cref="MissingHits"/>(-1)로 표시해 두고 화면은
        /// 그 값을 '-'로 그린다. 채우는 곳은 <see cref="ScoreboardClient.MarkMissingHits"/>.
        /// </summary>
        public int ht;

        /// <summary>이 기록에 <c>ht</c> 키가 아예 없었다는 표식.</summary>
        public const int MissingHits = -1;

        public bool HasHits => ht >= 0;
    }

    [Serializable]
    sealed class ScoreboardBoard
    {
        public ScoreboardEntry[] entries;
    }

    [Serializable]
    sealed class ScoreSubmitRequest
    {
        public string name;
        public string token;
        public long score;
        public string seed;
        public string ship;
        public string difficulty;
        public string grade;
        public string replayHash;
        public bool daily;

        // P1.5 상세 통계 (서버 선택 필드). JsonUtility는 값이 0이어도 키를 항상 실어
        // 보내는데, 서버가 0 이상만 받아들이므로 그대로도 안전하다.
        public int stage;
        public int room;
        public int options;
        public int levels;
        public int bombs;
        public int graze;
        public int maxCombo;
        public int hitsTaken;
    }

    [Serializable]
    sealed class ScoreSubmitResponse
    {
        public bool ok;
        public int rank;
    }

    /// <summary>제출할 런 요약. 이름·토큰은 클라이언트가 채우므로 호출측이 몰라도 된다.</summary>
    public struct ScoreSubmission
    {
        public long Score;
        public string Seed;
        public string Ship;
        public string Difficulty;
        public string Grade;
        public string ReplayHash;
        public bool Daily;

        // P1.5: 점수 한 줄로는 "어떻게 죽었는지"가 남지 않는다. 도달 지점과 빌드 규모,
        // 봄/그레이즈까지 같이 올려야 보드가 기록이 아니라 이야기가 된다.
        public int Stage;
        public int Room;
        public int Options;
        public int Levels;
        public int Bombs;
        public int Graze;
        public int MaxCombo;

        /// <summary>허용한 피격 수 (REQ-105). 서버 필드 <c>ht</c>, 상한 999.</summary>
        public int HitsTaken;
    }

    /// <summary>
    /// 코루틴 숙주. 스코어보드는 화면(게임오버/타이틀)이 사라진 뒤에도 응답이 올 수 있고,
    /// 씬 전환으로 코루틴이 끊기면 요청이 조용히 유실된다 — 그래서 전용 GameObject를
    /// DontDestroyOnLoad로 하나만 띄운다. 게임 상태는 건드리지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreboardRunner : MonoBehaviour
    {
    }

    /// <summary>
    /// 글로벌 스코어보드 클라이언트 (P1). 제출/조회 둘 다 실패해도 게임 진행을 막지 않는
    /// 것이 원칙이다 — 예외를 던지지 않고 콜백에 에러 문자열만 넘긴다.
    ///
    /// 신원은 익명 디바이스 토큰(PlayerPrefs)뿐이다. 계정도, 개인정보도 없다.
    /// </summary>
    public static class ScoreboardClient
    {
        public const string BaseUrl = "https://rss-scoreboard.coreboard.workers.dev";

        /// <summary>익명 디바이스 토큰 (서버가 중복/도배를 거르는 유일한 키).</summary>
        public const string TokenPrefKey = "rss_device_token";

        /// <summary>보드에 표시할 이름. 없으면 제출 시점에 프롬프트로 받는다.</summary>
        public const string NamePrefKey = "rss_player_name";

        public const int NameMinLength = 2;
        public const int NameMaxLength = 10;

        /// <summary>WebGL 폰 회선을 감안한 상한. 넘으면 조용히 포기한다.</summary>
        const int TimeoutSeconds = 8;

        static ScoreboardRunner _runner;

        static ScoreboardRunner Host
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("ScoreboardClient");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<ScoreboardRunner>();
                }
                return _runner;
            }
        }

        /// <summary>
        /// 디바이스 토큰. 없으면 이 자리에서 만들어 저장한다. Guid는 Presentation 소관이라
        /// 써도 된다 (결정론 금지 규칙은 Shmup.Core 한정 — AGENTS.md §4).
        /// </summary>
        public static string DeviceToken
        {
            get
            {
                string token = PlayerPrefs.GetString(TokenPrefKey, string.Empty);
                if (string.IsNullOrEmpty(token))
                {
                    token = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(TokenPrefKey, token);
                    PlayerPrefs.Save();
                }
                return token;
            }
        }

        /// <summary>저장된 표시 이름. 없으면 빈 문자열.</summary>
        public static string PlayerName
        {
            get { return PlayerPrefs.GetString(NamePrefKey, string.Empty); }
            set
            {
                string clean = SanitizeName(value);
                if (string.IsNullOrEmpty(clean)) return;
                PlayerPrefs.SetString(NamePrefKey, clean);
                PlayerPrefs.Save();
            }
        }

        public static bool HasPlayerName
        {
            get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(NamePrefKey, string.Empty)); }
        }

        /// <summary>
        /// 서버 규칙(2~10자)에 맞게 다듬는다. 제어문자는 보드 표시를 깨뜨리므로 버리고,
        /// 길이 미달이면 null을 돌려 "이름 없음"으로 취급한다 — 서버가 400을 주기 전에
        /// 클라이언트에서 걸러야 사용자가 이유를 알 수 있다.
        /// </summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var sb = new StringBuilder(NameMaxLength);
            for (int i = 0; i < raw.Length && sb.Length < NameMaxLength; i++)
            {
                char c = raw[i];
                if (char.IsControl(c)) continue;
                sb.Append(c);
            }
            string trimmed = sb.ToString().Trim();
            return trimmed.Length >= NameMinLength ? trimmed : null;
        }

        /// <summary>
        /// 점수 제출. 콜백은 (순위, 에러) — 에러가 null이면 성공이다.
        /// 이름이 없으면 요청을 보내지 않는다 (호출측이 먼저 프롬프트를 띄워야 한다).
        /// </summary>
        public static void Submit(ScoreSubmission submission, Action<int, string> onDone)
        {
            string playerName = PlayerName;
            if (string.IsNullOrEmpty(playerName))
            {
                if (onDone != null) onDone(0, "NO NAME");
                return;
            }

            var payload = new ScoreSubmitRequest
            {
                name = playerName,
                token = DeviceToken,
                score = submission.Score,
                seed = submission.Seed ?? string.Empty,
                ship = submission.Ship ?? string.Empty,
                difficulty = submission.Difficulty ?? string.Empty,
                grade = submission.Grade ?? string.Empty,
                replayHash = submission.ReplayHash ?? string.Empty,
                daily = submission.Daily,
                // 서버는 음수/상한 초과를 통째로 버린다 — 여기서 미리 잘라 "값이 통째로
                // 사라진 기록"이 나오지 않게 한다 (상한은 worker.js의 stat()과 같은 값).
                stage = Clamp(submission.Stage, 99),
                room = Clamp(submission.Room, 99),
                options = Clamp(submission.Options, 9),
                levels = Clamp(submission.Levels, 99),
                bombs = Clamp(submission.Bombs, 999),
                graze = Clamp(submission.Graze, 999999),
                maxCombo = Clamp(submission.MaxCombo, 99),
                hitsTaken = Clamp(submission.HitsTaken, 999)
            };
            Host.StartCoroutine(SubmitRoutine(JsonUtility.ToJson(payload), onDone));
        }

        /// <summary>서버가 받아 주는 범위로 자른다. 음수는 0.</summary>
        static int Clamp(int value, int max)
        {
            if (value < 0) return 0;
            return value > max ? max : value;
        }

        /// <summary>
        /// 보드 한 줄에 들어갈 기체 약칭. 이름 전체를 쓸 자리가 없어 두 글자로 줄인다 —
        /// 모르는 id는 앞 두 글자를 대문자로(신규 기체가 추가돼도 칸이 비지 않는다).
        /// </summary>
        public static string ShipAbbrev(string shipId)
        {
            if (string.IsNullOrEmpty(shipId)) return "  ";
            switch (shipId.ToLowerInvariant())
            {
                case "starter": return "ST";
                case "interceptor": return "IC";
                case "bulwark": return "BW";
                default:
                    return shipId.Length >= 2
                        ? shipId.Substring(0, 2).ToUpperInvariant()
                        : shipId.ToUpperInvariant();
            }
        }

        /// <summary>보드 조회. 콜백은 (엔트리, 에러) — 점수 내림차순, 최대 100줄.</summary>
        public static void FetchBoard(bool daily, Action<ScoreboardEntry[], string> onDone)
        {
            Host.StartCoroutine(BoardRoutine(daily ? "/v1/board/daily" : "/v1/board/all", onDone));
        }

        static IEnumerator SubmitRoutine(string json, Action<int, string> onDone)
        {
            using (var request = new UnityWebRequest(BaseUrl + "/v1/scores", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendWithTimeout(request);

                string error = DescribeError(request);
                if (error != null)
                {
                    Report("submit", error);
                    if (onDone != null) onDone(0, error);
                    yield break;
                }

                ScoreSubmitResponse response = null;
                string parseError = null;
                try
                {
                    response = JsonUtility.FromJson<ScoreSubmitResponse>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    parseError = e.GetType().Name;
                }
                if (response == null || !response.ok)
                {
                    string reason = parseError ?? "REJECTED";
                    Report("submit", reason);
                    if (onDone != null) onDone(0, reason);
                    yield break;
                }
                if (onDone != null) onDone(response.rank, null);
            }
        }

        static IEnumerator BoardRoutine(string path, Action<ScoreboardEntry[], string> onDone)
        {
            using (var request = UnityWebRequest.Get(BaseUrl + path))
            {
                yield return SendWithTimeout(request);

                string error = DescribeError(request);
                if (error != null)
                {
                    Report("board", error);
                    if (onDone != null) onDone(null, error);
                    yield break;
                }

                ScoreboardBoard board = null;
                string parseError = null;
                try
                {
                    board = JsonUtility.FromJson<ScoreboardBoard>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    parseError = e.GetType().Name;
                }
                if (board == null || board.entries == null)
                {
                    string reason = parseError ?? "EMPTY";
                    Report("board", reason);
                    if (onDone != null) onDone(null, reason);
                    yield break;
                }
                MarkMissingHits(request.downloadHandler.text, board.entries);
                if (onDone != null) onDone(board.entries, null);
            }
        }

        /// <summary>
        /// <c>ht</c> 키가 없는 기록을 <see cref="ScoreboardEntry.MissingHits"/>로 표시한다.
        ///
        /// JsonUtility는 없는 키를 0으로 채우는데, 피격 수는 **0이 최고 성적**이라
        /// 그 0이 "무피격 완주"로 읽힌다. 다른 통계는 1부터 시작하는 스테이지 번호로
        /// 구 항목을 가려낼 수 있었지만(<c>st &lt;= 0</c>), 피격 수에는 그런 여유가 없다.
        /// 서버가 값이 없는 필드의 키 자체를 빼므로(JSON.stringify가 undefined를 지운다)
        /// **원문에 키가 있었는지**가 유일하게 정확한 판정이다.
        ///
        /// 스캔은 entries 배열의 최상위 객체 경계만 센다 — 문자열 안의 중괄호와
        /// 이스케이프를 건너뛰므로 이름에 <c>{</c>나 <c>"</c>가 들어 있어도 어긋나지 않는다.
        /// 실패하면 아무것도 표시하지 않는다: 못 가려낸 구 항목이 0으로 보이는 것보다
        /// 예외로 보드가 통째로 사라지는 쪽이 나쁘다.
        /// </summary>
        static void MarkMissingHits(string json, ScoreboardEntry[] entries)
        {
            if (string.IsNullOrEmpty(json) || entries == null) return;
            int index = 0;
            int depth = 0;
            int objectStart = -1;
            bool inString = false;
            for (int i = 0; i < json.Length && index < entries.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') i++;            // 이스케이프 한 글자 건너뛰기
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{')
                {
                    depth++;
                    // 배열 요소는 루트 객체({"entries":[...]}) 바로 안쪽 깊이 2다.
                    if (depth == 2) objectStart = i;
                    continue;
                }
                if (c != '}') continue;
                depth--;
                if (depth != 1 || objectStart < 0) continue;
                // 부분 문자열을 뜨지 않고 구간만 훑는다 (보드 100줄 × 조회마다).
                bool hasKey = json.IndexOf(
                    "\"ht\"", objectStart, i - objectStart + 1, StringComparison.Ordinal) >= 0;
                if (entries[index] != null && !hasKey)
                    entries[index].ht = ScoreboardEntry.MissingHits;
                objectStart = -1;
                index++;
            }
        }

        /// <summary>
        /// 타임아웃을 직접 감시한다. <c>UnityWebRequest.timeout</c>은 WebGL에서 무시되고,
        /// 게임오버 화면은 timeScale이 0일 수 있어 WaitForSeconds도 못 쓴다 —
        /// 실시간 시계로 재고 초과하면 Abort한다.
        /// </summary>
        static IEnumerator SendWithTimeout(UnityWebRequest request)
        {
            request.timeout = TimeoutSeconds;
            var operation = request.SendWebRequest();
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    request.Abort();
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>성공이면 null, 아니면 화면에 띄우지 않을 짧은 진단 문자열.</summary>
        static string DescribeError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.InProgress)
                return "TIMEOUT";
            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = request.error;
                return string.IsNullOrEmpty(message) ? request.result.ToString() : message;
            }
            if (request.responseCode >= 400)
                return "HTTP " + request.responseCode;
            return null;
        }

        /// <summary>
        /// 실패는 플레이어에게 조용하다. 다만 완전히 삼키면 원인 추적이 불가능해지므로
        /// 개발 빌드에서만 경고를 남긴다 (기존 capacity 경고와 같은 규칙).
        /// </summary>
        static void Report(string what, string error)
        {
            if (Debug.isDebugBuild || Application.isEditor)
                Debug.LogWarning($"[scoreboard] {what} failed — {error}");
        }
    }

    /// <summary>
    /// 스코어보드 표시 이름 입력. 폰 브라우저(WebGL)에서는 Unity의 InputField에 소프트
    /// 키보드가 붙지 않아 글자를 아예 못 넣는다 — 브라우저의 <c>window.prompt</c>를
    /// jslib 브리지로 빌려 쓴다 (Assets/Plugins/WebGL/NamePrompt.jslib).
    /// </summary>
    public static class NamePrompt
    {
        /// <summary>WebGL이 아닌 곳에서 쓰는 고정 이름 (에디터 흐름 확인용).</summary>
        public const string EditorFallbackName = "DEV";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern IntPtr RssPromptName(string defaultName);
#endif

        /// <summary>취소하면 null. 그 외에는 사용자가 넣은 문자열 그대로(정제는 호출측).</summary>
        public static string Ask(string defaultName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr buffer = RssPromptName(defaultName ?? string.Empty);
            if (buffer == IntPtr.Zero) return null;   // window.prompt 취소
            return ReadUtf8(buffer);
#else
            // 에디터/데스크톱에는 브라우저 프롬프트가 없다. 별도 입력 UI를 만드는 대신
            // 고정 이름으로 제출 흐름만 확인한다 — 실제 입력 경로는 WebGL 빌드에서 본다.
            return string.IsNullOrEmpty(defaultName) ? EditorFallbackName : defaultName;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// jslib가 <c>_malloc</c>으로 잡아 준 UTF-8 버퍼를 직접 읽는다.
        /// <c>Marshal.PtrToStringAnsi</c>는 인코딩 해석이 플랫폼마다 갈려 한글 이름이 깨진다.
        ///
        /// 버퍼는 해제하지 않는다: 세션당 한 번, 수십 바이트다. jslib에서 <c>_free</c>를
        /// 부르려면 이맥스크립튼 심벌 의존 선언이 필요해 빌드가 깨질 위험이 더 크다.
        /// </summary>
        static string ReadUtf8(IntPtr buffer)
        {
            int length = 0;
            while (Marshal.ReadByte(buffer, length) != 0) length++;
            if (length == 0) return string.Empty;
            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
#endif
    }
}
