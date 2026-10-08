using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.BossContent.Editor
{
    /// TFW 등장 연출을 짜서 보스 프리팹에 배선한다. (Tools/Boss/Setup TheFirstWielder Entrance)
    ///
    /// 왜 별도 메뉴인가: TheFirstWielderEffectSetup은 "패턴이 쓰는 이펙트"를 만든다.
    /// 등장은 한 번만 재생되는 컷신이고, 크기의 근거도 판정 반경이 아니라 **보스 몸 자체**라
    /// 재는 값도 만드는 물건도 다르다. 한 파일에 섞으면 둘 중 하나를 손볼 때마다 다른 쪽이 흔들린다.
    ///
    /// 여기서도 크기를 상수로 박지 않는다. 카메라 자리·무기 고리 반경·바닥 링 지름이 전부
    /// 프리팹에서 잰 보스 치수의 배수다 — 모델이 커지거나 작아져도 그림의 비율이 유지된다.
    ///
    /// 실측(현재 프리팹, 루트 스케일 1.5):
    ///   투구 꼭대기 4.88m · 투구 중심 4.39m · 가슴 3.59m · 폭 5.39m · 무기 포함 6.90m
    public static class TheFirstWielderEntranceSetup
    {
        private const string BossPrefabPath = "Assets/_Project/BossContent/03. Prefabs/Boss/Boss_TheFirstWielder.prefab";
        private const string OutFolder      = "Assets/_Project/BossContent/06. Effects/TFW/Entrance";
        private const string FxFolder       = "Assets/_Project/BossContent/06. Effects/TFW/Generated";

        /// 빌려 오는 무기 창고. 보스 자신의 무기가 3종(대검·검·방패)뿐이라 고리가 단조롭다.
        /// 이 FBX 하나에 검 5·방패 5·지팡이 4·활 4가 들어 있다.
        private const string BorrowedWeaponFbx =
            "Assets/Packages/P09_Modular_Humanoid/Model_DATA/FBX/Weapon/P09_Weapon.fbx";

        /// 이름으로 무기를 고르지 않는다 — 메시를 가진 렌더러면 전부 무기로 본다.
        ///
        /// 처음엔 "P09_Weapon_"으로 시작하는 것만 골랐는데 하나도 안 잡혔다.
        /// 그 이름들은 **머티리얼** 이름이고, 실제 노드 이름은 Sword_001·Shield_001처럼 붙어 있다.
        /// 묶음 노드(Sword·Bow)와 활 본(Bow_Root·String)은 렌더러가 없어 저절로 걸러진다.
        ///
        /// 화살만 뺀다 — 너무 가늘어서 길이를 맞춰도 작대기로만 보인다.
        private const string BorrowedWeaponExclude = "Arrow";

        private const string RigName    = "EntranceCinematicRig";
        private const string CameraName = "EntranceCamera";
        private const string FlashName  = "EntranceFlash";
        private const string GroundName = "FX_TFW_EntranceGround";

        /// 등장의 박자. TfwBossEntrance의 기본값과 같은 숫자다 — 카메라 키가 이 시각들 위에 얹힌다.
        private const float ShadowSeconds = 1.2f;
        private const float HoverSeconds  = 1.5f;
        private const float DiveSeconds   = 0.45f;
        private const float ImpactSeconds = 1.05f;
        private const float RoarSeconds   = 1.2f;
        private const float BlendOut      = 0.55f;

        /// 검붉은 팔레트 — 패턴 이펙트(TheFirstWielderEffectSetup)와 같은 계열을 쓴다.
        /// 1을 넘겨야 블룸이 물린다.
        private static readonly Color RiteColor  = new(3.4f, 0.22f, 0.10f, 1f);
        private static readonly Color ShardColor = new(1.8f, 0.30f, 0.16f, 1f);

        /// 잰 보스 치수. 모든 배치가 여기서 나온다.
        private class BossSize
        {
            public float BodyHeight;      // 발바닥 → 투구 꼭대기
            public float HelmetHeight;    // 투구 중심 — 클로즈업이 조준할 점
            public float ChestHeight;     // 전신을 담을 때 조준할 점
            public float BodyWidth;       // 좌우 폭 — 무기 고리가 이보다 밖에 있어야 한다

            public override string ToString()
                => $"몸 {BodyHeight:F2}m / 투구 {HelmetHeight:F2}m / 가슴 {ChestHeight:F2}m / 폭 {BodyWidth:F2}m";
        }

        [MenuItem("Tools/Boss/Setup TheFirstWielder Entrance")]
        public static void Run()
        {
            var size = Measure();
            if (size == null) return;

            EnsureFolder(OutFolder);

            var ground = BuildGroundRite(size);
            var flashMaterial = MakeFlashMaterial();
            var profile = MakeVolumeProfile();

            if (!Wire(size, ground, flashMaterial, profile)) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TFW 등장] 배선 완료 — {size}\n" +
                      $"컷신 길이 {Total:F2}초 (정적 {ShadowSeconds} / 강하 {HoverSeconds} / " +
                      $"내리꽂기 {DiveSeconds} / 착지 {ImpactSeconds} / 포효 {RoarSeconds})");
        }

        private static float Total => ShadowSeconds + HoverSeconds + DiveSeconds + ImpactSeconds + RoarSeconds;

        // ══════════ 1. 실측 ══════════

        /// 콜라이더가 아니라 메시로 잰다 — 콜라이더는 판정용으로 부풀린 값이라 그림의 근거가 못 된다.
        ///
        /// 무기는 일부러 빼고 잰다. 등에 멘 대검까지 합치면 6.9m가 나오는데,
        /// 카메라가 담아야 하는 건 사람 형상이지 칼끝이 아니다. 칼끝을 기준으로 프레임을 잡으면
        /// 정작 보스가 화면 아래쪽에 작게 눌린다.
        private static BossSize Measure()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[TFW 등장] 보스 프리팹을 열지 못했습니다: {BossPrefabPath}");
                return null;
            }

            try
            {
                // 페이즈에 따라 꺼져 있는 파츠가 있다. 재기 전에 전부 켠다.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true);

                bool hasBody = false, hasHelmet = false;
                Bounds body = default, helmet = default;

                // 스킨드 메시만 본다 — 무기는 별도 프리팹의 MeshRenderer라 자연히 걸러진다.
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;

                    string n = smr.gameObject.name;
                    if (n.StartsWith("Helmet") || n.StartsWith("Head"))
                    {
                        if (!hasHelmet) { helmet = smr.bounds; hasHelmet = true; }
                        else helmet.Encapsulate(smr.bounds);
                    }

                    if (!hasBody) { body = smr.bounds; hasBody = true; }
                    else body.Encapsulate(smr.bounds);
                }

                if (!hasBody)
                {
                    Debug.LogError("[TFW 등장] 스킨드 메시를 찾지 못해 치수를 잴 수 없습니다.");
                    return null;
                }

                // 몸 높이는 투구 꼭대기까지다. 스킨드 메시 전체를 쓰면 등에 붙은 장식까지 딸려 온다.
                float bodyHeight = hasHelmet ? helmet.max.y : body.max.y * 0.71f;

                return new BossSize
                {
                    BodyHeight   = bodyHeight,
                    HelmetHeight = hasHelmet ? helmet.center.y : bodyHeight * 0.90f,
                    ChestHeight  = bodyHeight * 0.735f,
                    BodyWidth    = body.size.x,
                };
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ══════════ 2. 결정화 — 보스가 오기 전에 바닥이 먼저 반응한다 ══════════

        /// 정적 구간(1.2초)에 깔리는 바닥 연출.
        ///
        /// 이 박자가 비어 있던 게 등장 연출의 가장 큰 구멍이었다 — 이름은 "그림자"인데
        /// 화면에는 1.2초 동안 아무 일도 일어나지 않았다. 보스를 보여주기 전에
        /// **공간이 먼저 보스의 것이 되는** 게 큰 보스 등장의 문법이다.
        ///
        /// 파편이 아래로 무너지지 않고 위로 솟는 게 핵심이다. 무너지면 붕괴로 읽히고,
        /// 솟으면 힘이 모이는 것으로 읽힌다. 중력을 음수로 줘서 뒤집는다.
        private static GameObject BuildGroundRite(BossSize s)
        {
            var field = AssetDatabase.LoadAssetAtPath<Material>($"{FxFolder}/TFW_M_Field.mat");
            var crack = AssetDatabase.LoadAssetAtPath<Material>($"{FxFolder}/TFW_M_Crack.mat");
            var debris = AssetDatabase.LoadAssetAtPath<Material>($"{FxFolder}/TFW_M_Debris.mat");
            var dust = AssetDatabase.LoadAssetAtPath<Material>($"{FxFolder}/TFW_M_Dust.mat");

            var root = new GameObject(GroundName);

            // 바깥으로 퍼지는 링. 보스 키의 5배까지 밀어야 "아레나를 먹었다"로 읽힌다.
            float ringDiameter = s.BodyHeight * 5f;
            if (field != null)
                AddDecal(root, "ClaimRing", field, ringDiameter, ShadowSeconds, 1.6f, inner: 0.55f);

            // 균열은 링보다 안쪽에서 갈라진다 — 같은 크기로 겹치면 한 장으로 보인다.
            if (crack != null)
                AddDecal(root, "Fractures", crack, s.BodyHeight * 3.4f, ShadowSeconds * 0.75f, 2.2f, inner: 0f);

            AddRisingShards(root, s, debris);
            AddRisingAsh(root, s, dust);

            return Save(root, GroundName);
        }

        /// 눕힌 데칼 쿼드 한 장. 회전을 자식에 두는 건 큐가 루트 회전을 보스 방향으로 쓰기 때문이다.
        private static void AddDecal(GameObject parent, string name, Material material,
                                     float diameter, float fillSeconds, float fadeSeconds, float inner)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            quad.transform.localScale = new Vector3(diameter, diameter, 1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var decal = quad.AddComponent<PatternFieldDecal>();
            decal.FillSeconds = fillSeconds;
            decal.FadeSeconds = fadeSeconds;
            decal.InnerRadius = inner;
        }

        /// 링을 따라 지면 파편이 위로 솟는다. 중력이 음수라 시간이 갈수록 더 빨리 오른다.
        private static void AddRisingShards(GameObject parent, BossSize s, Material material)
        {
            var ps = AddParticles(parent, "RisingShards", material);

            var main = ps.main;
            main.duration = ShadowSeconds;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(s.BodyHeight * 0.05f, s.BodyHeight * 0.17f);
            main.startColor = ShardColor;
            // 3D 회전을 쓰면 x·y·z 세 축이 모두 같은 모드여야 한다. 하나만 범위로 주고
            // 나머지를 기본값(상수)으로 두면 "curves must all be in the same mode"로 거부된다.
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.28f;   // 음수 = 위로. 파편이 솟는 이유가 전부 이 한 줄이다
            main.maxParticles = 220;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            // 한 번에 다 뿜지 않고 세 번 나눠 뿜는다 — 바깥으로 번져 나가는 것처럼 보인다.
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.00f, (short)40),
                new ParticleSystem.Burst(0.28f, (short)55),
                new ParticleSystem.Burst(0.62f, (short)70),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = s.BodyHeight * 1.5f;
            shape.donutRadius = s.BodyHeight * 0.9f;
            shape.radiusThickness = 1f;

            // 세 축을 모두 같은 모드(두 상수 사이 범위)로 준다. 옆으로 조금 흩어지는 게
            // 수직으로만 솟는 것보다 자연스럽다.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
            vel.y = new ParticleSystem.MinMaxCurve(1.4f, 4.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);

            FadeTail(ps);
        }

        /// 링 안쪽에서 천천히 피어오르는 재. 파편만 있으면 딱딱해서 공기가 안 생긴다.
        private static void AddRisingAsh(GameObject parent, BossSize s, Material material)
        {
            var ps = AddParticles(parent, "RisingAsh", material);

            var main = ps.main;
            main.duration = ShadowSeconds + HoverSeconds;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(s.BodyHeight * 0.35f, s.BodyHeight * 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.30f, 0.10f, 0.07f, 0.40f), new Color(0.13f, 0.05f, 0.04f, 0.24f));
            main.gravityModifier = -0.06f;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = s.BodyHeight * 2.1f;
            shape.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            FadeTail(ps);
        }

        private static ParticleSystem AddParticles(GameObject parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// 수명 끝에서 서서히 사라진다. 이게 없으면 파티클이 제 크기 그대로 뚝 꺼진다.
        private static void FadeTail(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;

            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.75f, 0.55f), new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        // ══════════ 3. 섬광과 그레이딩 ══════════

        /// 착지 순간 화면을 덮는 흰 판의 머티리얼. 알파는 런타임에 프로퍼티 블록으로만 민다.
        private static Material MakeFlashMaterial()
        {
            string path = $"{OutFolder}/TFW_M_EntranceFlash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "TFW_M_EntranceFlash" };
                AssetDatabase.CreateAsset(mat, path);
            }

            // URP Unlit을 반투명으로 돌리는 정해진 절차. 하나라도 빠지면 흰 판이 불투명하게 남는다.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// 컷신 전용 그레이딩. 게임플레이와 같은 룩으로 컷신을 돌리면 "연출"로 안 읽힌다.
        /// 값은 얌전하게 — 여기서 과하게 밀면 보스가 안 보이고 필터만 보인다.
        private static VolumeProfile MakeVolumeProfile()
        {
            string path = $"{OutFolder}/TFW_EntranceVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.overrideState = true; bloom.intensity.value = 1.25f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.85f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.72f;

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.overrideState = true;  vignette.intensity.value = 0.42f;
            vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.55f;

            var grade = GetOrAdd<ColorAdjustments>(profile);
            grade.contrast.overrideState = true;   grade.contrast.value = 14f;
            grade.saturation.overrideState = true; grade.saturation.value = -6f;
            grade.colorFilter.overrideState = true;
            grade.colorFilter.value = new Color(1f, 0.94f, 0.90f);   // 아주 옅은 붉은 기 — TFW 팔레트

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// 프로파일을 지웠다 다시 만들지 않는다 — 프리팹이 GUID로 잡고 있어서
        /// 재생성하면 참조가 흔들린다. 있으면 값만 갱신한다.
        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
            => profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(true);

        // ══════════ 4. 카메라 — 컷이 리듬을 만든다 ══════════

        /// 컷 구성. 모든 좌표가 잰 보스 치수의 배수라 모델이 바뀌어도 그림의 비율이 유지된다.
        ///
        /// 세 가지가 이전 구성과 다르다.
        ///   1) 하드컷 — 한 대가 8초를 미끄러지던 걸 다섯 컷으로 쪼갰다.
        ///   2) 로우앵글 — 착지 컷의 카메라를 보스 키의 1/5 높이에 둔다. 위에서 내려다보면
        ///      아무리 큰 보스도 작아 보인다. 올려다봐야 커 보인다.
        ///   3) 시야각 — 압축(34)으로 시작해 착지에 광각(58+킥)으로 벌린다. 고정 54.5로는
        ///      두 그림 다 못 낸다.
        private static TfwBossEntrance.CameraKey[] BuildCameraKeys(BossSize s)
        {
            float h = s.BodyHeight;
            float w = s.BodyWidth;

            float tHover  = ShadowSeconds;                 // 1.20
            float tDive   = tHover + HoverSeconds;         // 2.70
            float tImpact = tDive + DiveSeconds;           // 3.15
            float tRoar   = tImpact + ImpactSeconds;       // 4.20
            float tEnd    = tRoar + RoarSeconds;           // 5.40

            return new[]
            {
                // ① 결정화 와이드 — 지면 가까이서 살짝 올려본다. 하늘이 프레임에 들어와야
                //    "위에서 뭔가 온다"가 깔린다.
                Key(0f, new Vector3(0f, h * 0.33f, -h * 2.90f), cut: true, fov: 34f, pitch: -6f),
                Key(tHover - 0.05f, new Vector3(0f, h * 0.43f, -h * 3.40f), fov: 34f, pitch: -4f),

                // ② 강하 트래킹 — 측면 높은 자리에서 떨어지는 보스를 따라 내려온다.
                Key(tHover, new Vector3(w * 0.67f, h * 1.95f, -h * 2.25f),
                    cut: true, fov: 46f, lookAt: s.ChestHeight),
                Key(tDive, new Vector3(w * 0.37f, h * 0.82f, -h * 1.95f),
                    fov: 50f, lookAt: s.ChestHeight),

                // ③ 착지 로우앵글 — 이 연출의 핵심 컷. 카메라가 보스 키의 1/5 높이에 있다.
                Key(tDive + 0.01f, new Vector3(0f, h * 0.185f, -h * 1.74f),
                    cut: true, fov: 58f, lookAt: s.HelmetHeight),
                Key(tImpact + 0.30f, new Vector3(0f, h * 0.20f, -h * 1.70f),
                    fov: 58f, lookAt: s.HelmetHeight),

                // ④ 무기 고리 — 반경 w*1.4의 고리가 프레임에 다 들어오도록 뒤로 뺀다.
                Key(tImpact + 0.80f, new Vector3(0f, h * 0.39f, -h * 2.25f),
                    fov: 60f, lookAt: s.ChestHeight * 0.95f),

                // ⑤ 투구 클로즈업 — 갑옷 보스의 '얼굴 공개'. 금속·가죽은 실시간 렌더가
                //    가장 잘 버티는 재질이라 이 보스는 3D 클로즈업을 해도 무너지지 않는다.
                Key(tRoar + 0.02f, new Vector3(w * 0.17f, s.HelmetHeight * 1.03f, -h * 0.70f),
                    cut: true, fov: 40f, lookAt: s.HelmetHeight * 1.01f),

                // ⑥ 풀샷 복귀 — 여기서 바로 플레이어 카메라로 섞여 들어간다.
                Key(tEnd - BlendOut, new Vector3(0f, h * 1.07f, -h * 2.56f),
                    cut: true, fov: 55f, lookAt: s.ChestHeight * 0.85f),
                Key(tEnd, new Vector3(0f, h * 1.15f, -h * 2.72f),
                    fov: 55f, lookAt: s.ChestHeight * 0.85f),
            };
        }

        private static TfwBossEntrance.CameraKey Key(float at, Vector3 position, bool cut = false,
                                                     float fov = 0f, float lookAt = 0f, float pitch = 0f)
            => new()
            {
                AtSeconds = at,
                Position = position,
                LookAtBoss = lookAt > 0f,
                LookAtHeight = lookAt,
                PitchDegrees = pitch,
                Cut = cut,
                FieldOfView = fov,
            };

        // ══════════ 5. 배선 ══════════

        private static bool Wire(BossSize s, GameObject ground, Material flashMaterial, VolumeProfile profile)
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[TFW 등장] 보스 프리팹을 열지 못했습니다: {BossPrefabPath}");
                return false;
            }

            try
            {
                var entrance = root.GetComponent<TfwBossEntrance>();
                if (entrance == null)
                {
                    Debug.LogError("[TFW 등장] 보스 루트에 TfwBossEntrance가 없습니다.");
                    return false;
                }

                var rig = Find(root.transform, RigName);
                var camera = rig != null ? Find(rig, CameraName) : null;
                if (rig == null || camera == null)
                {
                    Debug.LogError($"[TFW 등장] '{RigName}/{CameraName}'을 찾지 못했습니다.");
                    return false;
                }

                var flash = AttachFlash(camera, flashMaterial);
                var volume = AttachVolume(rig.gameObject, profile);
                TuneCamera(camera.gameObject);
                var rite = AttachWeaponRite(root, s);

                var so = new SerializedObject(entrance);

                so.FindProperty("_shadowSeconds").floatValue = ShadowSeconds;
                so.FindProperty("_hoverSeconds").floatValue = HoverSeconds;
                so.FindProperty("_diveSeconds").floatValue = DiveSeconds;
                so.FindProperty("_impactHoldSeconds").floatValue = ImpactSeconds;
                so.FindProperty("_roarSeconds").floatValue = RoarSeconds;
                so.FindProperty("_blendOutSeconds").floatValue = BlendOut;

                // 키가 LookAtHeight를 비웠을 때의 기본 조준점. 가슴보다 살짝 아래여야
                // 프레임 위쪽에 여백이 남아 큰 보스가 커 보인다.
                so.FindProperty("_lookAtHeight").floatValue = s.ChestHeight * 0.9f;

                so.FindProperty("_impactFlash").objectReferenceValue = flash;
                so.FindProperty("_cinematicVolume").objectReferenceValue = volume;
                so.FindProperty("_weaponRite").objectReferenceValue = rite;

                WriteCameraKeys(so.FindProperty("_cameraKeys"), BuildCameraKeys(s));
                WriteShadowCue(so.FindProperty("_shadowCues"), ground);

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// 카메라 바로 앞에 흰 판을 세운다. 시야각이 34~72도까지 움직이므로 가장 넓을 때를
        /// 기준으로, 초광각 모니터까지 덮도록 가로를 넉넉히 잡는다.
        private static Renderer AttachFlash(Transform camera, Material material)
        {
            var existing = Find(camera, FlashName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = FlashName;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(camera, false);

            const float distance = 0.4f;   // 근평면(0.3)보다 앞
            float halfHeight = Mathf.Tan(75f * 0.5f * Mathf.Deg2Rad) * distance;
            quad.transform.localPosition = new Vector3(0f, 0f, distance);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(halfHeight * 6f, halfHeight * 2f, 1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;   // 착지 전엔 꺼져 있어야 한다

            return r;
        }

        private static Volume AttachVolume(GameObject rig, VolumeProfile profile)
        {
            var volume = rig.GetComponent<Volume>() ?? rig.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;   // 로비·죽음 연출 볼륨보다 위
            volume.weight = 1f;
            volume.sharedProfile = profile;
            volume.enabled = false;   // 등장 박자가 켠다
            return volume;
        }

        /// 컷신 카메라의 안티에일리어싱을 켠다. 투구 클로즈업에서 계단현상이 그대로 보이는 건
        /// 인디 티가 나는 대표적 신호인데, 지금까지 이 카메라는 None이었다.
        private static void TuneCamera(GameObject cameraObject)
        {
            var data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) return;

            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderPostProcessing = true;
        }

        /// 무기 의식 컴포넌트를 얹고 원본 무기와 수렴 목표를 꽂는다.
        /// 고리 반경은 보스 폭의 1.4배 — 몸에 겹치지 않으면서 프레임 안에 남는 거리다.
        private static TfwEntranceWeaponRite AttachWeaponRite(GameObject root, BossSize s)
        {
            var rite = root.GetComponent<TfwEntranceWeaponRite>() ?? root.AddComponent<TfwEntranceWeaponRite>();

            var sources = new[]
            {
                Find(root.transform, "Sword1_1_1"),
                Find(root.transform, "Sword1_3_3"),
                Find(root.transform, "Shield1_3_1"),
            };

            var so = new SerializedObject(rite);
            var list = so.FindProperty("_weaponSources");

            int written = 0;
            list.arraySize = 3;
            foreach (var source in sources)
            {
                if (source == null) continue;
                list.GetArrayElementAtIndex(written++).objectReferenceValue = source.gameObject;
            }
            list.arraySize = written;

            if (written == 0)
                Debug.LogWarning("[TFW 등장] 보스 무기 원본을 하나도 찾지 못했습니다.");

            int borrowed = WriteBorrowedWeapons(so.FindProperty("_meshWeapons"));

            so.FindProperty("_radius").floatValue = s.BodyWidth * 1.4f;
            so.FindProperty("_height").floatValue = s.ChestHeight * 1.06f;
            so.FindProperty("_heightVariance").floatValue = s.BodyHeight * 0.22f;
            so.FindProperty("_convergeHeight").floatValue = s.ChestHeight;

            // 무기 길이는 보스 키에서 나온다. 실측 대검이 화면에서 2.54m로 보였고
            // 그게 키의 0.52배다 — 사람 손 기준으로 만들어진 빌려 온 무기도 이 길이로 맞춰야
            // 7m급 보스 옆에서 장난감처럼 보이지 않는다.
            so.FindProperty("_targetLength").floatValue = s.BodyHeight * 0.55f;
            so.FindProperty("_lengthVariance").floatValue = s.BodyHeight * 0.12f;

            // 고리를 다 채우되, 착지 구간(1.05초) 안에 마지막 자루까지 솟아야 한다.
            int count = Mathf.Max(1, written + borrowed);
            so.FindProperty("_count").intValue = count;
            so.FindProperty("_riseInterval").floatValue =
                Mathf.Max(0.012f, (ImpactSeconds - 0.42f) / count);

            Debug.Log($"[TFW 등장] 무기 {count}종 — 보스 {written} + 빌려 온 것 {borrowed}, " +
                      $"길이 {s.BodyHeight * 0.55f:F2}m로 정규화");

            // 무기가 매달린 본으로 모이게 한다. 없으면 가슴 높이로 모인다.
            var hand = sources[0] != null ? sources[0].parent : null;
            so.FindProperty("_convergeTarget").objectReferenceValue = hand;

            so.ApplyModifiedPropertiesWithoutUndo();
            return rite;
        }

        /// 빌려 온 FBX에서 무기를 전부 긁어 담는다.
        ///
        /// FBX의 자식은 서브에셋이라 씬 오브젝트로 참조할 수 없다. 그래서 모델 프리팹을
        /// 에셋으로 읽어 렌더러를 훑고, 메시와 머티리얼만 뽑아 넣는다 — 런타임 쪽이
        /// 딱 그 둘만 쓰도록 되어 있어서 이대로 맞아떨어진다.
        ///
        /// 크기는 여기서 손대지 않는다. 런타임이 메시 바운즈로 최장축을 재서 맞추므로,
        /// 나중에 다른 무기를 손으로 끼워 넣어도 알아서 같은 길이로 선다.
        private static int WriteBorrowedWeapons(SerializedProperty list)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BorrowedWeaponFbx);
            if (model == null)
            {
                Debug.LogWarning($"[TFW 등장] 무기 FBX를 읽지 못했습니다: {BorrowedWeaponFbx}");
                list.arraySize = 0;
                return 0;
            }

            var found = new List<(string name, Mesh mesh, Material[] materials)>();

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;

                // 활은 스킨드로 들어온다. 바인드 포즈 그대로 그려지는데, 공중에 멈춰 있는
                // 무기에는 그게 정확히 원하는 그림이라 그대로 쓴다.
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;

                if (mesh == null) continue;
                if (IsExcluded(name) || IsExcluded(mesh.name)) continue;

                found.Add((name, mesh, renderer.sharedMaterials));
            }

            if (found.Count == 0)
                Debug.LogWarning($"[TFW 등장] {BorrowedWeaponFbx}에서 무기를 찾지 못했습니다 — " +
                                 "임포트 설정에서 메시가 꺼져 있는지 확인하세요.");

            // 이름순으로 정렬한다 — 고리 위 순서가 곧 인덱스라, 순서가 매번 달라지면
            // 튜닝한 그림이 재현되지 않는다.
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            list.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Label").stringValue = found[i].name;
                e.FindPropertyRelative("Mesh").objectReferenceValue = found[i].mesh;

                var materials = e.FindPropertyRelative("Materials");
                materials.arraySize = found[i].materials.Length;
                for (int m = 0; m < found[i].materials.Length; m++)
                    materials.GetArrayElementAtIndex(m).objectReferenceValue = found[i].materials[m];
            }

            // 어떤 이름이 실제로 잡혔는지 남긴다. 이름 규칙을 잘못 짚어 0개가 나왔던 적이 있어서,
            // 다음에 같은 일이 생기면 로그만 보고 바로 알 수 있어야 한다.
            Debug.Log($"[TFW 등장] 빌려 온 무기 {found.Count}종: " +
                      string.Join(", ", found.ConvertAll(f => f.name)));

            return found.Count;
        }

        private static bool IsExcluded(string name)
            => name.IndexOf(BorrowedWeaponExclude, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static void WriteCameraKeys(SerializedProperty keys, TfwBossEntrance.CameraKey[] source)
        {
            keys.arraySize = source.Length;
            for (int i = 0; i < source.Length; i++)
            {
                var e = keys.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("AtSeconds").floatValue = source[i].AtSeconds;
                e.FindPropertyRelative("Position").vector3Value = source[i].Position;
                e.FindPropertyRelative("LookAtBoss").boolValue = source[i].LookAtBoss;
                e.FindPropertyRelative("PitchDegrees").floatValue = source[i].PitchDegrees;
                e.FindPropertyRelative("Cut").boolValue = source[i].Cut;
                e.FindPropertyRelative("FieldOfView").floatValue = source[i].FieldOfView;
                e.FindPropertyRelative("LookAtHeight").floatValue = source[i].LookAtHeight;
            }
        }

        /// 결정화 큐 한 건. 보스에 붙이지 않는다 — 이 박자에 보스는 28m 상공이라
        /// 붙이면 바닥 연출이 하늘에 뜬다.
        private static void WriteShadowCue(SerializedProperty cues, GameObject prefab)
        {
            cues.arraySize = 1;
            var cue = cues.GetArrayElementAtIndex(0);
            cue.FindPropertyRelative("Label").stringValue = "Shadow_GroundRite";
            cue.FindPropertyRelative("AtSeconds").floatValue = 0f;
            cue.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            cue.FindPropertyRelative("AnchorBoneName").stringValue = string.Empty;
            cue.FindPropertyRelative("AttachToAnchor").boolValue = false;
            // 착지까지 남아 있어야 한다 — 정적+강하+내리꽂기 뒤로 여운을 조금 더 준다.
            cue.FindPropertyRelative("Lifetime").floatValue =
                ShadowSeconds + HoverSeconds + DiveSeconds + 2.5f;
            cue.FindPropertyRelative("LocalOffset").vector3Value = Vector3.zero;
            cue.FindPropertyRelative("Scale").floatValue = 1f;
            cue.FindPropertyRelative("ManualOverride").boolValue = false;
        }

        // ══════════ 잡일 ══════════

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static GameObject Save(GameObject go, string name)
        {
            string path = $"{OutFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
