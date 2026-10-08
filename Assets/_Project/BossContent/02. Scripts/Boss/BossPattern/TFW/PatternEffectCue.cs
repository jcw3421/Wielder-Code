using UnityEngine;

namespace Game.BossContent
{
    /// 패턴 연출 1건 — "Active 시작 후 몇 초에, 어느 본에, 무슨 파티클을".
    /// 판정(서버)과 분리된 순수 연출이라 값이 어긋나도 데미지에는 영향이 없다.
    [System.Serializable]
    public class PatternEffectCue
    {
        [Tooltip("인스펙터 식별용 — 로직에는 쓰이지 않는다")]
        public string Label;

        [Tooltip("Active 시작 기준 초. 보통 타격 시점과 맞추지만 예고 이펙트는 더 앞에 둔다.")]
        public float AtSeconds;

        [Tooltip("★ 여기에 파티클 프리팹을 꽂는다. 비어 있으면 그냥 넘어간다.")]
        public GameObject Prefab;

        [Tooltip("붙일 본 이름. 비우면 보스 루트. 이름은 자식에서 재귀로 찾는다.")]
        public string AnchorBoneName;

        [Tooltip("체크 시 본에 붙어 따라다닌다. 해제하면 생성 위치에 남는다(장판·바닥 균열 등).")]
        public bool AttachToAnchor = true;

        public float Lifetime = 3f;

        [Tooltip("앵커 기준 로컬 오프셋(x=우, y=상, z=전방). 판정이 보스 앞에서 일어나는 패턴은 z를 그 도달거리만큼 준다.")]
        public Vector3 LocalOffset;

        [Tooltip("프리팹 배율. 이 보스는 키가 7m라 사람 크기로 만들어진 이펙트는 1.0으로는 묻힌다.")]
        public float Scale = 1f;

        [Tooltip("체크 시 이 큐는 손으로 맞춘 값으로 본다 — 이펙트 셋업(Setup TheFirstWielder Effects)이 " +
                 "다시 계산해도 시점·위치·크기·프리팹을 덮어쓰지 않는다. 제작소에서 값을 건드리면 자동으로 켜진다.")]
        public bool ManualOverride;

        /// 이 큐의 프리팹을 놓는다. 앵커를 찾는 방식은 재생기마다 다르므로(패턴은 본 캐시,
        /// 등장은 보스 루트) 위치·회전·부모는 호출부가 정하고, 여기서는 배율과 수명만 책임진다.
        /// 프리팹이 안 꽂힌 큐는 조용히 건너뛴다.
        public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            if (Prefab == null) return null;

            var instance = Object.Instantiate(Prefab, position, rotation, parent);

            // 프리팹 자체 배율(0.28처럼 눌러둔 것도 있다)을 유지한 채 곱한다.
            if (Scale > 0f && !Mathf.Approximately(Scale, 1f))
                instance.transform.localScale *= Scale;

            if (Lifetime > 0f) Object.Destroy(instance, Lifetime);
            return instance;
        }
    }

    /// 이 패턴이 재생할 연출 목록을 내놓는다는 표시.
    /// PatternDefinition(공유 파일)을 건드리지 않으려고 TFW 쪽에서 인터페이스로 얹었다.
    public interface IPatternEffectSource
    {
        PatternEffectCue[] EffectCues { get; }
    }
}
