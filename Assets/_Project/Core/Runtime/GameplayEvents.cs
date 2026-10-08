using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>모듈 간 단방향 신호 채널. 게임플레이 모듈(Combat/World/BossContent)이
    /// 상태 변화·신호를 발행하면 다른 모듈이 구독한다. 모듈이 서로를 직접 참조하지 않게 해
    /// 의존 경계(특히 형제 World↔BossContent, UI→게임플레이)를 지키는 장치다.
    ///
    /// 규칙:
    ///  - 이벤트 발행(Raise*)은 서버(State Authority)에서. 클라이언트에서 발행하지 않는다.
    ///  - 구독자 종류:
    ///     · UI 구독자 — 표시·연출만. 여기서 게임 상태를 바꾸지 않는다.
    ///     · 서버 권한 게임플레이 시스템(호스트) — 자신의 State Authority로 상태를 바꿀 수 있다
    ///       (예: 적 처치→경험치 지급, 임시 스탯 적용). 클라이언트에서는 바꾸지 않는다.
    ///
    /// 초안: 정적 허브. 규모가 커지면 DI/스코프 기반 채널로 교체 가능(인터페이스 유지 권장).</summary>
    public static class GameplayEvents
    {
        // 플레이어 체력 변화 (playerId, current, max)
        public static event Action<int, int, int> PlayerHealthChanged;
        public static void RaisePlayerHealthChanged(int playerId, int current, int max)
            => PlayerHealthChanged?.Invoke(playerId, current, max);

        // 플레이어 스테미너 변화 (playerId, current, max) — 대시 자원. 최대치는 장착 무기가 정한다.
        public static event Action<int, int, int> PlayerStaminaChanged;
        public static void RaisePlayerStaminaChanged(int playerId, int current, int max)
            => PlayerStaminaChanged?.Invoke(playerId, current, max);

        // 플레이어 사망 (playerId) — 서버(PlayerLifeCycle)가 HP 0 도달 시 발행.
        // UI(사망 표시)·인카운터(전멸 판정) 등이 구독. (관전 카메라는 IsDead를 직접 읽음)
        public static event Action<int> PlayerDied;
        public static void RaisePlayerDied(int playerId)
            => PlayerDied?.Invoke(playerId);

        // 플레이어 부활 "요청" (playerId) — 호스트 측 부활 정책 소스가 발행하고,
        // 서버 권한 PlayerLifeCycle이 구독해 부활을 적용한다. 정책과 메커니즘을 분리한다
        // (DebuffRequested 패턴). ※ 요청을 발행하는 구체 트리거는 아직 없음(ADR-003).
        public static event Action<int> PlayerReviveRequested;
        public static void RaisePlayerReviveRequested(int playerId)
            => PlayerReviveRequested?.Invoke(playerId);

        // 플레이어 부활 완료 (playerId) — 서버가 부활을 확정 적용한 뒤 발행. UI 등이 구독.
        public static event Action<int> PlayerRevived;
        public static void RaisePlayerRevived(int playerId)
            => PlayerRevived?.Invoke(playerId);

        // 체인(풀체인 빌드) 진행도 — 궁극기 사용으로 누적 (playerId, 현재 단계, 윈도우 남은 초)
        // ※ 시그니처는 궁극기 게이지/체인 단계 분리 시 재설계 예정.
        public static event Action<int, int, int> ChainProgress;
        public static void RaiseChainProgress(int playerId, int stage, int windowSecondsLeft)
            => ChainProgress?.Invoke(playerId, stage, windowSecondsLeft);

        // 풀체인 발동 (참여 인원 수)
        public static event Action<int> FullChainTriggered;
        public static void RaiseFullChainTriggered(int participants)
            => FullChainTriggered?.Invoke(participants);

        // 보스 체력/페이즈 (bossId, currentHp, maxHp, phase)
        public static event Action<int, int, int, int> BossStateChanged;
        public static void RaiseBossStateChanged(int bossId, int currentHp, int maxHp, int phase)
            => BossStateChanged?.Invoke(bossId, currentHp, maxHp, phase);

        // 적(Enemy) 체력 변화 (enemyId, current, max)
        public static event Action<int, int, int> EnemyHealthChanged;
        public static void RaiseEnemyHealthChanged(int enemyId, int current, int max)
            => EnemyHealthChanged?.Invoke(enemyId, current, max);

        // 적 사망 (enemyId, 마지막 피해 sourcePlayerId — 없으면 -1)
        public static event Action<int, int> EnemyDied;
        public static void RaiseEnemyDied(int enemyId, int sourcePlayerId)
            => EnemyDied?.Invoke(enemyId, sourcePlayerId);

        // 궁극기 게이지 변화 (playerId, current, max)
        public static event Action<int, int, int> UltimateGaugeChanged;
        public static void RaiseUltimateGaugeChanged(int playerId, int current, int max)
            => UltimateGaugeChanged?.Invoke(playerId, current, max);

        // 플레이어 아바타 스폰 (playerId, avatar GameObject)
        public static event Action<int, GameObject> PlayerSpawned;
        public static void RaisePlayerSpawned(int playerId, GameObject avatar)
            => PlayerSpawned?.Invoke(playerId, avatar);

        // 아이템 드랍 발생 (월드 좌표 대신 식별/카운트만 — 표시는 UI가 결정)
        public static event Action<ItemId, int> ItemDropped;
        public static void RaiseItemDropped(ItemId item, int count)
            => ItemDropped?.Invoke(item, count);

        // 경험치 변화 (playerId, 현재 경험치, 다음 레벨까지 필요량, 현재 레벨)
        public static event Action<int, int, int, int> PlayerExpChanged;
        public static void RaisePlayerExpChanged(int playerId, int currentExp, int expToNext, int level)
            => PlayerExpChanged?.Invoke(playerId, currentExp, expToNext, level);

        // 레벨업 (playerId, 새 레벨)
        public static event Action<int, int> PlayerLeveledUp;
        public static void RaisePlayerLeveledUp(int playerId, int newLevel)
            => PlayerLeveledUp?.Invoke(playerId, newLevel);

        // 스탯 변화 (playerId, 스탯 종류, 적용 레이어, 값) — UI가 레이어별 표시 갱신
        public static event Action<int, StatType, StatLayer, int> PlayerStatChanged;
        public static void RaisePlayerStatChanged(int playerId, StatType type, StatLayer layer, int value)
            => PlayerStatChanged?.Invoke(playerId, type, layer, value);

        // 임시 스탯 적용 요청 (playerId, 수정자, 지속 틱) — 전투 외 소스(예: World 맵 효과)가 발행하고
        // Combat의 서버 권한 성장 시스템이 구독해 호스트에서 적용한다. (형제 모듈→Combat 디커플링)
        public static event Action<int, StatModifier, int> TemporaryStatRequested;
        public static void RaiseTemporaryStatRequested(int playerId, StatModifier modifier, int durationTicks)
            => TemporaryStatRequested?.Invoke(playerId, modifier, durationTicks);

        // 디버프 적용 요청 (targetId, debuffInfo) — BossContent/Combat 등이 발행하고
        // 디버프 처리 시스템(서버 권한)이 구독해 적용한다.
        public static event Action<int, DebuffInfo> DebuffRequested;
        public static void RaiseDebuffRequested(int targetId, DebuffInfo info)
            => DebuffRequested?.Invoke(targetId, info);

        // 디버프 실제 적용됨 (targetId, debuffInfo) — 서버가 디버프를 확정 적용한 뒤 발행. UI 등이 구독.
        public static event Action<int, DebuffInfo> DebuffApplied;
        public static void RaiseDebuffApplied(int targetId, DebuffInfo info)
            => DebuffApplied?.Invoke(targetId, info);

        // 디버프 해제됨 (targetId, debuffType) — 만료·정화 등으로 디버프가 끝났을 때 발행.
        public static event Action<int, DebuffType> DebuffRemoved;
        public static void RaiseDebuffRemoved(int targetId, DebuffType type)
            => DebuffRemoved?.Invoke(targetId, type);

        // 버프 적용 요청 (targetId, buffInfo) — BossContent/Combat 등이 발행하고
        // 버프 처리 시스템(서버 권한)이 구독해 적용한다.
        public static event Action<int, BuffInfo> BuffRequested;
        public static void RaiseBuffRequested(int targetId, BuffInfo info)
            => BuffRequested?.Invoke(targetId, info);

        // 버프 실제 적용됨 (targetId, buffInfo) — 서버가 버프를 확정 적용한 뒤 발행. UI 등이 구독.
        public static event Action<int, BuffInfo> BuffApplied;
        public static void RaiseBuffApplied(int targetId, BuffInfo info)
            => BuffApplied?.Invoke(targetId, info);

        // 버프 해제됨 (targetId, buffType) — 만료·정화 등으로 버프가 끝났을 때 발행.
        public static event Action<int, BuffType> BuffRemoved;
        public static void RaiseBuffRemoved(int targetId, BuffType type)
            => BuffRemoved?.Invoke(targetId, type);

        // 데미지 숫자 표시용 (targetId, amount, worldPos, type) — 표시 전용.
        // 각 클라에서 RPC 경유로 발행됨. UI 표시만 구독, 게임플레이 로직 구독 금지.
        public static event Action<int, int, Vector3, DamageType, int> DamageDealt;
        public static void RaiseDamageDealt(int targetId, int amount, Vector3 worldPos, DamageType type, int sourcePlayerId)
            => DamageDealt?.Invoke(targetId, amount, worldPos, type, sourcePlayerId);

        // 월드 앵커 HP바 표시 (앵커 Transform, heightOffset, current, max) — 어디에 어떤 값으로
        // 바를 띄울지 알리는 표시 전용 신호. 게임플레이 로직이 구독하지 말 것.
        // 상단 단일 보스바용 EnemyHealthChanged와 별개 채널인 이유: 목표물은 동시에 여러 개
        // 존재해 단일 바로는 성립하지 않는다.
        // 키가 int id가 아니라 앵커 Transform인 이유: 발행자가 각 피어의 로컬 인스턴스 자신이라
        // 로컬 참조가 그대로 유효하고, id 발급·충돌 규칙이 필요 없어진다.
        // heightOffset이 UI 전역 상수가 아니라 발행자 소유인 이유: 목표물마다 크기가 달라서다.
        // 단위는 월드 단위, 앵커 위로 띄울 거리.
        public static event Action<Transform, float, int, int> WorldHealthBarChanged;
        public static void RaiseWorldHealthBarChanged(Transform anchor, float heightOffset, int current, int max)
            => WorldHealthBarChanged?.Invoke(anchor, heightOffset, current, max);

        // 월드 앵커 HP바 제거 (앵커 Transform) — current <= 0이거나 대상이 사라질 때
        // 발행자가 반드시 호출한다. 바를 지우는 책임은 발행자에게 있다.
        public static event Action<Transform> WorldHealthBarRemoved;
        public static void RaiseWorldHealthBarRemoved(Transform anchor)
            => WorldHealthBarRemoved?.Invoke(anchor);

        // 방어 성공 (playerId, 저스트가드 여부) — 서버가 피해를 감면했을 때 각 클라에서 RPC 경유로 발행.
        /// 막기 성공으로 열린 창에서 반격이 나갔다. 표시·온보딩용.
        public static event Action<int> CounterAttacked;
        public static void RaiseCounterAttacked(int playerId)
            => CounterAttacked?.Invoke(playerId);

        // 표시 전용. isJustGuard=true면 완전 무효(저스트가드), false면 일반 막기.
        public static event Action<int, bool> GuardSucceeded;
        public static void RaiseGuardSucceeded(int playerId, bool isJustGuard)
            => GuardSucceeded?.Invoke(playerId, isJustGuard);

        // 어그로 적용 요청 (playerId, 수치) — Combat 등이 발행하고
        // 서버 권한 적(BossContent)이 구독해 자기 어그로 테이블에 적용한다.
        // (형제 모듈 Combat↔BossContent 디커플링용)
        //
        // 피해로 생기는 어그로는 DamageInfo.AggroAmount를 타고 가므로 이 이벤트를 쓰지 않는다.
        // 막기·힐처럼 남을 때리지 않는 행동은 DamageInfo 자체가 만들어지지 않아
        // 그 경로로 갈 수 없다 — 이 통로가 있는 이유다.
        //
        // ※ 위 GuardSucceeded와는 다른 신호다. 그쪽은 RPC(RpcTargets.InputAuthority) 경유라
        //   막은 당사자 클라에서만 뜨고 상태를 바꾸지 않는다(HUD 배너·온보딩 진행이 구독).
        public static event Action<int, int> AggroRequested;
        public static void RaiseAggroRequested(int playerId, int amount)
            => AggroRequested?.Invoke(playerId, amount);

        public static event Action<int, int> WeaponEquipped;
        public static void RaiseWeaponEquipped(int weaponId, int sourcePlayerId)
            => WeaponEquipped?.Invoke(weaponId, sourcePlayerId);

        /// 인벤토리 창이 열렸다. 온보딩이 "인벤토리를 열어보세요"를 판정하는 데 쓴다.
        public static event Action InventoryOpened;
        public static void RaiseInventoryOpened() => InventoryOpened?.Invoke();

        public static event Action<int, int, int> ItemPickedUp;
        public static void RaiseItemPickedUp(int playerID, int itemId, int count)
            => ItemPickedUp?.Invoke(playerID, itemId, count);

        public static event Action<int> OnPlayerDespawned;
        public static void RaisePlayerDespawned(int playerID)
            => OnPlayerDespawned?.Invoke(playerID);

        public static event Action<int> OnBossSpawned;
        public static void RaiseBossSpawned(int bossID)
            => OnBossSpawned?.Invoke(bossID);

        public static event Action<int> OnBossDespawned;
        public static void RaiseBossDespawned(int bossID)
            => OnBossDespawned?.Invoke(bossID);

        // 무기 장착 요청 (itemId) — 인벤토리는 무기 도감이라 ItemId로 무기를 유일하게 식별한다. -1이면 해제.
        public static event Action<int> OnWeaponEquipRequested;
        public static void RaiseWeaponEquipRequested(int itemId)
            => OnWeaponEquipRequested?.Invoke(itemId);

        public static event Action<int, int, int, float> OnWeaponUsed;
        public static void RaiseWeaponUsed(int playerID, int weaponID, int attackType, float cooldown)
            => OnWeaponUsed?.Invoke(playerID, weaponID, attackType, cooldown);

        // 닉네임 변경 (playerId, nickname) — 로컬 설정 변경·네트워크 동기화 공용
        public static event Action<int, string> NicknameChanged;
        public static void RaiseNicknameChanged(int playerId, string nickname)
            => NicknameChanged?.Invoke(playerId, nickname);

        // 채팅 수신 (playerId, nickname, message) — 서버가 전파한 메시지를 모든 피어에서 발행.
        // 보내기는 IChatService.SendChat.
        public static event Action<int, string, string> ChatMessageReceived;
        public static void RaiseChatMessageReceived(int playerId, string nickname, string message)
            => ChatMessageReceived?.Invoke(playerId, nickname, message);

        // 잡몹 웨이브 스폰 요청 (zoneId) — 던전 진행(서버)이 발행하고, 같은 zoneId를 가진
        // 잡몹 스포너가 구독해 스폰을 시작한다. 형제 모듈(Dungeon↔MinionsGO) 디커플링용.
        public static event Action<int> MinionWaveRequested;
        public static void RaiseMinionWaveRequested(int zoneId)
            => MinionWaveRequested?.Invoke(zoneId);

        // 카메라 흔들림 요청 (세기) — 순수 로컬 연출이라 예외적으로 각 피어에서 발행한다.
        // 트리거가 이미 동기화된 네트워크 상태(보스 패턴 단계 등)라 피어마다 같은 시점에 걸린다.
        // 구독자는 로컬 카메라 리그뿐이고, 여기서 게임 상태를 바꾸지 않는다.
        public static event Action<CameraShakeStrength> CameraShakeRequested;
        public static void RaiseCameraShakeRequested(CameraShakeStrength strength)
            => CameraShakeRequested?.Invoke(strength);

        // 잡몹 웨이브 전멸 (zoneId) — 스포너(서버)가 자기 웨이브를 모두 스폰하고 그 개체가
        // 전부 사라진 뒤 발행. 던전 진행이 단계 클리어 판정에 쓴다.
        public static event Action<int> MinionWaveCleared;
        public static void RaiseMinionWaveCleared(int zoneId)
            => MinionWaveCleared?.Invoke(zoneId);

        // ── 보스 연대 기믹 (표시 전용) ─────────────────────────────────────────
        // 파티 전원의 협동을 요구하는 보스 기믹의 진행 상황. 판정은 전부 서버(BossContent)가 하고,
        // 여기로는 복제된 결과만 흐른다 — UI는 이 세 신호만 보고 그리며 기믹의 종류를 몰라도 된다.
        // 발행 지점은 BossGimmickNetState의 OnChangedRender라 모든 피어에서 같은 값으로 뜬다.
        // 게임플레이 로직이 구독하지 말 것(표시 전용).

        // 기믹 시작 (bossId, gimmickId, 채워야 하는 양, 제한 시간 초)
        // required가 0이면 진행도 막대가 없는 기믹이다. 제한 시간 0은 무제한.
        public static event Action<int, int, int, int> BossGimmickStarted;
        public static void RaiseBossGimmickStarted(int bossId, int gimmickId, int required, int timeLimitSeconds)
            => BossGimmickStarted?.Invoke(bossId, gimmickId, required, timeLimitSeconds);

        // 기믹 진행 (bossId, 현재, 필요, 남은 초) — "3 / 4" 처럼 그대로 그리면 된다.
        public static event Action<int, int, int, int> BossGimmickProgress;
        public static void RaiseBossGimmickProgress(int bossId, int current, int required, int remainingSeconds)
            => BossGimmickProgress?.Invoke(bossId, current, required, remainingSeconds);

        // 기믹 종료 (bossId, gimmickId, 판정) — 판정: 1=성공, 2=실패, 3=취소(보스 사망 등으로 접힘).
        // 값은 BossContent의 GimmickStatus와 짝이다. Core가 그 enum을 알 수 없어 int로 싣는다
        // (CurrentAnimStateId·TargetModeId가 enum을 정수로 싣는 것과 같은 방식).
        public static event Action<int, int, int> BossGimmickResolved;
        public static void RaiseBossGimmickResolved(int bossId, int gimmickId, int status)
            => BossGimmickResolved?.Invoke(bossId, gimmickId, status);
    }


}
