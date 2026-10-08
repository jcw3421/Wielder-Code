using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>온보딩 진행의 단일 담당. 과정 하나를 붙들고, 스텝이 끝났는지 판정하고,
    /// 결과를 <see cref="OnboardingEvents"/>로 알린다.
    ///
    /// 판정은 <see cref="GameplayEvents"/>만 구독해서 한다. 러너는 무기도 던전도 모른다 —
    /// 어떤 과정을 언제 띄울지는 <see cref="OnboardingCourseProvider"/>가, 어떻게 보일지는 UI가 정한다.
    /// 그래서 새 분야를 붙일 때 손댈 곳은 공급자와 (필요하면) 트리거 enum뿐이다.
    ///
    /// 진행을 막지 않는다 — 스텝이 전부 동시에 살아 있고, 어느 것을 먼저 하든 그 줄만 체크된다.
    ///
    /// 온보딩은 혼자 도는 곳이라 "내 이벤트인지"를 가리지 않는다. 4인 세션에 그대로 붙이면
    /// 남의 처치·남의 체인도 내 진행도를 채운다.</summary>
    public sealed class OnboardingRunner : MonoBehaviour
    {
        [Tooltip("이 러너가 구독할 과정 공급자들. 비우면 자식에서 자동으로 찾는다.")]
        [SerializeField] private List<OnboardingCourseProvider> _providers = new();

        private IOnboardingCourse _course;
        private bool[] _completed;
        private int _killCount;
        private bool _finished;

        private void Awake()
        {
            // 인스펙터에서 Size만 늘리고 비워 둔 칸은 없는 것으로 친다 — 그걸 세어 버리면
            // "비었을 때 자동 탐색"이 영영 안 돈다.
            _providers.RemoveAll(provider => provider == null);

            if (_providers.Count == 0)
                GetComponentsInChildren(true, _providers);
        }

        private void OnEnable()
        {
            foreach (var provider in _providers)
                if (provider != null)
                    provider.CourseRequested += StartCourse;

            GameplayEvents.ItemPickedUp += OnItemPickedUp;
            GameplayEvents.InventoryOpened += OnInventoryOpened;
            GameplayEvents.WeaponEquipped += OnWeaponEquipped;
            GameplayEvents.EnemyDied += OnEnemyDied;
            GameplayEvents.UltimateGaugeChanged += OnUltimateGaugeChanged;
            GameplayEvents.ChainProgress += OnChainProgress;
            GameplayEvents.OnWeaponUsed += OnWeaponUsed;
            GameplayEvents.GuardSucceeded += OnGuardSucceeded;
            GameplayEvents.CounterAttacked += OnCounterAttacked;
        }

        private void OnDisable()
        {
            foreach (var provider in _providers)
                if (provider != null)
                    provider.CourseRequested -= StartCourse;

            GameplayEvents.ItemPickedUp -= OnItemPickedUp;
            GameplayEvents.InventoryOpened -= OnInventoryOpened;
            GameplayEvents.WeaponEquipped -= OnWeaponEquipped;
            GameplayEvents.EnemyDied -= OnEnemyDied;
            GameplayEvents.UltimateGaugeChanged -= OnUltimateGaugeChanged;
            GameplayEvents.ChainProgress -= OnChainProgress;
            GameplayEvents.OnWeaponUsed -= OnWeaponUsed;
            GameplayEvents.GuardSucceeded -= OnGuardSucceeded;
            GameplayEvents.CounterAttacked -= OnCounterAttacked;
        }

        private void Start()
        {
            if (_providers.Count == 0)
            {
                Debug.LogError("[OnboardingRunner] 과정 공급자가 하나도 없습니다.");
                enabled = false;
                return;
            }

            foreach (var provider in _providers)
                if (provider != null)
                    provider.Begin();
        }

        /// <summary>표시할 과정을 갈아 끼운다. 이전 과정의 진행도는 버린다 —
        /// 무기를 바꿔 끼면 이전 무기의 진행도를 이어받지 않는 게 맞다.</summary>
        private void StartCourse(IOnboardingCourse course)
        {
            if (course?.Steps == null)
            {
                Debug.LogError("[OnboardingRunner] 스텝이 없는 과정을 받았습니다.");
                return;
            }

            _course = course;
            _completed = new bool[course.Steps.Count];
            _killCount = 0;
            _finished = false;

            OnboardingEvents.RaiseCourseStarted(course);

            // 풀체인은 4인이 모여야 터진다 — 혼자 도는 온보딩에서는 실제로 발동시킬 수 없다.
            // 줄은 그대로 보여 주되(그런 게 있다는 걸 알리는 게 목적이다) 즉시 완료로 친다.
            Complete(OnboardingTrigger.FullChain, _ => true);
        }

        // ── 이벤트 → 스텝 ───────────────────────────────────────

        private void OnItemPickedUp(int playerId, int itemId, int count)
            => Complete(OnboardingTrigger.ItemPickedUp, param => param == 0 || param == itemId);

        private void OnInventoryOpened()
            => Complete(OnboardingTrigger.InventoryOpened, _ => true);

        // weaponId 0은 "맨손"이다(장착 해제 시 모델을 되돌리려고 쏜다). 장착으로 치면 안 된다.
        private void OnWeaponEquipped(int weaponId, int sourcePlayerId)
        {
            if (weaponId <= 0) return;
            Complete(OnboardingTrigger.WeaponEquipped, param => param == 0 || param == weaponId);
        }

        private void OnEnemyDied(int enemyId, int sourcePlayerId)
        {
            _killCount++;
            Complete(OnboardingTrigger.EnemyKilled, param => _killCount >= Mathf.Max(1, param));
        }

        private void OnUltimateGaugeChanged(int playerId, int current, int max)
        {
            if (max <= 0 || current < max) return;
            Complete(OnboardingTrigger.UltimateGaugeFull, _ => true);
        }

        private void OnChainProgress(int playerId, int stage, int windowSecondsLeft)
            => Complete(OnboardingTrigger.ChainStageReached, param => stage >= Mathf.Max(1, param));

        private void OnWeaponUsed(int playerId, int weaponId, int attackType, float cooldown)
            => Complete(OnboardingTrigger.WeaponActionUsed, param => param == attackType);

        // Param이 1이면 저스트가드만 인정한다. 0이면 일반 막기도 통과.
        private void OnGuardSucceeded(int playerId, bool isJustGuard)
            => Complete(OnboardingTrigger.GuardSucceeded, param => param == 0 || isJustGuard);

        private void OnCounterAttacked(int playerId)
            => Complete(OnboardingTrigger.CounterAttacked, _ => true);

        /// 같은 트리거를 쓰는 스텝을 모두 훑어, 조건을 만족한 것만 체크한다.
        /// (같은 트리거에 Param만 다른 줄 — 체인 2단계·3단계 — 을 함께 둘 수 있다.)
        private void Complete(OnboardingTrigger trigger, Func<int, bool> satisfied)
        {
            if (_course == null || _completed == null || _finished) return;

            var steps = _course.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (_completed[i]) continue;
                if (steps[i].Trigger != trigger) continue;
                if (!satisfied(steps[i].Param)) continue;

                _completed[i] = true;
                OnboardingEvents.RaiseStepCompleted(i);
            }

            TryFinish();
        }

        private void TryFinish()
        {
            for (int i = 0; i < _completed.Length; i++)
                if (!_completed[i]) return;

            _finished = true;
            OnboardingEvents.RaiseCourseCompleted(_course);
        }
    }
}
