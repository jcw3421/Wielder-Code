using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.BossContent.Editor
{
    /// TFW 몸에 상시로 붙는 검붉은 오라를 깔아준다.
    ///
    /// 원본 렌더러의 머티리얼 배열은 건드리지 않고, 같은 메시·본을 공유하는 오라 전용
    /// SkinnedMeshRenderer를 자식으로 하나 만든다. 이유:
    ///  - 머티리얼을 하나 덧붙이는 방식은 서브메시가 여러 개일 때 "마지막 서브메시"만 두 번 그린다.
    ///    즉 갑옷만 빛나고 몸통은 안 빛나는 식이 된다.
    ///  - 전용 오브젝트면 껐다 켜기·강도 조절이 쉽고, 지우고 싶으면 자식만 지우면 된다.
    ///
    /// 여러 번 실행해도 안전하다 — 이미 있으면 머티리얼과 설정만 갱신한다.
    public static class TheFirstWielderBodyAuraSetup
    {
        private const string BossPrefabPath =
            "Assets/_Project/BossContent/03. Prefabs/Boss/Boss_TheFirstWielder.prefab";
        private const string ShaderPath =
            "Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_BodyAura.shadergraph";
        private const string MaterialPath =
            "Assets/_Project/BossContent/06. Effects/TFW/Generated/TFW_M_BodyAura.mat";
        private const string OuterShaderPath =
            "Assets/_Project/BossContent/06. Effects/TFW/Shaders/TFW_OuterAuraParticle.shader";
        private const string OuterMaterialPath =
            "Assets/_Project/BossContent/06. Effects/TFW/Generated/TFW_M_OuterAuraSmoke.mat";
        private const string SparkMaterialPath =
            "Assets/_Project/BossContent/06. Effects/TFW/Generated/TFW_M_Spark.mat";

        private const string AuraObjectName = BossAuraVelocity.AuraObjectName;
        private const string OuterAuraName = "FX_TFW_OuterAura";
        private const string GreatswordName = "Sword1_1_1";
        private const string SwordName = "Sword1_3_3";

        [MenuItem("Tools/Boss/Setup TheFirstWielder Body Aura")]
        public static void Setup()
        {
            var material = EnsureMaterial();
            if (material == null) return;

            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[TFW Aura] 보스 프리팹을 열 수 없음: {BossPrefabPath}");
                return;
            }

            try
            {
                // 이전 실행에서 만든 복제본을 먼저 지운다. 같은 부모 아래 원본 렌더러가
                // 여럿이면 parent.Find 방식이 하나의 오라를 계속 재사용해 목록이 중복된다.
                foreach (var oldAura in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (oldAura.gameObject.name == AuraObjectName)
                        Object.DestroyImmediate(oldAura.gameObject);
                }

                var auras = new List<Renderer>();
                foreach (var source in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (source.gameObject.name == AuraObjectName) continue;   // 이전에 만든 오라는 원본이 아니다
                    if (IsWeaponRenderer(source.transform)) continue;       // 무기는 전용 셸/트레일이 담당한다
                    if (source.sharedMesh == null) continue;

                    auras.Add(BuildAura(source, material));
                }

                if (auras.Count == 0)
                {
                    Debug.LogError("[TFW Aura] SkinnedMeshRenderer를 찾지 못했습니다 — 프리팹 구조를 확인하세요.");
                    return;
                }

                BindVelocityDriver(root, auras);
                BuildOuterAura(root);

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
                Debug.Log($"[TFW Aura] 오라 렌더러 {auras.Count}개 생성·갱신 완료. " +
                          $"세기 조절은 {MaterialPath}의 Glow / Rim Width로 합니다.", material);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// 원본과 같은 메시·본을 쓰는 오라 렌더러를 원본의 자식으로 만든다.
        /// 본 배열을 그대로 물려받으므로 애니메이션을 따로 먹일 필요가 없다 — 원본과 같이 움직인다.
        private static SkinnedMeshRenderer BuildAura(SkinnedMeshRenderer source, Material material)
        {
            var go = new GameObject(AuraObjectName);
            go.transform.SetParent(source.transform, false);
            go.transform.SetAsLastSibling();

            var aura = go.GetComponent<SkinnedMeshRenderer>();
            if (aura == null) aura = go.AddComponent<SkinnedMeshRenderer>();

            aura.sharedMesh = source.sharedMesh;
            aura.bones = source.bones;
            aura.rootBone = source.rootBone;
            aura.quality = source.quality;
            aura.localBounds = source.localBounds;

            // 서브메시마다 같은 오라 머티리얼을 물린다 — 몸 전체가 감싸져야 하니까.
            var materials = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            aura.sharedMaterials = materials;

            // 오라는 빛이지 물체가 아니다. 그림자를 만들거나 받지 않고, 광원 계산도 타지 않는다.
            aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura.receiveShadows = false;
            aura.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            aura.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            aura.allowOcclusionWhenDynamic = false;

            // 껍질이 법선으로 부풀고 진행 반대쪽으로 끌린다. 고정 바운즈로는 화면
            // 가장자리에서 통째로 사라지므로 매 프레임 다시 재는 쪽을 택한다.
            aura.updateWhenOffscreen = true;

            return aura;
        }

        private static bool IsWeaponRenderer(Transform source)
        {
            for (var current = source; current != null; current = current.parent)
                if (current.name == GreatswordName || current.name == SwordName) return true;
            return false;
        }

        /// 보스 루트에 속도 공급자를 붙이고, 방금 만든 오라 렌더러들을 물려준다.
        /// 이게 없으면 셰이더의 Velocity가 0이라 꼬리가 나오지 않는다.
        private static void BindVelocityDriver(GameObject root, List<Renderer> auras)
        {
            var driver = root.GetComponent<BossAuraVelocity>();
            if (driver == null) driver = root.AddComponent<BossAuraVelocity>();

            var so = new SerializedObject(driver);
            var list = so.FindProperty("_auraRenderers");
            list.arraySize = auras.Count;
            for (int i = 0; i < auras.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = auras[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// 몸에서 떨어져 크게 피어오르는 바깥 연무와 불씨. 월드 공간 입자라 보스가
        /// 이동하면 방금 지나온 자리에 잠깐 남아 셸 오라보다 긴 잔상을 만든다.
        private static void BuildOuterAura(GameObject root)
        {
            var old = root.transform.Find(OuterAuraName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var container = new GameObject(OuterAuraName);
            container.transform.SetParent(root.transform, false);

            var dust = EnsureOuterAuraMaterial();
            var spark = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
            if (dust == null || spark == null)
            {
                Debug.LogWarning("[TFW Aura] 외곽 오라용 Dust/Spark 머티리얼을 찾지 못했습니다.");
                return;
            }

            ConfigureSmoke(CreateParticleLayer(container.transform, "Smoke", new Vector3(0f, 1.75f, -0.25f)), dust);
            ConfigureEmbers(CreateParticleLayer(container.transform, "Embers", new Vector3(0f, 1.35f, -0.15f)), spark);
        }

        private static Material EnsureOuterAuraMaterial()
        {
            AssetDatabase.ImportAsset(OuterShaderPath, ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(OuterShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[TFW Aura] 외곽 연무 셰이더를 임포트할 수 없습니다: {OuterShaderPath}");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(OuterMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "TFW_M_OuterAuraSmoke" };
                AssetDatabase.CreateAsset(material, OuterMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BlackColor", new Color(0.0015f, 0f, 0.0005f, 1f));
            material.SetColor("_RedColor", new Color(1.35f, 0.009f, 0.0025f, 1f));
            material.SetFloat("_Density", 0.48f);
            material.SetFloat("_Erosion", 0.53f);
            material.SetFloat("_NoiseScale", 3.2f);
            material.SetFloat("_FlowSpeed", 0.38f);
            material.SetFloat("_RedAmount", 0.14f);
            material.renderQueue = 3020;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static ParticleSystem CreateParticleLayer(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.AddComponent<ParticleSystem>();
        }

        private static void ConfigureSmoke(ParticleSystem ps, Material material)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 18;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.72f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.18f),
                new Color(1f, 1f, 1f, 0.38f));

            var emission = ps.emission;
            emission.rateOverTime = 4.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.05f, 1.45f, 0.48f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.05f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(0.18f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0.08f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0.18f);
            noise.frequency = 0.32f;
            noise.scrollSpeed = 0.16f;
            noise.damping = true;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(0.28f, 1f), new Keyframe(1f, 1.18f)));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.12f, 0f, 0.005f), 0f),
                        new GradientColorKey(new Color(0.8f, 0.008f, 0.006f), 0.42f),
                        new GradientColorKey(new Color(0.015f, 0f, 0.002f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.68f, 0.18f),
                        new GradientAlphaKey(0.36f, 0.72f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -1f;
        }

        private static void ConfigureEmbers(ParticleSystem ps, Material material)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 12;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.8f, 0.018f, 0.008f, 0.9f), new Color(0.7f, 0.001f, 0f, 0.65f));

            var emission = ps.emission;
            emission.rateOverTime = 2.8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 1.45f, 0.65f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.7f, 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.1f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.01f, 0f), 0f),
                        new GradientColorKey(new Color(1.35f, 0.025f, 0.008f), 0.35f),
                        new GradientColorKey(new Color(0.3f, 0f, 0f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                        new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 0.9f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material EnsureMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[TFW Aura] 셰이더를 찾을 수 없음: {ShaderPath} " +
                               "(그래프가 임포트 실패했는지 콘솔을 확인하세요)");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // 그래프의 기본값이 곧 룩이다. 여기서는 렌더 큐만 못박는다 —
            // 불투명 뒤에 그려야 몸을 덮지 않고 감싼다.
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
