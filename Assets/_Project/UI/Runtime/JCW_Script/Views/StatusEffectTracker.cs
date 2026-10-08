using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 상태이상 아이콘들을 정렬해 보여주는 컨테이너(Layout Group 권장).
    /// 효과 종류를 int 키로만 다뤄 버프·디버프 어느 쪽이든 그대로 재사용한다. 아이콘은 풀링한다.
    /// </summary>
    public sealed class StatusEffectTracker : MonoBehaviour
    {
        [SerializeField] private StatusEffectIcon _iconPrefab;
        [SerializeField] private Transform _container;   // 보통 자기 자신(Layout Group)

        private readonly Dictionary<int, StatusEffectIcon> _active = new();
        private readonly Stack<StatusEffectIcon> _pool = new();

        private void Awake()
        {
            if (_container == null) _container = transform;
        }

        /// <summary>효과 적용/갱신. 같은 키면 기존 아이콘을 갱신한다.</summary>
        public void Show(int key, Sprite icon, float durationSeconds)
        {
            if (!_active.TryGetValue(key, out StatusEffectIcon view))
            {
                view = Rent();
                _active[key] = view;
            }
            view.Bind(icon, durationSeconds);
        }

        /// <summary>효과 해제. 해당 아이콘을 풀로 반환한다.</summary>
        public void Hide(int key)
        {
            if (!_active.TryGetValue(key, out StatusEffectIcon view)) return;
            _active.Remove(key);
            Recycle(view);
        }

        public void Clear()
        {
            foreach (StatusEffectIcon view in _active.Values)
                Recycle(view);
            _active.Clear();
        }

        private StatusEffectIcon Rent()
        {
            StatusEffectIcon view = _pool.Count > 0 ? _pool.Pop() : Instantiate(_iconPrefab, _container);
            view.transform.SetAsLastSibling();
            view.gameObject.SetActive(true);
            return view;
        }

        private void Recycle(StatusEffectIcon view)
        {
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }
    }
}
