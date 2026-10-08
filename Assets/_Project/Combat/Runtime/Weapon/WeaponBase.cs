using Fusion;
using Game.Combat;
using Game.Combat.Chain;
using Game.Combat.Player;
using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public struct AttackEntry
    {
        public float AnimSpeed;
        public float LungeDistance;
        public float LungeStartDelay;
        public float LungeDuration;
        public AnimationCurve LungeCurve;
        public string AnimName;
        public float AttackOccurDelay;
        public float Cooldown;
        public float Angle;
        public float Range;
        public float Duration;
        public float Damage;
        public float AggroAmount;
    }

    public struct PendingAttack : INetworkStruct
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float RangeDistance;
        public float RangeAngle;
        public float Damage;
        public float AggroAmount;
        public int ApplyCnt;
        public DealLogicType LogicType;
        public DamageType DmgType;
        public NetworkBool GainGauge;
    }

    public struct PendingBuff : INetworkStruct
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float RangeDistance;
        public float RangeAngle;
        public int ApplyCnt;
        public BuffLogicType LogicType;
        public BuffType Type;
        public int Value;
        public int DurationTicks;
        public NetworkBool Cleansable;
        public int SourceId;
    }

    public abstract class WeaponBase : NetworkBehaviour, IWeapon
    {
        static ChainController _chainController;
        [Header("Weapon Data")]
        [SerializeField] protected WeaponRecord data;
        [SerializeField] protected RuntimeAnimatorController anim;

        [SerializeField] ReinforcementSetup setup;

        [Header("Hit Effect")]
        [SerializeField, Tooltip("이 무기가 적을 맞힐 때마다 피격 지점에 뜨는 공용 타격 이펙트. 비워두면 안 뜬다.")]
        private GameObject hitEffect;
        [SerializeField, Tooltip("대상 중심에서 위로 얼마나 띄울지(m). 0이면 콜라이더 한가운데.")]
        private float hitEffectHeightOffset;
        [SerializeField, Tooltip("피격 지점 주변으로 흩뿌릴 반경(m). 0이면 정확히 중앙. 연출용이라 데미지 판정과는 무관하다.")]
        private float hitEffectScatter = 0.25f;

        [Header("Dash")]
        [SerializeField, Tooltip("시트의 대시 행 이름. 이 이름의 AttackRecord가 대시의 애니·이펙트·전진·지속을 갖는다.")]
        private string dashAttackName = "대시";
        [SerializeField, Tooltip("시트의 백대시 행 이름. 이동 입력 없이 대시하면 이 행이 쓰인다. 없으면 대시 행으로 대체.")]
        private string backDashAttackName = "백대시";

        /// 시트에서 찾은 대시·백대시 행의 인덱스. -1 = 해당 행 없음 → 그 무기는 대시할 수 없다.
        private int _dashIdx = -1;
        private int _backDashIdx = -1;

        // 대시 행의 클립 State 해시. 0 = 시트에 AnimName이 없음 → 공용 클립을 쓴다.
        // 매 대시마다 StringToHash를 돌리지 않도록 Spawned에서 한 번만 만든다.
        private int _dashAnimHash;
        private int _backDashAnimHash;

        /// 대시 클립이 따로 없을 때 쓰는 직업 공용 State.
        private static readonly int DashHash = Animator.StringToHash("Dash");
        private static readonly int BackDashHash = Animator.StringToHash("BackDash");

        /// 대시 지속(= 무적 시간 = 다음 대시까지의 간격) 타이머.
        [Networked] private TickTimer DashTimer { get; set; }

        /// 대시 자원. 같은 GameObject의 컴포넌트가 소유한다(WeaponStamina).
        private WeaponStamina _stamina;

        /// <summary>대시 연출·전진·지속을 시작한다(서버). 대시 행이 평타와 같은 경로로
        /// 애니·배속·전진·이펙트를 낸다 — 판정만 없다(_skipHits가 타격을 건너뛴다).</summary>
        /// <param name="backward">이동 입력 없이 눌렀는가. 그때는 백대시 행(있으면)을 쓴다.</param>
        protected void PlayDash(bool backward)
        {
            // 백대시 행이 없으면 앞대시 행으로 대신한다 — 클립은 앞구르기여도 대시 자체는 나가야 한다.
            int idx = backward && _backDashIdx >= 0 ? _backDashIdx : _dashIdx;
            if (idx < 0) return;   // CanDash가 막아주지만, 여기서도 인덱스를 믿지 않는다

            // guardrail:allow AUTH — 유일 호출부 OnDashPressed()가 HasStateAuthority로 막는다
            DashTimer = TickTimer.CreateFromSeconds(Runner, Entry(idx).Duration);

            BeginAttack(idx, faceAim: false);   // 대시 방향은 상태(DashState)가 이미 잡았다

            // 시트 행에 클립 State가 적혀 있으면 그것, 없으면 직업 공용 클립.
            int animHash = idx == _backDashIdx ? _backDashAnimHash : _dashAnimHash;
            RaiseAnimationPlay(animHash != 0 ? animHash : backward ? BackDashHash : DashHash, 0.05f);
        }

        public ReinforcementSetup ReinforcementSetup { get => setup; }
        [Networked] public int CurrentReinforcement { get; set; }
        public Reinforcement ReinforceCur { get => ReinforcementSetup.entrys[CurrentReinforcement]; }
        public Reinforcement ReinforceNext 
        { 
            get => ReinforcementSetup.MaxReinforce <= CurrentReinforcement + 1
                ? null 
                : ReinforcementSetup.entrys[CurrentReinforcement + 1]; 
        }


        protected AttackEntry[] attacks;
        
        public int MaxGauge { get; private set; } = 100;
        public int GainPerHit { get; private set; } = 20;

        [Networked, OnChangedRender(nameof(OnGaugeChanged))]
        public int Gauge { get; protected set; }

        /// 공격속도 배율. 1 = 시트 기준. 버프·장비가 서버에서 이 값을 바꾸면
        /// 쿨다운·지속·타격 시점·애니메이션이 전부 함께 빨라진다.
        [Networked] public float AttackSpeed { get; private set; }

        public void SetAttackSpeed(float value)
        {
            if (!HasStateAuthority) return;
            AttackSpeed = Mathf.Max(0.1f, value);   // 0 이하는 시간이 무한대가 된다
        }

        /// 공격속도가 반영된 이 공격의 실제 값.
        /// 시트의 시간 값은 전부 "공격속도 1 기준"이므로 여기서 한 번에 환산한다 —
        /// 무기 코드는 Entry()가 준 숫자만 쓰면 되고 공격속도를 알 필요가 없다.
        protected AttackEntry Entry(int idx)
        {
            var e = attacks[idx];
            float speed = AttackSpeed > 0f ? AttackSpeed : 1f;

            e.Cooldown /= speed;
            e.Duration /= speed;
            e.AttackOccurDelay /= speed;
            e.LungeStartDelay /= speed;
            e.LungeDuration /= speed;
            e.AnimSpeed *= speed;   // 배속만 반대 방향 — 빨라질수록 재생이 빨라야 한다

            return e;
        }

        protected List<Collider> _damagables = new(); // 적중한 적 1프레임 캐시해서 궁극기 게이지 채우는 용도 

        void OnGaugeChanged()
            => GameplayEvents.RaiseUltimateGaugeChanged(Object.InputAuthority.PlayerId, Gauge, MaxGauge);
        
        
        // 네트워킹 타이머(쿨타임, 지속시간, 파티클 재생 타이밍) 동기화
        // 32를 넘기면 캔슬 RPC가 쓰는 int 비트마스크(CancelTimelines)가 깨진다 — 늘릴 땐 그쪽도 같이 본다.
        private const int MaxEntries = 16;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<TickTimer> CooldownTimers => default;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<TickTimer> DurationTimers => default;

        // 타격도 이펙트도 "공격 시작 틱 + 지금까지 처리한 개수"만 동기화한다.
        // 타이머를 개수만큼 두지 않으므로 다단이 몇 타든 네트워크 비용이 같다.
        [Networked, Capacity(MaxEntries)]
        public NetworkArray<int> AttackStartTick => default;   // 0 = 진행 중 아님

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<int> HitsFired => default;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<int> ParticlesFired => default;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<PendingAttack> PendingAttacks => default;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<TickTimer> BuffDelayTimers => default;

        [Networked, Capacity(MaxEntries)]
        public NetworkArray<PendingBuff> PendingBuffs => default;

        // RPC로 받은 이펙트 재생 요청을 렌더까지 미뤄두는 큐(공격 인덱스, 이펙트 인덱스).
        // anchored가 켜지면 캐스터가 아니라 origin/yaw가 가리키는 월드 지점을 기준으로 놓는다.
        private readonly List<(int attack, int particle, Vector3 origin, float yaw, bool anchored)> _pendingFx = new();

        // 서버가 알려준 피격 지점들. 대상 위치라 시전자 보간과는 무관하지만,
        // 배치는 다른 이펙트와 같은 렌더 경로를 타게 둔다.
        private readonly List<Vector3> _pendingHitFx = new();

        // 캐스터가 아니라 월드 고정 지점에 이펙트를 놓는 공격(-1 = 없음)과 그 지점·방향.
        // 서버가 풀체인 발동 시 확정하고 RPC로 각 피어에 넘긴다.
        private int _fxAnchorIdx = -1;
        private Vector3 _fxAnchor;
        private float _fxAnchorYaw;

        /// 풀체인 발동 시 ChainController가 준 앵커를 해당 공격의 이펙트 기준점으로 확정한다.
        /// 팀 합동기라 시전자에 붙으면 안 된다. 방향은 오소링된 오프셋이 뒤집히지 않게 시전자 것을 쓴다.
        protected void SetFullChainAnchor(int idx, Vector3 anchor)
        {
            _fxAnchorIdx = idx;
            _fxAnchor = anchor;
            _fxAnchorYaw = transform.eulerAngles.y;
        }

        // FollowCaster가 켜진 이펙트. 풀 인스턴스는 재사용되므로 재생 회차(version)를 같이 들고
        // 다른 공격에 넘어간 것을 붙잡지 않게 한다.
        private readonly List<FollowingEffect> _followingEffects = new();

        private readonly struct FollowingEffect
        {
            private readonly EffectHandle _handle;
            private readonly Vector3 _offset;
            private readonly Quaternion _rotation;

            /// 이 이펙트를 낳은 공격. 캔슬 때 "아직 진행 중이던 공격의 연출"만 골라 접기 위해 들고 있는다 —
            /// 배리어처럼 이미 발동이 끝난 지속 이펙트는 캔슬과 무관하게 계속 따라다녀야 한다.
            public readonly int AttackIdx;

            public FollowingEffect(EffectHandle handle, Vector3 offset, Quaternion rotation, int attackIdx)
            {
                _handle = handle;
                _offset = offset;
                _rotation = rotation;
                AttackIdx = attackIdx;
            }

            public bool IsPlaying => _handle.IsPlaying;
            public void Stop(bool clear) => _handle.Stop(clear);

            public void Follow(Transform owner) => _handle.SetTransform(
                owner.position + owner.rotation * _offset,
                owner.rotation * _rotation);
        }

        private bool[] _buffApplied;

        /// 타격 판정을 건너뛸 공격(대시·백대시처럼 연출만 있는 행). Spawned에서 확정된다.
        private bool[] _skipHits;

        private PlayerCombatStats _combatStats;

        /// 공격력 증가는 기본 공격력에 더한 뒤 공격별 계수를 곱한다 —
        /// 계수 뒤에 더하면 계수 0.4짜리 약한 타와 1.2짜리 강한 타가 같은 보너스를 받는다.
        protected float GetDamage(float multiplier)
        {
            // 무기는 아바타의 자식이라 부모에서 찾는다. 공격 중에만 호출되므로 이 시점엔 활성 상태다.
            if (_combatStats == null) _combatStats = GetComponentInParent<PlayerCombatStats>();

            float attackPower = data.BaseDamage * (_combatStats != null ? 100 + _combatStats.AttackPowerBonus : 100) * 0.01f;
            return attackPower * ReinforceCur.Damage * multiplier;
        }
        protected List<Collider> _buffables = new(); // 버프 적중 대상 1프레임 캐시

        public RuntimeAnimatorController AnimatorController => anim;
        public event Action<int, float> OnAnimPlayRequested;
        protected void RaiseAnimationPlay(int hash, float duration) => OnAnimPlayRequested?.Invoke(hash, duration); // 무기가 애니메이션 재생 타이밍 결정

        /// 전진기 요청 (거리 m, 시작 지연 s, 지속 s, 진행 곡선). 실제 이동은 PlayerStateMachine이 서버 틱에서 수행한다.
        public event Action<float, float, float, AnimationCurve> OnLungeRequested;
        /// 애니메이션 배속 요청. Base.controller의 State가 SpeedParameter를 쓰므로 무기별로 달라질 수 있다.
        public event Action<float> OnAnimSpeedRequested;

        /// 공격이 실제로 발동한 순간. 상태머신이 이때 조준 방향으로 몸을 돌린다.
        public event Action OnAttackBegan;

        /// 공격 발동 시점(무기가 입력을 받아 공격을 시작한 틱)에 호출한다.
        /// 여기서 타임라인 시작 틱을 찍고, 애니 배속·전진을 상태머신에 알린다.
        /// 타격과 이펙트는 이 틱을 기준으로 각자의 시점에 자동 발생한다.
        /// <param name="faceAim">발동 순간 조준 방향으로 몸을 돌릴지.
        /// 차징 루프처럼 매 틱 반복 호출되는 구간은 false — 계속 돌리면 조준이 고정되지 않는다.</param>
        protected void BeginAttack(int idx, bool faceAim = true)
        {
            AttackStartTick.Set(idx, Runner.Tick);
            HitsFired.Set(idx, 0);
            ParticlesFired.Set(idx, 0);

            if (faceAim) OnAttackBegan?.Invoke();

            var e = Entry(idx);
            OnAnimSpeedRequested?.Invoke(e.AnimSpeed);

            // 전진이 없는 공격이어도 알린다 — 받는 쪽이 이전 전진을 끊어야 콤보 사이가 안 튄다.
            OnLungeRequested?.Invoke(e.LungeDistance, e.LungeStartDelay, e.LungeDuration, e.LungeCurve);
        }
        // 궁을 냈다는 예약. 체인 연결은 이 궁이 처음 적중하는 순간(ApplyHit)에 일어난다.
        private bool _ultHookPending;

        /// 궁 발동 시 호출. 기본은 적중을 확인하고 연결한다 — 허공에 휘두른 궁은 체인을 잇지 못한다.
        /// <param name="onCast">데미지 판정이 없는 지원형 궁(힐·부활 등)은 적중이라는 사건 자체가 없으므로
        /// 시전 즉시 연결한다. 앵커는 넘기지 않는다(대상이 적이 아니라 아군이라 연출 기준이 될 수 없다).</param>
        protected void HookFullChain(bool onCast = false)
        {
            if (!onCast)
            {
                _ultHookPending = true;
                return;
            }

            _chainController?.HookFullChain(
                FullChainAction, Object.InputAuthority.PlayerId, null, transform.position);
        }
        public virtual void Initialize(ChainController chainController)
        {
            _chainController = chainController;
            Gauge = 0;
        }

        public override void Spawned()
        {
            _stamina = GetComponent<WeaponStamina>();

            if (HasStateAuthority) AttackSpeed = 1f;

            // 인덱스는 data.Attacks와 1:1로 유지한다. 유효한 것만 세어 길이를 줄이면
            // 파티클 없는 항목 하나에 뒤쪽이 통째로 밀려 다른 공격의 값을 읽게 된다.
            attacks = new AttackEntry[data.Attacks.Length];
            for (int i = 0; i < attacks.Length; i++)
            {
                var atk = data.Attacks[i];
                attacks[i] = new AttackEntry
                {
                    Angle = atk.Angle,
                    Damage = atk.Damage,
                    Duration = atk.Duration,
                    AttackOccurDelay = atk.AttackOccurDelay,
                    AnimName = atk.AnimName,
                    Cooldown = atk.Cooldown,
                    AnimSpeed = atk.AnimSpeed,
                    LungeDistance = atk.LungeDistance,
                    LungeStartDelay = atk.LungeStartDelay,
                    LungeDuration = atk.LungeDuration,
                    LungeCurve = atk.LungeCurve,
                    Range = atk.Range,
                    AggroAmount = atk.AggroAmount
                };
            }

            _buffApplied = new bool[attacks.Length];

            // 대시·백대시 행은 이름으로 찾는다. 시트에서 행이 빠지거나 이름이 바뀌면 -1로 남는다 —
            // 그 무기는 대시를 못 한다. 조용히 다르게 동작하는 것보다 눈에 띄게 막히는 편이 낫다.
            _dashIdx = FindAttackIndex(data.Attacks, dashAttackName);
            _backDashIdx = FindAttackIndex(data.Attacks, backDashAttackName);

            if (_dashIdx < 0)
                Debug.LogWarning($"[{name}] 시트에서 대시 행 \"{dashAttackName}\"을 찾지 못해 대시가 꺼집니다. " +
                                 "시트에 행을 추가하고 Sync하거나, 무기의 '대시 행 이름'을 맞추세요.");

            _dashAnimHash = AnimHashOf(_dashIdx);
            _backDashAnimHash = AnimHashOf(_backDashIdx);

            // 대시는 판정이 없는 행이다. 어느 인덱스를 건너뛸지 여기서 정해 두면
            // 공용 타임라인(ApplyTimeline)은 "대시"라는 개념을 몰라도 된다.
            _skipHits = new bool[attacks.Length];
            if (_dashIdx >= 0) _skipHits[_dashIdx] = true;
            if (_backDashIdx >= 0) _skipHits[_backDashIdx] = true;
        }

        private int AnimHashOf(int idx)
        {
            if (idx < 0) return 0;

            string state = data.Attacks[idx].AnimName;
            return string.IsNullOrEmpty(state) ? 0 : Animator.StringToHash(state);
        }

        /// <summary>시트 이름으로 공격 인덱스를 찾는다. 없으면 -1.
        /// 편집기(EffectOffsetWindow)도 같은 규칙으로 행을 표시해야 해서 static으로 공유한다 —
        /// 규칙이 갈리면 편집기가 "[대시]"라고 표시한 행과 서버가 고르는 행이 달라진다.</summary>
        public static int FindAttackIndex(AttackRecord[] attacks, string name)
        {
            if (attacks == null || string.IsNullOrWhiteSpace(name)) return -1;

            name = name.Trim();
            for (int i = 0; i < attacks.Length; i++)
            {
                var rec = attacks[i];
                if (rec != null && string.Equals(rec.Name?.Trim(), name,
                        System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        // 서버가 이펙트 발생을 감지하면 전 피어에 방송한다(호스트 포함 1회씩).
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayParticle(int attackIdx, int particleIdx)
        {
            // 여기서 바로 배치하면 안 된다. RPC는 시뮬레이션 단계에서 실행돼 transform이 "틱 위치"인데,
            // 화면의 캐릭터는 KCC가 보간한 "렌더 위치"에 그려진다. 최대 1틱만큼 어긋나 이펙트가 뜬다.
            // 이펙트는 순수 연출이므로 Render에서 배치한다.
            _pendingFx.Add((attackIdx, particleIdx, default, 0f, false));
        }

        // 풀체인처럼 캐스터와 무관한 월드 지점에 놓는 이펙트. 시전자 위치를 안 쓰므로
        // 보간 위치를 기다릴 이유가 없지만, 배치 자체는 같은 렌더 경로를 타게 둔다.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayParticleAt(int attackIdx, int particleIdx, Vector3 origin, float yaw)
        {
            _pendingFx.Add((attackIdx, particleIdx, origin, yaw, true));
        }

        /// 적이 실제로 맞은 지점. 판정은 서버만 알고 있으므로 위치를 그대로 알린다.
        ///
        /// 때린 본인에게만 보낸다 — 히트 이펙트는 "내 공격이 들어갔다"는 손맛 피드백이라
        /// 남의 타격까지 그릴 이유가 없다. 4인 파티가 잡몹을 난타할 때 화면에 뜨는 이펙트와
        /// 오가는 RPC가 통째로 1/4이 된다. 팀원의 공격은 각자 자기 화면에서 본다.
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_PlayHitEffect(Vector3 position)
        {
            _pendingHitFx.Add(position);
        }

        /// 이펙트 배치는 렌더에서 한다 — 보간된 실제 화면 위치를 기준으로 삼아야 캐릭터와 안 어긋난다.
        public override void Render()
        {
            UpdateFollowingEffects(_followingEffects);
            PlayPendingHitEffects();

            if (_pendingFx.Count == 0) return;

            foreach (var (attackIdx, particleIdx, origin, yaw, anchored) in _pendingFx)
            {
                if (attackIdx < 0 || attackIdx >= data.Attacks.Length) continue;

                var rec = data.Attacks[attackIdx];
                if (particleIdx < 0 || particleIdx >= rec.ParticleCount) continue;

                var p = rec.Particle(particleIdx);
                if (p.Prefab == null || !p.Prefab.TryGetComponent(out ParticleGroup group)) continue;

                // 오프셋은 기준점 로컬값 — 여기서 한 번만 월드로 푼다(EffectPool은 그대로 쓴다).
                // 앵커 이펙트는 전장의 한 지점이 기준이고, 그 외에는 시전자가 기준이다.
                var basePos = anchored ? origin : transform.position;
                var baseRot = anchored ? Quaternion.Euler(0f, yaw, 0f) : transform.rotation;

                var rot = Quaternion.Euler(p.Euler);
                var handle = EffectPool.Instance.Play(group,
                    basePos + baseRot * p.Offset,
                    baseRot * rot,
                    p.SafeSpeed);

                // 앵커 이펙트는 시전자를 따라가면 안 된다 — 월드에 고정되는 게 목적이다.
                if (handle.IsPlaying && p.FollowCaster && !anchored)
                    _followingEffects.Add(new FollowingEffect(handle, p.Offset, rot, attackIdx));
            }

            _pendingFx.Clear();
        }

        /// 피격 지점에 공용 타격 이펙트를 놓는다. 방향은 공격자의 정면을 쓴다 —
        /// 베기·타격처럼 방향성이 있는 이펙트가 때린 쪽을 향하게 하려는 것이다.
        ///
        /// 위치를 조금씩 흩뿌린다. 같은 자리에 도장 찍듯 뜨면 다단히트가 한 발처럼 보인다.
        /// 순수 연출이라 각 피어가 알아서 굴린다 — 판정과 무관하므로 결정론을 지킬 필요가 없고,
        /// 서버가 오프셋을 실어 보내면 RPC만 무거워진다.
        private void PlayPendingHitEffects()
        {
            if (_pendingHitFx.Count == 0) return;

            if (hitEffect != null && hitEffect.TryGetComponent(out ParticleGroup group))
            {
                foreach (var position in _pendingHitFx)
                {
                    var scatter = hitEffectScatter > 0f
                        // guardrail:allow RNG — 판정과 무관한 연출용 산포(각 피어 로컬)
                        ? UnityEngine.Random.insideUnitSphere * hitEffectScatter
                        : Vector3.zero;

                    // 정면 축을 기준으로만 굴린다 — 스파크가 튀는 방향(공격자 정면)은 그대로 두고
                    // 무늬만 매번 달라지게 하려는 것이다. 축까지 흔들면 뒤로 튀는 게 섞인다.
                    // guardrail:allow RNG — 판정과 무관한 연출용 회전(각 피어 로컬)
                    var roll = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward);

                    EffectPool.Instance.Play(group, position + scatter, transform.rotation * roll);
                }
            }

            _pendingHitFx.Clear();
        }

        /// FollowCaster 이펙트를 시전자에 붙여 둔다. EffectPool은 월드 좌표에 한 번 놓고 끝이라,
        /// 차징처럼 지속되는 이펙트는 여기서 매 프레임 따라오게 해야 시전자를 놓치지 않는다.
        private void UpdateFollowingEffects(List<FollowingEffect> effects)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (!effects[i].IsPlaying)
                {
                    effects.RemoveAt(i);
                    continue;
                }

                effects[i].Follow(transform);
            }
        }

        private static void StopEffects(List<FollowingEffect> effects, bool clear)
        {
            foreach (var effect in effects) effect.Stop(clear);
            effects.Clear();
        }

        protected virtual void OnDisable()
        {
            // 무기를 내려놓는 것뿐이라 이미 떠 있는 파티클은 제 수명대로 두고 방출만 멈춘다.
            StopEffects(_followingEffects, clear: false);
            _pendingFx.Clear();
            _pendingHitFx.Clear();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        protected void RPC_RaiseWeaponUsed(int playerID, int weaponID, int attackType, float cooldown)
            => GameplayEvents.RaiseWeaponUsed(playerID, weaponID, attackType, cooldown);

        protected void ScheduleAttack(int idx, DealLogicType logicType, DealAreaParams areaParams, DamageType dmgType, bool gainGauge)
        {
            // 타이밍은 BeginAttack이 찍은 시작 틱이 갖는다. 여기서는 "무엇을 때릴지"만 남긴다.
            PendingAttacks.Set(idx, new PendingAttack
            {
                Origin = areaParams.origin,
                Direction = areaParams.direction,
                RangeDistance = areaParams.range_distance,
                RangeAngle = areaParams.range_angle,
                Damage = areaParams.damage,
                AggroAmount = Entry(idx).AggroAmount,
                ApplyCnt = areaParams.applyCnt,
                LogicType = logicType,
                DmgType = dmgType,
                GainGauge = gainGauge
            });
        }

        protected void ScheduleBuff(int idx, BuffLogicType logicType, BuffAreaParams areaParams)
        {
            var entry = Entry(idx);
            if (entry.AttackOccurDelay <= 0f)
            {
                BuffApplier.ApplyBuff(_buffables, logicType, areaParams);
                _buffables.Clear();
                return;
            }

            BuffDelayTimers.Set(idx, TickTimer.CreateFromSeconds(Runner, entry.AttackOccurDelay));
            _buffApplied[idx] = false; // 타이머 관측(!expired) 리셋은 1틱 미만 딜레이·실행 순서에 따라 누락될 수 있어 스케줄 시점에 명시적으로 리셋
            PendingBuffs.Set(idx, new PendingBuff
            {
                Origin = areaParams.origin,
                Direction = areaParams.direction,
                RangeDistance = areaParams.range_distance,
                RangeAngle = areaParams.range_angle,
                ApplyCnt = areaParams.applyCnt,
                LogicType = logicType,
                Type = areaParams.buff.Type,
                Value = areaParams.buff.Value,
                DurationTicks = areaParams.buff.DurationTicks,
                Cleansable = areaParams.buff.Cleansable,
                SourceId = areaParams.buff.SourceId
            });
        }

        /// 공격 시작 틱으로부터의 경과를 보고, 시점이 지난 타격·이펙트를 순서대로 처리한다.
        /// 한 틱에 여러 개가 몰릴 수 있으므로 while로 훑는다(틱 기준이라 항상 같은 결과).
        private void ApplyTimeline(int idx)
        {
            int startTick = AttackStartTick[idx];
            if (startTick <= 0) return;

            var rec = data.Attacks[idx];
            float elapsed = (Runner.Tick - startTick) * Runner.DeltaTime;
            float speed = AttackSpeed > 0f ? AttackSpeed : 1f;

            int hits = HitsFired[idx];
            var pending = PendingAttacks[idx];

            // 판정 없는 행(대시 등)은 타격을 끝난 것으로 표시하고 지나간다.
            // 안 그러면 예약이 비어 있는 채로 ApplyHit이 돌아 0데미지 판정이 한 번 나간다.
            if (_skipHits[idx]) hits = rec.HitCount;

            while (hits < rec.HitCount && elapsed >= rec.HitTime(hits) / speed)
            {
                int landed = ApplyHit(pending, hits, rec.HitCount);
                if (landed > 0) OnHitLanded(idx, landed);
                hits++;
            }
            HitsFired.Set(idx, hits);

            int fx = ParticlesFired[idx];
            bool anchored = idx == _fxAnchorIdx;
            while (fx < rec.ParticleCount && elapsed >= rec.Particle(fx).Time / speed)
            {
                if (anchored) RPC_PlayParticleAt(idx, fx, _fxAnchor, _fxAnchorYaw);
                else RPC_PlayParticle(idx, fx);
                fx++;
            }
            ParticlesFired.Set(idx, fx);

            if (hits >= rec.HitCount && fx >= rec.ParticleCount)
                AttackStartTick.Set(idx, 0);
        }

        /// 한 타가 실제로 하나 이상의 대상에 적중했을 때 서버에서 호출된다(허공 스윙은 안 온다).
        /// 적중 여부에 따라 쌓이는 무기 고유 자원(예: 광전사 분노 스택)이 여기에 붙는다.
        protected virtual void OnHitLanded(int idx, int targetCount) { }

        /// 데미지도 게이지도 타수로 나눠 갖는다 — 다단으로 쪼개도 총량이 그대로다.
        /// 반환값은 이 타에 실제로 맞은 대상 수(0이면 허공).
        private int ApplyHit(PendingAttack pending, int hitIndex, int hitCount)
        {
            var areaParams = new DealAreaParams
            {
                origin = pending.Origin,
                direction = pending.Direction,
                range_distance = pending.RangeDistance,
                range_angle = pending.RangeAngle,
                damage = AttackRecord.Split(pending.Damage, hitIndex, hitCount),
                aggro = AttackRecord.Split(pending.AggroAmount, hitIndex, hitCount),
                applyCnt = pending.ApplyCnt
            };

            DealDamageApplier.ApplyDamage(_damagables, pending.LogicType, areaParams, pending.DmgType, Object.InputAuthority.PlayerId);

            int landed = _damagables.Count;

            // 맞은 대상마다 타격 이펙트. 판정을 아는 건 서버뿐이라 지점을 각 피어에 알린다.
            if (hitEffect != null)
            {
                foreach (var hit in _damagables)
                    RPC_PlayHitEffect(hit.bounds.center + Vector3.up * hitEffectHeightOffset);
            }

            // 궁이 처음 적중한 순간에 체인을 잇는다. 맞힌 지점이 곧 풀체인 이펙트·판정의 앵커가 된다.
            // 다단 궁이라도 첫 적중 한 번만 연결한다 — 예약을 소비하지 않으면 궁 한 방에 체인이 여러 단 오른다.
            if (landed > 0 && _ultHookPending && pending.DmgType == DamageType.Ultimate)
            {
                _ultHookPending = false;
                var target = _damagables[0].transform;
                _chainController?.HookFullChain(
                    FullChainAction, Object.InputAuthority.PlayerId, target, target.position);
            }

            if (pending.GainGauge)
                Gauge = Mathf.Min(MaxGauge, Gauge + AttackRecord.Split(GainPerHit, hitIndex, hitCount) * landed);

            // 때리면 스테미너가 찬다(평타·스킬·궁 공통). 게이지와 같은 방식으로 타수만큼 나눠 가지므로
            // 다단으로 쪼갠 공격이 더 많이 회복하지 않는다. 대상 수는 곱하지 않는다 — 광역기 한 방으로
            // 스테미너가 가득 차면 회피 자원으로서의 의미가 없어진다.
            if (_stamina != null)
                _stamina.Restore(AttackRecord.Split(_stamina.GainPerAttack, hitIndex, hitCount));

            _damagables.Clear();
            return landed;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (attacks == null) return;

            for (int i = 0; i < attacks.Length; i++)
                ApplyTimeline(i);

            for (int i = 0; i < attacks.Length; i++)
            {
                var timer = BuffDelayTimers[i];
                bool expired = timer.IsRunning && timer.Expired(Runner);

                if (expired && !_buffApplied[i])
                {
                    var pending = PendingBuffs[i];
                    var areaParams = new BuffAreaParams
                    {
                        origin = pending.Origin,
                        direction = pending.Direction,
                        range_distance = pending.RangeDistance,
                        range_angle = pending.RangeAngle,
                        applyCnt = pending.ApplyCnt,
                        buff = new BuffInfo(pending.Type, pending.Value, pending.DurationTicks, pending.Cleansable, pending.SourceId)
                    };

                    BuffApplier.ApplyBuff(_buffables, pending.LogicType, areaParams);

                    _buffables.Clear();
                    _buffApplied[i] = true;
                }

                if (!expired) _buffApplied[i] = false;
            }
        }

        public int ID { get => data.Id; }
        public void ResetGauge() => Gauge = 0;
        public abstract bool CanAttack { get; protected set; }
        public abstract bool CanSkill { get; protected set; }
        public abstract bool CanUltimate { get; protected set; }
        public abstract bool IsAttackFinished();
        public abstract bool IsSkillFinished();
        public abstract bool IsUltimateFinished();
        public virtual void OnAttackPressed() { }
        public virtual void OnAttackReleased() { }
        public virtual void OnAttackTick() { }
        public virtual void OnSkillPressed() { }
        public virtual void OnSkillReleased() { }
        public virtual void OnSkillTick() { }
        public virtual void OnUltimatePressed() { }
        public virtual void OnUltimateReleased() { }
        public virtual void OnUltimateTick() { }
        // 대시 수치(소모·최대치·회복)는 WeaponStamina가, 대시 "행동"(어느 행을 쓸지·지속·무적)은 여기가 갖는다.
        public virtual bool CanDash => _dashIdx >= 0 && _stamina != null && _stamina.CanSpendDash;
        public virtual bool IsDashFinished() => DashTimer.ExpiredOrNotRunning(Runner);
        public virtual void OnDashPressed(bool backward)
        {
            if (!HasStateAuthority) return;
            if (!_stamina.TrySpendDash()) return;

            PlayDash(backward);
        }
        public virtual void FullChainAction(Vector3 anchor)
        {
            Debug.Log("[**풀체인 발동**] 근데 구현 안 해서 default로 로그만 찍음");
        }
        /// 슈퍼아머 없음이 기본 — 경직에도 끊긴다. 보호가 필요한 무기가 구간별로 override한다.
        public virtual CCType CurrentImmunityUpTo => CCType.None;

        /// 피해 감면 없음이 기본. 막기를 가진 무기가 구간별로 override한다.
        public virtual int IncomingDamagePercent => 100;

        /// 막기 성공에 반응할 것이 없는 무기는 그대로 둔다.
        public virtual void OnGuardSucceeded(bool justGuard) { }

        /// 평타를 끊고 스킬로 넘어가는 건 기본적으로 막혀 있다 — 열지 말지도, 언제 열지도 무기가 정한다.
        public virtual bool CanCancelIntoSkill => false;

        /// 스킬 홀드 중 들어온 평타 입력. 쓸 곳이 있는 무기(방패의 반격)만 받는다.
        public virtual void OnAttackDuringSkill() { }

        /// 무기가 소유한 대시 자원. 막기처럼 다른 소모처를 가진 무기가 쓴다.
        protected WeaponStamina Stamina => _stamina;
        public void Cancel()
        {
            CancelTimelines();
            OnCancel();
        }
        protected virtual void OnCancel() { }
        private void CancelTimelines()
        {
            if (!HasStateAuthority) return;
            if (attacks == null) return;

            // 아직 진행 중인 공격만 캔슬 대상이다. 비트마스크로 넘기는 이유는,
            // 서버가 아래에서 AttackStartTick을 0으로 만들고 나면 원격 피어는
            // "무엇이 진행 중이었는지"를 알 방법이 없기 때문이다.
            int running = 0;
            for (int i = 0; i < attacks.Length && i < MaxEntries; i++)
                if (AttackStartTick[i] > 0) running |= 1 << i;
            if (running == 0) return;

            for (int i = 0; i < attacks.Length; i++)
            {
                AttackStartTick.Set(i, 0);   // 남은 타격·이펙트 시점을 더 이상 보지 않는다
                BuffDelayTimers.Set(i, default);
            }

            RPC_CancelEffects(running);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_CancelEffects(int runningMask)
        {
            _pendingFx.Clear();
            for (int i = _followingEffects.Count - 1; i >= 0; i--)
            {
                int idx = _followingEffects[i].AttackIdx;
                if (idx < 0 || idx >= MaxEntries) continue;
                if ((runningMask & (1 << idx)) == 0) continue;

                _followingEffects[i].Stop(clear: true);
                _followingEffects.RemoveAt(i);
            }
        }
    }
}
