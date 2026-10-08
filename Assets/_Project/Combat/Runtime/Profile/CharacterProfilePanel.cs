using DG.Tweening;
using Fusion;
using Game.Combat.Player;
using Game.Core;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Combat.Profile
{
    /// <summary>P키로 여는 캐릭터 프로필. 좌측 일러스트·중앙 스탯·우측 어택/스킬/풀체인 설명으로 나뉜다.
    ///
    /// 표시만 하는 뷰다 — 게임 상태를 바꾸지 않으므로 서버 권한과 무관하게 로컬에서 읽기만 한다.
    /// 강화 수치(Reinforcement)가 Combat 소유라 Reinforcer와 같은 자리(Combat)에 둔다.</summary>
    public sealed class CharacterProfilePanel : MonoBehaviour, IToggleUI
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Canvas _canvas;
        [SerializeField, Tooltip("P키에 바인딩된 액션. FirstInputAction/Player/Profile을 물린다.")]
        private InputActionReference _toggleProfileAction;

        [System.Serializable]
        private struct WeaponVisual
        {
            [Tooltip("ItemRecord의 Id. 인벤토리·IItemDB가 쓰는 값과 같아야 한다.")]
            public int WeaponId;
            [Tooltip("이 무기를 들었을 때 켤 오브젝트.")]
            public GameObject Root;
        }

        [Header("View")]
        [SerializeField] private TextMeshProUGUI _nickname;

        [SerializeField, Tooltip("무기별 전신 일러스트. 일러마다 크기·위치가 달라 미리 배치해 두고 켜고 끄기만 한다 " +
            "(WeaponModelSelector가 3D 모델을 다루는 방식과 같다).")]
        private WeaponVisual[] _illusts;

        [SerializeField, Tooltip("무기별 아이콘. 일러스트와 같은 이유로 미리 배치해 두고 토글한다.")]
        private WeaponVisual[] _weaponIcons;
        [SerializeField] private TextMeshProUGUI _weaponName;   // "목방패 + 3"
        [SerializeField] private TextMeshProUGUI _weaponDamage;       // 총공격력
        [SerializeField] private TextMeshProUGUI _baseAttack;          // 무기 기본 공격력(시트값)
        [SerializeField] private TextMeshProUGUI _bonusAttack;         // 강화로 늘어난 몫
        [SerializeField] private TextMeshProUGUI _weaponExp;    // "60%"
        [SerializeField] private TextMeshProUGUI _reinforceRate;      // "35%"
        [SerializeField] private TextMeshProUGUI _reinforceAttempts;  // "12회"
        [SerializeField] private TextMeshProUGUI _health;       // "80 / 100"
        [SerializeField] private TextMeshProUGUI _stamina;        // "100 / 100"
        [SerializeField] private TextMeshProUGUI _dashCost;       // "30"
        [SerializeField] private TextMeshProUGUI _staminaRegen;   // "20 / 초"
        [SerializeField] private TextMeshProUGUI _attackGain;      // "5"

        [SerializeField, Tooltip("방패 전용 항목(반격 회복). 다른 무기를 들면 통째로 숨긴다.")]
        private GameObject _counterRefundRow;
        [SerializeField] private TextMeshProUGUI _counterRefund;   // "15"

        [Header("Open/Close Tween")]
        [SerializeField, Tooltip("여닫는 데 걸리는 시간(초).")]
        private float _tweenDuration = 0.22f;
        [SerializeField, Tooltip("열리기 시작할 때의 크기 배율. 1보다 작으면 살짝 커지며 열린다.")]
        private float _startScale = 0.92f;

        [Header("Skill Description")]
        [SerializeField] private TextMeshProUGUI _attackDesc;
        [SerializeField] private TextMeshProUGUI _skillDesc;
        [SerializeField] private TextMeshProUGUI _ultimateDesc;
        [SerializeField] private TextMeshProUGUI _fullChainDesc;
        [SerializeField, Tooltip("대시 설명. 시트 텍스트가 아니라 소모·쿨타임에서 조립한다.")]
        private TextMeshProUGUI _dashDesc;

        private CanvasGroup _group;
        private RectTransform _panelRect;
        // 계층이 바뀌지 않으므로 한 번만 모아 둔다(바깥→안쪽 순서라, 되감아 쓰면 안쪽부터가 된다).
        private LayoutGroup[] _layoutGroups;
        private Tween _tween;
        private bool _isOpen;

        private PlayerStateMachine _fsm;
        private PlayerLifeCycle _lifeCycle;
        private StringBuilder _builder;

        // 닫히는 연출이 도는 동안에도 "닫혔다"고 봐야 ToggleUIManager의 열림 스택이 어긋나지 않는다.
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _builder = new StringBuilder();
            _panelRect = _panel.GetComponent<RectTransform>();
            _layoutGroups = _panel.GetComponentsInChildren<LayoutGroup>(true);
            if (!_panel.TryGetComponent(out _group))
                _group = _panel.AddComponent<CanvasGroup>();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            // 이 액션은 PlayerInputCollector가 쓰는 복제 인스턴스가 아니라 에셋 원본이라 여기서 직접 켠다.
            if (_toggleProfileAction != null)
            {
                _toggleProfileAction.action.Enable();
                _toggleProfileAction.action.performed += OnToggleKey;
            }

            GameplayEvents.PlayerSpawned += OnPlayerSpawned;
            GameplayEvents.OnPlayerDespawned += OnPlayerDespawned;
            GameplayEvents.WeaponEquipped += OnWeaponEquipped;
            GameplayEvents.PlayerHealthChanged += OnHealthChanged;
            GameplayEvents.PlayerStaminaChanged += OnStaminaChanged;
            GameplayEvents.PlayerStatChanged += OnStatChanged;
            GameplayEvents.NicknameChanged += OnNicknameChanged;
        }

        private void OnDisable()
        {
            // 파괴된 오브젝트를 트윈이 계속 건드리지 않게 정리한다.
            _tween?.Kill();
            _tween = null;

            if (_toggleProfileAction != null)
                _toggleProfileAction.action.performed -= OnToggleKey;

            GameplayEvents.PlayerSpawned -= OnPlayerSpawned;
            GameplayEvents.OnPlayerDespawned -= OnPlayerDespawned;
            GameplayEvents.WeaponEquipped -= OnWeaponEquipped;
            GameplayEvents.PlayerHealthChanged -= OnHealthChanged;
            GameplayEvents.PlayerStaminaChanged -= OnStaminaChanged;
            GameplayEvents.PlayerStatChanged -= OnStatChanged;
            GameplayEvents.NicknameChanged -= OnNicknameChanged;
        }

        // 내 아바타만 잡는다 — 파티원 스폰 이벤트도 같이 오므로 InputAuthority로 거른다.
        private void OnPlayerSpawned(int playerId, GameObject avatar)
        {
            if (!avatar.TryGetComponent<NetworkObject>(out var no) || !no.HasInputAuthority) return;

            _fsm = avatar.GetComponent<PlayerStateMachine>();
            _lifeCycle = avatar.GetComponent<PlayerLifeCycle>();
            if (IsOpen) Refresh();
        }

        private void OnPlayerDespawned(int playerId)
        {
            if (_fsm == null || _fsm.Object == null) return;
            if (_fsm.Object.InputAuthority.PlayerId != playerId) return;

            _fsm = null;
            _lifeCycle = null;
        }

        private void OnWeaponEquipped(int weaponId, int playerId) => RefreshIfOpen();
        private void OnHealthChanged(int playerId, int current, int max) => RefreshIfOpen();
        private void OnStaminaChanged(int playerId, int current, int max) => RefreshIfOpen();
        private void OnStatChanged(int playerId, StatType type, StatLayer layer, int value) => RefreshIfOpen();
        private void OnNicknameChanged(int playerId, string nickname) => RefreshIfOpen();

        private void RefreshIfOpen()
        {
            if (IsOpen) Refresh();
        }

        private void OnToggleKey(InputAction.CallbackContext context)
        {
            if (InputGuard.SuppressKeyboard) return;   // 채팅 등 InputField 입력 중
            if (!ServiceLocator.TryGet<IToggleUIManager>(out var mgr))
            {
                // 매니저가 없으면 단독 토글 — Reinforcer와 같은 폴백.
                if (IsOpen) OnManagedClose();
                else OnManagedOpen(0);
                return;
            }

            if (IsOpen) mgr.Close(this);
            else mgr.Open(this);
        }

        private void Refresh()
        {
            if (ServiceLocator.TryGet<PlayerContextData>(out var ctx))
                _nickname.text = ctx.Nickname;

            RefreshPlayerStats();
            RefreshWeapon();
        }

        private void RefreshPlayerStats()
        {
            if (_lifeCycle != null)
                _health.text = _builder.Clear()
                    .Append(_lifeCycle.CurrentHealth).Append(" / ").Append(_lifeCycle.MaxHealth).ToString();
        }

        private void RefreshWeapon()
        {
            IWeapon weapon = _fsm != null ? _fsm.Weapon : null;
            if (weapon == null)
            {
                SelectVisuals(-1);
                _weaponName.text = "-";
                SetText(_baseAttack, "-");
                SetText(_bonusAttack, "-");
                SetText(_dashDesc, string.Empty);
                SetText(_attackDesc, string.Empty);
                SetText(_skillDesc, string.Empty);
                SetText(_ultimateDesc, string.Empty);
                SetText(_fullChainDesc, string.Empty);
                _weaponDamage.text = "-";
                _weaponExp.text = "-";
                SetText(_reinforceRate, "-");
                SetText(_reinforceAttempts, "-");
                SetText(_stamina, "-");
                SetText(_dashCost, "-");
                SetText(_staminaRegen, "-");
                SetText(_attackGain, "-");
                if (_counterRefundRow != null) _counterRefundRow.SetActive(false);
                return;
            }

            // 무기마다 복장이 통째로 다르므로 일러스트도 무기별 한 장이다.
            SelectVisuals(weapon.ID);

            if (ServiceLocator.TryGet<IItemDB>(out var itemDB) && itemDB.TryGet(weapon.ID, out var record))
            {
                _weaponName.text = _builder.Clear()
                    .Append(record.Name).Append(" + ").Append(weapon.CurrentReinforcement).ToString();

                // 설명은 상시 표시다 — 무기를 바꾸면 세 칸이 같이 갈린다.
                SetText(_attackDesc, record.AttackDesc);
                SetText(_skillDesc, record.SkillDesc);
                SetText(_ultimateDesc, record.UltimateDesc);
                SetText(_fullChainDesc, record.FullChainDesc);
            }

            RefreshDamage(weapon);
            RefreshExp(weapon);
            RefreshReinforce(weapon);
            RefreshStamina(weapon);
            RefreshShieldOnly(weapon);
            RefreshDash(weapon);
        }

        /// <summary>대시 설명은 시트 텍스트가 아니라 실제 수치에서 조립한다 —
        /// 소모는 WeaponStamina가, 쿨타임은 시트의 대시 행이 갖는다.</summary>
        private void RefreshDash(IWeapon weapon)
        {
            if (_dashDesc == null) return;

            WeaponStamina stamina = weapon is Component c ? c.GetComponent<WeaponStamina>() : null;
            AttackRecord dash = FindAttack(weapon.ID, DashAttackName);
            if (stamina == null || dash == null)
            {
                _dashDesc.text = string.Empty;
                return;
            }

            _dashDesc.text = _builder.Clear()
                .Append("스테미너를 소모해 회피한다. 대시 중에는 피해를 받지 않는다.").Append('\n')
                .Append("소모 ").Append(stamina.DashCost)
                .Append(" / 회피 ").Append(dash.Duration.ToString("F2")).Append("초")
                .Append(" / 쿨타임 ").Append(dash.Cooldown.ToString("F2")).Append('초')
                .ToString();
        }

        /// 시트의 대시 행 이름. WeaponBase의 기본값과 같아야 한다.
        private const string DashAttackName = "대시";

        private static AttackRecord FindAttack(int weaponId, string name)
        {
            if (!ServiceLocator.TryGet<IWeaponDB>(out var db) || !db.TryGet(weaponId, out var data)) return null;
            if (data.Attacks == null) return null;

            foreach (AttackRecord a in data.Attacks)
                if (a != null && a.Name == name) return a;
            return null;
        }

        /// <summary>방패에만 있는 항목. 무기별로 고유 수치가 있으면 이런 식으로 행을 켜고 끈다
        /// — 무기 로직을 공통화하지 않는 것과 같은 이유로, 표시도 무기별로 둔다.</summary>
        private void RefreshShieldOnly(IWeapon weapon)
        {
            var shield = weapon as Weapon_WoodShield;
            if (_counterRefundRow != null) _counterRefundRow.SetActive(shield != null);
            if (shield != null) SetText(_counterRefund, shield.CounterRefund.ToString());
        }

        /// <summary>대시 자원. 최대치·소모량·회복 속도가 무기마다 달라 무기 정보와 함께 보여준다
        /// (WeaponStamina는 무기와 같은 GameObject에 붙는다).</summary>
        private void RefreshStamina(IWeapon weapon)
        {
            WeaponStamina stamina = weapon is Component c ? c.GetComponent<WeaponStamina>() : null;
            if (stamina == null)
            {
                SetText(_stamina, "-");
                SetText(_dashCost, "-");
                SetText(_staminaRegen, "-");
                SetText(_attackGain, "-");
                return;   // 방패 전용 행은 RefreshShieldOnly가 따로 판단한다
            }

            SetText(_stamina, _builder.Clear()
                .Append(stamina.Current).Append(" / ").Append(stamina.Max).ToString());
            SetText(_dashCost, stamina.DashCost > 0
                ? stamina.DashCost.ToString()
                : "-");   // 0이면 제한 없이 대시할 수 있다
            SetText(_staminaRegen, _builder.Clear()
                .Append(Mathf.RoundToInt(stamina.RegenPerSecond)).Append("/초").ToString());
            SetText(_attackGain, stamina.GainPerAttack.ToString());
        }

        /// <summary>강화 성공률과 누적 강화 시도 횟수. 둘 다 "다음 강화"에 대한 정보다.</summary>
        private void RefreshReinforce(IWeapon weapon)
        {
            SetText(_reinforceRate, weapon.ReinforceNext == null
                ? "-"   // 최대 단계면 더 올릴 수 없다
                : _builder.Clear().Append(Mathf.RoundToInt(weapon.ReinforceCur.successRate * 100)).Append('%').ToString());

            int attempts = 0;
            if (TryFindEntry(weapon.ID, out var entry))
                attempts = entry.AccumulatedReinforcement;

            SetText(_reinforceAttempts, _builder.Clear().Append(attempts).Append("회").ToString());
        }

        /// <summary>인벤토리에서 이 무기의 도감 엔트리를 찾는다(강화 레벨·숙련도·누적 시도).</summary>
        private static bool TryFindEntry(int itemId, out WeaponEntryData entry)
        {
            if (ServiceLocator.TryGet<PlayerContextData>(out var ctx))
            {
                for (int i = 0; i < ctx.WeaponInventory.Count; i++)
                {
                    if (ctx.WeaponInventory[i].ItemId != itemId) continue;
                    entry = ctx.WeaponInventory[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// 선택 항목이라 배선되지 않았을 수 있다.
        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null) target.text = value;
        }

        /// <summary>강화 배율(1.1·1.2…)이 아니라 실제 공격력을 보여준다.
        /// WeaponBase.GetDamage와 같은 식이되, 공격별 계수와 임시 버프는 뺀 무기 자체의 값이다.</summary>
        private void RefreshDamage(IWeapon weapon)
        {
            if (!ServiceLocator.TryGet<IWeaponDB>(out var weaponDB) || !weaponDB.TryGet(weapon.ID, out var data))
            {
                _weaponDamage.text = "-";
                return;
            }

            int total = Mathf.RoundToInt(data.BaseDamage * weapon.ReinforceCur.Damage);
            int baseAtk = Mathf.RoundToInt(data.BaseDamage);
            _weaponDamage.text = total.ToString();
            SetText(_baseAttack, baseAtk.ToString());
            SetText(_bonusAttack, _builder.Clear().Append('+').Append(total - baseAtk).ToString());
        }

        /// <summary>숙련도는 "3 / 5"가 아니라 백분율로 보여준다.</summary>
        private void RefreshExp(IWeapon weapon)
        {
            int maxExp = weapon.ReinforceCur.maxExp;
            if (maxExp <= 0)
            {
                _weaponExp.text = "-";   // 최대 강화 단계엔 더 쌓을 숙련도가 없다
                return;
            }

            int currentExp = TryFindEntry(weapon.ID, out var entry) ? entry.Exp : 0;

            int percent = Mathf.Clamp(Mathf.RoundToInt(currentExp * 100f / maxExp), 0, 100);
            _weaponExp.text = _builder.Clear().Append(percent).Append('%').ToString();
        }

        /// <summary>최초 오픈 때 세로 배치가 겹치는 걸 막는다.
        ///
        /// RightColumn은 Child Control Height가 꺼져 있어 자식(WeaponInfo·PlayerStats)의 현재 높이를 그대로 읽는데,
        /// 그 높이는 자식 자신의 ContentSizeFitter가 정한다. 한 번의 레이아웃 패스에서는 부모가 자리를 잡은 뒤에야
        /// 자식 Fitter가 높이를 채우므로, 켜진 첫 프레임엔 높이 0 기준으로 쌓여 섹션이 겹친다.
        /// 안쪽 그룹부터 거슬러 올라가며 잡아 줘야 바깥이 확정된 높이를 본다.</summary>
        private void RebuildLayout()
        {
            for (int i = _layoutGroups.Length - 1; i >= 0; i--)
            {
                LayoutGroup group = _layoutGroups[i];
                if (group != null && group.isActiveAndEnabled)
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            }
        }

        // CanvasGroup.DOFade는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다(ChatPanel과 동일).
        private Tween FadeTo(float alpha)
            => DOTween.To(() => _group.alpha, v => _group.alpha = v, alpha, _tweenDuration);

        private void SelectVisuals(int weaponId)
        {
            Toggle(_illusts, weaponId);
            Toggle(_weaponIcons, weaponId);
        }

        private static void Toggle(WeaponVisual[] entries, int weaponId)
        {
            foreach (WeaponVisual entry in entries)
                entry.Root.SetActive(entry.WeaponId == weaponId);
        }


        public void OnManagedOpen(int sortingOrder)
        {
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = sortingOrder;
            }

            _isOpen = true;
            _panel.SetActive(true);
            Refresh();
            RebuildLayout();

            _tween?.Kill();
            _group.alpha = 0f;
            _panelRect.localScale = Vector3.one * _startScale;

            // 게임이 멈춰도 UI는 움직여야 하므로 unscaled time으로 돈다.
            _tween = DOTween.Sequence()
                .Join(FadeTo(1f))
                .Join(_panelRect.DOScale(1f, _tweenDuration).SetEase(Ease.OutBack))
                .SetUpdate(true);
        }

        public void OnManagedClose()
        {
            _isOpen = false;

            _tween?.Kill();
            _tween = DOTween.Sequence()
                .Join(FadeTo(0f))
                .Join(_panelRect.DOScale(_startScale, _tweenDuration).SetEase(Ease.InQuad))
                .SetUpdate(true)
                .OnComplete(() => _panel.SetActive(false));
        }
    }
}
