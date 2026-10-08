using System.Collections.Generic;
using Game.Core;
using Game.Networking;
using UnityEngine;

namespace Game.UI
{
    /// DamageDealt 구독 → 월드스페이스 숫자 스폰(표시 전용).
    public class DamageNumberPresenter : MonoBehaviour
    {
        [SerializeField] private DamageNumber _numberPrefab;
        [SerializeField] private int _prewarmCount = 16;
        [Header("배치")][SerializeField] private float _cameraPull = 1.5f;
        [Header("영수증 스택")][SerializeField] private float _stackStep = 0.35f;
        [SerializeField] private float _stackWindow = 0.6f;
        [SerializeField] private int _stackMax = 8;

        private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
        private readonly Dictionary<int, StackState> _stacks = new Dictionary<int, StackState>();
        private readonly List<int> _staleIds = new List<int>();
        private Camera _camera;
        private struct StackState { public int Index; public float LastTime; }

        private void Awake()
        {
            _camera = Camera.main;
            if (_numberPrefab == null) return;
            for (int i = 0; i < _prewarmCount; i++) _pool.Enqueue(CreateInstance());
        }

        private void OnEnable() => GameplayEvents.DamageDealt += HandleDamageDealt;
        private void OnDisable() => GameplayEvents.DamageDealt -= HandleDamageDealt;

        private void HandleDamageDealt(int targetId, int amount, Vector3 worldPos, DamageType type, int sourceId)
        {
            if (_numberPrefab == null || (!IsMine(sourceId) && !IsMine(targetId))) return;
            if (_camera == null) _camera = Camera.main;
            var number = Rent();
            Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
            Vector3 pull = _camera != null ? -_camera.transform.forward * _cameraPull : Vector3.zero;
            number.transform.position = worldPos + pull + up * (_stackStep * NextStackIndex(targetId));
            number.gameObject.SetActive(true);
            number.Play(amount, type, sourceId, _camera, Release);
        }
        private int NextStackIndex(int targetId)
        {
            float now = Time.time;
            int index = 0;
            if (_stacks.TryGetValue(targetId, out var state) && now - state.LastTime <= _stackWindow)
                index = (state.Index + 1) % _stackMax;
            _stacks[targetId] = new StackState { Index = index, LastTime = now };
            if (_stacks.Count > 64) PruneStacks(now);
            return index;
        }
        private void PruneStacks(float now)
        {
            _staleIds.Clear();
            foreach (var pair in _stacks) if (now - pair.Value.LastTime > _stackWindow) _staleIds.Add(pair.Key);
            for (int i = 0; i < _staleIds.Count; i++) _stacks.Remove(_staleIds[i]);
        }
        private DamageNumber Rent()
        {
            while (_pool.Count > 0) { var pooled = _pool.Dequeue(); if (pooled != null) return pooled; }
            return CreateInstance();
        }
        private void Release(DamageNumber number)
        {
            if (number == null) return;
            number.gameObject.SetActive(false);
            _pool.Enqueue(number);
        }
        private DamageNumber CreateInstance()
        {
            var number = Instantiate(_numberPrefab, transform);
            number.gameObject.SetActive(false);
            return number;
        }
        private bool IsMine(int sourceId)
        {
            var runner = SessionContext.Current?.Runner;
            return runner != null && runner.LocalPlayer.PlayerId == sourceId;
        }
    }
}
