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
        Text _scoreText, _statsText, _extraText, _modifierText;
        int _shownRun = int.MinValue;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("GameOverCanvas", 90);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0.35f, 0.02f, 0.05f, 0.45f));
            var panel = UiKit.CreatePanel(canvas.transform, new Vector2(400f, 180f));

            UiKit.CreateCornerText(panel, _fontBold, "GAME OVER", 22, UiKit.TextDanger,
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), TextAnchor.UpperCenter, "Title");
            _scoreText = UiKit.CreateCornerText(panel, _fontBold, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), TextAnchor.UpperCenter, "Score");
            _statsText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -74f), TextAnchor.UpperCenter, "Stats");
            _extraText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), TextAnchor.UpperCenter, "Extra");
            _modifierText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), TextAnchor.UpperCenter, "Modifiers");
            UiKit.CreateCornerText(panel, _font,
                "[ENTER]/(A) 재출격 - 파워업 승계      [R]/(B) 타이틀", 11, UiKit.TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), TextAnchor.LowerCenter, "Hints");

            _root.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            bool over = _director.IsRunOver;
            if (_root.activeSelf != over)
                _root.SetActive(over);
            if (!over) return;

            if (_shownRun != _director.RunNumber)
            {
                _shownRun = _director.RunNumber;
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
