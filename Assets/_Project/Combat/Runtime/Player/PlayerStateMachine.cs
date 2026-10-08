using Fusion;
using Game.Core;
using Game.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Player
{
    public sealed class PlayerStateMachine : ServerAuthoritativeBehaviour
    {
        private readonly Dictionary<PlayerState, PlayerStateBase> _states = new()
        {
            { PlayerState.Idle, new IdleState() },
            { PlayerState.Moving, new MovingState() },
            { PlayerState.Attacking, new AttackingState() },
            { PlayerState.Skill, new SkillState() },
            { PlayerState.Ultimate, new UltimateState() },
            { PlayerState.Dash, new DashState() },
            { PlayerState.CrowdControlled, new CrowdControlledState() },
            { PlayerState.Dead, new PlayerDeadState() },
        };

        [SerializeField] private Animator _animator;

        private static readonly int AnimMoveX = Animator.StringToHash("MoveX");
        private static readonly int AnimMoveY = Animator.StringToHash("MoveY");
        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        // CC 클립은 직업 공용이라 모든 무기 Override Controller가 같은 상태 이름을 가진다.
        private static readonly int FlinchHash = Animator.StringToHash("Flinch");
        private static readonly int StunHash = Animator.StringToHash("Stun");
        // Base.controller의 State가 쓰는 배속 파라미터. Override Controller는 클립만 덮으므로
        // State speed를 무기별로 나눌 수 없다 → 파라미터로 런타임에 배선한다.
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

        // 파라미터 미설치 상태에서도 게임은 굴러가야 한다 — 없으면 배속 배선만 건너뛴다.
        private bool _hasAttackSpeedParam;

        [SerializeField]
        [Tooltip("이동 방향(MoveX/MoveY) 블렌드 보간 시간(초). 클수록 방향 전환이 늘어져 턴 모션이 도드라진다.")]
        private float _locomotionDamp = 0.18f;

        private PlayerStateBase _current;

        [Networked] public PlayerState CurrentState { get; private set; }
        [Networked] public Vector2 BlendDir { get; set; }
        [Networked] int RequestedAnimHash { get; set; }
        [Networked] float RequestedAnimFadeDuration { get; set; }

        // 전진기(lunge) — 서버 권한, 틱 기반. 루트모션을 쓰지 않는 이유는 KCC가 transform을
        // 소유하고 시뮬레이션이 틱 단위라, 렌더 타이밍에 도는 OnAnimatorMove가 리심에서 재현되지 않기 때문.
        [Networked] private TickTimer LungeDelay { get; set; }
        [Networked] private int LungeStartTick { get; set; }   // 0 = 전진 중 아님
        [Networked] private float LungeDistance { get; set; }
        [Networked] private float LungeDuration { get; set; }

        // 진행 곡선은 네트워크 타입이 아니지만, 평가는 서버(State Authority)에서만 일어나므로
        // 로컬 참조로 충분하다. 클라는 전진을 예측하지 않고 KCC 스냅샷만 받는다.
        private AnimationCurve _lungeCurve;
        [Networked] private float RequestedAnimSpeed { get; set; }

        public PlayerMovement Movement { get; private set; }
        public IWeapon Weapon { get; private set; }
        [Networked] private NetworkInputData LastInput { get; set; }
        // 장착 무기는 서버가 확정하고 전 피어가 렌더 콜백으로 따라간다 (ADR-001)
        [Networked, OnChangedRender(nameof(OnWeaponIdChanged))] private int EquippedWeaponId { get; set; }
        [SerializeField] WeaponContainer weapons;
        private int _appliedWeaponId = -1;   // 이 피어에 실제 적용된 무기 (중복 적용 방지)

        /// <summary>지금 무적인가. 대시 중에는 피해를 무시한다(PlayerLifeCycle이 읽는다).
        /// 궁극기 중 CC 면역과 같은 결 — 전 무기 공통 규칙이라 무기가 아니라 상태로 판정한다.</summary>
        public bool IsInvulnerable => CurrentState == PlayerState.Dash;

        // 사망·CC 게이트용. 같은 GameObject(아바타 루트)의 컴포넌트를 참조.
        private PlayerLifeCycle _lifeCycle;
        private PlayerCrowdControlController _cc;

        public override void Spawned()
        {
            _lifeCycle = GetComponent<PlayerLifeCycle>();
            _cc = GetComponent<PlayerCrowdControlController>();
            GameplayEvents.RaisePlayerSpawned(Object.InputAuthority.PlayerId, gameObject);
            GameplayEvents.OnWeaponEquipRequested += OnChangeWeaponRequested;
            StartCoroutine(BroadcastEquippedWeaponNextFrame());
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            GameplayEvents.OnWeaponEquipRequested -= OnChangeWeaponRequested;
            if (HasStateAuthority && ServiceLocator.TryGet<WeaponExpQueryService>(out var svc))
                svc.ClearPlayer(Object.InputAuthority.PlayerId);
            if (HasStateAuthority && ServiceLocator.TryGet<InventoryReadyService>(out var readySvc))
                readySvc.ClearPlayer(Object.InputAuthority.PlayerId);
            GameplayEvents.RaisePlayerDespawned(Object.InputAuthority.PlayerId);
        }
        public void Initialize(PlayerMovement movement)
        {
            Movement = movement;
            Movement.TakeControl();
            if (!HasStateAuthority && EquippedWeaponId > 0)
                ApplyWeapon(EquippedWeaponId);
            _current = _states[PlayerState.Idle];
        }

        // UI 장착 요청(로컬 이벤트)은 내 아바타만 반응해 서버에 요청한다.
        private void OnChangeWeaponRequested(int itemId)
        {
            if (!Object.HasInputAuthority) return;
            if (!ServiceLocator.TryGet<PlayerContextData>(out var ctx)) return;

            if (itemId < 0)
            {
                ctx.EquippedItemId = -1;
                RPC_RequestWeaponUnequip();
                return;
            }

            int reinforcementLevel = -1;
            foreach (var item in ctx.WeaponInventory)
            {
                if (item.ItemId == itemId)
                {
                    reinforcementLevel = item.ReinforcementLevel;
                    break;
                }
            }
            if (reinforcementLevel < 0) return; // 보유하지 않은 무기

            ctx.EquippedItemId = itemId;
            RPC_RequestWeaponEquip(itemId, reinforcementLevel);
        }

        /// <summary>무기 exp 만충 여부를 서버에 보고한다. 서버 픽업 가드(WeaponExpQueryService)가 참조.</summary>
        public void ReportWeaponExpState(int itemId, bool isFull)
        {
            if (!Object.HasInputAuthority) return;
            RPC_ReportWeaponExpState(itemId, isFull);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ReportWeaponExpState(int itemId, NetworkBool isFull)
        {
            if (ServiceLocator.TryGet<IWeaponExpQuery>(out var svc))
                svc.SetExpFull(Object.InputAuthority.PlayerId, itemId, isFull);
        }

        /// <summary>인벤토리 로드 완료 여부를 서버에 보고한다. 서버 픽업 가드(InventoryReadyService)가 참조.</summary>
        public void ReportInventoryReady(bool isReady)
        {
            if (!Object.HasInputAuthority) return;
            RPC_ReportInventoryReady(isReady);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ReportInventoryReady(NetworkBool isReady)
        {
            if (ServiceLocator.TryGet<InventoryReadyService>(out var svc))
                svc.SetReady(Object.InputAuthority.PlayerId, isReady);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestWeaponEquip(int id, int reinforcementLevel)
        {
            if (ServiceLocator.Get<IWeaponDB>().TryGet(id, out _) == false)
                return;
            if (!weapons.Data.ContainsKey(id))
                return;
            EquippedWeaponId = id;
            ApplyWeapon(id);
            if (Weapon != null)
            {
                int max = Weapon.ReinforcementSetup != null ? Weapon.ReinforcementSetup.MaxReinforce - 1 : 0;
                Weapon.CurrentReinforcement = Mathf.Clamp(reinforcementLevel, 0, max);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestWeaponUnequip()
        {
            EquippedWeaponId = -1;
            Unequip();
        }

        // WeaponModelSelector에서 "무기 없음"(기본 헤어·아머)에 배정된 Weapon Id.
        private const int UnarmedWeaponId = 0;

        private void Unequip()
        {
            if (Weapon != null)
            {
                Weapon.OnAnimPlayRequested -= RequestAnimPlay;
                weapons.Data[Weapon.ID].gameObject.SetActive(false);
            }
            Weapon = null;
            _appliedWeaponId = -1;

            // 맨손 모델로 되돌린다. WeaponModelSelector는 WeaponEquipped만 듣기 때문에,
            // 여기서 안 알리면 무기 로직만 내려가고 손에 든 메시는 그대로 남는다.
            GameplayEvents.RaiseWeaponEquipped(UnarmedWeaponId, Object.InputAuthority.PlayerId);
        }

        /// <summary>스폰 직후 이 아바타의 장착 상태를 로컬 이벤트로 한 번 알린다.
        ///
        /// WeaponEquipped는 로컬 이벤트라 네트워크를 타지 않는다. 늦게 참여한 피어는 남들이
        /// 이미 장착을 마친 뒤에 들어오므로 그 이벤트를 영영 못 받는다(EquippedWeaponId는
        /// 복제되지만 OnChangedRender는 값이 바뀔 때만 불린다). 아바타가 스스로 알리면
        /// 그 피어에서도 스폰과 동시에 같은 신호가 흐른다.
        ///
        /// 한 프레임 미루는 이유: Fusion은 같은 오브젝트 안 컴포넌트들의 Spawned 순서를
        /// 보장하지 않아, 지금 바로 쏘면 아직 구독 전인 WeaponModelSelector가 놓친다.</summary>
        private IEnumerator BroadcastEquippedWeaponNextFrame()
        {
            yield return null;
            if (Object == null || !Object.IsValid) yield break;

            int id = EquippedWeaponId > 0 ? EquippedWeaponId : UnarmedWeaponId;
            GameplayEvents.RaiseWeaponEquipped(id, Object.InputAuthority.PlayerId);
        }

        private void OnWeaponIdChanged()
        {
            if (EquippedWeaponId < 0)
                Unequip();
            else
                ApplyWeapon(EquippedWeaponId);
        }

        private void ApplyWeapon(int id)
        {
            if (id == _appliedWeaponId) return;
            if (!weapons.Data.TryGetValue(id, out var next) || next == null)
                return;

            if (Weapon != null)
            {
                Weapon.OnAnimPlayRequested -= RequestAnimPlay;
                Weapon.OnLungeRequested -= RequestLunge;
                Weapon.OnAnimSpeedRequested -= RequestAnimSpeed;
                Weapon.OnAttackBegan -= FaceAimOnAttack;
                weapons.Data[Weapon.ID].gameObject.SetActive(false);
            }
            // 무기를 바꾸면 이전 무기가 예약해 둔 전진은 무효다. 안 끊으면 장착 직후에
            // 남아 있던 전진이 뒤늦게 발동해 캐릭터가 앞으로 밀린다.
            CancelLunge();

            Weapon = next;
            _appliedWeaponId = id;
            next.gameObject.SetActive(true);
            Weapon.OnAnimPlayRequested += RequestAnimPlay;
            Weapon.OnLungeRequested += RequestLunge;
            Weapon.OnAnimSpeedRequested += RequestAnimSpeed;
            Weapon.OnAttackBegan += FaceAimOnAttack;
            ApplyWeaponAnimator(Weapon);

            if (HasStateAuthority)
                Weapon.ResetGauge();

            if (HasInputAuthority)
                if (ServiceLocator.Get<IItemDB>().TryGet(id, out var data))
                    if (ServiceLocator.TryGet<ISidePopupManager>(out var popup))
                        popup.Show($"{data.Name}을(를) 장착!");

            GameplayEvents.RaiseWeaponEquipped(Weapon.ID, Object.InputAuthority.PlayerId);
        }

        // 무기(직업)의 Override Controller를 Animator에 꽂는다. 무기 교체 시에도 호출.
        // 컨트롤러를 갈면 Animator가 기본 상태로 리셋되므로 곧바로 Locomotion을 재생한다.
        public void ApplyWeaponAnimator(IWeapon weapon)
        {
            if (_animator == null || weapon?.AnimatorController == null) return;

            if (_animator.runtimeAnimatorController != weapon.AnimatorController)
            {
                _animator.runtimeAnimatorController = weapon.AnimatorController;
                _animator.CrossFadeInFixedTime(LocomotionHash, 0f);
            }

            RefreshAnimatorParams();   // 컨트롤러가 그대로여도 파라미터 유무는 확인해 둔다
        }

        /// 배속 파라미터가 설치돼 있는지 캐시한다.
        /// 없는 파라미터에 SetFloat을 하면 Animator가 매 호출 예외를 뱉어 Render에서 콘솔이 도배된다.
        /// 설치는 에디터 메뉴(Tools > Combat > Base.controller에 배속 파라미터 설치)가 담당한다.
        private void RefreshAnimatorParams()
        {
            _hasAttackSpeedParam = HasAnimatorParam(AttackSpeedHash);

            if (!_hasAttackSpeedParam)
                Debug.LogWarning("[PlayerStateMachine] 애니메이터에 AttackSpeed 파라미터가 없습니다. " +
                                 "공격별 배속이 꺼진 채로 동작합니다. " +
                                 "메뉴: Tools > Combat > Base.controller에 배속 파라미터 설치");
        }

        private bool HasAnimatorParam(int hash)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return false;

            foreach (var p in _animator.parameters)
                if (p.nameHash == hash) return true;

            return false;
        }
        public void RequestAnimPlay(int stateHash, float transitionDuration = 0.1f)
        {
            if (!HasStateAuthority) return;
            RequestedAnimFadeDuration = transitionDuration;
            RequestedAnimHash = stateHash;

            RPC_OnAnimHashChanged();
        }
        /// <summary>타격이 발동할 때마다 조준 방향으로 즉시 돌린다.
        /// 상태 진입 시에만 돌리면, 콤보가 한 Attacking 상태 안에서 이어질 때
        /// 첫 타 방향에 고정된다(빠르게 연타할수록 방향이 안 바뀌는 증상).</summary>
        private void FaceAimOnAttack()
        {
            if (!HasStateAuthority || Movement == null) return;
            Movement.Face(LastInput.AimPoint - transform.position);
        }

        /// <summary>애니 배속 요청. 애니메이션 재생과 같은 경로(서버 → 전 피어 RPC)로 나가야
        /// 피어마다 배속이 갈리지 않는다. 재생 요청 직전에 호출되므로 CrossFade가 이 값을 물고 시작한다.</summary>
        public void RequestAnimSpeed(float speed)
        {
            if (!HasStateAuthority) return;
            RequestedAnimSpeed = speed;
            RPC_OnAnimSpeedChanged();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnAnimSpeedChanged()
        {
            if (_animator != null && _hasAttackSpeedParam)
                _animator.SetFloat(AttackSpeedHash, RequestedAnimSpeed);
        }

        /// <summary>전진기 요청. 무기가 공격을 발동한 틱에 호출한다(서버 전용).
        /// 거리·지속을 속도로 환산해 두고, 실제 이동은 FixedUpdateNetwork가 틱마다 나눠 적용한다.</summary>
        public void RequestLunge(float distance, float startDelay, float duration, AnimationCurve curve)
        {
            if (!HasStateAuthority) return;

            // 새 공격이 시작되면 이전 전진은 거기서 끝난다. 콤보 다음 타가 전진이 없는 공격이어도
            // 이전 전진이 계속 밀지 않도록, 값 설정 전에 무조건 정리한다.
            CancelLunge();
            if (distance == 0f || duration <= 0f) return;

            LungeDistance = distance;
            LungeDuration = duration;
            _lungeCurve = curve;
            // 지연 0이어도 타이머가 즉시 만료되도록 최소 1틱 — 아래 만료 관측이 다음 틱에 시작을 집는다.
            LungeDelay = TickTimer.CreateFromTicks(Runner, Mathf.Max(1, Mathf.RoundToInt(startDelay / Runner.DeltaTime)));
        }

        /// 예약된 전진과 진행 중인 전진을 모두 끊는다.
        private void CancelLunge()
        {
            if (!HasStateAuthority) return;
            LungeDelay = default;
            LungeStartTick = 0;
            _lungeCurve = null;
        }

        /// 진행률 p(0~1)에서의 이동 진행률. 곡선이 없으면 등속으로 떨어진다.
        private float LungeProgress(float p)
            => _lungeCurve != null && _lungeCurve.length > 0 ? _lungeCurve.Evaluate(p) : p;

        /// <summary>이번 틱에 적용할 전진량을 KCC에 넣는다. Movement.Tick() 전에 호출해야 한다.</summary>
        private void ApplyLunge()
        {
            if (!HasStateAuthority) return;   // 전진은 서버 권한 (ADR-001) — 호출부도 서버 분기지만 여기서도 막는다

            if (LungeDelay.Expired(Runner))
            {
                LungeStartTick = Runner.Tick;
                LungeDelay = default;
            }

            if (LungeStartTick <= 0) return;

            // 시간이 아니라 틱 수로 진행률을 낸다 — 같은 틱 시퀀스면 항상 같은 거리가 나온다(결정론).
            float dt = Runner.DeltaTime;
            float elapsed = (Runner.Tick - LungeStartTick) * dt;

            // 이번 틱의 이동량 = 곡선의 두 시점 차이. 곡선이 가속/감속이면 그대로 따라간다.
            float p1 = Mathf.Clamp01(elapsed / LungeDuration);
            float p0 = Mathf.Clamp01((elapsed - dt) / LungeDuration);

            Movement.AddDelta(transform.forward * (LungeDistance * (LungeProgress(p1) - LungeProgress(p0))));

            // 진행률이 1에 닿는 틱까지 적용하고 끝낸다. 먼저 return하면 마지막 구간만큼
            // 목표 거리에 못 미쳐 매번 조금씩 덜 나간다.
            if (p1 >= 1f) CancelLunge();
        }

        /// <summary>CC 클립 재생 요청. 경직 도중 기절로 덮어써지는 경우가 있어 상태 진입이 아니라
        /// CC가 실제로 갱신되는 지점(PlayerCrowdControlController.ApplyCC)에서 호출한다.</summary>
        public void PlayCCAnim(CCType type)
            => RequestAnimPlay(type == CCType.Stun ? StunHash : FlinchHash);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_OnAnimHashChanged() => OnAnimHashChanged();
        private void OnAnimHashChanged()
        {
            if (_animator != null)
                _animator.CrossFadeInFixedTime(RequestedAnimHash, RequestedAnimFadeDuration);
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput(out NetworkInputData input))
            {
                LastInput = input;
            }

            // 클라이언트(Input Authority): 상태 전이·전투 판정은 서버 전용(ADR-001)이므로
            // 서버가 확정한 상태를 기준으로 "이동만" 로컬 예측한다. 미스프레딕션은 KCC가
            // 리심 + 렌더 감쇠(PredictionCorrectionSpeed)로 부드럽게 교정한다.
            if (!HasStateAuthority)
            {
                PredictMovement(LastInput);
                return;
            }
            if (_current == null) return;

            Movement.ClearInput(); // 이동 상태가 아니면 이번 틱 입력은 0 (아래 Execute가 덮어쓸 수 있음)

            // 사망 중엔 무조건 Dead로 강제 전이(입력 판정 스킵), 부활하면 Idle로 복귀.
            // 그 외에는 상태머신이 입력으로 다음 상태를 판정한다. (사망/부활은 PlayerLifeCycle.IsDead가 주도)
            // CC(경직·기절)도 사망과 같은 게이트 — 걸려 있는 동안 입력 판정을 건너뛰고, 풀리면 Idle로 복귀한다.
            // 우선순위·면역 판정은 전부 PlayerCrowdControlController가 끝낸 뒤라 여기선 결과만 읽는다.
            bool dead = _lifeCycle != null && _lifeCycle.IsDead;
            bool crowdControlled = _cc != null && _cc.ActiveCC != CCType.None;
            PlayerState next;
            if (dead) next = PlayerState.Dead;
            else if (CurrentState == PlayerState.Dead) next = PlayerState.Idle;
            else if (crowdControlled) next = PlayerState.CrowdControlled;
            else if (CurrentState == PlayerState.CrowdControlled) next = PlayerState.Idle;
            else next = _current.Execute(this, LastInput);

            if (next != CurrentState)
                TransitionTo(next, LastInput);

            // 사망·CC 중엔 전진을 끊는다 — 경직당한 채로 계속 밀려나가면 안 된다.
            if (dead || crowdControlled) CancelLunge();
            else ApplyLunge();

            Movement.Tick(); // 이번 틱 입력으로 KCC 시뮬레이션 실행
        }
        /// <summary>입력의 이동 성분을 실제 적용한다. 서버 시뮬레이션(PlayerStateBase.Move)과
        /// 클라 예측(PredictMovement)이 같은 코드를 타야 결정론이 유지된다. 이동 중이면 true.</summary>
        public bool ApplyMovement(NetworkInputData input)
        {
            Vector3 moveDir = new Vector3(input.Move.x, 0, input.Move.y);
            Vector3 local = transform.InverseTransformDirection(moveDir);
            // guardrail:allow AUTH — 클라 쓰기는 예측값. 서버 스냅샷이 항상 덮어쓴다.
            BlendDir = new Vector2(local.x, local.z);
            Movement.Move(moveDir);
            return input.Move.sqrMagnitude > 0.01f;
        }

        // 클라 예측: 서버 확정 상태가 이동 가능(Idle/Moving)일 때만 입력을 넣고,
        // 그 외 상태에서도 KCC 틱은 돌려 중력·관성 예측을 유지한다.
        private void PredictMovement(NetworkInputData input)
        {
            if (!Object.HasInputAuthority || Movement == null) return;

            Movement.ClearInput();
            if (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Moving)
                ApplyMovement(input);
            Movement.Tick();
        }

        public override void Render()
        {
            // KCC 렌더 보간 + 예측 오차 감쇠 (수동 업데이트 모드라 여기서 구동)
            if (Movement != null)
                Movement.RenderTick();

            if (_animator == null) return;
            // guardrail:allow RNG
            _animator.SetFloat(AnimMoveX, BlendDir.x, _locomotionDamp, Time.deltaTime);
            // guardrail:allow RNG
            _animator.SetFloat(AnimMoveY, BlendDir.y, _locomotionDamp, Time.deltaTime);
        }
        private void TransitionTo(PlayerState next, NetworkInputData input)
        {
            PlayerState previous = CurrentState;

            _current?.Exit(this, LastInput);
            // guardrail:allow
            CurrentState = next;
            // guardrail:allow
            _current = _states[next];

            bool returnedToLocomotion =
                previous != PlayerState.Idle &&
                previous != PlayerState.Moving &&
                (next == PlayerState.Idle || next == PlayerState.Moving);

            if (returnedToLocomotion)
                RequestAnimPlay(LocomotionHash);

            _current.Enter(this, LastInput);
        }
    }
}
