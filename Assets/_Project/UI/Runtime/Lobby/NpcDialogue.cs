using System.Linq;
using Fusion;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>로비 NPC 근접 대화. 사거리 안에서 F를 누르면 다음 대사로 넘어가고,
    /// 마지막 대사 다음엔 다시 안내 문구로 돌아온다.
    ///
    /// 대사는 순수한 로컬 연출이라 네트워크 상태를 만들지 않는다. 그래서 다른 클라이언트의
    /// 화면에는 아무 영향이 없고, 권한 규칙(서버 단독 상태 변경)과도 무관하다.</summary>
    public sealed class NpcDialogue : MonoBehaviour
    {
        [SerializeField] private string _npcName = "마을 주민";

        [Tooltip("F를 누를 때마다 순서대로 나온다. 마지막 다음엔 안내 문구로 돌아간다.")]
        [SerializeField, TextArea] private string[] _lines;

        [SerializeField] private float _range = 3.5f;
        [SerializeField] private float _labelHeight = 2.3f;
        [SerializeField] private TMP_FontAsset _font;

        /// 세션이 붙기 전엔 아바타가 없으므로 매 프레임 뒤지지 않고 이 간격으로만 다시 찾는다.
        private const float PlayerQueryInterval = 0.5f;

        private WorldLabel _label;
        private Transform _localPlayer;
        private float _nextPlayerQuery;

        /// -1 이면 "[F] ...에게 말 걸기" 안내, 0 이상이면 해당 대사.
        private int _line = -1;

        private void Awake()
        {
            _label = WorldLabel.Create(transform, Vector3.up * _labelHeight, 340f, 26f, _font, "DialogueLabel");
            _label.Visible = false;
        }

        private void Update()
        {
            var player = ResolveLocalPlayer();
            if (player == null)
            {
                Reset();
                return;
            }

            if ((player.position - transform.position).sqrMagnitude > _range * _range)
            {
                Reset();
                return;
            }

            if (!InputGuard.SuppressKeyboard && Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
                Advance();

            _label.Visible = true;
            _label.SetText(_line < 0
                ? $"[F] {_npcName}에게 말 걸기"
                : $"<b>{_npcName}</b>\n{_lines[_line]}");
        }

        private void Advance()
        {
            if (_lines == null || _lines.Length == 0) return;
            _line++;
            if (_line >= _lines.Length) _line = -1;
        }

        /// 사거리를 벗어나면 대사를 처음부터 다시 듣게 한다.
        private void Reset()
        {
            _line = -1;
            if (_label != null) _label.Visible = false;
        }

        private Transform ResolveLocalPlayer()
        {
            if (_localPlayer != null) return _localPlayer;
            if (Time.time < _nextPlayerQuery) return null;
            _nextPlayerQuery = Time.time + PlayerQueryInterval;

            var runner = NetworkRunner.Instances.FirstOrDefault();
            if (runner != null && runner.IsRunning &&
                runner.TryGetPlayerObject(runner.LocalPlayer, out var avatar) && avatar != null)
                _localPlayer = avatar.transform;

            return _localPlayer;
        }
    }
}
