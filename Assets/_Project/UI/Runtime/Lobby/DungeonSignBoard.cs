using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>포탈 옆에 세우는 던전 이름 표지판. 문구만 인스펙터에서 채우면 라벨은 런타임에 만든다.</summary>
    public sealed class DungeonSignBoard : MonoBehaviour
    {
        [SerializeField] private string _title = "던전";

        [Tooltip("제목 아래에 작게 붙는 한 줄 설명. 비우면 제목만 나온다.")]
        [SerializeField] private string _subtitle;

        [SerializeField] private Color _titleColor = Color.white;
        [SerializeField] private float _height = 3.4f;
        [SerializeField] private TMP_FontAsset _font;

        private void Awake()
        {
            var label = WorldLabel.Create(transform, Vector3.up * _height, 440f, 36f, _font, "SignLabel");
            label.SetText(string.IsNullOrEmpty(_subtitle)
                ? $"<b>{_title}</b>"
                : $"<b>{_title}</b>\n<size=60%>{_subtitle}</size>");
            label.SetColor(_titleColor);
        }
    }
}
