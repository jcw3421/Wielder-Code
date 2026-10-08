using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면에서 게임 시작 버튼을 눌렀을 때의 연출을 담당한다.
/// 순서: 캐릭터 기상 애니메이션 → 디졸브(사라짐) 이펙트 → 씬 전환.
///
/// 디졸브 머티리얼 인스턴스는 성능을 위해 두 단계로 나누어 처리한다.
///   1) Awake 시점에 미리 생성 (원본 텍스처 복사까지 완료, 화면엔 아직 영향 없음)
///   2) StartGame 호출 시점에 렌더러에 실제 할당 (가벼운 배열 교체만 발생)
/// 이렇게 분리하면 버튼을 누른 순간 무거운 머티리얼 생성 작업 없이
/// 바로 연출이 시작되어 끊김이 없다.
/// </summary>
public class TitleStartSequence : MonoBehaviour
{
    [Header("캐릭터")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private string standUpTrigger = "StandUp";

    [Header("디졸브 머티리얼")]
    [Tooltip("AllIn13DShader로 Fade 이펙트를 미리 켜놓은 머티리얼을 넣어주세요")]
    [SerializeField] private Material dissolveMaterial;

    [Tooltip("셰이더마다 다를 수 있으니 실제 프로퍼티 이름에 맞게 수정하세요 (URP Lit 기준 _BaseMap/_BaseColor, 구 셰이더는 _MainTex/_Color)")]
    [SerializeField] private string[] texturePropertiesToCopy = { "_MainTex", "_BaseMap" };
    [SerializeField] private string[] colorPropertiesToCopy = { "_Color", "_BaseColor" };

    [Header("디졸브 타이밍")]
    [SerializeField] private float dissolveStartDelay = 1.0f;
    [SerializeField] private float dissolveDuration = 1.5f;

    [Header("씬 전환")]
    [SerializeField] private string nextSceneName = "SampleScene";
    [SerializeField] private float sceneLoadDelay = 1.0f;

    private static readonly int FadeAmountID = Shader.PropertyToID("_FadeAmount");

    /// <summary>렌더러 하나와, 그 렌더러에 넣어줄 디졸브 머티리얼 세트를 함께 묶어둔다.</summary>
    private struct RendererMaterialSet
    {
        public Renderer Renderer;
        public Material[] DissolveMaterials;
    }

    private readonly List<RendererMaterialSet> preparedSets = new List<RendererMaterialSet>();
    private readonly List<Material> allDissolveMaterialInstances = new List<Material>();

    private bool materialsPrepared;
    private bool materialsSwapped;
    private bool sequenceStarted;

    private void Awake()
    {
        PrepareDissolveMaterials();
    }

    private void OnDestroy()
    {
        foreach (Material mat in allDissolveMaterialInstances)
        {
            if (mat != null)
            {
                Destroy(mat);
            }
        }
        allDissolveMaterialInstances.Clear();
    }

    /// <summary>버튼 등 UI에서 호출. 연출을 시작한다. 중복 호출은 무시된다.</summary>
    public void StartGame()
    {
        if (sequenceStarted) return;
        sequenceStarted = true;
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        PlayStandUpAnimation();

        if (dissolveStartDelay > 0f)
        {
            yield return new WaitForSeconds(dissolveStartDelay);
        }

        // 미리 만들어둔 머티리얼을 렌더러에 실제로 꽂아넣는다 (가벼운 작업).
        SwapToDissolveMaterial();

        yield return AnimateDissolve(dissolveDuration);

        if (sceneLoadDelay > 0f)
        {
            yield return new WaitForSeconds(sceneLoadDelay);
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void PlayStandUpAnimation()
    {
        if (characterAnimator != null && !string.IsNullOrEmpty(standUpTrigger))
        {
            characterAnimator.SetTrigger(standUpTrigger);
        }
    }

    private IEnumerator AnimateDissolve(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetDissolveAmount(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetDissolveAmount(1f);
    }

    private void SetDissolveAmount(float amount)
    {
        foreach (Material mat in allDissolveMaterialInstances)
        {
            mat.SetFloat(FadeAmountID, amount);
        }
    }

    /// <summary>
    /// 캐릭터의 모든 렌더러에 대해 디졸브 머티리얼 인스턴스를 미리 만들어둔다.
    /// 원본 텍스처/컬러를 복사해두므로, 아직 렌더러에 할당하지 않은 상태에서는
    /// 화면상 캐릭터 룩이 그대로 유지된다.
    /// </summary>
    private void PrepareDissolveMaterials()
    {
        if (materialsPrepared) return;
        if (characterAnimator == null || dissolveMaterial == null) return;

        foreach (Renderer renderer in characterAnimator.GetComponentsInChildren<Renderer>(true))
        {
            Material[] dissolveMats = CreateDissolveMaterialsFor(renderer.materials);

            preparedSets.Add(new RendererMaterialSet
            {
                Renderer = renderer,
                DissolveMaterials = dissolveMats
            });
        }

        materialsPrepared = true;
    }

    /// <summary>원본 머티리얼 배열에 대응하는 디졸브 머티리얼 배열을 생성한다.</summary>
    private Material[] CreateDissolveMaterialsFor(Material[] originalMaterials)
    {
        var result = new Material[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material instance = new Material(dissolveMaterial);
            CopyOriginalLook(originalMaterials[i], instance);
            instance.SetFloat(FadeAmountID, 0f);

            result[i] = instance;
            allDissolveMaterialInstances.Add(instance);
        }

        return result;
    }

    /// <summary>
    /// 원본 머티리얼의 텍스처/컬러를 디졸브 머티리얼 인스턴스로 복사한다.
    /// 이걸 안 하면 캐릭터가 디졸브 머티리얼 자체의 텍스처로 즉시 바뀌어 보인다.
    /// </summary>
    private void CopyOriginalLook(Material source, Material destination)
    {
        if (source == null) return;

        foreach (string texProp in texturePropertiesToCopy)
        {
            if (source.HasProperty(texProp) && destination.HasProperty(texProp))
            {
                destination.SetTexture(texProp, source.GetTexture(texProp));
            }
        }

        foreach (string colorProp in colorPropertiesToCopy)
        {
            if (source.HasProperty(colorProp) && destination.HasProperty(colorProp))
            {
                destination.SetColor(colorProp, source.GetColor(colorProp));
            }
        }
    }

    /// <summary>
    /// 미리 준비된 디졸브 머티리얼을 실제 렌더러에 할당한다.
    /// 인스턴스는 이미 다 만들어져 있으므로 배열 교체만 일어나 가볍다.
    /// </summary>
    private void SwapToDissolveMaterial()
    {
        if (materialsSwapped) return;

        if (!materialsPrepared)
        {
            // Awake 시점에 준비가 안 됐던 경우의 안전장치 (이 경우엔 스톨 발생 가능)
            PrepareDissolveMaterials();
        }

        foreach (RendererMaterialSet set in preparedSets)
        {
            set.Renderer.materials = set.DissolveMaterials;
        }

        materialsSwapped = true;
    }
}