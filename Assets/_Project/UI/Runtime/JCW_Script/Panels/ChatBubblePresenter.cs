using System.Collections.Generic;
using System.Linq;
using Fusion;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>채팅 메시지를 보낸 플레이어 머리 위에 <see cref="ChatBubble"/>을 띄운다.
    ///
    /// 채팅 로그와 똑같이 <see cref="GameplayEvents.ChatMessageReceived"/>만 본다.
    /// 즉 서버가 검증·전파한 메시지만 뜨므로, 클라이언트가 남의 머리 위에 임의로 띄울 수 없다.
    ///
    /// 높이는 닉네임(<see cref="PlayerNameplateLayer"/>가 아바타 기준 2.2m 지점을 스크린에 투영한다)
    /// 보다 위에 오도록 잡는다.
    ///
    /// HUD(_GameHUD)에 붙어 DontDestroyOnLoad로 씬을 넘어간다. 말풍선은 아바타의 자식이라
    /// 아바타가 사라지면 함께 정리된다.</summary>
    public sealed class ChatBubblePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _font;

        [Tooltip("말풍선이 떠 있는 시간(초).")]
        [SerializeField] private float _duration = 4f;

        [Tooltip("아바타 기준 말풍선 꼬리 끝 높이. 닉네임(2.2m)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float _height = 2.9f;

        [Tooltip("줄바꿈이 시작되는 최대 폭(px, 1px = 1cm). 짧은 말은 이보다 작게 나온다.")]
        [SerializeField] private float _maxWidth = 260f;

        [SerializeField] private float _fontSize = 26f;
        [SerializeField] private Color _backgroundColor = Color.white;
        [SerializeField] private Color _textColor = Color.black;
        [SerializeField] private float _fadeDuration = 0.25f;

        private readonly Dictionary<int, ChatBubble> _bubbles = new();
        private readonly Dictionary<int, float> _hideAt = new();

        /// Update 에서 딕셔너리를 순회하며 지우기 위한 재사용 버퍼(매 프레임 할당 방지).
        private readonly List<int> _expired = new();

        private void OnEnable() => GameplayEvents.ChatMessageReceived += OnChatReceived;
        private void OnDisable() => GameplayEvents.ChatMessageReceived -= OnChatReceived;

        private void OnChatReceived(int playerId, string nickname, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var avatar = FindAvatar(playerId);
            if (avatar == null) return;   // 아직 스폰 전이거나 이미 나간 플레이어

            if (!_bubbles.TryGetValue(playerId, out var bubble) || bubble == null)
            {
                bubble = ChatBubble.Create(avatar, _height, _maxWidth, _fontSize,
                                           _font, _backgroundColor, _textColor);
                _bubbles[playerId] = bubble;
            }
            else if (bubble.transform.parent != avatar)
            {
                bubble.Reattach(avatar, _height);   // 아바타가 다시 스폰된 경우
            }

            bubble.Show(message);
            _hideAt[playerId] = Time.time + _duration;
        }

        private void Update()
        {
            if (_hideAt.Count == 0) return;

            _expired.Clear();
            foreach (var pair in _hideAt)
                if (Time.time >= pair.Value)
                    _expired.Add(pair.Key);

            foreach (var playerId in _expired)
            {
                _hideAt.Remove(playerId);
                if (_bubbles.TryGetValue(playerId, out var bubble) && bubble != null)
                    bubble.Hide(_fadeDuration);
            }
        }

        private static Transform FindAvatar(int playerId)
        {
            var runner = NetworkRunner.Instances.FirstOrDefault();
            if (runner == null || !runner.IsRunning) return null;

            foreach (var player in runner.ActivePlayers)
                if (player.PlayerId == playerId &&
                    runner.TryGetPlayerObject(player, out var avatar) && avatar != null)
                    return avatar.transform;

            return null;
        }
    }
}
