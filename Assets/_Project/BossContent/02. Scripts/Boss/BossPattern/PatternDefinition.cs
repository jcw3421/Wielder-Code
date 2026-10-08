using Game.Core;
using UnityEngine;

namespace Game.BossContent
{

public abstract class PatternDefinition : ScriptableObject
    {
        [Header("공통 — 3단계 시간")]
        public float TelegraphSeconds = 1f;
        [Tooltip("체크 시 베이커가 ActiveSeconds를 건드리지 않는다 — loop 클립처럼 클립 길이가 지속시간을 정하지 않는 패턴용(돌진 등)")]
        public bool ManualActiveSeconds;
        public float ActiveSeconds = 0.3f;
        public float RecoverySeconds = 0.5f;
        [Header("공통 — 기즈모")]
        public Color GizmoColor = new Color(1f, 0.35f, 0.35f);

        [Header("공통 — 타겟팅")]
        [Tooltip("이 패턴이 대상을 고르는 방식. 패턴이 끝나면 어그로 기반 교전 타겟으로 되돌아간다. " +
                 "HighestAggro=어그로 1위가 확정으로 맞는다(잘 막은 탱커). " +
                 "Random=생존자 중 무작위, 탱커 뒤에 숨는 걸 무력화한다. " +
                 "Farthest=가장 먼 생존자, 뒤에서 딜하는 원거리를 끌어낸다.")]
        public TargetMode Targeting = TargetMode.HighestAggro;

        [Header("공통 — 밸런싱")]
        public int Weight = 1;
        public float CooldownSeconds = 0f;

        [Header("공통 — CC")]
        [Tooltip("적중 시 대상에게 거는 CC. 기본값 경직(Flinch). 스턴 전용 패턴은 Stun으로, CC를 안 거는 패턴은 None으로 오버라이드한다. 지속시간은 Core의 StatusEffectConstants 기본값을 쓴다.\n※ 근접 타격(HitPlayersIn*)에만 적용된다. 순수 장판 패턴(Eruption 등 근접 타격이 없는 패턴)은 이 값이 무시되고 Burst의 HitCC가 적용된다.")]
        public CCType HitCC = CCType.Flinch;

        [Header("공통 — 연출")]
        [Tooltip("조각(타격)마다 주는 화면 흔들림. 인덱스는 Active 조각 기준이라 전조·후딜은 세지 않는다. " +
                 "가벼운 타격은 Light, 한 방이 무거우면 Heavy, 안 흔들 조각은 None.\n" +
                 "조각이 하나뿐인 패턴은 한 칸만 채우면 된다. 비워두면 그 패턴은 흔들지 않는다.\n" +
                 "배열이 조각 수보다 짧으면 모자란 뒤쪽 조각은 마지막 값을 이어 쓴다.")]
        public CameraShakeStrength[] SegmentShakes;

        /// 이 조각(0-based)이 요청할 흔들림 세기.
        public CameraShakeStrength ShakeForSegment(int segmentIndex)
        {
            if (SegmentShakes == null || SegmentShakes.Length == 0) return CameraShakeStrength.None;
            return SegmentShakes[Mathf.Clamp(segmentIndex, 0, SegmentShakes.Length - 1)];
        }

        [Header("공통 — 기타")]
        public int Damage = 15;
        public int UnlockPhase = 1;
        [Tooltip("이 페이즈까지만 쓴다. 무기가 바뀌는 보스처럼 이전 무기 패턴을 봉인해야 할 때 쓴다.\n기본값(99)이면 사실상 상한 없음 — 기존 보스는 이 값을 건드릴 필요가 없다.")]
        public int LockPhase = 99;
        public int AnimatorPatternId;
        [Tooltip("타격 도달거리에 더해 개시를 앞당기는 여유")]
        public float EngageMargin = 0.5f;
        protected abstract float HitRange { get; }   // 각 패턴이 자기 기하 반경 제공
        public float AttackRange => HitRange + EngageMargin;   // 더는 abstract 아님


        // 각 SO가 자기 짝 패턴을 만든다 (다형성)
        public abstract BossPatternBase CreatePattern(BossServices services);

        /// 에디터 튜닝용: 이 패턴의 타격 범위를 보스 위치·방향 기준으로 그린다. 자식이 자기 모양 구현.
        public virtual void DrawRangeGizmo(Transform boss) { }

    }
}
