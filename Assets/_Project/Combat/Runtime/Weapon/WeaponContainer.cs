using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public class WeaponContainer : MonoBehaviour
    {
        Dictionary<int, WeaponBase> _datas;
        public Dictionary<int, WeaponBase> Data { get => _datas; }
        [SerializeField] List<WeaponBase> _weapons;

        private void Awake()
        {
            _datas = new();
            foreach(var w in _weapons)
            {
                _datas.Add(w.ID, w);
            }
        }

        /// <summary>itemId 무기의 해당 강화 단계에서 가득 찬 경험치 값. 무기나 강화표가 없으면 0.
        /// 강화표는 무기가 들고 있으므로 이 표의 주인인 여기서 답한다.</summary>
        public int GetMaxExp(int itemId, int reinforcementLevel)
        {
            if (_datas == null) return 0;
            if (!_datas.TryGetValue(itemId, out var weapon) || weapon == null) return 0;

            var setup = weapon.ReinforcementSetup;
            if (setup == null || setup.entrys == null || setup.entrys.Length == 0) return 0;

            int idx = Mathf.Clamp(reinforcementLevel, 0, setup.entrys.Length - 1);
            return setup.entrys[idx] != null ? setup.entrys[idx].maxExp : 0;
        }
    }
}
