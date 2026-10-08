using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 채팅 UI. 입력을 수집해 전송을 "요청"하고, 서버가 전파한 메시지를 표시만 한다.
    /// 전송은 <see cref="IChatService"/>(구현: Game.Networking의 PlayerChat)에 위임하고,
    /// 수신은 <see cref="GameplayEvents.ChatMessageReceived"/>를 구독한다. (UI는 상태를 바꾸지 않는다)
    ///
    /// 입력창은 평소 닫혀 있고 Enter로 연다. 열려 있는 동안은 InputField가 포커스를 쥐므로
    /// <see cref="InputGuardUpdater"/>가 <see cref="InputGuard.SuppressKeyboard"/>를 켜서 이동 키가 먹지 않는다
    /// — 전송(Enter)/취소(Esc) 시 포커스를 놓아 다시 움직일 수 있게 한다.
    ///
    /// 새 메시지가 <see cref="_idleDelay"/>초 동안 없으면 통째로 흐려진다(입력창이 열려 있으면 유지).
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TMP_Text _logText;

        [Tooltip("로그에 유지할 최대 줄 수.")]
        [SerializeField] private int _maxLines = 30;

        [Header("스크롤")]
        [Tooltip("로그를 감싸는 ScrollRect. 비워두면 스크롤 없이 그냥 쌓인다.")]
        [SerializeField] private ScrollRect _scroll;

        [Header("열기/닫기")]
        [Tooltip("Enter로 켜고 끄는 입력창 묶음. 비워두면 InputField 오브젝트를 쓴다.")]
        [SerializeField] private GameObject _inputRoot;

        [Header("유휴 페이드")]
        [Tooltip("패널 전체 알파를 다루는 CanvasGroup. 비워두면 페이드하지 않는다.")]
        [SerializeField] private CanvasGroup _group;

        [Tooltip("마지막 메시지 이후 이 시간(초)이 지나면 흐려진다.")]
        [SerializeField] private float _idleDelay = 6f;

        [Tooltip("흐려졌을 때의 알파. 0이면 완전히 안 보인다.")]
        [Range(0f, 1f)][SerializeField] private float _idleAlpha = 0f;

        [SerializeField] private float _fadeDuration = 0.4f;

        /// <summary>유저가 채팅을 입력·확정했을 때(전송 요청). 추가 훅이 필요할 때만 쓴다.
        /// 네트워크 전송은 이 이벤트와 무관하게 <see cref="IChatService"/>로 나간다.</summary>
        public UnityEvent<string> OnSubmitMessage;

        // verticalNormalizedPosition 0 = 맨 아래. 딱 0으로 떨어지는 일이 드물어 여유를 둔다.
        private const float BottomThreshold = 0.02f;

        // 닉네임 색
        private const string NicknameColor = "#F2D16B";

        private readonly Queue<string> _lines = new();

        private Tween _fadeTween;
        private float _idleTimer;

        // 닫힌 프레임 번호. Enter 전송으로 닫힌 그 프레임에 Update가 같은 Enter를 보고
        // 다시 열어버리는 것을 막는다(TMP의 onSubmit과 이 Update가 같은 프레임에 돈다).
        private int _closedFrame = -1;

        private Coroutine _focusRoutine;

        private bool IsOpen => _inputRoot != null && _inputRoot.activeSelf;

        private void Awake()
        {
            if (_inputField != null)
                _inputField.onSubmit.AddListener(HandleSubmit);

            if (_inputRoot == null && _inputField != null)
                _inputRoot = _inputField.gameObject;

            GameplayEvents.ChatMessageReceived += HandleChatReceived;
        }

        private void Start()
        {
            Close();                 // 평소엔 닫혀 있다
            _idleTimer = _idleDelay; // 시작하자마자 흐린 상태
            if (_group != null) _group.alpha = _idleAlpha;
            ApplyRaycastBlocking();
        }

        private void OnDestroy()
        {
            GameplayEvents.ChatMessageReceived -= HandleChatReceived;
            _fadeTween?.Kill();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!IsOpen)
            {
                if (Time.frameCount != _closedFrame &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                    Open();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();             // 취소 — 보내지 않고 닫는다
            }

            if (IsOpen) return;      // 입력 중엔 흐려지지 않는다

            if (_idleTimer < _idleDelay)
            {
                _idleTimer += Time.unscaledDeltaTime;
                if (_idleTimer >= _idleDelay)
                    FadeTo(_idleAlpha);
            }
        }

        private void Open()
        {
            if (_inputRoot == null || _inputField == null) return;

            _inputRoot.SetActive(true);
            _inputField.text = string.Empty;
            ShowNow();
            ScrollToBottom();                   // 열면 항상 최신 대화부터 보인다

            // 포커스는 다음 프레임에 준다. 지금 주면 창을 연 그 Enter를 UI 모듈이 곧이어
            // "Submit"으로 InputField에 넘겨 TMP가 포커스를 도로 놓아버린다(= 클릭해야만 입력됨).
            if (_focusRoutine != null) StopCoroutine(_focusRoutine);
            _focusRoutine = StartCoroutine(FocusNextFrame());
        }

        private IEnumerator FocusNextFrame()
        {
            yield return null;
            _focusRoutine = null;

            if (!IsOpen || _inputField == null) yield break;

            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(_inputField.gameObject);

            _inputField.ActivateInputField();   // 포커스 → InputGuardUpdater가 이동 키를 막는다
            _inputField.caretPosition = 0;
        }

        private void Close()
        {
            _closedFrame = Time.frameCount;

            if (_inputField != null)
            {
                _inputField.text = string.Empty;
                _inputField.DeactivateInputField();
            }
            if (_inputRoot != null)
                _inputRoot.SetActive(false);

            // 포커스를 확실히 놓아야 InputGuard가 풀려 다시 움직일 수 있다.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && _inputField != null && es.currentSelectedGameObject == _inputField.gameObject)
                es.SetSelectedGameObject(null);

            ApplyRaycastBlocking();
        }

        // 서버가 전파한 메시지(내가 보낸 것도 서버를 거쳐 여기로 돌아온다).
        private void HandleChatReceived(int playerId, string nickname, string message)
            => AppendMessage($"<color={NicknameColor}>{nickname}</color>: {message}");

        private void HandleSubmit(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (ServiceLocator.TryGet<IChatService>(out var chat))
                    chat.SendChat(text);
                else
                    AppendMessage("<color=#888888>[채팅 서버 연결 안 됨]</color>");  // 세션 밖(단독 실행) 등

                OnSubmitMessage?.Invoke(text);
            }

            Close();   // 보내면 닫는다 — 포커스를 놓아야 다시 움직일 수 있다
        }

        /// <summary>수신 메시지를 로그에 추가(네트워킹 계층에서 호출).</summary>
        public void AppendMessage(string message)
        {
            if (_logText == null || string.IsNullOrEmpty(message)) return;

            // 유저가 지난 로그를 보려고 위로 올려둔 상태면 자동 스크롤을 하지 않는다.
            bool wasAtBottom = IsScrolledToBottom();

            _lines.Enqueue(message);
            while (_lines.Count > _maxLines)
                _lines.Dequeue();

            _logText.text = string.Join("\n", _lines);
            ShowNow();   // 새 메시지 → 다시 또렷하게, 유휴 타이머 리셋

            if (wasAtBottom)
                ScrollToBottom();
        }

        // ScrollRect가 없으면 "항상 맨 아래"로 친다(자동 스크롤 조건을 통과시켜도 무해).
        private bool IsScrolledToBottom()
        {
            if (_scroll == null) return true;
            if (_scroll.content == null) return true;

            // 내용이 뷰포트보다 짧으면 스크롤 자체가 없으므로 맨 아래로 본다.
            var viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            if (_scroll.content.rect.height <= viewport.rect.height) return true;

            return _scroll.verticalNormalizedPosition <= BottomThreshold;
        }

        private void ScrollToBottom()
        {
            if (_scroll == null) return;

            // 방금 바꾼 텍스트로 레이아웃이 다시 잡힌 뒤라야 위치가 맞는다.
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0f;
            _scroll.StopMovement();   // 관성으로 다시 밀려 올라가지 않게
        }

        // 즉시 또렷하게 만들고 유휴 타이머를 되감는다.
        private void ShowNow()
        {
            _idleTimer = 0f;
            FadeTo(1f);
        }

        private void FadeTo(float alpha)
        {
            if (_group == null) return;

            _fadeTween?.Kill();

            if (_fadeDuration <= 0f || !isActiveAndEnabled)
            {
                _group.alpha = alpha;
                ApplyRaycastBlocking();
                return;
            }

            // CanvasGroup.DOFade는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다(BossUiPanel과 동일).
            _fadeTween = DOTween.To(() => _group.alpha, v => _group.alpha = v, alpha, _fadeDuration)
                                .SetUpdate(true)               // 일시정지 중에도 동작
                                .OnComplete(ApplyRaycastBlocking);
            ApplyRaycastBlocking();
        }

        // 흐려져 있고 닫혀 있을 땐 클릭을 통과시킨다(안 보이는 패널이 마우스를 먹지 않게).
        private void ApplyRaycastBlocking()
        {
            if (_group == null) return;
            _group.blocksRaycasts = IsOpen || _group.alpha > 0.05f;
            _group.interactable = _group.blocksRaycasts;
        }
    }
}
