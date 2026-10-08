using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 타이틀 화면 전용 연출. 게임 시작 버튼을 누르면 검 뽑는 애니메이션을 재생하고,
/// 애니메이션 중 손이 검 손잡이에 닿는 타이밍(handAttachDelay)에 맞춰 검 메시의 부모를
/// 등 소켓 → 손 소켓으로 갈아끼운다. Combat 모듈의 네트워크 무기 시스템과는 무관한,
/// 순수 로컬(비-네트워크) 연출용 스크립트다.
/// </summary>
public class SwordDrawSequence : MonoBehaviour
{
    [Header("캐릭터")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private string drawTrigger = "DrawSword";

    [Header("검 메시 & 소켓")]
    [Tooltip("실제로 부모를 갈아끼울 검 메시(또는 그 루트) Transform.")]
    [SerializeField] private Transform swordTransform;

    [Tooltip("평소 검이 꽂혀있는 등 소켓(뼈대 자식으로 미리 배치).")]
    [SerializeField] private Transform backSocket;

    [Tooltip("쥐었을 때 검이 붙어야 할 손 소켓(뼈대 자식으로 미리 배치).")]
    [SerializeField] private Transform handSocket;

    [Header("타이밍")]
    [Tooltip("DrawSword 애니메이션 재생 후, 손이 검에 닿는 시점(초). 애니메이션 재생해보며 눈으로 맞춘다.")]
    [SerializeField] private float handAttachDelay = 0.4f;

    [Header("VFX")]
    [Tooltip("타이틀 씬 진입 시 재생할 Visual Effect (VFX Graph).")]
    [SerializeField] private VisualEffect titleVfx;

    [Tooltip("VFX Graph 안에서 정의한 이벤트 이름. VFX Graph 에디터의 이벤트 노드 이름과 정확히 일치해야 함 (대소문자 포함).")]
    [SerializeField] private string vfxPlayEventName = "OnCreate";

    private Coroutine _sequenceRoutine;

    private void Awake()
    {
        // 시작 상태 = 검은 등에.
        AttachSwordTo(backSocket);
    }

    private void Start()
    {
        // VisualEffect 컴포넌트가 OnEnable에서 초기화를 마친 뒤(Start 시점)에
        // 이벤트를 보내야 확실히 반응한다. Awake에서 바로 SendEvent하면
        // 초기화 타이밍 문제로 무시될 수 있다.
        PlayTitleVfx();
    }

    private void PlayTitleVfx()
    {
        if (titleVfx == null)
        {
            Debug.LogWarning($"{nameof(SwordDrawSequence)}: titleVfx가 할당되지 않았습니다.", this);
            return;
        }

        if (string.IsNullOrEmpty(vfxPlayEventName))
        {
            Debug.LogWarning($"{nameof(SwordDrawSequence)}: vfxPlayEventName이 비어있습니다.", this);
            return;
        }

        titleVfx.SendEvent(vfxPlayEventName);
    }

    /// <summary>게임 시작 버튼 OnClick에 연결. 중복 호출은 무시한다.</summary>
    public void PlayDraw()
    {
        if (_sequenceRoutine != null) return;
        _sequenceRoutine = StartCoroutine(DrawSequence());
    }

    private IEnumerator DrawSequence()
    {
        if (characterAnimator != null && !string.IsNullOrEmpty(drawTrigger))
            characterAnimator.SetTrigger(drawTrigger);

        if (handAttachDelay > 0f)
            yield return new WaitForSeconds(handAttachDelay);

        AttachSwordTo(handSocket);
    }

    private void AttachSwordTo(Transform socket)
    {
        if (swordTransform == null || socket == null) return;
        swordTransform.SetParent(socket, false);
    }
}