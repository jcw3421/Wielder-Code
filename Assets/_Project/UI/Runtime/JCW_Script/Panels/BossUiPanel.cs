using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 보스 영역 총괄. 보스가 등장하면 활성화되고 HP·페이즈·버프/디버프를 관리한다.
    /// 값은 <see cref="HudOrchestrator"/>가 밀어준다.
    /// </summary>
    public sealed class BossUiPanel : MonoBehaviour
    {
        [Header("루트")]
        [Tooltip("보스 등장/퇴장 시 페이드할 CanvasGroup. 없으면 GameObject 활성/비활성만 한다.")]
        [SerializeField] private CanvasGroup _root;

        [Header("스탯")]
        [SerializeField] private BossStatusBar _hpBar;
        [SerializeField] private TMP_Text _phaseText;
        [Tooltip("지금은 임시로 bossId를 찍고, 이름 데이터가 오면 이름으로 교체한다.")]
        [SerializeField] private TMP_Text _nameText;

        [Header("버프/디버프")]
        [SerializeField] private StatusEffectDisplay _statusEffects;

        [Header("연출")]
        [SerializeField] private float _fadeDuration = 0.3f;

        public int BossId { get; private set; } = -1;
        public bool IsActive { get; private set; }

        private Tween _fadeTween;

        private void Awake() => SetVisible(false, instant: true);

        /// <summary>보스 등장 — UI를 켠다(OnBossSpawned).</summary>
        public void Show(int bossId)
        {
            BossId = bossId;
            IsActive = true;
            _statusEffects?.Clear();
            BindFromDB(bossId);
            SetVisible(true);
        }

        /// <summary>보스 퇴장 — UI를 끈다(OnBossDespawned·씬 전환 등). HP와 무관하게 끈다.</summary>
        public void Hide()
        {
            BossId = -1;
            IsActive = false;
            _statusEffects?.Clear();
            SetVisible(false);
        }

        /// <summary>보스 HP·페이즈 갱신. maxHp가 0 이하면 퇴장 처리한다.</summary>
        public void SetState(int bossId, int currentHp, int maxHp, int phase)
        {
            if (maxHp <= 0)
            {
                if (IsActive) Hide();
                return;
            }

            if (!IsActive) Show(bossId);

            _hpBar?.SetValue(currentHp, maxHp);
            if (_phaseText != null) _phaseText.text = $"Phase {phase}";
        }

        /// <summary>
        /// 페이즈 변화 없이 HP만 갱신(피격 등). 보스 데미지는 EnemyHealthChanged로 오므로
        /// Orchestrator가 보스 id 매칭 후 이 메서드로 넘겨준다. 활성 상태에서만 반영.
        /// </summary>
        public void SetHealth(int currentHp, int maxHp)
        {
            if (!IsActive) return;
            _hpBar?.SetValue(currentHp, maxHp);
        }

        // 이름·최대체력은 bossId로 DB에서 읽는다(BossDefinition이 쓰는 BossRecord와 같은 출처).
        // HP 이벤트는 "변화"에만 오므로 등장 시점에 여기서 채우지 않으면 직전 대상의 값이 바에 남는다.
        private void BindFromDB(int bossId)
        {
            if (!ServiceLocator.TryGet(out IBossDB bossDB) || !bossDB.TryGet(bossId, out BossRecord boss))
            {
                if (_nameText != null) _nameText.text = $"BOSS #{bossId}";   // DB 미등록·미로드(BootStrap 안 거치고 씬 직접 실행 등)
                return;
            }

            if (_nameText != null) _nameText.text = boss.Name;
            _hpBar?.SetValue(boss.BaseHealth, boss.BaseHealth, animate: false);   // 등장은 풀피
        }

        public void ApplyBuff(BuffInfo info) => _statusEffects?.ApplyBuff(info);
        public void RemoveBuff(BuffType type) => _statusEffects?.RemoveBuff(type);
        public void ApplyDebuff(DebuffInfo info) => _statusEffects?.ApplyDebuff(info);
        public void RemoveDebuff(DebuffType type) => _statusEffects?.RemoveDebuff(type);

        // 시각적 표시만 담당한다. IsActive 상태 플래그는 SetState가 관리한다.
        private void SetVisible(bool visible, bool instant = false)
        {
            if (_root == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            _fadeTween?.Kill();
            _root.interactable = visible;
            _root.blocksRaycasts = visible;

            float target = visible ? 1f : 0f;
            if (instant || _fadeDuration <= 0f || !isActiveAndEnabled)
            {
                _root.alpha = target;
            }
            else
            {
                // CanvasGroup.DOFade는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다.
                _fadeTween = DOTween.To(() => _root.alpha, v => _root.alpha = v, target, _fadeDuration);
            }
        }

        private void OnDestroy() => _fadeTween?.Kill();
    }
}
