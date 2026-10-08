using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// TFW 중심 장판의 폭발 표시를 소유 보스의 발도로 연결한다. 피해 판정에는 관여하지 않는다.
    [DisallowMultipleComponent]
    public sealed class TfwIaidoFieldLink : NetworkBehaviour
    {
        [Networked] private NetworkObject SourceBoss { get; set; }
        [Networked] private int SourcePatternSeq { get; set; }

        private bool _spawned;
        private bool _played;
        private TfwIaidoExplosionVisual _pending;
        private static readonly int StrikeState = Animator.StringToHash("Base Layer.PhaseTransition_Recovery");

        public override void Spawned()
        {
            _spawned = true;
            _played = false;
            if (HasStateAuthority)
            {
                // 이 컴포넌트는 Spread=0, UnderEachPlayer=false인 TFW 페이즈 장판에만 붙는다.
                BossNetState match = null;
                foreach (var player in UnityEngine.Object.FindObjectsByType<BossPatternEffectPlayer>(FindObjectsSortMode.None))
                {
                    var net = player.GetComponent<BossNetState>();
                    if (net == null || net.Object == null || !net.Object.IsValid || net.Runner != Runner ||
                        !net.HasStateAuthority || net.CurrentAnimPatternId != 99 ||
                        net.CurrentAnimStateId != (int)BossAnimState.PhaseTransition) continue;
                    var delta = net.transform.position - transform.position;
                    if (new Vector2(delta.x, delta.z).sqrMagnitude > 0.0001f) continue;
                    if (match != null)
                    {
                        Debug.LogError("[TFW Iaido] 동일 위치의 전환 보스가 둘 이상이어서 발도 소유자를 결정할 수 없습니다.", this);
                        return;
                    }
                    match = net;
                }
                if (match != null)
                {
                    SourceBoss = match.Object;
                    SourcePatternSeq = match.PatternStartSeq;
                }
                else Debug.LogWarning("[TFW Iaido] 장판 중심에서 전환 중인 TFW를 찾지 못했습니다.", this);
            }
            TryPlay();
        }

        internal void ExplosionEnabled(TfwIaidoExplosionVisual visual)
        {
            _pending = visual;
            TryPlay();
        }

        public override void Render()
        {
            if (_pending != null) TryPlay(); // 소유 보스의 스냅샷/오브젝트가 뒤늦게 도착한 경우만 재시도.
        }

        private void TryPlay()
        {
            if (!_spawned || _played || _pending == null || !_pending.isActiveAndEnabled ||
                SourceBoss == null || !SourceBoss.IsValid) return;
            var owner = SourceBoss.GetComponent<BossNetState>();
            if (owner == null || owner.PatternStartSeq != SourcePatternSeq || owner.CurrentAnimPatternId != 99 ||
                owner.CurrentAnimStateId != (int)BossAnimState.PhaseTransition)
            {
                _pending = null;
                return;
            }
            var animator = owner.GetComponent<Animator>();
            if (animator == null || !animator.HasState(0, StrikeState)) { _pending = null; return; }
            _played = true;
            _pending.PrimeParticles();
            _pending = null;
            animator.SetInteger(BossAnimParams.BossState, owner.CurrentAnimStateId);
            animator.SetInteger(BossAnimParams.AnimatorPatternId, owner.CurrentAnimPatternId);
            animator.SetInteger(BossAnimParams.PatternStep, owner.CurrentPatternStep);
            animator.ResetTrigger(BossAnimParams.StartPattern);
            animator.Play(StrikeState, 0, 0f);
            animator.Update(0f);
            GameplayEvents.RaiseCameraShakeRequested(CameraShakeStrength.Heavy);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _spawned = false;
            _played = false;
            _pending = null;
        }
    }
}
