using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace Game.BossContent.Editor
{
    /// TFW 전용 이펙트를 만들어 깔고, 무기에 궤적 트레일을 붙이고, 패턴 SO의 큐를 배선한다.
    ///
    /// 크기를 상수로 박지 않는다. 이 보스는 크고(키 7m 안팎) 무기 스케일이 제각각이라
    /// (대검 3.0 / 검 2.0 / 방패 1.76) 프리팹을 열어 **메시 바운즈를 직접 잰다**.
    /// 참격 폭은 칼날 길이에서, 방패 충격은 방패 폭에서, 장판은 SO의 판정 반경에서 나온다.
    ///
    /// 만드는 것
    ///   1) 노이즈·형태 텍스처   — 코드로 픽셀을 그려 PNG로 저장
    ///   2) 머티리얼            — TFW_SlashTrail / TFW_GroundField 셰이더 그래프에서 생성
    ///   3) 이펙트 프리팹        — 장판 데칼(차오름), 참격 스파크, 바닥 폭발, 직선
    ///   4) 무기 궤적 리본       — 칼자루·칼끝 두 점을 기록해 면을 그린다(BossWeaponTrail)
    ///      + 발밑 분진          — 발 본의 접지를 감지해 먼지를 낸다(BossGroundDust)
    ///   5) 큐 배선             — SO의 XxxAtSeconds/Reach/Radius를 읽어 시점·위치·크기 계산
    public static class TheFirstWielderEffectSetup
    {
        private const string BossPrefabPath = "Assets/_Project/BossContent/03. Prefabs/Boss/Boss_TheFirstWielder.prefab";
        private const string SoFolder       = "Assets/_Project/BossContent/09. ScriptableObjects/Boss/TFW";
        private const string OutFolder      = "Assets/_Project/BossContent/06. Effects/TFW/Generated";
        private const string ShaderFolder   = "Assets/_Project/BossContent/06. Effects/TFW/Shaders";

        private const string BossDefPath    = "Assets/_Project/BossContent/09. ScriptableObjects/Boss/TFW/TFWBossDefinition.asset";
        private const string AoeTemplate    = "Assets/_Project/BossContent/03. Prefabs/Effect/BossAoeField.prefab";

        private const string GreatswordName = "Sword1_1_1";   // 1~3페이즈 대검
        private const string SwordName      = "Sword1_3_3";   // 4페이즈 검
        private const string ShieldName     = "Shield1_3_1";  // 4페이즈 방패
        private const string TrailChildName = "TFW_WeaponTrail";
        private const string AuraChildName  = "TFW_BladeAura";
        private const string MeshAuraChildName = "TFW_WeaponMeshAura";
        private const string CuesProperty   = "_effectCues";

        // HDR 색 — 1을 넘겨야 블룸이 물린다. 대검은 붉게, 검+방패는 푸르게 구분한다.
        private static readonly Color GreatswordColor = new(6.0f, 1.6f, 0.45f, 1f);
        private static readonly Color SwordColor      = new(0.9f, 3.2f, 6.0f, 1f);
        private static readonly Color ShieldColor     = new(6.0f, 4.4f, 1.4f, 1f);
        private static readonly Color GroundColor     = new(3.0f, 0.8f, 0.2f, 1f);
        private static readonly Color MarkColor       = new(5.0f, 0.5f, 0.5f, 1f);

        /// 검기 쿼드의 폭 = 칼폭 × 이 배수. 크게 잡을수록 칼 밖으로 번질 여백이 넓다.
        /// MakeAura가 이 값의 역수를 셰이더 _BladeWidth로 넣는다.
        private const float AuraQuadWidthFactor = 1.28f;

        /// 무기 전체 길이 중 아래에서 이 비율까지는 손잡이로 보고 쿼드에서 잘라낸다.
        ///
        /// 바운즈는 자루째 재기 때문에 그대로 쓰면 손잡이에도 기가 실린다. 손이 쥔 자리에서
        /// 기가 뻗어나가면 검기가 아니라 횃불이다. 셰이더의 _HiltFade로는 못 지운다 —
        /// 그건 칼몸이 시작하는 지점을 부드럽게 하는 용도고, 자루 길이는 무기마다 다르다.
        private const float AuraBladeStart = 0.38f;

        // 몸·무기 셸·리본이 공유하는 검정/진홍 팔레트.
        private static readonly Color WeaponAuraRed = new(0.32f, 0.0015f, 0.001f, 1f);
        private static readonly Color WeaponAuraHot = new(1.65f, 0.012f, 0.003f, 1f);
        private static readonly Color WeaponAuraBlack = new(0.0015f, 0f, 0.0005f, 1f);
        private static readonly Color BladeAuraHot = new(2.2f, 0.018f, 0.0035f, 1f);

        // 레이저 — 안쪽은 선명한 진홍 HDR, 바깥은 배경을 실제로 덮는 먹빛 연무.
        // 파티클은 가산 계열 머티리얼에서도 보이도록 별도의 밝은 포문색을 쓴다.
        private static readonly Color LaserCoreColor   = new(5.2f, 0.055f, 0.012f, 1f);
        private static readonly Color LaserHaloColor   = new(0.003f, 0.00015f, 0.0003f, 1f);
        private static readonly Color LaserMuzzleColor = new(2.8f, 0.015f, 0.004f, 1f);

        [MenuItem("Tools/Boss/Setup TheFirstWielder Effects")]
        private static void Run()
        {
            if (!EditorUtility.DisplayDialog("TFW 이펙트 생성",
                    $"'{OutFolder}'에 이펙트를 생성하고,\n무기에 궤적 트레일을 붙이고,\n패턴 SO의 EffectCues를 배선합니다.\n\n" +
                    "보스 프리팹이 수정됩니다.", "생성", "취소"))
                return;

            var size = Measure();
            if (size == null) return;

            EnsureFolder(OutFolder);

            var art = BuildArt();
            var fx  = BuildEffects(size, art);
            int trails = AttachWeaponTrails(size, art);
            int cues   = WireAllCues(fx, size);
            bool field = WirePhaseTransitionField(size, art, fx);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TFW FX] 이펙트 {fx.Count}종 생성 · 트레일 {trails}개 부착 · 큐 {cues}건 배선 · " +
                      $"거합 장판 {(field ? "배선" : "건너뜀")}\n{size}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(OutFolder);
        }

        [MenuItem("Tools/Boss/Setup TheFirstWielder Weapon Aura")]
        public static void SetupWeaponAura()
        {
            var size = Measure();
            if (size == null) return;
            EnsureFolder(OutFolder);

            var art = new Art
            {
                Trail = MakeWeaponTrailMaterial(),
                AuraGreat = MakeAura("TFW_M_AuraGreat", WeaponAuraRed),
                AuraSword = MakeAura("TFW_M_AuraSword", WeaponAuraRed),
                WeaponMeshAura = MakeWeaponMeshAura(),
                Dust = AssetDatabase.LoadAssetAtPath<Material>($"{OutFolder}/TFW_M_Dust.mat"),
            };

            int trails = AttachWeaponTrails(size, art);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TFW FX] 검 메시 오라와 검붉은 불꽃 트레일 {trails}개 갱신 완료.");
        }

        // ══════════ 지목 예고 표시 ══════════

        private const string MarkerPrefabName    = "FX_TFW_MarkNet";
        private const string MarkedStrikeDefPath = SoFolder + "/MarkedStrikeDef.asset";

        /// 손으로 받아 온 외부 텍스처를 두는 자리. 코드로 그린 것(Generated)과 섞이지 않게 폴더를 나눈다.
        private const string ExtTextureFolder   = "Assets/_Project/BossContent/06. Effects/TFW/Textures";
        private const string MarkReticleTexture = ExtTextureFolder + "/TFW_Ext_MarkReticle.png";

        /// 표시가 타격 뒤에도 잠깐 남는 여운. 맞는 순간 표시가 먼저 꺼지면 뭘 맞았는지 안 보인다.
        private const float MarkerAfterglowSeconds = 0.6f;

        /// 지목 예고 표시를 만들어 MarkedStrikeDef에 꽂는다.
        ///
        /// 크기·타이밍을 상수로 박지 않고 SO에서 읽는다 — ImpactRadius가 곧 위험 반경이고,
        /// 표시(MarkAtSeconds)와 타격(ImpactAtSeconds) 사이가 곧 차오름 시간이다.
        /// 보이는 원과 맞는 원이 다르면 예고가 거짓말이 된다.
        [MenuItem("Tools/Boss/Setup TheFirstWielder Marker Prefab")]
        public static void SetupMarkerPrefab()
        {
            var def = AssetDatabase.LoadAssetAtPath<MarkedStrikeDefinition>(MarkedStrikeDefPath);
            if (def == null)
            {
                Debug.LogError($"[TFW FX] 지목 SO를 찾지 못했습니다: {MarkedStrikeDefPath}");
                return;
            }

            EnsureFolder(OutFolder);

            var noise   = MakeTexture("TFW_Noise", 256, NoisePixel);
            var fill    = MakeField("TFW_M_Mark", MarkColor, 0f, noise);
            var reticle = MakeMarkReticleMaterial();

            var prefab = BuildMarker(def, fill, reticle);
            if (prefab == null) return;

            var so = new SerializedObject(def);
            so.FindProperty("MarkerPrefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TFW FX] 지목 표시 {MarkerPrefabName} 생성 · MarkedStrikeDef에 배선 완료.\n" +
                      $"  반경 {def.ImpactRadius:F2}m · 차오름 {def.ImpactAtSeconds - def.MarkAtSeconds:F2}초 · " +
                      $"외부 텍스처 {(reticle != null ? "적용" : "없음(고리만)")}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = prefab;
        }

        /// 외부에서 받아 온 조준 고리 텍스처를 얹는 오버레이 머티리얼.
        /// 파일이 아직 없으면 null을 돌려 오버레이만 건너뛴다 — 차오르는 고리는 그대로 나온다.
        private static Material MakeMarkReticleMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkReticleTexture);
            if (tex == null)
            {
                Debug.LogWarning($"[TFW FX] 외부 텍스처가 없어 조준 고리를 건너뜁니다: {MarkReticleTexture}");
                return null;
            }

            var m = MakeMaterial("TFW_M_MarkReticle", "Universal Render Pipeline/Particles/Unlit", MarkColor);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 2f);     // Additive — 바닥 위에서 빛나야 예고로 읽힌다
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// BossAoeField 템플릿을 열어 판정 부품을 걷어내고 표시 전용으로 바꾼다.
        /// NetworkObject 구성은 이미 검증된 것이라 물려받는다(BuildPhaseField와 같은 이유).
        private static GameObject BuildMarker(MarkedStrikeDefinition def, Material fill, Material reticle)
        {
            var root = PrefabUtility.LoadPrefabContents(AoeTemplate);
            if (root == null)
            {
                Debug.LogError($"[TFW FX] 장판 템플릿을 열지 못했습니다: {AoeTemplate}");
                return null;
            }

            try
            {
                // 판정은 MarkedStrike가 서버 틱에서 직접 굴린다. 표시가 따로 때리면 두 번 맞는다.
                if (root.TryGetComponent<BossAoeField>(out var field)) Object.DestroyImmediate(field);
                if (root.TryGetComponent<SphereCollider>(out var hit)) Object.DestroyImmediate(hit);

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                float fillSeconds = Mathf.Max(0.05f, def.ImpactAtSeconds - def.MarkAtSeconds);

                // 셰이더가 쿼드 반폭의 _OuterRadius까지만 그린다. 그 몫만큼 키워야
                // 보이는 테두리가 ImpactRadius와 정확히 겹친다.
                float diameter = def.ImpactRadius * 2f / FieldOuterRadius;

                var warning = new GameObject("Warning");
                warning.transform.SetParent(root.transform, false);
                AddDecalQuad(warning, fill, diameter, fillSeconds, 0f);

                if (reticle != null) AddOverlayQuad(warning, reticle, diameter);

                var marker = root.GetComponent<BossTelegraphMarker>();
                if (marker == null) marker = root.AddComponent<BossTelegraphMarker>();

                var mso = new SerializedObject(marker);
                mso.FindProperty("_lifeSeconds").floatValue = fillSeconds + MarkerAfterglowSeconds;
                mso.ApplyModifiedPropertiesWithoutUndo();

                // DeleteAsset을 하지 않는다 — 지웠다 만들면 guid가 바뀌어 SO가 들고 있던
                // 참조가 끊긴다(네트워크 프리팹이라 더 아프다).
                return PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{MarkerPrefabName}.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// 차오름 없이 그냥 깔리는 쿼드. PatternFieldDecal을 붙이지 않는다 —
        /// 조준 고리는 처음부터 끝까지 같은 모양으로 떠 있어야 위치 표시로 읽힌다.
        private static void AddOverlayQuad(GameObject root, Material mat, float diameter)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ReticleSheet";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, 0.03f, 0f);   // 차오르는 원 바로 위
            quad.transform.localScale = new Vector3(diameter, diameter, 1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ══════════ 1. 실측 ══════════

        private class BossSize
        {
            public float BodyHeight;
            public float BodyRadius;
            public float ChestHeight;
            public float GreatswordLength;
            public float SwordLength;
            public float ShieldRadius;

            public override string ToString()
                => $"  몸체 {BodyHeight:F2}m(반경 {BodyRadius:F2}) 가슴 {ChestHeight:F2}m / " +
                   $"대검 {GreatswordLength:F2}m 검 {SwordLength:F2}m 방패 R{ShieldRadius:F2}m";
        }

        /// 콜라이더가 아니라 메시로 잰다 — 콜라이더는 판정용으로 부풀려 둔 값이라 연출의 근거가 못 된다.
        private static BossSize Measure()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[TFW FX] 보스 프리팹을 열지 못했습니다: {BossPrefabPath}");
                return null;
            }

            try
            {
                // 무기는 페이즈에 따라 꺼져 있다. 재기 전에 전부 켠다.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true);

                bool has = false;
                var body = new Bounds();
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;
                    if (!has) { body = smr.bounds; has = true; }
                    else body.Encapsulate(smr.bounds);
                }

                if (!has)
                {
                    Debug.LogError("[TFW FX] 몸체 SkinnedMeshRenderer를 찾지 못했습니다.");
                    return null;
                }

                var size = new BossSize
                {
                    BodyHeight  = body.size.y,
                    BodyRadius  = Mathf.Max(body.size.x, body.size.z) * 0.5f,
                    ChestHeight = body.center.y,
                    GreatswordLength = LongestAxis(root, GreatswordName),
                    SwordLength      = LongestAxis(root, SwordName),
                    ShieldRadius     = LongestAxis(root, ShieldName) * 0.5f,
                };

                if (size.GreatswordLength <= 0.01f) size.GreatswordLength = size.BodyHeight * 0.8f;
                if (size.SwordLength      <= 0.01f) size.SwordLength      = size.BodyHeight * 0.55f;
                if (size.ShieldRadius     <= 0.01f) size.ShieldRadius     = size.BodyRadius;

                return size;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Find(GameObject root, string objectName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t;
            return null;
        }

        private static bool TryWorldBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool has = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                // 지난번 실행이 붙여 둔 검기 쿼드는 무기보다 크다. 같이 재면 무기가 잰 만큼
                // 커지고, 그걸로 다시 쿼드를 만들면 실행할 때마다 부풀어 오른다.
                if (r.gameObject.name == AuraChildName || r.gameObject.name == MeshAuraChildName) continue;

                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        private static float LongestAxis(GameObject root, string objectName)
        {
            var t = Find(root, objectName);
            if (t != null && TryWorldBounds(t, out var b))
                return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

            Debug.LogWarning($"[TFW FX] 무기 '{objectName}'을 찾지 못했습니다 — 몸 크기로 근사합니다.");
            return 0f;
        }

        // ══════════ 2. 텍스처·머티리얼 ══════════

        private class Art
        {
            public Material Trail;       // 무기 궤적 리본
            public Material Spark;       // 참격 스파크
            public Material Field;       // 일반 장판
            public Material Donut;       // 고리 장판(안쪽 안전지대)
            public Material Mark;        // 지목 표식
            public Material Dust;        // 먼지 — 가산이면 흙먼지가 아니라 빛무리가 된다
            public Material Blast;       // 근·중·원 폭발의 바닥 충격파
            public Material Crack;       // 섬단이 낸 바닥 균열
            public Material Slash;       // 일도양단이 낸 검 자국 데칼
            public Material Beam;        // 일자 레이저 줄기
            public Material LaserSmoke;  // 레이저 먹빛 연무 — 알파 블렌드라 검정이 실제로 보인다
            public Material AuraGreat;   // 대검 검기 — 칼날 외곽에 서리는 기운
            public Material AuraSword;   // 검 검기(4페이즈)
            public Material WeaponMeshAura; // 실제 검 메시를 감싸는 상시 검붉은 오라
            public Material ArcGreat;    // 대검 참격파 — 허공을 가르고 날아간 초승달
            public Material ArcSword;    // 검 참격파(4페이즈)
            public Material Debris;      // 튀어오르는 돌 파편 — 빛이 아니라 물체라 Lit으로 그린다
            public Mesh DebrisMesh;
            public Texture2D Noise;
        }

        private static Art BuildArt()
        {
            var noise  = MakeTexture("TFW_Noise",  256, NoisePixel);
            var dot    = MakeTexture("TFW_Dot",    128, DotPixel);
            var puff   = MakeTexture("TFW_Puff",   256, PuffPixel);

            // 궤적 리본. 셰이더가 UV를 "u=나이 / v=칼날 방향"으로 읽어 겉테두리와 검은 몸통을
            // 스스로 그린다 — 텍스처가 필요 없다.
            var trail = MakeWeaponTrailMaterial();

            // 불티는 트레일 셰이더를 같이 쓸 수 없다. 저 셰이더는 리본 UV를 전제로 하는데
            // 파티클 쿼드의 UV는 의미가 달라서, 그대로 쓰면 테두리만 빨간 네모가 나온다.
            // 불티는 가산으로 겹쳐 타오르는 게 맞으므로 URP 파티클 언릿을 쓴다.
            var spark = MakeMaterial("TFW_M_Spark", "Universal Render Pipeline/Particles/Unlit", Color.white);
            if (spark.HasProperty("_BaseMap")) spark.SetTexture("_BaseMap", dot);
            spark.SetFloat("_Surface", 1f);   // Transparent
            spark.SetFloat("_Blend", 2f);     // Additive
            spark.SetFloat("_ZWrite", 0f);
            spark.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // 먼지는 빛이 아니라 흙이다. 가산으로 그리면 밝은 빛무리가 되므로 URP 기본 파티클
            // 언릿의 알파 블렌딩을 쓴다 — 부드러운 원 텍스처에 탁한 색이면 충분하다.
            // 텍스처는 부드러운 원이 아니라 노이즈로 가장자리를 갉아낸 뭉게구름을 쓴다.
            // 완전한 원은 아무리 겹쳐도 동그란 얼룩으로 보이지 먼지로 안 읽힌다.
            var dustMat = MakeMaterial("TFW_M_Dust", "Universal Render Pipeline/Particles/Unlit", Color.white);
            if (dustMat.HasProperty("_BaseMap")) dustMat.SetTexture("_BaseMap", puff);
            dustMat.SetFloat("_Surface", 1f);   // Transparent
            dustMat.SetFloat("_Blend", 0f);     // Alpha
            dustMat.SetFloat("_ZWrite", 0f);
            dustMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var laserSmoke = MakeMaterial("TFW_M_LaserSmoke", "Universal Render Pipeline/Particles/Unlit", Color.white);
            if (laserSmoke.HasProperty("_BaseMap")) laserSmoke.SetTexture("_BaseMap", puff);
            laserSmoke.SetFloat("_Surface", 1f);
            laserSmoke.SetFloat("_Blend", 0f);     // Standard Alpha. Additive에서는 검정이 사라진다.
            laserSmoke.SetFloat("_ZWrite", 0f);
            laserSmoke.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // 돌 파편은 발광체가 아니라 물체다. 조명을 받아야 바닥에서 튀어오른 덩어리로 읽힌다.
            //
            // 일반 Lit이 아니라 **파티클 전용 Lit**을 쓴다. 일반 Lit은 파티클의 색·알파를
            // 무시해서, 수명이 끝나는 순간 돌이 통째로 사라진다(팍 꺼짐). 파티클 Lit을
            // 반투명으로 두면 colorOverLifetime의 알파가 먹혀 서서히 옅어진다.
            var debris = MakeMaterial("TFW_M_Debris", "Universal Render Pipeline/Particles/Lit", Color.white);
            if (debris.HasProperty("_BaseColor")) debris.SetColor("_BaseColor", new Color(0.26f, 0.22f, 0.19f, 1f));
            if (debris.HasProperty("_Smoothness")) debris.SetFloat("_Smoothness", 0.12f);
            debris.SetFloat("_Surface", 1f);   // Transparent
            debris.SetFloat("_Blend", 0f);     // Alpha
            debris.SetFloat("_ZWrite", 0f);
            debris.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var donut = LoadDonutDefinition();
            float donutInner = donut != null && donut.OuterRadius > 0.01f
                ? Mathf.Clamp01(donut.InnerRadius / donut.OuterRadius * FieldOuterRadius)
                : 0.2375f;

            return new Art
            {
                Noise = noise,
                Trail = trail,
                Spark = spark,
                Dust  = dustMat,
                Field = MakeField("TFW_M_Field", GroundColor, 0f, noise),
                Donut = MakeField("TFW_M_Donut", GroundColor, donutInner, noise),
                Mark  = MakeField("TFW_M_Mark",  MarkColor,   0f, noise),
                Blast = MakeBlast("TFW_M_Blast", GreatswordColor),
                Crack = MakeCrack("TFW_M_Crack", GroundColor),
                Slash = MakeSlash("TFW_M_Slash", GroundColor),
                Beam  = MakeBeam("TFW_M_Beam", LaserCoreColor, LaserHaloColor),
                LaserSmoke = laserSmoke,
                AuraGreat = MakeAura("TFW_M_AuraGreat", WeaponAuraRed),
                AuraSword = MakeAura("TFW_M_AuraSword", WeaponAuraRed),
                WeaponMeshAura = MakeWeaponMeshAura(),
                ArcGreat  = MakeArc("TFW_M_ArcGreat",   WeaponAuraRed, WeaponAuraHot),
                ArcSword  = MakeArc("TFW_M_ArcSword",   WeaponAuraRed, WeaponAuraHot),
                Debris = debris,
                DebrisMesh = BuildRockMesh(),
            };
        }

        /// 돌 파편 메시를 구워 에셋으로 저장한다.
        ///
        /// 빌트인 큐브를 쓰면 화면에 **말 그대로 정육면체**가 굴러다닌다. 돌로 보이려면
        /// 면이 불규칙하고 각져야 한다. 정팔면체를 두 번 쪼개 구를 만든 뒤 꼭지점마다
        /// 반지름을 흔들고, 삼각형마다 꼭지점을 분리해 평면 셰이딩(각진 면)으로 만든다.
        private static Mesh BuildRockMesh()
        {
            string path = $"{OutFolder}/TFW_Rock.mesh";

            // 정팔면체
            var verts = new List<Vector3>
            {
                Vector3.up, Vector3.down,
                Vector3.left, Vector3.right,
                Vector3.forward, Vector3.back,
            };
            var tris = new List<int>
            {
                0,4,3, 0,3,5, 0,5,2, 0,2,4,
                1,3,4, 1,5,3, 1,2,5, 1,4,2,
            };

            // 두 번 쪼개 면 수를 늘린다(8 → 128면)
            for (int pass = 0; pass < 2; pass++)
            {
                var next = new List<int>(tris.Count * 4);
                var mid = new Dictionary<long, int>();

                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Midpoint(verts, mid, a, b);
                    int bc = Midpoint(verts, mid, b, c);
                    int ca = Midpoint(verts, mid, c, a);

                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }

            // 꼭지점마다 반지름을 흔들어 울퉁불퉁하게. 시드를 고정해 매번 같은 돌이 나온다.
            var rng = new System.Random(20260825);   // guardrail:allow RNG — 에디터 메시 생성. 시드를 고정해 결과가 항상 같다
            for (int i = 0; i < verts.Count; i++)
            {
                var n = verts[i].normalized;
                float bump = 0.72f
                           + Mathf.Sin(n.x * 4.3f + n.y * 2.1f) * 0.13f
                           + Mathf.Sin(n.z * 5.7f - n.x * 3.3f) * 0.10f
                           + (float)rng.NextDouble() * 0.16f;

                // 축마다 다르게 눌러 납작하고 길쭉한 덩어리로 만든다
                verts[i] = Vector3.Scale(n * bump, new Vector3(1f, 0.68f, 0.86f));
            }

            // 삼각형마다 꼭지점을 분리 — 이래야 면이 각지게 보인다(부드러우면 감자다)
            var flatVerts = new Vector3[tris.Count];
            var flatNormals = new Vector3[tris.Count];
            var flatTris = new int[tris.Count];

            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 v0 = verts[tris[i]], v1 = verts[tris[i + 1]], v2 = verts[tris[i + 2]];
                Vector3 nrm = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                for (int k = 0; k < 3; k++)
                {
                    flatVerts[i + k] = k == 0 ? v0 : (k == 1 ? v1 : v2);
                    flatNormals[i + k] = nrm;
                    flatTris[i + k] = i + k;
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "TFW_Rock" };
            else mesh.Clear();
            mesh.SetVertices(flatVerts);
            mesh.SetNormals(flatNormals);
            mesh.SetTriangles(flatTris, 0);
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int found)) return found;

            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }

        private static Material MakeBlast(string name, Color fire)
        {
            var m = MakeMaterial(name, "TFW_Blast.shadergraph", fire);
            m.SetColor("_ScorchColor", new Color(0.10f, 0.07f, 0.05f, 1f));
            m.SetFloat("_RingWidth", 0.14f);
            m.SetFloat("_Wobble", 0.10f);
            m.SetFloat("_Glow", 2.8f);
            return m;
        }

        private static Material MakeSlash(string name, Color glow)
        {
            var m = MakeMaterial(name, "TFW_SlashMark.shadergraph", glow);
            m.SetColor("_ScorchColor", new Color(0.10f, 0.07f, 0.05f, 1f));
            m.SetFloat("_Width", 0.09f);
            m.SetFloat("_Glow", 3.6f);
            return m;
        }

        private static Material MakeBeam(string name, Color core, Color halo)
        {
            var m = MakeMaterial(name, "TFW_Beam.shadergraph", core);
            m.SetColor("_EdgeColor", halo);

            // 브레스처럼 요동치는 몸통이 읽히도록 심은 유지하고 외곽 질감을 넓힌다.
            m.SetFloat("_CoreWidth", 0.10f);  // 좁고 뜨거운 진홍 필라멘트
            m.SetFloat("_HaloWidth", 0.96f);  // 넓은 먹빛 shroud가 흐름 노이즈를 드러낸다
            m.SetFloat("_Glow", 1.45f);       // 발사 직후 HDR 코어 대비
            return m;
        }

        /// 검기. 세기는 런타임(BossBladeAura)이 휘두르는 속도로 밀어 넣으므로
        /// 머티리얼의 _Strength는 0으로 둔다 — 씬에 세워만 둔 무기가 혼자 빛나면 안 된다.
        private static Material MakeAura(string name, Color glow)
        {
            var m = MakeMaterial(name, "TFW_BladeAura.shadergraph", glow);
            m.SetColor("_BaseColor", new Color(0.01f, 0.0002f, 0.0008f, 1f));
            m.SetColor("_EdgeColor", BladeAuraHot);
            m.SetFloat("_Strength", 0f);
            m.SetFloat("_Glow", 0.62f);
            m.SetFloat("_Spread", 0.15f);
            m.SetFloat("_EdgeWidth", 0.02f);
            m.SetFloat("_Hollow", 0.72f);
            m.SetFloat("_FlameSpeed", 2.45f);
            m.SetFloat("_Fade", 0.76f);

            // 칼날 반폭이 쿼드 반폭에서 차지하는 비율. 쿼드를 넓히면 그만큼 실루엣이
            // 좁아져야 하므로 배수의 역수다 — 두 값이 따로 놀면 검기가 칼에서 어긋난다.
            m.SetFloat("_BladeWidth", 1f / AuraQuadWidthFactor);
            return m;
        }

        private static Material MakeWeaponTrailMaterial()
        {
            var m = MakeMaterial("TFW_M_Trail", "TFW_SlashTrail.shadergraph", WeaponAuraRed);
            m.SetColor("_EdgeColor", WeaponAuraHot);
            m.SetColor("_MidColor", WeaponAuraRed);
            m.SetColor("_CoreColor", WeaponAuraBlack);
            m.SetFloat("_CoreAlpha", 0.70f);
            m.SetFloat("_EdgeWidth", 0.075f);
            m.SetFloat("_Dissolve", 0.78f);
            m.SetFloat("_FlameSpeed", 2.15f);
            m.SetFloat("_Glow", 0.48f);
            return m;
        }

        private static Material MakeWeaponMeshAura()
        {
            var m = MakeMaterial("TFW_M_WeaponMeshAura", "TFW_BodyAura.shadergraph", WeaponAuraRed);
            m.SetColor("_AuraColor", new Color(0.05f, 0.0008f, 0.0015f, 1f));
            m.SetColor("_DeepColor", WeaponAuraBlack);
            m.SetColor("_CoreColor", new Color(1.35f, 0.012f, 0.003f, 1f));
            m.SetColor("_FringeColor", Color.black);
            m.SetFloat("_Glow", 0.42f);
            m.SetFloat("_RimWidth", 0.24f);
            m.SetFloat("_RimPower", 3.1f);
            m.SetFloat("_NoiseScale", 4.2f);
            m.SetFloat("_RiseSpeed", 0.42f);
            m.SetFloat("_Turbulence", 0.7f);
            m.SetFloat("_EmberRate", 0.3f);
            m.SetFloat("_Pulse", 0.05f);
            m.SetFloat("_ShellHeight", 0.012f);
            m.SetFloat("_TrailLength", 0f);
            m.SetFloat("_TrailStretch", 0f);
            m.SetFloat("_Fade", 0.24f);
            m.renderQueue = 3010;
            return m;
        }

        /// 참격파. 타격 순간에 한 번 그어지고 사그라든다 — 진행은 PatternFieldDecal이 굴린다.
        private static Material MakeArc(string name, Color glow, Color hot)
        {
            var m = MakeMaterial(name, "TFW_SlashArc.shadergraph", glow);
            m.SetColor("_EdgeColor", hot);
            m.SetFloat("_Radius", 0.78f);
            m.SetFloat("_Thickness", 0.11f);
            m.SetFloat("_ArcAngle", 2.15f);
            m.SetFloat("_Detail", 0.9f);
            m.SetFloat("_Glow", 0.72f);
            return m;
        }

        private static Material MakeCrack(string name, Color glow)
        {
            var m = MakeMaterial(name, "TFW_GroundCrack.shadergraph", glow);
            m.SetColor("_EdgeColor", new Color(0.10f, 0.07f, 0.05f, 1f));
            m.SetFloat("_CrackWidth", 0.018f);
            m.SetFloat("_Branches", 7f);
            m.SetFloat("_Glow", 4f);
            return m;
        }

        /// 장판 셰이더가 쿼드 반폭 중 실제로 그리는 비율. 쿼드 크기를 판정 반경에 맞출 때
        /// 이 값으로 나눠야 보이는 테두리와 맞는 원이 겹친다.
        private const float FieldOuterRadius = 0.95f;

        private static Material MakeField(string name, Color color, float inner, Texture2D noise)
        {
            var m = MakeMaterial(name, "TFW_GroundField.shadergraph", color);
            m.SetColor("_EdgeColor", color * 1.9f);
            m.SetFloat("_InnerRadius", inner);
            m.SetFloat("_OuterRadius", FieldOuterRadius);

            // 0 = 완전한 원. 테두리 일렁임도 스캔·줄무늬도 끈다 — 장판은 "여기 서 있으면
            // 맞는다"가 한눈에 읽히는 게 먼저다. 올리면 다시 구불거린다.
            m.SetFloat("_Detail", 0f);
            return m;
        }

        private delegate float PixelFunc(float u, float v);

        /// 부드러운 원 — 스파크용.
        private static float DotPixel(float u, float v)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float k = Mathf.Clamp01(1f - d);
            return k * k;
        }

        /// 먼지 뭉치 — 가운데가 진한 원을 fbm 노이즈로 갉아 가장자리를 너덜너덜하게 만든다.
        private static float PuffPixel(float u, float v)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float core = Mathf.Clamp01(1f - d);
            core *= core;

            float n = NoisePixel(u, v);
            return Mathf.Clamp01(core * (0.5f + n * 1.0f) * 1.2f);
        }


        /// 타일링되는 값 노이즈. 디졸브·테두리 흔들림에 쓴다.
        private static float NoisePixel(float u, float v)
        {
            float total = 0f, amp = 1f, freq = 4f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                total += ValueNoise(u * freq, v * freq, (int)freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return Mathf.Clamp01(total / norm);
        }

        private static float ValueNoise(float x, float y, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash(xi, yi, period), b = Hash(xi + 1, yi, period);
            float c = Hash(xi, yi + 1, period), d = Hash(xi + 1, yi + 1, period);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // period로 감싸 좌우·상하가 이어지게 한다(타일링).
        private static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }

        private static Texture2D MakeTexture(string name, int size, PixelFunc fn)
        {
            string path = $"{OutFolder}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = fn((x + 0.5f) / size, (y + 0.5f) / size);
                px[y * size + x] = new Color(a, a, a, a);   // R에도 넣어 노이즈로 쓸 수 있게
            }

            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.alphaIsTransparency = true;
                imp.wrapMode = name == "TFW_Noise" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// 셰이더를 가져온다.
        ///
        /// Shader Graph가 만드는 셰이더 이름은 "{m_Path}/{에셋 파일명}"이라
        /// Shader.Find로 맞히려면 파일명을 그대로 알아야 하고, 파일을 옮기거나 이름을 바꾸면 깨진다.
        /// 그래서 그래프는 **에셋 경로로 직접 읽는다** — 이름 규칙과 무관하게 확실하다.
        /// (.shadergraph는 임포트되면 Shader가 메인 에셋이라 Shader로 바로 로드된다)
        private static Shader LoadShader(string reference)
        {
            if (!reference.EndsWith(".shadergraph")) return Shader.Find(reference);

            string path = $"{ShaderFolder}/{reference}";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                Debug.LogError($"[TFW FX] 셰이더 그래프를 읽지 못했습니다: {path}");
            return shader;
        }

        private static Material MakeMaterial(string name, string shaderName, Color color)
        {
            var shader = LoadShader(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[TFW FX] '{shaderName}' 대신 URP Unlit으로 대체합니다.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            string path = $"{OutFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                // 기존 머티리얼은 프리팹과 씬이 GUID로 참조한다. 삭제 후 재생성하면
                // Inspector가 에셋을 잡고 있을 때 실패하고, 성공해도 참조가 흔들릴 수 있다.
                mat.shader = shader;
                mat.name = name;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ══════════ 3. 이펙트 프리팹 ══════════

        private static Dictionary<string, GameObject> BuildEffects(BossSize s, Art art)
        {
            var donut = LoadDonutDefinition();
            float donutDiameter = donut != null && donut.OuterRadius > 0.01f
                ? donut.OuterRadius * 2f / FieldOuterRadius
                : 29.473684f;
            float donutInner = donut != null && donut.OuterRadius > 0.01f
                ? Mathf.Clamp01(donut.InnerRadius / donut.OuterRadius * FieldOuterRadius)
                : 0.2375f;
            float donutFill = donut != null ? Mathf.Max(0.01f, donut.BurstAtSeconds) : 0.5f;

            var fx = new Dictionary<string, GameObject>
            {
                // 장판 — 눕힌 쿼드에 GroundField 셰이더. 차오름은 PatternFieldDecal이 굴린다.
                ["Field"] = SaveDecal("FX_TFW_Field", art.Field, s.BodyRadius * 2f, 0f, 1.5f, art),
                ["Donut"] = SaveDecal("FX_TFW_Donut", art.Donut, donutDiameter, donutInner, donutFill, art),
                ["Mark"]  = SaveDecal("FX_TFW_Mark",  art.Mark,  s.BodyRadius * 2f, 0f, 1.2f, art),

                // 참격 — 트레일이 본 궤적을 그리고, 이쪽은 타격 순간의 스파크를 얹는다.
                ["GreatswordHit"] = SaveSpark("FX_TFW_GreatswordHit", s.GreatswordLength, GreatswordColor, art, art.ArcGreat),
                ["SwordHit"]      = SaveSpark("FX_TFW_SwordHit",      s.SwordLength,      SwordColor,      art, art.ArcSword),
                ["ShieldBash"]    = SaveSpark("FX_TFW_ShieldBash",    s.ShieldRadius * 2f, ShieldColor,    art, null),

                // 직선 — 찌르기·레이저
                ["Beam"] = SaveBeam("FX_TFW_Beam", s.GreatswordLength, GreatswordColor, art),

                // 바닥 폭발
                ["GroundCrack"] = SaveGround("FX_TFW_GroundCrack", s.BodyRadius, GroundColor, art),
                ["PhaseBurst"]  = SaveGround("FX_TFW_PhaseBurst",  s.BodyHeight * 0.5f, GreatswordColor, art),

                // 근·중·원 3연속 폭발 — 충격파 데칼 + 화염 + 파편
                ["Blast"] = SaveBlast("FX_TFW_Blast", s.BodyRadius * 2f, art),

                // 섬단이 바닥을 가른 자국 — 균열 데칼 + 파편 + 흙먼지
                ["Crack"] = SaveCrack("FX_TFW_Crack", s.BodyRadius * 2.4f, art),

                // 일도양단 — 검이 찍힌 자국 + 자국 양옆으로 솟는 돌
                ["Slash"] = SaveSlash("FX_TFW_Slash", s.BodyRadius * SlashLengthFactor,
                                                      s.BodyRadius * SlashWidthFactor, art),

                // 6방향 레이저 — 한 프리팹 안에 줄기를 다 넣는다(큐 하나로 전부 보인다)
                ["Laser"]       = SaveRadialBeams("FX_TFW_Laser", LaserBeamCount, s.BodyHeight, art),
                ["LaserCharge"] = SaveRadialCharge("FX_TFW_LaserCharge", LaserBeamCount, s.BodyHeight, art),
            };
            return fx;
        }

        /// 일도양단 자국의 크기 = BodyRadius × 이 배수. 보스가 워낙 커서 3.0/1.1은 발밑에 묻혔다.
        /// 지금 값이면 CleaveComboDefinition의 판정(CleaveLength 8m / CleaveWidth 3m)과 거의 맞는다.
        /// 여기를 고치면 WriteCues의 Kind.Slash baseSize도 같이 따라온다.
        private const float SlashLengthFactor = 4.5f;
        private const float SlashWidthFactor  = 1.65f;

        /// 방사형 줄기 개수. LaserComboDefinition.LaserCount의 기본값과 맞춰 둔다 —
        /// 연출용이라 어긋나도 판정에는 영향이 없지만, 맞아야 보이는 대로 피할 수 있다.
        private const int LaserBeamCount = 6;

        /// 일도양단 자국.
        ///
        /// 검이 지나간 자리에 길쭉한 홈이 파이고(데칼), 그 홈을 따라 바닥에 스파크가 튀며,
        /// 홈 **양옆 두 줄**에서 돌이 솟아오른다. 방사형이 아니라 한 줄이 핵심이다 —
        /// 어디를 베었는지가 그림으로 읽혀야 한다.
        ///
        /// length는 벤 방향(로컬 +Z), width는 좌우 폭이다.
        private static GameObject SaveSlash(string name, float length, float width, Art art)
        {
            var root = new GameObject(name);

            // 홈이 그어지는 데 0.25초. 느리면 "한 방"의 타격감이 죽는다.
            AddDecalQuad(root, art.Slash, new Vector2(width, length), 0.25f, 2.0f);

            // 홈을 따라 바닥에서 튀는 스파크 — 낮게 깔려야 "바닥에 튀는" 걸로 읽힌다
            var sparks = AddLayer(root, "GroundSparks");
            var pm = sparks.main;
            pm.duration = 0.45f;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(length * 0.3f, length * 1.1f);
            pm.startSize = new ParticleSystem.MinMaxCurve(width * 0.03f, width * 0.11f);
            pm.startColor = GroundColor;
            pm.gravityModifier = 1.3f;
            pm.maxParticles = 500;
            sparks.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    70),
                new ParticleSystem.Burst(0.08f, 40),
            });

            var ps = sparks.shape;
            ps.enabled = true;
            ps.shapeType = ParticleSystemShapeType.Box;
            ps.rotation = new Vector3(-72f, 0f, 0f);                        // 거의 눕혀 튄다
            ps.scale = new Vector3(width * 0.25f, length * 0.95f, 0.05f);   // 회전 뒤 y가 홈 방향이다

            var sc = sparks.collision;
            sc.enabled = true;
            sc.type = ParticleSystemCollisionType.World;
            sc.mode = ParticleSystemCollisionMode.Collision3D;
            sc.bounce = 0.4f;
            sc.dampen = 0.3f;

            FadeOut(sparks);
            Shrink(sparks);

            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.sharedMaterial = art.Spark;
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.14f;
            sr.lengthScale = 3.5f;

            // 양옆 두 줄에서 솟는 돌. 부호만 다른 같은 설정이다.
            AddRisingRocks(root, "RocksLeft",  -1f, length, width, art);
            AddRisingRocks(root, "RocksRight", +1f, length, width, art);

            // 바닥 데칼과 공중의 돌 사이가 비면 두 이펙트가 따로 논다.
            // 그 사이 높이를 잔불·열기·불티로 메운다.
            AddGrooveEmbers(root, length, width, art);
            AddGrooveHeat(root, length, width, art);

            AddSmoke(AddLayer(root, "Dust"), Mathf.Max(width, length * 0.25f), art, 22);
            AddImpactRing(root, width * 0.85f, GreatswordColor, art, 24, horizontal: true);
            AddImpactShroud(root, width * 0.9f, art, 8);

            return Save(root, name);
        }

        /// 홈을 따라 낮게 떠오르는 잔불. 데칼 바로 위에서 시작해 돌 높이까지 천천히 오른다 —
        /// 바닥과 공중을 잇는 역할이라 수명이 데칼과 같아야 한다.
        private static void AddGrooveEmbers(GameObject root, float length, float width, Art art)
        {
            var ps = AddLayer(root, "Embers");

            var main = ps.main;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.25f, width * 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.02f, width * 0.07f);
            main.startColor = GroundColor;
            main.gravityModifier = -0.06f;      // 열기에 실려 떠오른다
            main.maxParticles = 400;

            ps.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    45),
                new ParticleSystem.Burst(0.15f, 30),
                new ParticleSystem.Burst(0.35f, 20),
                new ParticleSystem.Burst(0.60f, 12),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(width * 0.9f, length * 0.9f, 0.05f);
            shape.angle = 20f;

            // 공기에 실려 흔들린다
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = width * 0.5f;
            noise.frequency = 0.5f;

            FadeOut(ps);
            Shrink(ps);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Spark;
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// 홈에서 피어오르는 열기. 잔불보다 크고 흐릿해 빈 공간의 부피를 채운다.
        private static void AddGrooveHeat(GameObject root, float length, float width, Art art)
        {
            var ps = AddLayer(root, "Heat");

            var main = ps.main;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(width * 0.3f, width * 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.25f, width * 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.10f;
            main.maxParticles = 300;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.6f, 0.55f, 0.18f, 0.5f), new Color(0.9f, 0.28f, 0.10f, 0.3f));

            ps.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    22),
                new ParticleSystem.Burst(0.25f, 16),
                new ParticleSystem.Burst(0.55f, 10),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(width * 0.7f, length * 0.85f, 0.05f);
            shape.angle = 14f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Dust;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingFudge = 5f;
        }

        /// 자국 한쪽에서 솟아오르는 돌. side가 -1이면 왼쪽, +1이면 오른쪽이다.
        /// 위로 솟으면서 바깥으로 살짝 밀려나간다 — 땅이 갈라져 밀려 올라온 모양새다.
        private static void AddRisingRocks(GameObject root, string name, float side,
                                           float length, float width, Art art)
        {
            var ps = AddLayer(root, name);

            var main = ps.main;
            main.duration = 0.5f;

            // 데칼과 같이 사라져야 한다. 데칼은 0.25초 그어진 뒤 2초에 걸쳐 빠지므로
            // 돌도 그 끝(≈2.25초)에 맞춘다 — 따로 놀면 돌만 먼저 없어져 허전해진다.
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(width * 1.2f, width * 3.4f);

            // 축마다 따로 흩는다 — 같은 배율로 키우면 크기만 다른 같은 돌이 반복된다
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(width * 0.10f, width * 0.34f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(width * 0.07f, width * 0.22f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(width * 0.10f, width * 0.30f);

            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 300;

            // 홈이 그어지는 속도에 맞춰 이어 솟는다. 한 번에 다 터뜨리면 벽처럼 보인다.
            ps.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    10),
                new ParticleSystem.Burst(0.06f, 12),
                new ParticleSystem.Burst(0.13f, 10),
                new ParticleSystem.Burst(0.21f,  7),
                new ParticleSystem.Burst(0.30f,  5),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            // ⚠ shape.rotation은 셰이프의 **스케일 축까지** 같이 돌린다.
            // X로 -90도 돌리면 셰이프 로컬 Z가 월드 +Y(위)로 가고 로컬 Y가 월드 -Z로 간다.
            // 그래서 "자국을 따라 길게"는 z가 아니라 **y**에 넣어야 한다.
            // (z에 넣으면 긴 축이 수직으로 서 버려서 한 지점에서 줄줄이 솟는다)
            shape.rotation = new Vector3(-90f, 0f, side * 15f);   // 위로, 바깥으로 15도
            shape.scale = new Vector3(width * 0.30f, length * 0.9f, 0.05f);
            shape.position = new Vector3(side * width * 0.42f, 0f, 0f);   // 홈 옆줄
            shape.angle = 14f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.2f;
            col.dampen = 0.55f;

            // 데칼이 빠지는 구간과 겹치게 마지막 30%에서 옅어진다
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = art.DebrisMesh;
            r.sharedMaterial = art.Debris;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// 6방향 레이저 발사.
        ///
        /// 예전엔 입자를 길게 늘여 흉내냈는데, 그러면 알갱이가 보여서 레이저로 안 읽힌다.
        /// 레이저는 면이라 **길게 늘인 쿼드 한 장**에 빔 셰이더를 붙여야 한다.
        /// 뻗어나가는 연출은 PatternFieldDecal이 _Progress를 밀어 만든다.
        private static GameObject SaveRadialBeams(string name, int count, float height, Art art)
        {
            var root = new GameObject(name);
            float step = 360f / Mathf.Max(2, count);
            float length = height * 3.0f;      // 큐 Scale이 최종 길이를 다시 맞춘다
            // 검붉은 브레스의 외곽 결이 읽히도록 기존 폭보다 약 27% 넓힌다.
            // 판정 LaserWidth는 건드리지 않는 순수 비주얼 폭이다.
            float width  = height * 0.57f;
            float muzzleOut = height * 0.48f;  // 보스 중앙은 비우고 여섯 포문을 몸 밖에 둔다

            for (int i = 0; i < count; i++)
            {
                var arm = new GameObject($"Beam_{i}");
                arm.transform.SetParent(root.transform, false);
                arm.transform.localRotation = Quaternion.Euler(0f, i * step, 0f);

                // 빔 판. 쿼드 로컬 +Y가 발사 방향(+Z)으로 가도록 눕히고,
                // 앞쪽 절반만큼 밀어 포문에서 시작하게 한다.
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "CoreBreathSheet";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(arm.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localPosition = new Vector3(0f, 0f, muzzleOut + length * 0.5f);
                quad.transform.localScale = new Vector3(width, length, 1f);

                var qr = quad.GetComponent<MeshRenderer>();
                qr.sharedMaterial = art.Beam;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qr.receiveShadows = false;

                var decal = quad.AddComponent<PatternFieldDecal>();
                // _Progress가 0→1로 가는 시간이 곧 빔의 수명이다. 뻗는 건 셰이더가
                // 앞 12%에서 끝내므로, 길게 줘도 느릿느릿 기어나가지 않는다.
                decal.FillSeconds = 2.0f;
                decal.FadeSeconds = 0.25f;
                decal.InnerRadius = 0f;

                // 포문 화염 — 줄기가 몸에서 나갔다는 걸 보여준다
                var muzzle = AddLayer(arm, "HeadShock");
                muzzle.transform.localPosition = new Vector3(0f, 0f, muzzleOut);
                var mm = muzzle.main;
                mm.duration = 0.3f;
                mm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                mm.startSpeed = new ParticleSystem.MinMaxCurve(height * 1.5f, height * 4f);
                mm.startSize = new ParticleSystem.MinMaxCurve(width * 0.25f, width * 0.7f);
                mm.startColor = LaserMuzzleColor;
                mm.maxParticles = 120;
                muzzle.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

                var ms = muzzle.shape;
                ms.enabled = true;
                ms.shapeType = ParticleSystemShapeType.Cone;
                ms.angle = 18f;
                ms.radius = width * 0.2f;

                FadeOut(muzzle);
                Shrink(muzzle);

                var mr = muzzle.GetComponent<ParticleSystemRenderer>();
                mr.sharedMaterial = art.Spark;
                mr.renderMode = ParticleSystemRenderMode.Billboard;

                AddMuzzleShockRing(arm, muzzleOut, width, height, art);
                AddLaserDirectionalStreaks(arm, muzzleOut, width, height, art);
                AddLaserMuzzleSmoke(arm, muzzleOut, width, height, art);
                AddLaserGroundResidue(arm, muzzleOut, length, width, height, art);
                AddLaserTailDecay(arm, muzzleOut, width, height, art);
            }

            return Save(root, name);
        }

        /// 포문을 둘러싼 회전 마법진. 원주에 고정한 광점 두 겹을 반대로 돌려 문양을 만든다.
        private static void AddMagicMuzzleRing(GameObject arm, string name, float forward,
                                                float radius, float dotSize, short dots,
                                                float turnsPerSecond, Art art, bool clockwise,
                                                float lifetime = 2f, bool converge = false)
        {
            var ring = AddLayer(arm, name);
            ring.transform.localPosition = new Vector3(0f, 0f, forward);

            var main = ring.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = dotSize;
            main.startColor = LaserMuzzleColor;
            main.maxParticles = dots;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ring.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, dots) });

            var shape = ring.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;

            var velocity = ring.velocityOverLifetime;
            float signedOrbit = turnsPerSecond * (clockwise ? -1f : 1f);
            // Unity는 setter 호출 중에도 orbital X/Y/Z의 mode 일치를 즉시 검사한다.
            // 세 축을 전부 Constant로 고정하면 생성 순서나 기존 프리팹 상태와 무관하게 안전하다.
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(signedOrbit);
            velocity.enabled = true;
            if (converge)
                velocity.radial = new ParticleSystem.MinMaxCurve(-radius * 0.72f);

            FadeOut(ring);
            var renderer = ring.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// 발사 프레임에 포문 밖으로 퍼지는 짧은 충격 링. 고밀도 섬광과 함께 '콰앙'을 만든다.
        private static void AddMuzzleShockRing(GameObject arm, float forward, float width,
                                               float height, Art art)
        {
            var shock = AddLayer(arm, "HeadShockRing");
            shock.transform.localPosition = new Vector3(0f, 0f, forward);

            var main = shock.main;
            main.duration = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startSpeed = height * 0.9f;
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.025f, width * 0.065f);
            main.startColor = LaserMuzzleColor;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            shock.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });

            var shape = shock.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = width * 0.22f;
            shape.radiusThickness = 0f;

            FadeOut(shock);
            Shrink(shock);
            var renderer = shock.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.8f;
        }

        private static void AddLaserDirectionalStreaks(GameObject arm, float forward, float width,
                                                       float height, Art art)
        {
            var streaks = AddLayer(arm, "DirectionalFragments");
            streaks.transform.localPosition = new Vector3(0f, 0f, forward);
            var main = streaks.main;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 5f, height * 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.018f, width * 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(LaserMuzzleColor, LaserCoreColor);
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            streaks.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var shape = streaks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = width * 0.16f;
            FadeOut(streaks);

            var renderer = streaks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 4.5f;
        }

        private static void AddLaserMuzzleSmoke(GameObject arm, float forward, float width,
                                                float height, Art art)
        {
            var smoke = AddLayer(arm, "RollingBlackWake");
            smoke.transform.localPosition = new Vector3(0f, 0f, forward);
            var main = smoke.main;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.2f, height * 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.25f, width * 0.58f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.002f, 0f, 0.001f, 0.72f), new Color(0.08f, 0.002f, 0.001f, 0.5f));
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            smoke.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7) });

            var shape = smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = width * 0.18f;
            FadeOut(smoke);

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.45f));
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.LaserSmoke;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// 지형 질의 없이 보스 로컬 바닥 높이에 짧은 방향성 잔류를 둔다.
        /// 충돌이나 판정에는 참여하지 않고 0.7초 안에 사라져 경사면에서도 부담이 없다.
        private static void AddLaserGroundResidue(GameObject arm, float forward, float length,
                                                  float width, float height, Art art)
        {
            var residue = AddLayer(arm, "GroundTrace");
            residue.transform.localPosition = new Vector3(0f, -height * 0.43f, forward + length * 0.34f);
            var main = residue.main;
            main.duration = 0.16f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.12f, width * 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.01f, 0f, 0f, 0.5f), new Color(0.35f, 0.004f, 0.001f, 0.42f));
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            residue.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
            var shape = residue.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width * 0.35f, width * 0.06f, length * 0.68f);
            FadeOut(residue);
            var renderer = residue.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.LaserSmoke;
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        private static void AddLaserTailDecay(GameObject arm, float forward, float width,
                                               float height, Art art)
        {
            var tail = AddLayer(arm, "TailDecay");
            tail.transform.localPosition = new Vector3(0f, 0f, forward);
            var main = tail.main;
            main.duration = 0.42f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.3f, height * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.012f, width * 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(LaserCoreColor, LaserMuzzleColor);
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = tail.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.14f, 14) });
            var shape = tail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = width * 0.2f;
            FadeOut(tail);
            Shrink(tail);
            var renderer = tail.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 2.2f;
        }

        /// 발사 전 충전. 각 포문 자리로 기가 빨려 들어가며 심이 커진다.
        ///
        /// 안쪽으로 모으는 건 셰이프 반경에서 시작 속도를 **음수**로 줘서 만든다 —
        /// 파티클이 바깥에서 태어나 중심으로 빨려 들어간다.
        private static GameObject SaveRadialCharge(string name, int count, float height, Art art)
        {
            var root = new GameObject(name);
            float step = 360f / Mathf.Max(2, count);
            float muzzleOut = height * 0.48f;   // 실제 발사 포문과 정확히 같은 자리

            for (int i = 0; i < count; i++)
            {
                var arm = new GameObject($"Muzzle_{i}");
                arm.transform.SetParent(root.transform, false);
                arm.transform.localRotation = Quaternion.Euler(0f, i * step, 0f);

                // 빨려 들어가는 기
                var pull = AddLayer(arm, "ConvergingMass");
                pull.transform.localPosition = new Vector3(0f, 0f, muzzleOut);

                var gm = pull.main;
                gm.duration = 0.8f;
                gm.startLifetime = 0.35f;
                gm.startSpeed = -height * 1.1f;          // 음수 = 중심으로 모인다
                gm.startSize = new ParticleSystem.MinMaxCurve(height * 0.02f, height * 0.06f);
                gm.startColor = LaserMuzzleColor;
                gm.maxParticles = 400;
                gm.simulationSpace = ParticleSystemSimulationSpace.Local;   // 포문을 따라다닌다

                var ge = pull.emission;
                ge.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 35f), new Keyframe(0.55f, 90f), new Keyframe(1f, 220f)));

                var gs = pull.shape;
                gs.enabled = true;
                gs.shapeType = ParticleSystemShapeType.Sphere;
                gs.radius = height * 0.38f;
                gs.radiusThickness = 0f;                 // 껍질에서만 태어난다

                FadeOut(pull);

                var gr = pull.GetComponent<ParticleSystemRenderer>();
                gr.sharedMaterial = art.Spark;
                gr.renderMode = ParticleSystemRenderMode.Stretch;
                gr.velocityScale = 0.05f;
                gr.lengthScale = 2.5f;

                var inhale = AddLayer(arm, "ConvergingBlackMass");
                inhale.transform.localPosition = new Vector3(0f, 0f, muzzleOut);
                var im = inhale.main;
                im.duration = 0.8f;
                im.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
                im.startSpeed = new ParticleSystem.MinMaxCurve(-height * 0.35f, -height * 0.7f);
                im.startSize = new ParticleSystem.MinMaxCurve(height * 0.07f, height * 0.14f);
                im.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.003f, 0f, 0.001f, 0.45f), new Color(0.12f, 0.002f, 0.001f, 0.32f));
                im.maxParticles = 12;
                im.simulationSpace = ParticleSystemSimulationSpace.Local;
                var ie = inhale.emission;
                ie.rateOverTime = 8f;
                var ishape = inhale.shape;
                ishape.enabled = true;
                ishape.shapeType = ParticleSystemShapeType.Sphere;
                ishape.radius = height * 0.27f;
                ishape.radiusThickness = 0f;
                FadeOut(inhale);
                var ir = inhale.GetComponent<ParticleSystemRenderer>();
                ir.sharedMaterial = art.LaserSmoke;
                ir.renderMode = ParticleSystemRenderMode.Billboard;

                // 점점 커지는 심 — 언제 터질지 읽히게 한다
                var orb = AddLayer(arm, "CompressionOrb");
                orb.transform.localPosition = new Vector3(0f, 0f, muzzleOut);

                var om = orb.main;
                om.duration = 0.8f;
                om.startLifetime = 0.8f;
                om.startSpeed = 0f;
                om.startSize = height * 0.10f;
                om.startColor = LaserCoreColor;
                om.maxParticles = 4;
                om.simulationSpace = ParticleSystemSimulationSpace.Local;
                orb.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

                var os = orb.shape;
                os.enabled = false;

                // 작게 시작해 부풀다가 발사 직전에 확 커진다
                var sol = orb.sizeOverLifetime;
                sol.enabled = true;
                var grow = new AnimationCurve(
                    new Keyframe(0f, 0.15f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 1.8f));
                sol.size = new ParticleSystem.MinMaxCurve(1f, grow);

                var or_ = orb.GetComponent<ParticleSystemRenderer>();
                or_.sharedMaterial = art.Spark;
                or_.renderMode = ParticleSystemRenderMode.Billboard;

                // 충전 중 두 마법진이 빨라지며 안쪽으로 조여진다. 발사 이펙트의 포문과
                // 같은 위치라 예고가 끝난 바로 그 자리에서 레이저가 터진다.
                AddMagicMuzzleRing(arm, "RotatingGlyphOuter", muzzleOut, height * 0.25f,
                    height * 0.018f, 48, 5.2f, art, clockwise: true, lifetime: 0.8f, converge: true);
                AddMagicMuzzleRing(arm, "RotatingGlyphInner", muzzleOut - height * 0.015f, height * 0.16f,
                    height * 0.014f, 30, 7.4f, art, clockwise: false, lifetime: 0.8f, converge: true);

                var filaments = AddLayer(arm, "PreFireFilaments");
                filaments.transform.localPosition = new Vector3(0f, 0f, muzzleOut);
                var fm = filaments.main;
                fm.duration = 0.8f;
                fm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                fm.startSpeed = new ParticleSystem.MinMaxCurve(-height * 0.6f, -height * 1.4f);
                fm.startSize = new ParticleSystem.MinMaxCurve(height * 0.008f, height * 0.022f);
                fm.startColor = new ParticleSystem.MinMaxGradient(LaserCoreColor, LaserMuzzleColor);
                fm.maxParticles = 36;
                fm.simulationSpace = ParticleSystemSimulationSpace.Local;
                var fe = filaments.emission;
                fe.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.58f, 8f), new Keyframe(1f, 42f)));
                var fs = filaments.shape;
                fs.enabled = true;
                fs.shapeType = ParticleSystemShapeType.Circle;
                fs.radius = height * 0.22f;
                fs.radiusThickness = 0f;
                FadeOut(filaments);
                var fr = filaments.GetComponent<ParticleSystemRenderer>();
                fr.sharedMaterial = art.Spark;
                fr.renderMode = ParticleSystemRenderMode.Stretch;
                fr.velocityScale = 0.06f;
                fr.lengthScale = 3.2f;
            }

            return Save(root, name);
        }

        /// 자식 파티클 레이어를 하나 만든다. 이펙트 하나를 여러 겹으로 쌓는 게
        /// "화려함"의 실체다 — 파티클 시스템 하나로는 불티든 연기든 한 종류밖에 못 낸다.
        private static ParticleSystem AddLayer(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;

            return ps;
        }

        /// 튀어오르는 돌 파편. 중력에 끌려 포물선을 그리고 굴러떨어진다 —
        /// 빛나는 불티와 달리 "무게"가 있어야 바닥이 부서진 느낌이 난다.
        private static void ConfigureDebris(ParticleSystem ps, float scale, Art art, int count, float upward)
        {
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.5f, scale * 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.015f, scale * 0.06f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 400;

            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = upward;
            shape.radius = scale * 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // 공중에서 계속 돈다 — 정지한 채 날아가는 돌은 종잇조각처럼 보인다
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            // 바닥에 닿으면 튕기고 굴러 멎는다
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.28f;
            col.dampen = 0.45f;
            col.lifetimeLoss = 0.08f;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = art.DebrisMesh;
            r.sharedMaterial = art.Debris;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// 눕힌 데칼 쿼드를 만들어 루트에 붙인다(SaveDecal과 같은 이유로 회전은 자식에 둔다).
        private static void AddDecalQuad(GameObject root, Material mat, float diameter,
                                         float fillSeconds, float fadeSeconds)
            => AddDecalQuad(root, mat, new Vector2(diameter, diameter), fillSeconds, fadeSeconds);

        /// size.x = 좌우 폭, size.y = 앞뒤 길이. 눕히는 90도 회전 때문에 쿼드의 로컬 Y가
        /// 월드 Z(=보스 전방)로 간다 — 그래서 길쭉한 자국은 size.y를 키운다.
        private static void AddDecalQuad(GameObject root, Material mat, Vector2 size,
                                         float fillSeconds, float fadeSeconds)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WarningFillSheet";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var decal = quad.AddComponent<PatternFieldDecal>();
            decal.FillSeconds = fillSeconds;
            decal.FadeSeconds = fadeSeconds;
            decal.InnerRadius = 0f;
        }

        /// 근·중·원 폭발. 바닥 충격파(셰이더) + 솟구치는 화염 + 돌 파편 + 퍼지는 흙먼지.
        private static GameObject SaveBlast(string name, float diameter, Art art)
        {
            var root = new GameObject(name);
            float radius = diameter * 0.5f;

            var shockSheet = new GameObject("CoreShockSheet");
            shockSheet.transform.SetParent(root.transform, false);
            AddDecalQuad(shockSheet, art.Blast, diameter, 0.55f, 0.5f);

            var anticipation = AddLayer(root, "AnticipationCompression");
            var acm = anticipation.main;
            acm.duration = .1f; acm.startLifetime = .12f; acm.startSpeed = 0f;
            acm.startSize = radius * .28f; acm.startColor = GreatswordColor; acm.maxParticles = 1;
            var ace = anticipation.emission; ace.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var acs = anticipation.shape; acs.enabled = false;
            Shrink(anticipation); FadeOut(anticipation);
            var acr = anticipation.GetComponent<ParticleSystemRenderer>(); acr.sharedMaterial = art.Spark;

            // 화염 — 위로 솟았다가 사그라든다
            var fire = AddLayer(root, "RadialCoreEjecta");
            var fm = fire.main;
            fm.duration = 0.5f;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(radius * 2f, radius * 5f);
            fm.startSize = new ParticleSystem.MinMaxCurve(radius * 0.15f, radius * 0.45f);
            fm.startColor = GreatswordColor;
            fm.gravityModifier = -0.15f;
            fm.maxParticles = 400;
            fire.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

            var fs = fire.shape;
            fs.enabled = true;
            fs.shapeType = ParticleSystemShapeType.Cone;
            fs.angle = 32f;
            fs.radius = radius * 0.3f;
            fs.rotation = new Vector3(-90f, 0f, 0f);

            FadeOut(fire);
            Shrink(fire);

            var fr = fire.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = art.Spark;
            fr.renderMode = ParticleSystemRenderMode.Stretch;
            fr.velocityScale = 0.12f;
            fr.lengthScale = 2.2f;

            ConfigureDebris(AddLayer(root, "SecondaryDebris"), radius, art, 28, 45f);
            AddSmoke(AddLayer(root, "BlackRollingMass"), radius, art, 26);

            var afterglow = AddLayer(root, "DecayFractureEmbers");
            var agm = afterglow.main;
            agm.duration = .45f; agm.startLifetime = new ParticleSystem.MinMaxCurve(.55f, 1.1f);
            agm.startSpeed = new ParticleSystem.MinMaxCurve(radius * .12f, radius * .5f);
            agm.startSize = new ParticleSystem.MinMaxCurve(radius * .015f, radius * .05f);
            agm.startColor = GreatswordColor; agm.maxParticles = 24;
            var age = afterglow.emission; age.SetBursts(new[] { new ParticleSystem.Burst(.14f, 22) });
            var ags = afterglow.shape; ags.enabled = true; ags.shapeType = ParticleSystemShapeType.Circle;
            ags.radius = radius * .62f; ags.radiusThickness = .18f; ags.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(afterglow); Shrink(afterglow);
            var agr = afterglow.GetComponent<ParticleSystemRenderer>(); agr.sharedMaterial = art.Spark;

            return Save(root, name);
        }

        /// 일도양단 바닥 균열. 균열 데칼이 앞으로 뻗으면서 그 위로 파편과 흙먼지가 인다.
        private static GameObject SaveCrack(string name, float diameter, Art art)
        {
            var root = new GameObject(name);
            float radius = diameter * 0.5f;

            // 균열은 천천히 뻗어야 "갈라진다"로 읽힌다. 폭발보다 길게 준다.
            var residueSheet = new GameObject("ScorchResidueSheet");
            residueSheet.transform.SetParent(root.transform, false);
            AddDecalQuad(residueSheet, art.Crack, diameter, 0.45f, 1.6f);

            ConfigureDebris(AddLayer(root, "BranchEjecta"), radius, art, 34, 28f);
            AddSmoke(AddLayer(root, "RollingGrooveMass"), radius, art, 30);

            // 갈라진 틈에서 새는 빛
            var glow = AddLayer(root, "CrimsonFractureFilaments");
            var gm = glow.main;
            gm.duration = 0.6f;
            gm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            gm.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.3f, radius * 1.2f);
            gm.startSize = new ParticleSystem.MinMaxCurve(radius * 0.04f, radius * 0.14f);
            gm.startColor = GroundColor;
            gm.gravityModifier = -0.05f;
            gm.maxParticles = 300;
            glow.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    40),
                new ParticleSystem.Burst(0.12f, 30),
                new ParticleSystem.Burst(0.26f, 20),
            });

            var gs = glow.shape;
            gs.enabled = true;
            gs.shapeType = ParticleSystemShapeType.Box;
            gs.scale = new Vector3(radius * 0.25f, 0.05f, radius * 1.8f);

            FadeOut(glow);
            Shrink(glow);

            var gr = glow.GetComponent<ParticleSystemRenderer>();
            gr.sharedMaterial = art.Spark;
            gr.renderMode = ParticleSystemRenderMode.Stretch;
            gr.velocityScale = 0.08f;
            gr.lengthScale = 2.5f;

            return Save(root, name);
        }

        /// 피어오르는 흙먼지 레이어. 느리고 크고 오래 남는다 — 파편·불티와 시간축이 달라야
        /// 이펙트가 한 덩어리로 번쩍하고 끝나지 않는다.
        private static void AddSmoke(ParticleSystem ps, float scale, Art art, int count)
        {
            var main = ps.main;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.25f, scale * 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.35f, scale * 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.03f;
            main.maxParticles = 400;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.58f, 0.51f, 0.43f, 0.6f), new Color(0.38f, 0.33f, 0.28f, 0.4f));

            ps.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    (short)count),
                new ParticleSystem.Burst(0.18f, (short)(count / 2)),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = scale * 0.5f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.14f;
            limit.limit = new ParticleSystem.MinMaxCurve(scale * 0.5f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.7f));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Dust;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f;
        }

        /// 타격 바깥을 한 박자 늦게 감싸는 먹빛 외피. 가산 불티와 색·시간축을 분리한다.
        private static void AddImpactShroud(GameObject root, float scale, Art art, short count)
        {
            var ps = AddLayer(root, "ImpactShroud");
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.12f, scale * 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.22f, scale * 0.52f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.002f, 0f, 0.001f, 0.58f), new Color(0.10f, 0.002f, 0.001f, 0.35f));
            main.maxParticles = count;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0.025f, count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = scale * 0.22f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.35f));
            FadeOut(ps);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.LaserSmoke;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// 한 프레임 섬광 뒤 퍼지는 얇은 충격 윤곽. 원거리에서도 타격 크기를 읽게 한다.
        private static void AddImpactRing(GameObject root, float radius, Color color, Art art, short count,
                                          bool horizontal = false)
        {
            var ps = AddLayer(root, "ImpactRing");
            var main = ps.main;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = radius * 1.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.025f, radius * 0.06f);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.15f;
            shape.radiusThickness = 0f;
            if (horizontal) shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(ps);
            Shrink(ps);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.025f;
            renderer.lengthScale = 1.6f;
        }

        /// 경고 장판 가장자리에만 남는 저밀도 불티. 지속 emitter 없이 한 번 생성한다.
        private static void AddTelegraphEmbers(GameObject root, float radius, float lifetime,
                                               Color color, Art art)
        {
            var ps = AddLayer(root, "BoundaryEmbers");
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startLifetime = Mathf.Max(0.35f, lifetime + 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.02f, radius * 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.012f, radius * 0.028f);
            main.startColor = color;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * FieldOuterRadius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(ps);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// 바닥 장판. 큐가 `Instantiate(prefab, pos, anchor.rotation)`로 루트 회전을 덮으므로,
        /// 눕히는 90도 회전은 반드시 **자식**에 둬야 한다. (이전 버전이 빌보드를 써서 장판이
        /// 카메라를 향해 세로로 서 버린 게 "장판이 안 나온다"의 원인이었다.)
        private static GameObject SaveDecal(string name, Material mat, float diameter,
                                            float innerRadius, float fillSeconds, Art art)
        {
            var root = new GameObject(name);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Decal";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // XZ 평면에 눕힌다
            quad.transform.localPosition = new Vector3(0f, 0.02f, 0f);      // z-fighting 회피
            quad.transform.localScale = Vector3.one * diameter;

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var decal = quad.AddComponent<PatternFieldDecal>();
            decal.FillSeconds = fillSeconds;
            decal.FadeSeconds = 0.6f;
            decal.InnerRadius = innerRadius;

            Color accent = name.Contains("Mark", System.StringComparison.Ordinal) ? MarkColor : GroundColor;
            float radius = diameter * 0.5f;

            var boundary = AddLayer(root, "BoundaryFlow");
            var bm = boundary.main;
            bm.duration = fillSeconds; bm.startLifetime = Mathf.Max(.35f, fillSeconds + .2f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(radius * .015f, radius * .055f);
            bm.startSize = new ParticleSystem.MinMaxCurve(radius * .01f, radius * .026f);
            bm.startColor = accent; bm.maxParticles = 24; bm.simulationSpace = ParticleSystemSimulationSpace.Local;
            var be = boundary.emission; be.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var bs = boundary.shape; bs.enabled = true; bs.shapeType = ParticleSystemShapeType.Circle;
            bs.radius = radius * FieldOuterRadius; bs.radiusThickness = 0f; bs.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(boundary);
            var br = boundary.GetComponent<ParticleSystemRenderer>(); br.sharedMaterial = art.Spark;

            var sectors = AddLayer(root, "SectorImpactPulse");
            var sm = sectors.main;
            sm.duration = .2f; sm.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .34f);
            sm.startSpeed = radius * .9f; sm.startSize = new ParticleSystem.MinMaxCurve(radius * .012f, radius * .035f);
            sm.startColor = accent; sm.maxParticles = 18; sm.simulationSpace = ParticleSystemSimulationSpace.Local;
            var se = sectors.emission; se.SetBursts(new[] { new ParticleSystem.Burst(Mathf.Max(.01f, fillSeconds * .9f), 16) });
            var ssh = sectors.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Circle;
            ssh.radius = radius * .78f; ssh.radiusThickness = .12f; ssh.rotation = new Vector3(90f, 0f, 0f);
            FadeOut(sectors); Shrink(sectors);
            var sr = sectors.GetComponent<ParticleSystemRenderer>(); sr.sharedMaterial = art.Spark;
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = .025f; sr.lengthScale = 1.5f;

            return Save(root, name);
        }

        /// 타격 순간의 스파크. 궤적은 트레일이 그리므로 여기선 흩어지는 불티만 담당한다.
        /// arcMat이 null이면 참격파 없이 불티만 낸다(방패 후려치기는 베는 게 아니다).
        private static GameObject SaveSpark(string name, float reach, Color color, Art art, Material arcMat)
        {
            var root = new GameObject(name);

            // 0.00~0.05: 작은 응축점. 타격 시점 안에서 매우 짧게 압축된 뒤 열린다.
            var anticipation = AddLayer(root, "AnticipationCore");
            var am = anticipation.main;
            am.duration = 0.09f; am.startLifetime = 0.11f; am.startSpeed = 0f;
            am.startSize = reach * 0.16f; am.startColor = color * 1.25f; am.maxParticles = 1;
            var ae = anticipation.emission; ae.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var ash = anticipation.shape; ash.enabled = false;
            Shrink(anticipation); FadeOut(anticipation);
            var ar = anticipation.GetComponent<ParticleSystemRenderer>(); ar.sharedMaterial = art.Spark;

            // 0.04: 참격 면. 방패 타격은 베는 면이 없으므로 생략한다.
            if (arcMat != null)
            {
                var sheetRoot = new GameObject("CoreSlashSheet");
                sheetRoot.transform.SetParent(root.transform, false);
                AddSlashArc(sheetRoot, arcMat, reach);
            }

            // 방향성 파편: 넓은 원뿔이 아니라 얇은 절단 평면을 따라 멀리 뻗는다.
            var shards = AddLayer(root, "DirectionalShards");
            var sm = shards.main;
            sm.duration = 0.22f; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(reach * 2.2f, reach * 5.2f);
            sm.startSize = new ParticleSystem.MinMaxCurve(reach * 0.025f, reach * 0.075f);
            sm.startColor = color; sm.gravityModifier = 0.35f; sm.maxParticles = 44;
            var se = shards.emission; se.SetBursts(new[] { new ParticleSystem.Burst(0.045f, 38) });
            var ss = shards.shape; ss.enabled = true; ss.shapeType = ParticleSystemShapeType.Box;
            ss.scale = new Vector3(reach * 0.65f, reach * 0.05f, reach * 0.08f);
            FadeOut(shards); Shrink(shards);
            var sr = shards.GetComponent<ParticleSystemRenderer>(); sr.sharedMaterial = art.Spark;
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.09f; sr.lengthScale = 4.2f;

            // 검은 후류는 파편보다 늦고 크게 열려 타격에 부피를 준다.
            var wake = AddLayer(root, "BlackWake");
            var wm = wake.main;
            wm.duration = 0.22f; wm.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.72f);
            wm.startSpeed = new ParticleSystem.MinMaxCurve(reach * 0.12f, reach * 0.42f);
            wm.startSize = new ParticleSystem.MinMaxCurve(reach * 0.18f, reach * 0.42f);
            wm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.002f, 0f, 0.001f, .62f), new Color(.09f, .001f, 0f, .38f));
            wm.maxParticles = 8;
            var we = wake.emission; we.SetBursts(new[] { new ParticleSystem.Burst(0.055f, 7) });
            var ws = wake.shape; ws.enabled = true; ws.shapeType = ParticleSystemShapeType.Box;
            ws.scale = new Vector3(reach * .55f, reach * .08f, reach * .12f);
            FadeOut(wake);
            var wr = wake.GetComponent<ParticleSystemRenderer>(); wr.sharedMaterial = art.LaserSmoke;

            // 끊어진 필라멘트와 잔불이 impact 이후 decay를 담당한다.
            var embers = AddLayer(root, "DecayEmbers");
            var em = embers.main;
            em.duration = .45f; em.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.15f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(reach * .18f, reach * .75f);
            em.startSize = new ParticleSystem.MinMaxCurve(reach * .012f, reach * .04f);
            em.startColor = color; em.gravityModifier = .08f; em.maxParticles = 24;
            var ee = embers.emission; ee.SetBursts(new[] { new ParticleSystem.Burst(.10f, 22) });
            var es = embers.shape; es.enabled = true; es.shapeType = ParticleSystemShapeType.Box;
            es.scale = new Vector3(reach * .5f, reach * .05f, reach * .1f);
            var noise = embers.noise; noise.enabled = true; noise.strength = reach * .24f; noise.frequency = .7f;
            FadeOut(embers); Shrink(embers);
            var er = embers.GetComponent<ParticleSystemRenderer>(); er.sharedMaterial = art.Spark;

            return Save(root, name);
        }

        /// 참격파. 불티가 "맞았다"를 알린다면 이쪽은 "어떻게 베었는지"를 그린다.
        ///
        /// 눕히지 않는다 — 큐가 보스 회전 그대로 생성하므로 쿼드 기본 방향(+Z)이 곧
        /// 보스 전방이고, 세워 둬야 앞에 선 사람 눈에 초승달로 보인다.
        private static void AddSlashArc(GameObject root, Material arcMat, float reach)
        {
            if (arcMat == null) return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SlashArc";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);

            // 호는 UV의 +X를 가운데로 벌어진다. 내리치는 대각선이 되도록 돌려 둔다.
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, SlashArcTiltDegrees);

            // 셰이더의 _Radius(0.78)가 쿼드 반폭 기준이라, 호의 실제 반경은 size * 0.39다.
            quad.transform.localScale = Vector3.one * (reach * 1.4f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = arcMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            // 긋는 데 0.16초. 느리면 "한 번에 베었다"가 아니라 자라나는 띠로 보인다.
            var decal = quad.AddComponent<PatternFieldDecal>();
            decal.FillSeconds = 0.16f;
            decal.FadeSeconds = 0.5f;
            decal.InnerRadius = 0f;
        }

        /// 참격파를 내리치는 대각선으로 눕히는 각도. 0이면 호가 오른쪽으로 벌어진다.
        private const float SlashArcTiltDegrees = -50f;

        /// 잔불. 본 불티가 다 꺼진 뒤에도 느리게 떠다니다 사라진다 —
        /// 이게 있어야 타격이 "번쩍 끝"이 아니라 여운을 남긴다.
        private static void AddEmbers(ParticleSystem ps, float reach, Color color, Art art)
        {
            var main = ps.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(reach * 0.2f, reach * 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(reach * 0.015f, reach * 0.05f);
            main.startColor = color;
            main.gravityModifier = 0.12f;
            main.maxParticles = 300;

            ps.emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    28),
                new ParticleSystem.Burst(0.09f, 18),
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = reach * 0.18f;

            // 공기에 실려 흔들린다 — 직선으로 떠가는 잔불은 죽어 보인다
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = reach * 0.35f;
            noise.frequency = 0.6f;

            FadeOut(ps);
            Shrink(ps);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Spark;
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// 타격 순간의 섬광. 크고 밝게 한 프레임 터졌다 바로 죽는다.
        private static void AddFlash(ParticleSystem ps, float reach, Color color, Art art)
        {
            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.12f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(reach * 0.5f, reach * 0.8f);
            main.startColor = color * 1.6f;
            main.maxParticles = 8;

            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            // 한 점에서 딱 하나만 띄운다 — 섬광은 퍼질 필요가 없다
            var shape = ps.shape;
            shape.enabled = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.5f));

            FadeOut(ps);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Spark;
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private static GameObject SaveBeam(string name, float reach, Color color, Art art)
        {
            var root = new GameObject(name);

            var compression = AddLayer(root, "CompressionCore");
            var cm = compression.main;
            cm.duration = 0.14f;
            cm.startLifetime = 0.18f;
            cm.startSpeed = 0f;
            cm.startSize = reach * 0.12f;
            cm.startColor = color;
            cm.maxParticles = 2;
            var ce = compression.emission;
            ce.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var cs = compression.shape;
            cs.enabled = false;
            FadeOut(compression);
            Shrink(compression);
            var cr = compression.GetComponent<ParticleSystemRenderer>();
            cr.sharedMaterial = art.Spark;

            var core = AddLayer(root, "CoreStreak");
            var main = core.main;
            main.duration = 0.24f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(reach * 7f, reach * 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(reach * 0.035f, reach * 0.075f);
            main.startColor = color;
            main.maxParticles = 64;
            var emission = core.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 38) });
            var shape = core.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 2.5f;
            shape.radius = reach * 0.035f;
            FadeOut(core);
            var renderer = core.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = art.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 6.5f;

            var fragments = AddLayer(root, "DirectionalFragments");
            var fm = fragments.main;
            fm.duration = 0.18f;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(reach * 3.5f, reach * 7f);
            fm.startSize = new ParticleSystem.MinMaxCurve(reach * 0.012f, reach * 0.035f);
            fm.startColor = new ParticleSystem.MinMaxGradient(color, LaserMuzzleColor);
            fm.maxParticles = 22;
            var fe = fragments.emission;
            fe.SetBursts(new[] { new ParticleSystem.Burst(0.06f, 16) });
            var fs = fragments.shape;
            fs.enabled = true;
            fs.shapeType = ParticleSystemShapeType.Cone;
            fs.angle = 11f;
            fs.radius = reach * 0.055f;
            FadeOut(fragments);
            var fr = fragments.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = art.Spark;
            fr.renderMode = ParticleSystemRenderMode.Stretch;
            fr.lengthScale = 3.2f;

            var wake = AddLayer(root, "BlackWake");
            var wm = wake.main;
            wm.duration = 0.28f;
            wm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            wm.startSpeed = new ParticleSystem.MinMaxCurve(reach * 0.6f, reach * 1.4f);
            wm.startSize = new ParticleSystem.MinMaxCurve(reach * 0.08f, reach * 0.16f);
            wm.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.002f, 0f, 0.001f, 0.65f), new Color(0.1f, 0.002f, 0.001f, 0.4f));
            wm.maxParticles = 10;
            var we = wake.emission;
            we.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 7) });
            var ws = wake.shape;
            ws.enabled = true;
            ws.shapeType = ParticleSystemShapeType.Cone;
            ws.angle = 16f;
            ws.radius = reach * 0.04f;
            FadeOut(wake);
            var wr = wake.GetComponent<ParticleSystemRenderer>();
            wr.sharedMaterial = art.LaserSmoke;

            return Save(root, name);
        }

        private static GameObject SaveGround(string name, float radius, Color color, Art art)
        {
            var go = NewEffect(name);
            go.GetComponent<ParticleSystem>().name = "CoreGroundEjecta";
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.5f, radius * 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.12f, radius * 0.35f);
            main.startColor = color;
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = radius * 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // 위로 솟구친다

            FadeOut(ps);
            Shrink(ps);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Spark;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.1f;
            r.lengthScale = 2.5f;

            // 바닥을 때렸으면 돌이 튀고 먼지가 인다 — 불티만 나는 건 바닥이 아니라 허공이다.
            ConfigureDebris(AddLayer(go, "RadialStoneEjecta"), radius, art, 22, 35f);
            AddSmoke(AddLayer(go, "GroundDecayMass"), radius, art, 22);

            var residue = AddLayer(go, "ScorchDecayEmbers");
            var rm = residue.main;
            rm.duration = .38f; rm.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .95f);
            rm.startSpeed = new ParticleSystem.MinMaxCurve(radius * .08f, radius * .34f);
            rm.startSize = new ParticleSystem.MinMaxCurve(radius * .014f, radius * .045f);
            rm.startColor = color; rm.maxParticles = 20;
            var re = residue.emission; re.SetBursts(new[] { new ParticleSystem.Burst(.12f, 18) });
            var rs = residue.shape; rs.enabled = true; rs.shapeType = ParticleSystemShapeType.Circle;
            rs.radius = radius * .55f; rs.radiusThickness = .25f; rs.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(residue); Shrink(residue);
            var rr = residue.GetComponent<ParticleSystemRenderer>(); rr.sharedMaterial = art.Spark;

            return Save(go, name);
        }

        private static GameObject NewEffect(string name)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;   // 파괴는 큐의 Lifetime이 맡는다

            return go;
        }

        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;

            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Shrink(ParticleSystem ps)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));
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

        // ══════════ 4. 무기 궤적 트레일 ══════════

        /// 무기마다 칼자루·칼끝 기준점을 만들고 BossWeaponTrail(리본)에 등록한다.
        /// 그리고 발 본을 찾아 BossGroundDust를 붙인다.
        ///
        /// TrailRenderer를 쓰지 않는 이유: 그건 점 하나의 궤적이라 칼끝만 빛나는 실이 나온다.
        /// 검이 쓸고 간 면을 그리려면 두 점을 기록해 사각형을 이어야 한다.
        private static int AttachWeaponTrails(BossSize s, Art art)
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null) return 0;

            try
            {
                var pairs = new List<(Transform root, Transform tip)>();
                var auras = new List<Renderer>();
                AddBlade(root, GreatswordName, art.AuraGreat, art.WeaponMeshAura, pairs, auras);
                AddBlade(root, SwordName,      art.AuraSword, art.WeaponMeshAura, pairs, auras);

                var trail = root.GetComponent<BossWeaponTrail>() ?? root.AddComponent<BossWeaponTrail>();
                var netState = root.GetComponent<BossNetState>();

                var tso = new SerializedObject(trail);
                tso.FindProperty("_netState").objectReferenceValue = netState;
                tso.FindProperty("_material").objectReferenceValue = art.Trail;

                var blades = tso.FindProperty("_blades");
                blades.arraySize = pairs.Count;
                for (int i = 0; i < pairs.Count; i++)
                {
                    var e = blades.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Root").objectReferenceValue = pairs[i].root;
                    e.FindPropertyRelative("Tip").objectReferenceValue = pairs[i].tip;
                }
                tso.ApplyModifiedPropertiesWithoutUndo();

                // 검기 — 리본과 같은 무기에 붙지만 세기를 미는 주체가 달라 컴포넌트를 따로 둔다.
                var aura = root.GetComponent<BossBladeAura>() ?? root.AddComponent<BossBladeAura>();
                var aso = new SerializedObject(aura);
                aso.FindProperty("_netState").objectReferenceValue = netState;
                var auraList = aso.FindProperty("_auras");
                auraList.arraySize = auras.Count;
                for (int i = 0; i < auras.Count; i++)
                    auraList.GetArrayElementAtIndex(i).objectReferenceValue = auras[i];
                aso.ApplyModifiedPropertiesWithoutUndo();

                AttachDust(root, netState, s, art);

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
                return pairs.Count;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// 칼자루는 무기 트랜스폼 자신, 칼끝은 메시 바운즈 최장축의 끝에 만든 빈 오브젝트.
        /// 같은 김에 칼날 평면에 검기 쿼드도 세운다 — 둘 다 무기 바운즈에서 나오는 값이라 한자리에서 잰다.
        private static void AddBlade(GameObject root, string weaponName, Material auraMat, Material meshAuraMat,
                                     List<(Transform, Transform)> into, List<Renderer> auras)
        {
            var weapon = Find(root, weaponName);
            if (weapon == null)
            {
                Debug.LogWarning($"[TFW FX] 무기 '{weaponName}'이 없어 궤적을 건너뜁니다.");
                return;
            }

            var existing = weapon.Find(TrailChildName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var tip = new GameObject(TrailChildName);
            tip.transform.SetParent(weapon, false);
            tip.transform.localPosition = BladeTipLocal(weapon);

            into.Add((weapon, tip.transform));

            var aura = AddBladeAuraQuad(weapon, auraMat);
            if (aura != null) auras.Add(aura);
            AddWeaponMeshAura(weapon, meshAuraMat);
        }

        /// 실제 무기 메시를 한 번 더 그려 몸 오라와 같은 검붉은 셸을 입힌다.
        /// 공격 판정이나 활성 타이밍과 무관한 로컬 렌더링 레이어다.
        private static void AddWeaponMeshAura(Transform weapon, Material material)
        {
            foreach (var old in weapon.GetComponentsInChildren<Transform>(true))
            {
                if (old.name == MeshAuraChildName || old.name == BossAuraVelocity.AuraObjectName)
                    Object.DestroyImmediate(old.gameObject);
            }

            if (material == null) return;

            var sources = new List<MeshRenderer>();
            foreach (var source in weapon.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (source.gameObject.name == AuraChildName || source.gameObject.name == MeshAuraChildName) continue;
                if (source.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                sources.Add(source);
            }

            foreach (var source in sources)
            {
                var go = new GameObject(MeshAuraChildName);
                go.transform.SetParent(source.transform, false);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;

                var renderer = go.AddComponent<MeshRenderer>();
                int subMeshes = Mathf.Max(1, filter.sharedMesh.subMeshCount);
                var materials = new Material[subMeshes];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
            }

            var skinnedSources = new List<SkinnedMeshRenderer>();
            foreach (var source in weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (source.gameObject.name == BossAuraVelocity.AuraObjectName ||
                    source.gameObject.name == MeshAuraChildName || source.sharedMesh == null) continue;
                skinnedSources.Add(source);
            }

            foreach (var source in skinnedSources)
            {
                var go = new GameObject(MeshAuraChildName);
                go.transform.SetParent(source.transform, false);

                var renderer = go.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                renderer.bones = source.bones;
                renderer.rootBone = source.rootBone;
                renderer.quality = source.quality;
                renderer.localBounds = source.localBounds;
                int subMeshes = Mathf.Max(1, source.sharedMesh.subMeshCount);
                var materials = new Material[subMeshes];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.updateWhenOffscreen = true;
            }
        }

        /// 검기 쿼드. 칼날 평면에 겹쳐 세우고, 셰이더가 UV로 실루엣을 그린다.
        ///
        /// 쿼드의 로컬 축이 UV다 — +Y = UV.y(자루 → 칼끝), +X = UV.x(좌우), +Z = 칼 두께 방향.
        /// 그래서 무기 로컬 바운즈의 세 축을 길이 순으로 갈라 그대로 맞춰 준다.
        private static Renderer AddBladeAuraQuad(Transform weapon, Material auraMat)
        {
            var oldAuras = new List<GameObject>();
            foreach (var child in weapon.GetComponentsInChildren<Transform>(true))
                if (child.name == AuraChildName) oldAuras.Add(child.gameObject);
            foreach (var old in oldAuras) Object.DestroyImmediate(old);

            if (auraMat == null || !TryBladeLocalBounds(weapon, out var meshTransform, out var local)) return null;

            Vector3 c = local.center;
            Vector3 e = local.extents;

            // 최장축 = 칼날 방향, 최단축 = 두께(= 쿼드가 바라볼 방향), 나머지 = 칼폭
            int longAxis = e.x >= e.y && e.x >= e.z ? 0 : (e.y >= e.z ? 1 : 2);
            int thinAxis = e.x <= e.y && e.x <= e.z ? 0 : (e.y <= e.z ? 1 : 2);
            if (thinAxis == longAxis) return null;              // 정육면체에 가까우면 칼날로 못 읽는다
            int crossAxis = 3 - longAxis - thinAxis;

            float tipSign = Mathf.Abs(local.max[longAxis]) >= Mathf.Abs(local.min[longAxis]) ? 1f : -1f;
            float tipCoord = tipSign > 0f ? local.max[longAxis] : local.min[longAxis];
            float hiltCoord = tipSign > 0f ? local.min[longAxis] : local.max[longAxis];
            float bladeStart = Mathf.Lerp(hiltCoord, tipCoord, AuraBladeStart);
            float blade = Mathf.Abs(tipCoord - bladeStart);
            Vector3 position = c;
            position[longAxis] = (bladeStart + tipCoord) * 0.5f;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = AuraChildName;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(meshTransform, false);
            quad.transform.localPosition = position;
            quad.transform.localRotation = Quaternion.LookRotation(Axis(thinAxis), Axis(longAxis) * tipSign);

            // 실루엣 밖에 기운이 번질 여백을 준다. MakeAura가 이 배수의 역수를
            // _BladeWidth로 넣으므로 여기만 고치면 셰이더도 따라온다.
            quad.transform.localScale = new Vector3(e[crossAxis] * 2f * AuraQuadWidthFactor,
                                                    blade * 1.02f,
                                                    1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = auraMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;                                  // 휘두를 때 BossBladeAura가 켠다
            return r;
        }

        /// 회전된 월드 AABB는 검의 대각선까지 폭으로 포함하므로 검기 쿼드가 아래로
        /// 밀리고 커진다. 실제 검 메시 중 가장 긴 것을 골라 그 메시의 로컬 bounds를 쓴다.
        private static bool TryBladeLocalBounds(Transform weapon, out Transform meshTransform, out Bounds bounds)
        {
            meshTransform = null;
            bounds = default;
            float bestLength = 0f;

            foreach (var filter in weapon.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.gameObject.name == MeshAuraChildName ||
                    filter.gameObject.name == AuraChildName) continue;
                Vector3 scaled = Vector3.Scale(filter.sharedMesh.bounds.size, Abs(filter.transform.lossyScale));
                float length = Mathf.Max(scaled.x, Mathf.Max(scaled.y, scaled.z));
                if (length <= bestLength) continue;
                bestLength = length;
                meshTransform = filter.transform;
                bounds = filter.sharedMesh.bounds;
            }

            foreach (var skinned in weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null || skinned.gameObject.name == MeshAuraChildName ||
                    skinned.gameObject.name == BossAuraVelocity.AuraObjectName) continue;
                Vector3 scaled = Vector3.Scale(skinned.localBounds.size, Abs(skinned.transform.lossyScale));
                float length = Mathf.Max(scaled.x, Mathf.Max(scaled.y, scaled.z));
                if (length <= bestLength) continue;
                bestLength = length;
                meshTransform = skinned.transform;
                bounds = skinned.localBounds;
            }

            return meshTransform != null;
        }

        private static Vector3 Abs(Vector3 value)
            => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        /// 발밑 분진. 발 본을 찾아 넘겨주면 런타임이 접지를 감지해 알아서 터뜨린다.
        private static void AttachDust(GameObject root, BossNetState netState, BossSize s, Art art)
        {
            const string dustChild = "TFW_GroundDust";

            var old = root.transform.Find(dustChild);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(dustChild);
            go.transform.SetParent(root.transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ConfigureDust(ps, s, art);

            var dust = root.GetComponent<BossGroundDust>() ?? root.AddComponent<BossGroundDust>();
            var so = new SerializedObject(dust);
            so.FindProperty("_netState").objectReferenceValue = netState;
            so.FindProperty("_dust").objectReferenceValue = ps;

            // 양을 여기서 덮어쓴다. 컴포넌트가 이미 붙어 있으면 프리팹에 저장된 옛 값이
            // 그대로 남아, C# 기본값만 고쳐서는 분진이 줄지 않는다.
            so.FindProperty("_stepParticles").intValue = 9;
            so.FindProperty("_moveParticlesPerSpeed").floatValue = 2.5f;
            so.FindProperty("_actionParticles").intValue = 16;
            so.FindProperty("_actionSpread").floatValue = Mathf.Max(0.5f, s.BodyRadius * 0.9f);

            var feet = so.FindProperty("_feet");
            var found = new List<Transform>();
            foreach (var n in new[] { "Character1_LeftFoot", "Character1_RightFoot" })
            {
                var t = Find(root, n);
                if (t != null) found.Add(t);
                else Debug.LogWarning($"[TFW FX] 발 본 '{n}'을 찾지 못했습니다 — 착지 분진이 빠집니다.");
            }

            feet.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
                feet.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// 먼지·가루 — 불티와 정반대로 만든다. 느리게 퍼지고, 오래 남고, 위로 살짝 뜬 뒤 가라앉는다.
        private static void ConfigureDust(ParticleSystem ps, BossSize s, Art art)
        {
            float scale = s.BodyRadius;

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            // 지난 조정에서 너무 키웠다. 발밑 분진은 보스를 가리면 안 된다 —
            // 짧게 낮게 깔리고 옅게 빠지는 게 맞다.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.1f, scale * 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.18f, scale * 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.04f;                  // 살짝 가라앉는다 — 떠오르면 연기로 보인다
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.55f, 0.46f, 0.28f), new Color(0.42f, 0.37f, 0.31f, 0.16f));

            // 스스로는 안 뿜는다 — BossGroundDust가 발 위치에 직접 Emit한다.
            var em = ps.emission;
            em.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = scale * 0.25f;

            // 공기 저항 — 확 퍼졌다가 멎는다. 먼지처럼 보이는 핵심이다.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = new ParticleSystem.MinMaxCurve(scale * 0.4f);

            // 퍼지면서 커진다 — 불티(작아짐)와 반대다.
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.4f));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Dust;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f;                          // 바닥 이펙트보다 뒤에 깔린다

            AttachGrit(ps, scale, art);
        }

        /// 흙알갱이. 먼지 입자가 태어날 때 같이 튀는 작고 빠른 알갱이들 —
        /// 뭉게구름만 있으면 안개처럼 보인다. 알갱이가 섞여야 "흙"이 된다.
        private static void AttachGrit(ParticleSystem dust, float scale, Art art)
        {
            var go = new GameObject("Grit");
            go.transform.SetParent(dust.transform, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.5f, scale * 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.02f, scale * 0.07f);
            main.gravityModifier = 1.1f;                   // 먼지와 반대로 곧장 떨어진다
            main.maxParticles = 600;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.42f, 0.36f, 0.30f, 0.95f), new Color(0.26f, 0.22f, 0.18f, 0.9f));

            // 스스로 뿜지 않는다 — 먼지 입자가 태어날 때 서브 이미터로 딸려 나온다.
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = scale * 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            FadeOut(ps);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = art.Dust;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var sub = dust.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(ps, ParticleSystemSubEmitterType.Birth,
                              ParticleSystemSubEmitterProperties.InheritNothing);
        }

        /// 무기 메시에서 가장 먼 끝(칼끝)을 무기 로컬 좌표로 돌려준다.
        private static Vector3 BladeTipLocal(Transform weapon)
        {
            if (!TryBladeLocalBounds(weapon, out var meshTransform, out var local)) return Vector3.zero;

            Vector3 e = local.extents;
            int longAxis = e.x >= e.y && e.x >= e.z ? 0 : (e.y >= e.z ? 1 : 2);
            float tipSign = Mathf.Abs(local.max[longAxis]) >= Mathf.Abs(local.min[longAxis]) ? 1f : -1f;
            Vector3 tip = local.center;
            tip[longAxis] = (tipSign > 0f ? local.max[longAxis] : local.min[longAxis]) * 0.96f;
            return weapon.InverseTransformPoint(meshTransform.TransformPoint(tip));
        }

        // ══════════ 5. 거합(페이즈 전환) 장판 ══════════

        /// 장판 반경 = BodyRadius × 이 값. 거합은 보스를 중심으로 크게 한 번 터지는 광범위기다.
        private const float PhaseFieldRadiusFactor = 4.0f;

        /// 원이 다 차기까지 걸리는 시간 = 빠져나갈 수 있는 시간. 반경을 키우면 같이 키워야
        /// 피할 수 있는 패턴이 된다 — 반경 7m에 1초면 아무도 못 나간다.
        private const float PhaseFieldFuseSeconds = 2.2f;

        /// 거합은 패턴 SO가 아니라 BossDefinition의 AoeBurstConfig로 터진다.
        /// 그 FieldPrefab이 비어 있으면 BossAoeBurst.Spawn이 첫 줄에서 그냥 돌아가서
        /// 연출도 판정도 아예 없다 — 지금까지 그 상태였다. 여기서 만들어 꽂는다.
        private static bool WirePhaseTransitionField(BossSize s, Art art, Dictionary<string, GameObject> fx)
        {
            var def = AssetDatabase.LoadAssetAtPath<BossDefinition>(BossDefPath);
            if (def == null)
            {
                Debug.LogWarning($"[TFW FX] 보스 정의를 찾지 못해 거합 장판을 건너뜁니다: {BossDefPath}");
                return false;
            }

            var dso = new SerializedObject(def);
            var burst = dso.FindProperty("PhaseTransitionBurst");
            if (burst == null) return false;

            // 보스를 중심으로 큰 원 하나. 플레이어 발밑도, 주변 무작위도 아니다.
            // RandomCount 1 + Spread 0이면 BossAoeBurst가 보스 자리에 정확히 한 장을 깐다.
            burst.FindPropertyRelative("UnderEachPlayer").boolValue = false;
            burst.FindPropertyRelative("RandomCount").intValue = 1;
            burst.FindPropertyRelative("Spread").floatValue = 0f;
            burst.FindPropertyRelative("FuseSeconds").floatValue = PhaseFieldFuseSeconds;

            // 차오름은 도화선과 같은 길이여야 한다 — 원이 다 차는 순간이 곧 판정 시점이다.
            var prefab = BuildPhaseField(s, art, fx, PhaseFieldFuseSeconds);
            if (prefab == null) return false;

            burst.FindPropertyRelative("FieldPrefab").objectReferenceValue = prefab.GetComponent<NetworkObject>();
            dso.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
            return true;
        }

        /// 프리팹을 맨땅에서 만들지 않고 기존 BossAoeField를 열어 **비주얼만** 갈아끼운다.
        /// NetworkObject 구성은 이미 검증된 것이라 그대로 물려받는 편이 안전하다.
        private static GameObject BuildPhaseField(BossSize s, Art art,
                                                  Dictionary<string, GameObject> fx, float fuseSeconds)
        {
            const string name = "FX_TFW_PhaseField";

            var root = PrefabUtility.LoadPrefabContents(AoeTemplate);
            if (root == null)
            {
                Debug.LogWarning($"[TFW FX] 장판 템플릿을 열지 못했습니다: {AoeTemplate}");
                return null;
            }

            try
            {
                var field = root.GetComponent<BossAoeField>();
                var hit   = root.GetComponent<SphereCollider>();
                if (field == null || hit == null)
                {
                    Debug.LogWarning("[TFW FX] 템플릿에 BossAoeField/SphereCollider가 없습니다.");
                    return null;
                }

                // 템플릿의 비주얼은 통째로 버린다 — 다른 보스 크기에 맞춰 둔 것이다.
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                float radius = s.BodyRadius * PhaseFieldRadiusFactor;
                hit.radius = radius;      // 이 값이 곧 AoeBurstConfig.FieldRadius이자 실제 판정 반경
                hit.center = Vector3.zero;

                // 경고 — 바깥 테두리 원 + 안에서 차오르는 원. 다 차면 그 순간이 판정이다.
                //
                // 셰이더는 쿼드 반폭의 _OuterRadius(0.95)까지만 그린다. 쿼드를 그 몫만큼
                // 키워야 눈에 보이는 테두리가 실제 판정 반경과 정확히 겹친다 —
                // 보이는 원과 맞는 원이 다르면 차오름 연출이 거짓말이 된다.
                var warning = new GameObject("Warning");
                warning.transform.SetParent(root.transform, false);
                AddDecalQuad(warning, art.Field, radius * 2f / FieldOuterRadius, fuseSeconds, 0f);
                var boundary = AddLayer(warning, "PhaseBoundaryFlow");
                var bm = boundary.main;
                bm.duration = fuseSeconds; bm.startLifetime = fuseSeconds + .25f; bm.startSpeed = radius * .035f;
                bm.startSize = new ParticleSystem.MinMaxCurve(radius * .012f, radius * .028f);
                bm.startColor = GroundColor; bm.maxParticles = 28; bm.simulationSpace = ParticleSystemSimulationSpace.Local;
                var be = boundary.emission; be.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
                var bs = boundary.shape; bs.enabled = true; bs.shapeType = ParticleSystemShapeType.Circle;
                bs.radius = radius; bs.radiusThickness = 0f; bs.rotation = new Vector3(90f, 0f, 0f);
                FadeOut(boundary);
                var br = boundary.GetComponent<ParticleSystemRenderer>(); br.sharedMaterial = art.Spark;

                // 폭발 — 프리팹 기본 비활성. BossAoeField가 판정 틱에 켠다.
                GameObject explosion = null;
                if (fx.TryGetValue("Blast", out var blast) && blast != null)
                {
                    explosion = (GameObject)PrefabUtility.InstantiatePrefab(blast, root.transform);
                    explosion.name = "Explosion";
                    explosion.transform.localPosition = Vector3.zero;
                    explosion.transform.localRotation = Quaternion.identity;

                    // 폭발 프리팹은 BodyRadius*2 지름으로 만들어져 있다. 장판 지름에 맞춘다.
                    explosion.transform.localScale =
                        Vector3.one * (radius / Mathf.Max(0.01f, s.BodyRadius));
                    explosion.SetActive(false);
                }

                var fso = new SerializedObject(field);
                fso.FindProperty("_warningDecal").objectReferenceValue = warning;
                fso.FindProperty("_explosionVfx").objectReferenceValue = explosion;
                fso.FindProperty("_hitVolume").objectReferenceValue = hit;
                fso.ApplyModifiedPropertiesWithoutUndo();

                // 다른 프리팹과 달리 DeleteAsset을 하지 않는다 — 지웠다 만들면 guid가 바뀌어
                // BossDefinition이 들고 있던 참조가 끊긴다(네트워크 프리팹이라 더 아프다).
                return PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{name}.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ══════════ 6. 큐 배선 ══════════

        private static int WireAllCues(Dictionary<string, GameObject> fx, BossSize s)
        {
            int total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:PatternDefinition", new[] { SoFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<PatternDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def is IPatternEffectSource) total += WriteCues(def, fx, s);
            }
            return total;
        }

        private static int WriteCues(PatternDefinition def, Dictionary<string, GameObject> fx, BossSize s)
        {
            var so = new SerializedObject(def);
            var cuesProp = so.FindProperty(CuesProperty);
            if (cuesProp == null || !cuesProp.isArray) return 0;

            var hits = CollectHitTimings(so);
            if (hits.Count == 0) return 0;

            // 2차 발사를 끈 레이저는 후방 타격 시점이 남아 있어도 실제로 쏘지 않는다.
            // 큐까지 만들면 판정 없는 레이저가 뜬다.
            if (def is LaserComboDefinition laser && laser.LaserVolleys < 2)
                hits.RemoveAll(h => h.Prefix.ToLowerInvariant().Contains("backlaser"));

            bool swordShield = def.UnlockPhase >= 4;

            // 타격 1건이 큐 1건이 아니다. 예고가 있는 종류(레이저 충전 등)는 타격보다 앞선
            // 큐를 하나 더 낸다 — 그래서 먼저 목록을 만들고 배열 크기를 마지막에 정한다.
            var planned = new List<(string Label, float At, GameObject Prefab, float Life, float Scale, Vector3 Offset)>();

            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                var kind = Classify(hit.Prefix);

                float radius = FindFloat(so, hit.Prefix, "Radius", 0f);
                float reach  = FindFloat(so, hit.Prefix, "Reach", 0f);
                float length = FindFloat(so, hit.Prefix, "Length", 0f);
                float forward = length > 0.01f ? reach + length * 0.5f : reach;

                // 장판은 판정 반경이 곧 크기다. 참격은 무기 길이를 기준으로 잡는다.
                float baseSize = kind switch
                {
                    Kind.Arc    => swordShield ? s.SwordLength : s.GreatswordLength,
                    Kind.Shield => s.ShieldRadius * 2f,
                    Kind.Beam   => swordShield ? s.SwordLength : s.GreatswordLength,
                    Kind.Crack  => s.BodyRadius * 2.4f,     // 균열 프리팹이 이 지름으로 만들어졌다
                    Kind.Slash  => s.BodyRadius * SlashLengthFactor,
                    _           => s.BodyRadius * 2f,
                };
                float scale = radius > 0.01f
                    ? Mathf.Clamp(radius * 2f / Mathf.Max(0.01f, baseSize), 0.3f, 10f)
                    : 1f;

                var offset = new Vector3(
                    0f,
                    Grounded(kind) ? 0f : s.ChestHeight,
                    Centered(kind) ? 0f : forward);

                // 예고 — 타격 전에 먼저 뜬다. 레이저는 포문에 기를 모으는 시간이 곧 회피 리드타임이다.
                float charge = ChargeSeconds(kind);
                if (charge > 0.01f && fx.TryGetValue(ChargeKey(kind), out var chargeFx) && chargeFx != null)
                {
                    planned.Add(($"{hit.Prefix} — {Describe(kind)} 예고",
                                 Mathf.Max(0f, hit.AtSeconds - charge),
                                 chargeFx, charge + 0.15f, scale, offset));
                }

                planned.Add(($"{hit.Prefix} — {Describe(kind)}",
                             kind == Kind.Donut ? 0f : hit.AtSeconds,
                             fx.TryGetValue(PrefabKey(kind, swordShield), out var p) ? p : null,
                             Lifetime(kind), scale, offset));
            }

            planned.Sort((a, b) => a.At.CompareTo(b.At));

            // 손으로 맞춘 큐는 계산값으로 덮지 않는다(ManualOverride). 제작소에서 오프셋을 맞춰둔 뒤
            // 이 셋업을 다시 돌리면 그 값이 통째로 날아가기 때문이다. Label로 짝을 찾는다 —
            // Label은 "{타격필드} — {종류}" 규칙으로 결정론적이라 재계산해도 같은 이름이 나온다.
            var manual = SnapshotManualCues(cuesProp);

            cuesProp.arraySize = planned.Count;

            for (int i = 0; i < planned.Count; i++)
            {
                var c = planned[i];
                var cue = cuesProp.GetArrayElementAtIndex(i);

                cue.FindPropertyRelative("Label").stringValue = c.Label;
                cue.FindPropertyRelative("AtSeconds").floatValue = c.At;
                cue.FindPropertyRelative("AnchorBoneName").stringValue = string.Empty;
                cue.FindPropertyRelative("AttachToAnchor").boolValue = false;
                cue.FindPropertyRelative("Lifetime").floatValue = c.Life;
                cue.FindPropertyRelative("Scale").floatValue = c.Scale;
                cue.FindPropertyRelative("LocalOffset").vector3Value = c.Offset;
                cue.FindPropertyRelative("Prefab").objectReferenceValue = c.Prefab;
                cue.FindPropertyRelative("ManualOverride").boolValue = false;

                if (manual.TryGetValue(c.Label, out var kept))
                {
                    RestoreManualCue(cue, kept);
                    manual.Remove(c.Label);   // 짝을 찾았으니 뒤에 다시 붙이지 않는다
                }
            }

            // 계산 결과에 대응하는 항목이 없는 수동 큐(제작소에서 직접 추가한 연출 등)는 뒤에 그대로 남긴다.
            foreach (var kept in manual.Values)
            {
                int i = cuesProp.arraySize;
                cuesProp.arraySize = i + 1;
                RestoreManualCue(cuesProp.GetArrayElementAtIndex(i), kept, includeLabel: true);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
            return planned.Count;
        }

        /// 손으로 맞춘 큐 한 건의 값. SerializedProperty는 arraySize를 바꾸는 순간 무효가 되므로
        /// 덮어쓰기 전에 평범한 값으로 떠 둔다.
        private struct ManualCue
        {
            public string Label;
            public float AtSeconds;
            public Object Prefab;
            public string AnchorBoneName;
            public bool AttachToAnchor;
            public float Lifetime;
            public Vector3 LocalOffset;
            public float Scale;
        }

        private static Dictionary<string, ManualCue> SnapshotManualCues(SerializedProperty cuesProp)
        {
            var kept = new Dictionary<string, ManualCue>();
            for (int i = 0; i < cuesProp.arraySize; i++)
            {
                var cue = cuesProp.GetArrayElementAtIndex(i);
                var flag = cue.FindPropertyRelative("ManualOverride");
                if (flag == null || !flag.boolValue) continue;

                string label = cue.FindPropertyRelative("Label").stringValue;
                if (string.IsNullOrEmpty(label) || kept.ContainsKey(label)) label = $"{label}#{i}";  // Label 중복 시 분리

                kept[label] = new ManualCue
                {
                    Label           = cue.FindPropertyRelative("Label").stringValue,
                    AtSeconds       = cue.FindPropertyRelative("AtSeconds").floatValue,
                    Prefab          = cue.FindPropertyRelative("Prefab").objectReferenceValue,
                    AnchorBoneName  = cue.FindPropertyRelative("AnchorBoneName").stringValue,
                    AttachToAnchor  = cue.FindPropertyRelative("AttachToAnchor").boolValue,
                    Lifetime        = cue.FindPropertyRelative("Lifetime").floatValue,
                    LocalOffset     = cue.FindPropertyRelative("LocalOffset").vector3Value,
                    Scale           = cue.FindPropertyRelative("Scale").floatValue,
                };
            }
            return kept;
        }

        private static void RestoreManualCue(SerializedProperty cue, ManualCue kept, bool includeLabel = false)
        {
            if (includeLabel) cue.FindPropertyRelative("Label").stringValue = kept.Label;
            cue.FindPropertyRelative("AtSeconds").floatValue = kept.AtSeconds;
            cue.FindPropertyRelative("Prefab").objectReferenceValue = kept.Prefab;
            cue.FindPropertyRelative("AnchorBoneName").stringValue = kept.AnchorBoneName;
            cue.FindPropertyRelative("AttachToAnchor").boolValue = kept.AttachToAnchor;
            cue.FindPropertyRelative("Lifetime").floatValue = kept.Lifetime;
            cue.FindPropertyRelative("LocalOffset").vector3Value = kept.LocalOffset;
            cue.FindPropertyRelative("Scale").floatValue = kept.Scale;
            cue.FindPropertyRelative("ManualOverride").boolValue = true;
        }

        private static DonutFieldDefinition LoadDonutDefinition()
            => AssetDatabase.LoadAssetAtPath<DonutFieldDefinition>($"{SoFolder}/DonutFieldDef.asset");

        private enum Kind { Arc, Shield, Beam, Ground, Field, Donut, Mark, Blast, Crack, Slash, Radial }

        /// 이 종류가 타격 몇 초 전부터 예고를 띄우는지. 0이면 예고 없음.
        private static float ChargeSeconds(Kind k) => k switch
        {
            Kind.Radial => 0.8f,   // 포문에 기를 모으는 시간
            _ => 0f,
        };

        private static string ChargeKey(Kind k) => k switch
        {
            Kind.Radial => "LaserCharge",
            _ => string.Empty,
        };

        private static Kind Classify(string prefix)
        {
            string k = prefix.ToLowerInvariant();

            if (k.Contains("mark")) return Kind.Mark;
            if (k.Contains("field")) return Kind.Field;
            if (k.Contains("burst")) return Kind.Donut;

            // 근·중·원 3연속 폭발. 예전엔 장판을 재사용했는데, 그건 "여기가 위험하다"는
            // 예고 표시지 터지는 그림이 아니라 3연발이 밋밋하게 보였다.
            if (k.Contains("near") || k.Contains("mid") || k.Contains("far")) return Kind.Blast;

            // 일도양단 — 중심에서 사방으로 터지는 파편 자국.
            if (k.Contains("cleave")) return Kind.Slash;

            // 섬단 — 돌진선을 따라 길게 갈라진 자국.
            if (k.Contains("sunder") || k.Contains("dash")) return Kind.Crack;

            // 레이저는 보스 중심에서 방사형으로 뻗는다. 찌르기는 여전히 한 줄이다.
            if (k.Contains("laser")) return Kind.Radial;
            if (k.Contains("thrust")) return Kind.Beam;
            if (k.Contains("shield")) return Kind.Shield;
            if (k.Contains("bash") || k.Contains("impact")) return Kind.Ground;

            return Kind.Arc;
        }

        private static bool Grounded(Kind k)
            => k is Kind.Ground or Kind.Field or Kind.Donut or Kind.Mark
                 or Kind.Blast or Kind.Crack or Kind.Slash;

        /// 방사형 레이저는 보스 몸에서 사방으로 뻗으므로 앞으로 밀면 안 된다 —
        /// 중심이 곧 발사점이다.
        private static bool Centered(Kind k) => k is Kind.Radial;

        private static float Lifetime(Kind k) => k switch
        {
            Kind.Field   => 2.5f,
            Kind.Donut   => 3f,
            Kind.Mark    => 2.5f,
            Kind.Ground  => 2f,
            Kind.Blast   => 2.5f,
            Kind.Crack   => 4f,     // 균열은 한참 남아야 벤 자국으로 읽힌다
            Kind.Slash => 4f,
            Kind.Radial  => 2.4f,   // FillSeconds 2초 + 여운
            _ => 1.2f,
        };

        private static string PrefabKey(Kind k, bool swordShield) => k switch
        {
            Kind.Arc    => swordShield ? "SwordHit" : "GreatswordHit",
            Kind.Shield => "ShieldBash",
            Kind.Beam   => "Beam",
            Kind.Ground => "GroundCrack",
            Kind.Field  => "Field",
            Kind.Donut  => "Donut",
            Kind.Mark   => "Mark",
            Kind.Blast   => "Blast",
            Kind.Crack   => "Crack",
            Kind.Slash => "Slash",
            Kind.Radial  => "Laser",
            _ => "GroundCrack",
        };

        private static string Describe(Kind k) => k switch
        {
            Kind.Arc => "참격 스파크",
            Kind.Shield => "방패 충격",
            Kind.Beam => "직선",
            Kind.Ground => "바닥 폭발",
            Kind.Field => "장판",
            Kind.Donut => "고리 장판",
            Kind.Mark => "지목",
            Kind.Blast => "폭발",
            Kind.Crack => "바닥 균열",
            Kind.Slash => "검 자국",
            Kind.Radial => "방사형 레이저",
            _ => "타격",
        };

        private readonly struct HitTiming
        {
            public readonly string Prefix;
            public readonly float AtSeconds;
            public HitTiming(string prefix, float atSeconds) { Prefix = prefix; AtSeconds = atSeconds; }
        }

        private static List<HitTiming> CollectHitTimings(SerializedObject so)
        {
            var result = new List<HitTiming>();
            var it = so.GetIterator();

            while (it.NextVisible(true))
            {
                // 큐 배열 자신을 읽지 않는다. PatternEffectCue에도 AtSeconds 필드가 있어서,
                // 그냥 훑으면 지난 실행이 써 둔 큐가 다시 타격으로 잡히고 큐가 실행할 때마다 불어난다.
                if (it.propertyPath.StartsWith(CuesProperty)) continue;

                if (it.propertyType != SerializedPropertyType.Float) continue;
                if (!it.name.EndsWith("AtSeconds")) continue;
                result.Add(new HitTiming(it.name[..^"AtSeconds".Length], it.floatValue));
            }

            result.Sort((a, b) => a.AtSeconds.CompareTo(b.AtSeconds));
            return result;
        }

        private static float FindFloat(SerializedObject so, string prefix, string suffix, float fallback)
        {
            var p = so.FindProperty(prefix + suffix);
            if (p == null)
            {
                string stripped = prefix.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (stripped != prefix) p = so.FindProperty(stripped + suffix);
            }
            return p != null && p.propertyType == SerializedPropertyType.Float ? p.floatValue : fallback;
        }
    }
}
