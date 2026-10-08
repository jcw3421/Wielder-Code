using System;
using System.Globalization;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// 월드스페이스 단발 데미지 숫자 — 팝인 → 카메라 화면 기준으로 떠오름 → 페이드.
    /// 쿼터뷰 전용: 카메라가 회전하지 않으므로 빌보드 회전은 스폰 시 1회만 잡는다.
    /// 수명이 끝나면 파괴하지 않고 _onFinished 로 스폰한 쪽(풀)에 반납한다.
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumber : MonoBehaviour
    {
        [Header("타이밍")]
        [SerializeField] private float _lifetime = 0.8f;
        [SerializeField] private float _popDuration = 0.14f;   // 튀어나오는 시간
        [SerializeField] private float _fadeDuration = 0.3f;   // 마지막에 사라지는 시간

        [Header("움직임 (카메라 화면 기준)")]
        [SerializeField] private float _riseDistance = 1.2f;   // 화면 위쪽으로 올라가는 거리
        [SerializeField] private float _sideDrift = 0.1f;      // 좌우로 흩어지는 최대 거리(영수증이 흐트러지지 않게 작게)
        [SerializeField] private Ease _moveEase = Ease.OutCubic;

        [Header("색 — 머티리얼 본체색(핑크)에 곱해진다. 어두운 색일수록 테두리·글로우만 남는다.")]
        [SerializeField] private Color _normalColor = Color.navyBlue;
        [SerializeField] private Color _skillColor = Color.navyBlue;
        [SerializeField] private Color _ultimateColor = Color.navyBlue;
        [SerializeField] private Color _environmentColor = Color.purple;
        [SerializeField] private Color _selfDamageColor = Color.red;   // 가해자 미상(sourceId -1)

        [Header("스케일")]
        [SerializeField] private float _popScale = 1.35f;      // 팝인 최대 배율

        private Vector3 _baseScale;                            // 프리팹에 저장된 원래 크기
        private TextMeshPro _text;
        private Transform _tr;
        private Sequence _seq;
        private Action<DamageNumber> _onFinished;

        private void Awake()
        {
            _tr = transform;
            _text = GetComponent<TextMeshPro>();
            _baseScale = _tr.localScale;

            // 천 단위 콤마가 줄바꿈 지점으로 잡혀 "2,20 / 0" 처럼 접히는 걸 막는다.
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
        }

        /// worldPos 에 숫자를 띄운다. cam 은 빌보드·이동 방향 기준(쿼터뷰 메인 카메라).
        /// onFinished 는 연출이 끝났을 때 호출된다(풀 반납용, null 이면 자기 파괴).
        public void Play(int amount, DamageType type, int sourceId, Camera cam, Action<DamageNumber> onFinished)
        {
            _onFinished = onFinished;

            _text.text = amount.ToString("N0", CultureInfo.InvariantCulture);
            _text.color = ResolveColor(type, sourceId);
            _text.alpha = 1f;

            // 카메라 화면 축. 카메라가 없으면 월드 축으로 폴백.
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            if (cam != null) _tr.rotation = cam.transform.rotation;

            // 스폰 위치는 프레젠터가 영수증 스택 오프셋까지 계산해서 넣어준다.
            Vector3 start = _tr.position;
            Vector3 end = start
                          + up * _riseDistance
                          + right * UnityEngine.Random.Range(-_sideDrift, _sideDrift);

            _tr.localScale = Vector3.zero;

            _seq?.Kill();
            _seq = DOTween.Sequence();
            // 팝 → 원래 크기로 정착
            _seq.Append(_tr.DOScale(_baseScale * _popScale, _popDuration).SetEase(Ease.OutBack));
            _seq.Append(_tr.DOScale(_baseScale, _popDuration).SetEase(Ease.OutQuad));
            // 이동·페이드는 전체 수명에 걸쳐 병렬로
            _seq.Insert(0f, _tr.DOMove(end, _lifetime).SetEase(_moveEase));
            _seq.Insert(Mathf.Max(0f, _lifetime - _fadeDuration),
                DOTween.To(() => _text.alpha, v => _text.alpha = v, 0f, _fadeDuration).SetEase(Ease.InQuad));
            _seq.OnComplete(Finish);
        }

        private void Finish()
        {
            _seq = null;
            if (_onFinished != null) _onFinished(this);
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _seq?.Kill();
            _seq = null;
        }

        private void OnDisable()
        {
            // 풀 반납·씬 언로드 중 트윈이 살아남지 않도록
            _seq?.Kill();
            _seq = null;
        }

        private Color ResolveColor(DamageType type, int sourceId)
        {
            if (sourceId == -1) return _selfDamageColor;

            switch (type)
            {
                case DamageType.Skill: return _skillColor;
                case DamageType.Ultimate: return _ultimateColor;
                case DamageType.Environment: return _environmentColor;
                default: return _normalColor;
            }
        }
    }
}
