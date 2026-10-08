using System.Collections.Generic;
using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD 최상위 총괄자. <see cref="GameplayEvents"/>를 구독해 UI를 직접 바꾸지 않고
    /// 해당 서브 패널에게 값을 넘기기만 한다(라우팅 역할).
    ///
    /// "나 vs 파티원" 구분은 UI만의 관심사다 — Core 이벤트는 전부 playerId 단위라
    /// 로컬 플레이어 id를 기준으로 갈래를 나눈다. 로컬 id는 스폰 시 Fusion Input Authority로 자동 판별하며,
    /// 필요하면 <see cref="SetLocalPlayer"/>로 외부에서 지정할 수도 있다.
    ///
    /// Map/QuickBar/Chat 패널은 게임플레이 이벤트와 무관(입력 구동)해 여기서 라우팅하지 않는다.
    /// </summary>
    public sealed class HudOrchestrator : MonoBehaviour
    {
        [Header("이벤트로 갱신되는 패널")]
        [SerializeField] private PlayerUiPanel _playerPanel;
        [SerializeField] private PartyUiPanel _partyPanel;
        [SerializeField] private BossUiPanel _bossPanel;
        [SerializeField] private ChainVignette _chainVignette;   // 테두리 색 레이어 오버레이(선택)
        [SerializeField] private SkillCutIn _skillCutIn;         // 내 궁극기 전용 중앙 컷인(선택)
        [SerializeField] private PartyCutInStack _chainCutInStack; // 화면 우측 체인 카드 목록(선택). 나를 포함한 기여자 전원이 쌓인다
        [SerializeField] private GuardResultBanner _guardBanner;  // 내 방어 성공 표시(선택)

        private int _localPlayerId = -1;

        // 무기 장착 때 확정한 playerId→무기. 체인 기여자의 직업색(비네트)과 컷인 일러스트를 여기서 찾는다.
        // 무기 장착 이벤트는 모든 클라이언트에 오므로 남의 무기도 들어있다.
        private readonly Dictionary<int, WeaponRecord> _weaponByPlayer = new();

        // 체인 카드에 띄울 이름. 닉네임 이벤트는 컷인보다 먼저 오므로 받아둔다.
        private readonly Dictionary<int, string> _nicknameByPlayer = new();

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            GameplayEvents.PlayerHealthChanged += HandlePlayerHealthChanged;
            GameplayEvents.UltimateGaugeChanged += HandleUltimateGaugeChanged;
            GameplayEvents.PlayerStaminaChanged += HandlePlayerStaminaChanged;
            GameplayEvents.PlayerExpChanged += HandlePlayerExpChanged;
            GameplayEvents.PlayerLeveledUp += HandlePlayerLeveledUp;
            GameplayEvents.PlayerSpawned += HandlePlayerSpawned;
            GameplayEvents.WeaponEquipped += HandleWeaponEquipped;
            GameplayEvents.BuffApplied += HandleBuffApplied;
            GameplayEvents.BuffRemoved += HandleBuffRemoved;
            GameplayEvents.DebuffApplied += HandleDebuffApplied;
            GameplayEvents.DebuffRemoved += HandleDebuffRemoved;
            GameplayEvents.BossStateChanged += HandleBossStateChanged;
            GameplayEvents.EnemyHealthChanged += HandleEnemyHealthChanged;   // 보스 HP가 이 채널로 온다
            GameplayEvents.OnPlayerDespawned += HandleOnPlayerDespawned;
            GameplayEvents.OnBossSpawned += HandleOnBossSpawned;
            GameplayEvents.OnBossDespawned += HandleOnBossDespawned;
            GameplayEvents.ChainProgress += HandleChainProgress;
            GameplayEvents.FullChainTriggered += HandleFullChainTriggered;
            GameplayEvents.OnWeaponUsed += HandleWeaponUsed;
            GameplayEvents.NicknameChanged += HandleNicknameChanged;
            GameplayEvents.GuardSucceeded += HandleGuardSucceeded;
        }

        private void Unsubscribe()
        {
            GameplayEvents.PlayerHealthChanged -= HandlePlayerHealthChanged;
            GameplayEvents.UltimateGaugeChanged -= HandleUltimateGaugeChanged;
            GameplayEvents.PlayerStaminaChanged -= HandlePlayerStaminaChanged;
            GameplayEvents.PlayerExpChanged -= HandlePlayerExpChanged;
            GameplayEvents.PlayerLeveledUp -= HandlePlayerLeveledUp;
            GameplayEvents.PlayerSpawned -= HandlePlayerSpawned;
            GameplayEvents.WeaponEquipped -= HandleWeaponEquipped;
            GameplayEvents.BuffApplied -= HandleBuffApplied;
            GameplayEvents.BuffRemoved -= HandleBuffRemoved;
            GameplayEvents.DebuffApplied -= HandleDebuffApplied;
            GameplayEvents.DebuffRemoved -= HandleDebuffRemoved;
            GameplayEvents.BossStateChanged -= HandleBossStateChanged;
            GameplayEvents.EnemyHealthChanged -= HandleEnemyHealthChanged;
            GameplayEvents.OnPlayerDespawned -= HandleOnPlayerDespawned;
            GameplayEvents.OnBossSpawned -= HandleOnBossSpawned;
            GameplayEvents.OnBossDespawned -= HandleOnBossDespawned;
            GameplayEvents.ChainProgress -= HandleChainProgress;
            GameplayEvents.FullChainTriggered -= HandleFullChainTriggered;
            GameplayEvents.OnWeaponUsed -= HandleWeaponUsed;
            GameplayEvents.NicknameChanged -= HandleNicknameChanged;
            GameplayEvents.GuardSucceeded -= HandleGuardSucceeded;
        }

        /// <summary>네트워킹 계층이 로컬 플레이어 id를 확정하면 호출한다(선택 — 스폰 자동판별이 우선).
        /// 순서 문제로 내가 파티 카드로 먼저 들어갔다면 회수해 내 패널로 되돌린다.</summary>
        public void SetLocalPlayer(int playerId)
        {
            _localPlayerId = playerId;
            if (_partyPanel.Has(playerId))
            {
                _partyPanel.RemoveMember(playerId);
                _playerPanel.Bind($"Player {playerId}");
            }
        }

        private void HandlePlayerHealthChanged(int playerId, int current, int max)
        {
            if (IsLocal(playerId)) _playerPanel.SetHp(current, max);
            else _partyPanel.UpdateHealth(playerId, current, max);
        }

        private void HandleUltimateGaugeChanged(int playerId, int current, int max)
        {
            if (IsLocal(playerId)) _playerPanel.SetUltimate(current, max);
            else _partyPanel.UpdateUltimate(playerId, current, max);
        }

        // 스테미너는 내 것만 표시한다 — 남의 대시 자원은 판단에 쓸 일이 없다(경험치와 같은 취급).
        private void HandlePlayerStaminaChanged(int playerId, int current, int max)
        {
            if (IsLocal(playerId)) _playerPanel.SetStamina(current, max);
        }

        // 경험치·레벨은 내 것만 표시한다(파티원 카드엔 경험치 바 없음).
        private void HandlePlayerExpChanged(int playerId, int currentExp, int expToNext, int level)
        {
            if (IsLocal(playerId)) _playerPanel.SetExp(currentExp, expToNext, level);
        }

        private void HandlePlayerLeveledUp(int playerId, int newLevel)
        {
            if (IsLocal(playerId)) _playerPanel.PlayLevelUp(newLevel);
        }

        // 무기 장착·교체 → IWeaponDB로 직업을 조회 → 그 직업의 아이콘·얼티밋 세트로 전환.
        // 스폰 콜백 안에서 불리므로, DB 미등록이어도 예외로 netcode를 죽이지 않게 TryGet으로 방어한다.
        private void HandleWeaponEquipped(int weaponId, int playerId)
        {
            if (!ServiceLocator.TryGet(out IWeaponDB weaponDB)) return;
            if (!weaponDB.TryGet(weaponId, out WeaponRecord weapon)) return;

            _weaponByPlayer[playerId] = weapon;                  // 체인 비네트·컷인 조회용

            if (IsLocal(playerId)) _playerPanel.SetClass(weapon.ClassType);
            else _partyPanel.SetClass(playerId, weapon.ClassType);
        }

        private void HandlePlayerSpawned(int playerId, GameObject avatar)
        {
            if (IsLocalAvatar(playerId, avatar))
            {
                _localPlayerId = playerId;
                _partyPanel.RemoveMember(playerId);          // 혹시 파티로 먼저 들어갔으면 회수
                _playerPanel.Bind($"Player {playerId}");     // 나 → User 패널
            }
            else
            {
                _partyPanel.AddMember(playerId);             // 남 → 파티 카드
            }
        }

        // 퇴장한 플레이어의 파티 카드를 제거한다(로컬은 카드가 없어 무시됨).
        private void HandleOnPlayerDespawned(int playerId) => _partyPanel.RemoveMember(playerId);

        // 보스 스폰/디스폰으로 보스 UI를 켜고 끈다(HP 기반 숨김과 별개 — 씬 전환 등).
        // 퇴장은 지금 떠 있는 대상일 때만 끈다 — 허수아비 등 다른 개체의 퇴장이 보스바를 지우지 않게.
        private void HandleOnBossSpawned(int bossId) => _bossPanel.Show(bossId);
        private void HandleOnBossDespawned(int bossId)
        {
            if (_bossPanel.BossId == bossId) _bossPanel.Hide();
        }

        // 체인 진행 — 궁극기 사용자의 컷인을 띄우고 연결 윈도우 슬라이드바를 되감는다.
        // stage 0 = 윈도우 만료로 인한 취소 신호 → 연출을 거둔다.
        private void HandleChainProgress(int playerID, int stage, int windowSeconds)
        {
            RouteChainVignette(playerID, stage);
            RouteSkillCutIn(playerID, stage, windowSeconds);
        }

        // 비네트 테두리 레이어 라우팅. 기여자 직업색을 stage 링에 쌓는다. stage 0 = 취소(실패 플래시).
        private void RouteChainVignette(int playerID, int stage)
        {
            if (_chainVignette == null) return;

            if (stage <= 0)
            {
                _chainVignette.Fail();
                return;
            }

            if (_weaponByPlayer.TryGetValue(playerID, out WeaponRecord weapon))
                _chainVignette.SetLayer(stage, weapon.ClassType);
        }

        // 컷인 라우팅. 체인 진행 이벤트는 모든 클라이언트에 오므로 남의 궁극기도 내 화면에 뜬다.
        // 기여자는 나를 포함해 전원 우측 카드 목록에 stage 순서대로 쌓이고,
        // 그게 나였다면 화면 중앙 컷인이 추가로 함께 터진다.
        // stage 0 = 연결시간 초과로 체인 종료 → 양쪽 다 거둔다.
        private void RouteSkillCutIn(int playerID, int stage, int windowSeconds)
        {
            if (stage <= 0)
            {
                EndAllSkillCutIns();
                return;
            }

            if (!_weaponByPlayer.TryGetValue(playerID, out WeaponRecord weapon)) return;

            _chainCutInStack?.ShowLink(stage, weapon.ClassType, weapon.Id, GetNickname(playerID), windowSeconds);

            if (IsLocal(playerID)) _skillCutIn?.Play(weapon.Id);
        }

        private string GetNickname(int playerId)
        {
            return _nicknameByPlayer.TryGetValue(playerId, out string nickname)
                ? nickname
                : $"Player {playerId}";
        }

        // 풀체인 발동 — 체인이 소비됐으므로 연결 윈도우 연출도 끝난다.
        private void HandleFullChainTriggered(int participants)
        {
            _chainVignette?.Burst();
            EndAllSkillCutIns();
        }

        // 체인 종료는 기여자와 무관한 전역 신호 — 내 중앙 컷인과 쌓인 카드를 함께 거둔다.
        private void EndAllSkillCutIns()
        {
            _skillCutIn?.NotifyChainEnded();
            _chainCutInStack?.ClearAll();
        }


        // 무기 사용 → 해당 슬롯 쿨타임 시작. attackType은 DamageType(Normal=좌클릭, Skill=우클릭).
        // 얼티밋은 쿨타임 대신 게이지로 표시하고, 컷인은 ChainProgress가 담당하므로 여기선 무시한다.
        // 이벤트는 사용 당사자에게만 온다.
        private void HandleWeaponUsed(int playerId, int weaponId, int attackType, float cooldown)
        {
            if (!IsLocal(playerId)) return;

            switch ((DamageType)attackType)
            {
                case DamageType.Normal:
                    _playerPanel.TriggerSkillCooldown(PlayerUiPanel.SkillSlotId.LeftClick, cooldown);
                    break;
                case DamageType.Skill:
                    _playerPanel.TriggerSkillCooldown(PlayerUiPanel.SkillSlotId.RightClick, cooldown);
                    break;
            }
        }

        // 방어 성공 — 남이 막은 것까지 내 화면에 띄우면 시끄럽다. 내 것만 표시한다.
        private void HandleGuardSucceeded(int playerId, bool isJustGuard)
        {
            if (!IsLocal(playerId)) return;
            _guardBanner?.Play(isJustGuard);
        }

        // 버프/디버프는 대상(보스·나·파티원)만 가리면 나머지는 대칭이다.
        private void HandleBuffApplied(int targetId, BuffInfo info)
        {
            if (IsBossTarget(targetId)) _bossPanel.ApplyBuff(info);
            else if (IsLocal(targetId)) _playerPanel.ApplyBuff(info);
            else _partyPanel.ApplyBuff(targetId, info);
        }

        private void HandleBuffRemoved(int targetId, BuffType type)
        {
            if (IsBossTarget(targetId)) _bossPanel.RemoveBuff(type);
            else if (IsLocal(targetId)) _playerPanel.RemoveBuff(type);
            else _partyPanel.RemoveBuff(targetId, type);
        }

        private void HandleDebuffApplied(int targetId, DebuffInfo info)
        {
            if (IsBossTarget(targetId)) _bossPanel.ApplyDebuff(info);
            else if (IsLocal(targetId)) _playerPanel.ApplyDebuff(info);
            else _partyPanel.ApplyDebuff(targetId, info);
        }

        private void HandleDebuffRemoved(int targetId, DebuffType type)
        {
            if (IsBossTarget(targetId)) _bossPanel.RemoveDebuff(type);
            else if (IsLocal(targetId)) _playerPanel.RemoveDebuff(type);
            else _partyPanel.RemoveDebuff(targetId, type);
        }

        private void HandleBossStateChanged(int bossId, int currentHp, int maxHp, int phase)
        {
            _bossPanel.SetState(bossId, currentHp, maxHp, phase);
        }

        // 보스 데미지는 EnemyHealthChanged로 온다(BossStateChanged는 페이즈 변화 때만).
        // 보스 id가 일치할 때만 보스바 HP를 갱신한다(일반 적·미니언은 무시).
        private void HandleEnemyHealthChanged(int enemyId, int current, int max)
        {
            if (IsBossTarget(enemyId)) _bossPanel.SetHealth(current, max);
        }

        // "이 아바타가 나인가?" 판정. Fusion Input Authority가 가장 확실(내 아바타만 true).
        // 아바타에 NetworkObject가 없으면 SetLocalPlayer로 지정된 id로 대체 판정.
        private bool IsLocalAvatar(int playerId, GameObject avatar)
        {
            if (avatar != null)
            {
                NetworkObject networkObject = avatar.GetComponentInParent<NetworkObject>();
                if (networkObject != null) return networkObject.HasInputAuthority;
            }
            return IsLocal(playerId);
        }

        private void HandleNicknameChanged(int playerId, string nickname)
        {
            _nicknameByPlayer[playerId] = nickname;   // 체인 카드 이름표 조회용

            if (IsLocal(playerId)) _playerPanel.Bind(nickname);
            else if (_partyPanel.Has(playerId)) _partyPanel.UpdateNickname(playerId, nickname);
        }

        private bool IsLocal(int playerId) => _localPlayerId >= 0 && playerId == _localPlayerId;

        // 이 id가 지금 화면에 떠 있는 보스인가. 보스 HP·디버프 라우팅의 단일 판정 지점.
        private bool IsBossTarget(int id) => _bossPanel.IsActive && id == _bossPanel.BossId;
    }
}
