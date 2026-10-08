using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 파티원(=로컬 플레이어를 제외한 다른 플레이어들) 영역 총괄.
    /// 카드 프리팹을 playerId별로 생성/관리하고, Orchestrator가 넘겨준 값을 해당 카드로 라우팅한다.
    /// 이벤트 구독은 하지 않는다 — 그건 <see cref="HudOrchestrator"/> 책임.
    /// </summary>
    public sealed class PartyUiPanel : MonoBehaviour
    {
        [SerializeField] private PartyMemberCard _cardPrefab;
        [SerializeField] private Transform _cardContainer;   // Layout Group 붙은 부모

        private readonly Dictionary<int, PartyMemberCard> _cards = new();

        /// <summary>파티원 입장 → 카드 생성. 이미 있으면 재바인딩만.</summary>
        public void AddMember(int playerId, string displayName = null)
        {
            if (!_cards.TryGetValue(playerId, out PartyMemberCard card))
            {
                card = Instantiate(_cardPrefab, _cardContainer);
                _cards[playerId] = card;
            }
            card.Bind(playerId, displayName ?? $"Player {playerId}");
            card.gameObject.SetActive(true);
        }

        /// <summary>파티원 퇴장 → 카드 제거.</summary>
        public void RemoveMember(int playerId)
        {
            if (!_cards.TryGetValue(playerId, out PartyMemberCard card)) return;
            _cards.Remove(playerId);
            Destroy(card.gameObject);
        }

        public void SetClass(int playerId, ClassType classType)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.SetClass(classType);
        }

        public void UpdateHealth(int playerId, int current, int max)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.SetHp(current, max);
        }

        public void UpdateUltimate(int playerId, int current, int max)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.SetUltimate(current, max);
        }

        public void ApplyBuff(int playerId, BuffInfo info)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.ApplyBuff(info);
        }

        public void RemoveBuff(int playerId, BuffType type)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.RemoveBuff(type);
        }

        public void ApplyDebuff(int playerId, DebuffInfo info)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.ApplyDebuff(info);
        }

        public void RemoveDebuff(int playerId, DebuffType type)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.RemoveDebuff(type);
        }

        public void UpdateNickname(int playerId, string nickname)
        {
            if (_cards.TryGetValue(playerId, out PartyMemberCard card))
                card.UpdateNickname(nickname);
        }

        /// <summary>이 playerId가 파티원으로 표시되고 있는가(라우팅 판단용).</summary>
        public bool Has(int playerId) => _cards.ContainsKey(playerId);
    }
}
