using Fusion;
using UnityEngine;

namespace Game.BossContent
{
    /// 페이즈에 따라 프리팹에 미리 올려둔 무기 메시를 껐다 켠다.
    /// 순수 연출 — 상태를 바꾸지 않고 BossNetState.CurrentPhase를 읽기만 하므로
    /// ServerAuthoritativeBehaviour가 아닌 NetworkBehaviour를 상속한다.
    /// 무기가 본(손·등)에 각각 붙어 한 부모로 묶을 수 없으므로 페이즈당 오브젝트 목록을 받는다.
    public class BossPhaseWeaponSwitcher : NetworkBehaviour
    {
        /// 한 페이즈에서 켜둘 오브젝트 묶음. 예: 3페이즈 = { 검, 방패 }.
        [System.Serializable]
        public class PhaseWeapons
        {
            public GameObject[] Objects;
        }

        [SerializeField] private BossNetState _netState;
        [Tooltip("인덱스 = 페이즈-1. 페이즈가 배열보다 크면 마지막 묶음을 유지한다.")]
        [SerializeField] private PhaseWeapons[] _byPhase;

        private int _appliedPhase = -1;

        // 늦게 참여한 클라이언트는 이미 진행된 페이즈로 스폰된다 — 여기서 한 번 맞춰준다.
        public override void Spawned() => Apply(_netState.CurrentPhase);

        // OnChangedRender 대신 폴링 — BossView가 PatternStartSeq를 다루는 방식과 같다.
        public override void Render()
        {
            if (_netState.CurrentPhase != _appliedPhase)
                Apply(_netState.CurrentPhase);
        }

        private void Apply(int phase)
        {
            _appliedPhase = phase;
            // BossEntity.Spawned와의 실행 순서가 보장되지 않아 0으로 읽힐 수 있다.
            int index = Mathf.Clamp(phase - 1, 0, _byPhase.Length - 1);

            // 반드시 두 번에 나눠 돈다. 같은 무기를 여러 페이즈에 등록하는 건 정상적인 사용법인데
            // (대검을 1~3페이즈에 넣는 식), 한 번에 SetActive(i == index)로 처리하면
            // 뒤 페이즈의 false가 앞 페이즈의 true를 덮어써 현재 무기까지 꺼진다.
            for (int i = 0; i < _byPhase.Length; i++)
                SetActive(_byPhase[i], false);

            SetActive(_byPhase[index], true);
        }

        private static void SetActive(PhaseWeapons phase, bool active)
        {
            var objects = phase?.Objects;
            if (objects == null) return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) objects[i].SetActive(active);
            }
        }

        private void OnValidate()
        {
            if (_netState == null) _netState = GetComponent<BossNetState>();
            if (_byPhase != null && _byPhase.Length == 0)
                Debug.LogWarning($"{name}: BossPhaseWeaponSwitcher의 페이즈별 무기 목록이 비어 있음", this);
        }
    }
}
