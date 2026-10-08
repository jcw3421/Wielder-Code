using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// 디버프 UI 표시 테스트용 임시 도구. 숫자키로 대상에 디버프를 껐다 켰다 한다.
    /// 1=슬로우 2=공격력↓ 3=방어력↓ 4=도트 5=스턴, 0=전부 해제.
    ///
    /// ※ 실제 게임에선 서버(State Authority)가 DebuffApplied/Removed를 발행한다.
    ///   이건 그 이벤트를 흉내 내 UI(StatusEffectDisplay) 표시만 확인하는 용도다 — 빌드에 넣지 말 것.
    /// </summary>
    public sealed class DebuffTester : MonoBehaviour
    {
        [Tooltip("디버프를 걸 대상 id. 로컬 플레이어/파티원 playerId, 또는 보스 id.")]
        [SerializeField] private int _targetId;

        [Tooltip("지속 시간(틱). StatusEffectDisplay가 틱레이트로 초 환산한다. 300틱 ≈ 5초(@60).")]
        [SerializeField] private int _durationTicks = 300;

        [SerializeField] private int _value = 10;

        // 숫자키 1~5에 순서대로 매핑.
        private static readonly DebuffType[] Types =
        {
            DebuffType.Slow,
            DebuffType.AttackDown,
            DebuffType.DefenseDown,
            DebuffType.DamageOverTime,
            DebuffType.Stun,
        };

        // 지금 켜둔 디버프(토글 상태 추적).
        private readonly HashSet<DebuffType> _on = new();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < Types.Length; i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    Toggle(Types[i]);

            if (keyboard.digit0Key.wasPressedThisFrame)
                ClearAll();
        }

        private void Toggle(DebuffType type)
        {
            if (_on.Remove(type))
            {
                GameplayEvents.RaiseDebuffRemoved(_targetId, type);
                Debug.Log($"[DebuffTester] 해제 {type} → target {_targetId}");
            }
            else
            {
                _on.Add(type);
                GameplayEvents.RaiseDebuffApplied(_targetId, new DebuffInfo(type, _value, _durationTicks, cleansable: true));
                Debug.Log($"[DebuffTester] 적용 {type} → target {_targetId}");
            }
        }

        private void ClearAll()
        {
            foreach (DebuffType type in _on)
                GameplayEvents.RaiseDebuffRemoved(_targetId, type);
            _on.Clear();
            Debug.Log($"[DebuffTester] 전부 해제 → target {_targetId}");
        }
    }
}
