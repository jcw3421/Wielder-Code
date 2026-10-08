using UnityEngine;

namespace Game.BossContent
{
    public class BossView    
    {
        public Animator Animator { get; private set; }
        public IBossAnimSource NetState { get; private set; }
        /// 아직 아무 패턴도 보지 못했다는 표시. 실제 seq는 0부터 올라가므로 음수와 겹치지 않는다.
        private const int NoPatternSeen = -1;
        /// 기믹 seq도 같은 이유로 같은 표시를 쓴다(생성자 주석 참고).
        private const int NoGimmickSeen = -1;

        private int _lastPatternSeq = NoPatternSeen;
        private int _lastGimmickSeq = NoGimmickSeen;

        /// <summary>기믹 파라미터가 없는 컨트롤러(기믹을 안 쓰는 보스)에 Set*을 부르면
        /// 매 프레임 경고가 쌓인다 — 스폰 때 한 번 확인하고 없으면 통째로 건너뛴다.</summary>
        private readonly bool _hasGimmickParams;
        public bool HasGimmickAnimParams => _hasGimmickParams;
        private readonly bool _hasStaggerParam;

        public BossView(Animator animator, BossNetState netState)
        {
            Animator = animator;
            NetState = netState;
            _hasGimmickParams = BossAnimParams.HasGimmickParams(animator);
            _hasStaggerParam = BossAnimParams.HasStaggerParam(animator);

            // 여기서 netState.PatternStartSeq로 초기화하면 안 된다.
            // Spawned()는 모든 피어에서 도는데, 클라의 스폰이 서버의 패턴 시작보다 늦으면
            // 이미 올라간 seq를 "본 것"으로 저장해 버려 그 패턴의 StartPattern 트리거가
            // 영영 안 나간다 — 보스룸에 들어가자마자 첫 패턴만 애니가 안 뜨던 증상이 이것이었다.
            // 대신 아무것도 못 봤다고 두고, 첫 Render에서 진행 중인 패턴이 있으면 그때 건다.
            // 기믹 seq도 똑같은 이유로 초기화하지 않는다.
        }

        public void Render()
        {

            if (Animator == null) return;
            if (NetState.PatternStartSeq != _lastPatternSeq)
            {
                bool firstLook = _lastPatternSeq == NoPatternSeen;
                _lastPatternSeq = NetState.PatternStartSeq;

                // 첫 Render에서 seq가 0이면 아직 패턴이 시작된 적이 없다 — 걸 트리거가 없다.
                // 0보다 크면 이미 진행 중인 패턴이므로(늦참여·스폰 지연) 지금이라도 걸어준다.
                if (!firstLook || NetState.PatternStartSeq > 0)
                    Animator.SetTrigger(BossAnimParams.StartPattern);   // 패턴 시작마다 딱 1회, 안 놓침
            }
            if (_hasGimmickParams)
            {
                if (NetState.GimmickStartSeq != _lastGimmickSeq)
                {
                    bool firstLook = _lastGimmickSeq == NoGimmickSeen;
                    _lastGimmickSeq = NetState.GimmickStartSeq;

                    // 패턴과 같은 규칙이다 — 첫 Render에서 seq가 0이면 아직 열린 기믹이 없다.
                    if (!firstLook || NetState.GimmickStartSeq > 0)
                        Animator.SetTrigger(BossAnimParams.StartGimmick);   // 기믹 시작마다 딱 1회, 안 놓침
                }
                Animator.SetInteger(BossAnimParams.AnimatorGimmickId, NetState.CurrentAnimGimmickId);
                // 단계는 로직이 지휘한다 — 클립 길이(ExitTime)가 기믹의 시간을 정하면
                // 서버 판정과 어긋나므로, 애니는 이 값이 바뀔 때 다음 클립으로 넘어간다.
                Animator.SetInteger(BossAnimParams.GimmickPhase, NetState.CurrentGimmickPhase);
            }
            if (_hasStaggerParam)
                Animator.SetInteger(BossAnimParams.StaggerPhase, NetState.CurrentStaggerPhase);
            // guardrail:allow RNG — Render()는 클라이언트 연출 전용, Time.deltaTime은 SetFloat 댐핑에 필요
            Animator.SetFloat(BossAnimParams.MoveSpeed, NetState.AnimMoveSpeed, 0.1f, Time.deltaTime);
            Animator.SetInteger(BossAnimParams.BossState, NetState.CurrentAnimStateId);
            Animator.SetFloat(BossAnimParams.PatternSpeedMultiplier, NetState.AnimPatternSpeedMultiplier);
            Animator.SetInteger(BossAnimParams.AnimatorPatternId, NetState.CurrentAnimPatternId);
            Animator.SetInteger(BossAnimParams.PatternStep, NetState.CurrentPatternStep);
        }
    }
}

