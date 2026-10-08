using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Game/DataBases/Records/Item")]
    public class ItemRecord : DataRecord
    {
        [Header("Sheet Data")]
        [SerializeField] private string _name;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private string _tooltip;

        [Header("Manual")]
        [SerializeField] private GameObject _dropPrefab;
        [SerializeField] private Sprite _icon;

        [Header("Manual - Profile")]
        [TextArea, SerializeField] private string _attackDesc;
        [TextArea, SerializeField] private string _skillDesc;
        [TextArea, SerializeField] private string _ultimateDesc;
        [TextArea, SerializeField] private string _fullChainDesc;

        public GameObject DropPrefab => _dropPrefab;
        public Sprite Icon => _icon;
        public string AttackDesc => _attackDesc;
        public string SkillDesc => _skillDesc;
        public string UltimateDesc => _ultimateDesc;
        public string FullChainDesc => _fullChainDesc;
        public string Name => _name;
        public ItemType ItemType => _itemType;
        public string Tooltip => _tooltip;

        public void ApplySheetData(int id, string name, ItemType itemType, string tooltip)
        {
            SetId(id);
            _name = name;
            _itemType = itemType;
            _tooltip = tooltip;
        }
    }
}
