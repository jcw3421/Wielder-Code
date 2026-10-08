using UnityEngine;

namespace Game.UI
{
    /// <summary>NPC 개체마다 머리·옷 색을 다르게 입힌다. 피부(Body)는 건드리지 않는다.
    ///
    /// 원본 머티리얼을 복제해서 이 NPC 전용 사본에 색을 넣는다. 복제하는 이유가 두 가지다.
    ///   1. 원본을 그대로 칠하면 같은 머티리얼을 쓰는 다른 NPC와 플레이어 아바타까지 물든다.
    ///   2. 이 모델은 lilToon 머티리얼이라 MaterialPropertyBlock 이 먹지 않는다
    ///      (머티리얼마다 셰이더가 따로 생성되고 per-instance 프로퍼티를 받지 않는다).
    ///
    /// 파츠별 텍스처가 다르므로 "그 렌더러가 이미 쓰던 머티리얼"을 복제한다. 미리 만들어 둔
    /// .mat 하나를 전부에 덮어씌우면 다른 갑옷 세트에 엉뚱한 텍스처가 입혀진다.
    ///
    /// 대상은 P09 모듈러 휴머노이드의 파츠 이름(Armor_*, Hair_*)으로 고른다.</summary>
    public sealed class NpcAppearance : MonoBehaviour
    {
        [SerializeField] private Color _hairColor = Color.white;
        [SerializeField] private Color _outfitColor = Color.white;

        // lilToon 의 메인 컬러는 _Color, URP/Lit 계열은 _BaseColor 를 쓴다. 있는 쪽만 넣는다.
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// 파츠가 프리팹에서 조립된 뒤에 칠해야 하므로 Awake 가 아니라 Start 에서 돈다.
        private void Start()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!TryResolveTint(renderer, out var tint)) continue;

                var source = renderer.sharedMaterial;
                if (source == null) continue;

                var copy = new Material(source) { name = $"{source.name}_{name}" };
                if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, tint);
                if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, tint);

                renderer.sharedMaterial = copy;
            }
        }

        /// 렌더러가 파츠 오브젝트 자신에 붙어 있기도 하고 그 자식에 붙어 있기도 해서 부모까지 본다.
        private bool TryResolveTint(Renderer renderer, out Color tint)
        {
            var self = renderer.name;
            var parent = renderer.transform.parent;
            var parentName = parent != null ? parent.name : string.Empty;

            if (Matches(self, "Hair") || Matches(parentName, "Hair"))
            {
                tint = _hairColor;
                return true;
            }

            if (Matches(self, "Armor") || Matches(parentName, "Armor"))
            {
                tint = _outfitColor;
                return true;
            }

            tint = default;   // Body(피부)·Weapon 등은 원본 그대로 둔다
            return false;
        }

        private static bool Matches(string value, string prefix)
            => value.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
    }
}
