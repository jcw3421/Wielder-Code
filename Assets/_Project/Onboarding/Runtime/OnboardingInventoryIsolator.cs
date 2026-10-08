using System.Collections.Generic;
using Fusion;
using Game.Combat;
using Game.Combat.Player;
using Game.Core;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>온보딩 구간 동안 인벤토리를 격리한다. 들어갈 때 원본을 여기 옮겨 담고 비우고,
    /// 나올 때 그대로 되돌린다 — 그 사이에 주운 것은 버린다.
    ///
    /// 저장 차단은 여기서 하지 않는다. <see cref="OnboardingSession"/>이 규칙으로 걸어 두면
    /// 저장 담당(PlayerContextSaver)이 알아서 건너뛴다.
    ///
    /// 로컬 아바타는 DontDestroyOnLoad라 이 씬이 열릴 때 이미 살아 있다 — 스폰 이벤트가 오지 않으므로
    /// 필요한 순간에 직접 찾는다. 들어갈 때도 나올 때도 아바타는 존재한다.</summary>
    public sealed class OnboardingInventoryIsolator : MonoBehaviour
    {
        private readonly List<WeaponEntryData> _backup = new();
        private int _backupEquippedId = -1;
        private bool _isolated;

        // 구간이 열릴 때(OnEnable) 이미 구독돼 있어야 한다. 씬 로드에서는 모든 Awake가
        // 모든 OnEnable보다 먼저 돌므로 여기서 붙인다.
        private void Awake() => OnboardingSession.ActiveChanged += OnActiveChanged;

        /// 씬이 내려갈 때 세션과 이쪽 중 누가 먼저 파괴되는지는 정해져 있지 않다. 세션이 먼저면
        /// 닫힘 신호로 되돌아가고, 이쪽이 먼저면 여기서 되돌린다. <see cref="Exit"/>는 두 번 불려도 안전하다.
        /// 빠뜨리면 인벤토리를 영영 못 돌려받으므로 양쪽 다 막는다.
        private void OnDestroy()
        {
            OnboardingSession.ActiveChanged -= OnActiveChanged;
            Exit();
        }

        private void OnActiveChanged(bool active)
        {
            if (active) Enter();
            else Exit();
        }

        private void Enter()
        {
            if (_isolated) return;
            if (!TryGetContext(out var context, out var tracker)) return;
            _isolated = true;

            _backup.Clear();
            _backup.AddRange(context.WeaponInventory);
            _backupEquippedId = context.EquippedItemId;

            context.WeaponInventory.Clear();
            context.EquippedItemId = -1;
            tracker.Clear();
            ClearExpState(_backup);

            GameplayEvents.RaiseWeaponEquipRequested(-1);   // 음수 = 장착 해제 요청
        }

        private void Exit()
        {
            if (!_isolated) return;
            _isolated = false;

            if (!TryGetContext(out var context, out var tracker)) return;

            // 온보딩에서 주운 무기의 만충 기록부터 내린다. 남겨 두면 로비로 돌아간 뒤
            // 그 무기를 인벤토리에도 없는 채로 영영 못 줍는다.
            ClearExpState(context.WeaponInventory);

            context.WeaponInventory.Clear();
            context.WeaponInventory.AddRange(_backup);
            context.EquippedItemId = _backupEquippedId;

            tracker.Clear();
            foreach (var item in _backup)
            {
                int maxExp = GetMaxExp(item.ItemId, item.ReinforcementLevel);
                tracker.Upsert(item.ItemId, item.ReinforcementLevel, item.Exp, maxExp);
                ReportExpState(item.ItemId, maxExp > 0 && item.Exp >= maxExp);
            }

            _backup.Clear();
            GameplayEvents.RaiseWeaponEquipRequested(context.EquippedItemId);
        }

        /// 넘겨받은 무기들의 경험치 "가득 참" 표시를 서버에서 내린다.
        /// 인벤토리만 비우고 이걸 두면 만렙이었다는 기록이 서버에 남아, 같은 무기를 다시 주울 수 없다
        /// (줍기 자체가 서버 가드에서 거부된다). 로컬 캐시가 아니라 서버가 보는 값이라
        /// ReportWeaponExpState 경로로 내려야 한다.
        private void ClearExpState(List<WeaponEntryData> items)
        {
            foreach (var item in items)
                ReportExpState(item.ItemId, false);
        }

        /// 아바타 컴포넌트가 살아 있어도 NetworkObject는 먼저 사라질 수 있다(러너 종료·디스폰).
        /// 그 상태로 RPC를 태우면 NRE가 나고, 애초에 받을 서버도 없으므로 조용히 넘긴다.
        private void ReportExpState(int itemId, bool isFull)
        {
            var fsm = LocalStateMachine();
            if (fsm != null && fsm.Object != null)
                fsm.ReportWeaponExpState(itemId, isFull);
        }

        private int GetMaxExp(int itemId, int reinforcementLevel)
        {
            var weapons = LocalWeapons();
            return weapons != null ? weapons.GetMaxExp(itemId, reinforcementLevel) : 0;
        }

        private static bool TryGetContext(out PlayerContextData context, out PlayerInventoryTracker tracker)
        {
            tracker = null;
            return ServiceLocator.TryGet(out context) && ServiceLocator.TryGet(out tracker);
        }

        private PlayerStateMachine _localFsm;
        private WeaponContainer _localWeapons;

        private PlayerStateMachine LocalStateMachine()
        {
            if (_localFsm == null) FindLocalAvatar();
            return _localFsm;
        }

        private WeaponContainer LocalWeapons()
        {
            if (_localWeapons == null) FindLocalAvatar();
            return _localWeapons;
        }

        private void FindLocalAvatar()
        {
            var machines = FindObjectsByType<PlayerStateMachine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var machine in machines)
            {
                var no = machine.GetComponent<NetworkObject>();
                if (no == null || !no.HasInputAuthority) continue;

                _localFsm = machine;
                _localWeapons = machine.GetComponentInChildren<WeaponContainer>(true);
                return;
            }
        }
    }
}
