using System.Collections.Generic;
using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// 패턴 연출 재생기. 패턴 로직은 서버에서만 돌기 때문에(BossEntity가 StateAuthority에서만 FSM을 조립)
    /// 이펙트를 패턴 코드에서 만들면 호스트에만 보인다. 그래서 여기서 BossNetState를 읽어
    /// 모든 피어가 각자 로컬로 재생한다 — 네트워크로 오가는 건 이미 동기화된 패턴 id·단계뿐이다.
    ///
    /// 타이밍은 SO의 초를 그대로 쓰되 배속으로 나눈다(서버의 ToTicks와 같은 계산).
    public class BossPatternEffectPlayer : NetworkBehaviour
    {
        [SerializeField] private BossNetState _netState;
        [Tooltip("패턴별 연출 목록을 여기 Patterns에서 찾는다.")]
        [SerializeField] private BossDefinition _definition;
        [Tooltip("페이즈 전환 패턴은 BossDefinition의 Patterns에 없으므로 여기에 직접 넣는다.")]
        [SerializeField] private PatternEffectCue[] _phaseTransitionCues;
        [SerializeField] private Material _remadeImpactMaterial;
        [Networked, Capacity(32)] private NetworkArray<TfwWarningState> Warnings => default;
        private readonly TfwImpactVfx[] _warnings = new TfwImpactVfx[32];
        private readonly TfwWarningState[] _shownWarnings = new TfwWarningState[32];

        internal Material ImpactMaterial => _remadeImpactMaterial;
        internal float IaidoFuseSeconds => _definition.PhaseTransitionBurst.FuseSeconds;

        internal void SetWarning(int slot, TfwWarningState warning)
        {
            if (HasStateAuthority) Warnings.Set(slot, warning);
        }

        internal void SetTargetWarning(Vector3 position, float radius, int ticksUntilHit)
        {
            if (!HasStateAuthority) return;
            int now=Runner.Tick.Raw;
            SetWarning(31,new TfwWarningState { Shape=0,Center=position+Vector3.up*.055f,Dimensions=new Vector3(radius,0,0),
                Rotation=Quaternion.identity,Sequence=_netState.PatternStartSeq,StartTick=now,EndTick=now+Mathf.Max(1,ticksUntilHit) });
        }

        private void UpdateWarnings()
        {
            bool showing=(BossAnimState)_netState.CurrentAnimStateId==BossAnimState.Pattern && _netState.CurrentPatternStep<=1;
            int now=HasStateAuthority?Runner.Tick.Raw:Mathf.FloorToInt((float)(Object.RenderTime/Runner.DeltaTime));
            for(int i=0;i<_warnings.Length;i++)
            {
                var w=Warnings[i];
                if(!showing || w.Sequence!=_netState.PatternStartSeq || w.EndTick<=now || _remadeImpactMaterial==null)
                { ClearWarning(i); continue; }
                var old=_shownWarnings[i];
                if(_warnings[i]==null || old.Shape!=w.Shape || (old.Dimensions-w.Dimensions).sqrMagnitude>.000001f)
                {
                    ClearWarning(i);
                    _warnings[i]=TfwImpactVfx.CreateWarning(w.Shape,w.Center,w.Dimensions,w.Rotation,_remadeImpactMaterial);
                }
                _shownWarnings[i]=w;
                _warnings[i].transform.SetPositionAndRotation(w.Center,w.Rotation);
                _warnings[i].SetProgress(Mathf.Clamp01((float)(now-w.StartTick)/Mathf.Max(1,w.EndTick-w.StartTick)));
            }
        }
        private void ClearWarning(int slot)
        {
            if(_warnings[slot]==null)return;
            _warnings[slot].gameObject.SetActive(false);Destroy(_warnings[slot].gameObject);_warnings[slot]=null;
        }
        private void ClearWarnings() { for(int i=0;i<_warnings.Length;i++)ClearWarning(i); }

        private const int PhaseTransitionAnimId = 99;   // BossPhaseTransitionState와 맺은 계약

        private PatternEffectCue[] _current;
        private PatternDefinition _currentPattern;

        // 이 패턴이 화면을 흔들 시각(초, 배속 1 기준). 조각 시작이 아니라 조각별 타격 순간이다.
        // 타격 시점을 안 내놓는 패턴은 {0} 하나 — Active 진입에 한 번만 흔들린다.
        private static readonly float[] SingleSegment = { 0f };
        private float[] _shakeTimes = SingleSegment;
        private int _nextSegment;

        private int _lastSeq = -1;
        private int _nextCue;
        private float _activeElapsed;
        private bool _wasActive;

        /// 이 패턴의 Active가 시작되는 걸 처음부터 봤는가.
        /// 늦게 접속했거나 스폰이 늦어 이미 진행 중인 Active에 끼어든 경우, 타이머를 0으로 두면
        /// 이미 지나간 타격들의 큐가 지금 한꺼번에 터진다 — 그럴 땐 이 패턴의 연출을 통째로 건너뛴다.
        private bool _sawTelegraph;

        // 본 앵커 캐시. 큐마다 GetComponentsInChildren로 훑으면 리깅된 보스에서 배열 할당 + 전수 탐색이 반복된다.
        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>();

        private void Reset() => _netState = GetComponent<BossNetState>();

        public override void Render()
        {
            if (_netState == null) return;

            // 패턴이 새로 시작되면 큐 목록을 갈아끼우고 타이머를 되감는다.
            if (_netState.PatternStartSeq != _lastSeq)
            {
                _lastSeq = _netState.PatternStartSeq;
                _current = FindCues(_netState.CurrentAnimPatternId);
                _currentPattern = FindPattern(_netState.CurrentAnimPatternId);
                _shakeTimes = ShakeTimesOf(_currentPattern);
                _nextCue = 0;
                _activeElapsed = 0f;
                _wasActive = false;

                // 예고 단계부터 봤다면 Active 진입 시각을 정확히 잡을 수 있다.
                _sawTelegraph = !TfwPatternPhase.IsAttackActive(_netState);
            }

            UpdateWarnings();

            // 단계 값만 보면 안 된다 — 사망·무력화로 패턴이 끊겨도 그 값은 Active에 멈춰 있다.
            // (TfwPatternPhase 주석 참고) 그대로 두면 취소된 패턴의 남은 큐가 계속 터진다.
            if (!TfwPatternPhase.IsAttackActive(_netState))
            {
                _wasActive = false;
                return;
            }

            if (!_sawTelegraph) return;   // 진행 중인 Active에 끼어들었다 — 이번 패턴은 건너뛴다

            if (!_wasActive)
            {
                _wasActive = true;
                _activeElapsed = 0f;   // Active 진입 순간을 0으로
                _nextSegment = 0;
            }

            // 서버는 ToTicks에서 배속으로 나눈다 — 연출도 같은 비율로 당겨야 타격과 어긋나지 않는다.
            float speed = Mathf.Max(0.01f, _netState.AnimPatternSpeedMultiplier);
            _activeElapsed += Time.deltaTime;   // guardrail:allow RNG — 연출 타이밍(Render). 판정은 서버 틱이 잰다

            // 조각마다, 그 조각이 실제로 때리는 순간에 한 번씩 흔든다. 연계는 조각 하나가 곧 한 타라서
            // 패턴 전체에 한 번만 주면 뒤쪽 타격이 화면에 안 잡힌다.
            // 세기도 조각마다 다르다 — 기 모으기·백스텝처럼 타격이 없는 조각은 None으로 건너뛴다.
            while (_nextSegment < _shakeTimes.Length
                   && _activeElapsed >= _shakeTimes[_nextSegment] / speed)
            {
                // 거합은 Active 시작이 아니라 장판의 실제 폭발 신호에서 흔든다.
                if (_netState.CurrentAnimPatternId != PhaseTransitionAnimId)
                    GameplayEvents.RaiseCameraShakeRequested(ShakeForSegment(_nextSegment));
                _nextSegment++;
            }

            if (_current == null) return;
            while (_nextCue < _current.Length && _activeElapsed >= _current[_nextCue].AtSeconds / speed)
            {
                Play(_current[_nextCue]);
                _nextCue++;
            }
        }

        public override void Spawned()
        {
            if (_netState == null) _netState = GetComponent<BossNetState>();
            _lastSeq = -1;
        }

        public override void Despawned(NetworkRunner runner, bool hasState) { ClearWarnings(); }
        private void OnDisable() { ClearWarnings(); }

        /// 이 패턴의 조각별 타격 시각. 연계 SO만 들고 있고, 나머지는 단발로 본다.
        private float[] ShakeTimesOf(PatternDefinition pattern)
        {
            var times = pattern is ITfwComboDefinition combo ? combo.SegmentShakeSeconds : null;
            return times != null && times.Length > 0 ? times : SingleSegment;
        }

        /// 이 조각이 요청할 흔들림 세기. 페이즈 전환 패턴은 SO가 없어 Heavy로 고정한다.
        private CameraShakeStrength ShakeForSegment(int segmentIndex)
        {
            if (_netState.CurrentAnimPatternId == PhaseTransitionAnimId) return CameraShakeStrength.Heavy;
            return _currentPattern != null ? _currentPattern.ShakeForSegment(segmentIndex) : CameraShakeStrength.None;
        }

        private PatternDefinition FindPattern(int patternId)
        {
            if (_definition == null || _definition.Patterns == null) return null;

            foreach (var p in _definition.Patterns)
                if (p != null && p.AnimatorPatternId == patternId) return p;

            return null;
        }

        private PatternEffectCue[] FindCues(int patternId)
        {
            if (patternId == PhaseTransitionAnimId) return _phaseTransitionCues;
            if (_definition == null || _definition.Patterns == null) return null;

            foreach (var p in _definition.Patterns)
            {
                if (p == null || p.AnimatorPatternId != patternId) continue;
                return p is IPatternEffectSource source ? source.EffectCues : null;
            }
            return null;
        }

        private void Play(PatternEffectCue cue)
        {
            if (cue == null || cue.Prefab == null) return;   // 아직 안 꽂은 슬롯은 조용히 건너뛴다

            Transform anchor = ResolveAnchor(cue.AnchorBoneName);

            // 오프셋은 보스 정면 기준이다(큐 툴팁의 "z=전방"). 본 앵커의 회전은 리그마다 축이 제각각이라
            // 그걸 기준으로 삼으면 같은 값이 본을 바꿀 때마다 엉뚱한 방향으로 날아간다.
            // 붙는 위치는 앵커, 방향은 보스 루트 — 앵커가 루트일 때는 둘이 같아 기존 동작 그대로다.
            Vector3 position = anchor.position + transform.rotation * cue.LocalOffset;

            var instance = Instantiate(cue.Prefab, position, anchor.rotation,
                                       cue.AttachToAnchor ? anchor : null);

            // 프리팹 자체 배율(0.28처럼 눌러둔 것도 있다)을 유지한 채 곱한다.
            if (cue.Scale > 0f && !Mathf.Approximately(cue.Scale, 1f))
                instance.transform.localScale *= cue.Scale;

            if (cue.Lifetime > 0f) Destroy(instance, cue.Lifetime);
        }

        private Transform ResolveAnchor(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return transform;
            if (_anchors.TryGetValue(boneName, out var cached))
                return cached != null ? cached : transform;   // 본이 사라졌으면(무기 교체 등) 루트로

            Transform found = null;
            foreach (var t in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t.name != boneName) continue;
                found = t;
                break;
            }

            if (found == null)
                Debug.LogWarning($"{name}: 앵커 본 '{boneName}'을 찾지 못해 루트에 생성합니다.", this);

            _anchors[boneName] = found;   // 못 찾은 것도 캐시한다 — 매 큐마다 다시 훑지 않게
            return found != null ? found : transform;
        }
    }
}
