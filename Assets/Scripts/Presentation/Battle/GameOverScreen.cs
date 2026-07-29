using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 정식 게임오버 화면 (UGUI, DevCheats 임시 표시 대체).
    /// [Enter]/패드 South = 재출격(파워업 승계), [R]/패드 East = 타이틀.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverScreen : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        GameObject _root;
        Text _titleText, _scoreText, _statsText, _extraText, _modifierText, _hintsText;
        Image _dim;
        int _shownRun = int.MinValue;
        bool _shownCleared;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("GameOverCanvas", 90);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            var panel = UiKit.CreatePanel(canvas.transform, new Vector2(400f, 180f));

            _titleText = UiKit.CreateCornerText(panel, _fontBold, UiText.GameOverTitle, 22, UiKit.TextDanger,
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), TextAnchor.UpperCenter, "Title");
            _scoreText = UiKit.CreateCornerText(panel, _fontBold, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), TextAnchor.UpperCenter, "Score");
            _statsText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -74f), TextAnchor.UpperCenter, "Stats");
            _extraText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), TextAnchor.UpperCenter, "Extra");
            _modifierText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), TextAnchor.UpperCenter, "Modifiers");
            _hintsText = UiKit.CreateCornerText(panel, _font,
                UiText.GameOverHints, 11, UiKit.TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), TextAnchor.LowerCenter, "Hints");
            _dim = UiKit.CreateDim(canvas.transform, Color.clear, "Tint");
            _dim.transform.SetAsFirstSibling();

            _root.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            // 사망(RunOver)과 완주(RunCleared)를 같은 패널로 처리하되 문면을 바꾼다 (REQ-031)
            bool finished = _director.IsRunFinished;
            if (_root.activeSelf != finished)
                _root.SetActive(finished);
            if (!finished) return;

            if (_shownRun != _director.RunNumber || _shownCleared != _director.IsRunCleared)
            {
                _shownRun = _director.RunNumber;
                _shownCleared = _director.IsRunCleared;
                bool cleared = _shownCleared;
                _titleText.text = cleared ? UiText.RunClearedTitle : UiText.GameOverTitle;
                _titleText.color = cleared ? UiKit.TextAccent : UiKit.TextDanger;
                _hintsText.text = cleared ? UiText.RunClearedHints : UiText.GameOverHints;
                if (_dim != null)
                    _dim.color = cleared
                        ? new Color(0.06f, 0.22f, 0.12f, 0.45f)   // 승리: 청록 틴트
                        : new Color(0.35f, 0.02f, 0.05f, 0.45f);  // 패배: 적색 틴트
                var stats = _director.RunStats;
                float accuracy = stats.ShotsFired > 0
                    ? (float)stats.ShotsHit / stats.ShotsFired * 100f : 0f;
                _scoreText.text =
                    $"SCORE  {_director.TotalScore:D8}   (run {_director.RunNumber}, stage {_director.StageIndex})";
                _statsText.text =
                    $"KILLS {stats.Kills}   CAPSULES {stats.CapsulesCollected}   ACC {accuracy:0.#}%   SHOTS {stats.ShotsFired}";
                _extraText.text =
                    $"BEST COMBO x{_director.BestMultiplier}   GRAZE {stats.GrazeCount}";
                _modifierText.text = DescribeModifiers(_director.ActiveModifiers);
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool restart = (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool toTitle = (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            if (restart) _director.RestartRun();
            else if (toTitle) UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
        }

        static string DescribeModifiers(Shmup.Core.Simulation.BattleModifier modifiers)
        {
            if (modifiers == Shmup.Core.Simulation.BattleModifier.None) return "";
            var sb = new System.Text.StringBuilder(64);
            sb.Append("BUILD: ");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.PierceShot, "PIERCE");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.Ricochet, "RICOCHET");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.HomingMissile, "HOMING");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.KillExplosion, "BLAST");
            return sb.ToString();
        }

        static void AppendModifier(
            System.Text.StringBuilder sb,
            Shmup.Core.Simulation.BattleModifier modifiers,
            Shmup.Core.Simulation.BattleModifier flag,
            string label)
        {
            if ((modifiers & flag) == 0) return;
            if (sb.Length > 7) sb.Append(" + ");
            sb.Append(label);
        }
    }
}
