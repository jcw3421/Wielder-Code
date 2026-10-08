using Fusion;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.BossContent.Editor
{
    /// TFW의 SO·패턴·프리팹을 "클립과 파티클만 꽂으면 되는" 상태까지 만들어 둔다.
    /// 여러 번 실행해도 안전하다 — 이미 있는 에셋·컴포넌트·큐는 덮지 않는다.
    public static class TheFirstWielderSetup
    {
        private const string PrefabPath =
            "Assets/_Project/BossContent/03. Prefabs/Boss/Boss_TheFirstWielder.prefab";
        private const string ControllerPath =
            "Assets/_Project/BossContent/05. Animators/TheFirstWielderBoss/TheFirstWielderBoss.controller";
        private const string SoFolder = "Assets/_Project/BossContent/09. ScriptableObjects/Boss/TFW";

        private const int BossLayer = 8;      // TagManager: 8 = Boss
        private const int BlockerLayer = 13;  // TagManager: 13 = BossBlocker
        private const string BlockerName = "BossBlocker";

        [MenuItem("Tools/Boss/Setup TheFirstWielder (SO + Prefab)")]
        private static void Setup()
        {
            // 1페이즈 대검
            var cleave     = CreateOrLoad<CleaveComboDefinition>("CleaveComboDef");
            var dash       = CreateOrLoad<SunderingDashDefinition>("SunderingDashDef");
            var rising     = CreateOrLoad<RisingComboDefinition>("RisingComboDef");
            var triBlast   = CreateOrLoad<TriBlastDefinition>("TriBlastDef");
            var fourStrike = CreateOrLoad<FourStrikeComboDefinition>("FourStrikeComboDef");

            // 2페이즈 검+방패 (HP 50% 이하)
            var swordShield = CreateOrLoad<SwordShieldComboDefinition>("SwordShieldComboDef");
            var shieldBash  = CreateOrLoad<ShieldBashComboDefinition>("ShieldBashComboDef");
            var sixStrike   = CreateOrLoad<RushSixStrikeDefinition>("RushSixStrikeDef");
            var cascade     = CreateOrLoad<CascadeFieldComboDefinition>("CascadeFieldComboDef");
            var laser       = CreateOrLoad<LaserComboDefinition>("LaserComboDef");
            var marked      = CreateOrLoad<MarkedStrikeDefinition>("MarkedStrikeDef");
            var donut       = CreateOrLoad<DonutFieldDefinition>("DonutFieldDef");

            var definition = CreateOrLoad<BossDefinition>("TFWBossDefinition");

            ConfigurePatterns(cleave, dash, rising, triBlast, fourStrike,
                              swordShield, shieldBash, sixStrike, cascade, laser, marked, donut);
            ConfigureEffectCues(cleave, dash, rising, triBlast, fourStrike);
            ConfigureDefinition(definition, cleave, dash, rising, triBlast, fourStrike,
                                swordShield, shieldBash, sixStrike, cascade, laser, marked, donut);
            ConfigurePrefab(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TFW Setup] 완료. 남은 일은 (1) 애니 클립 연결 (2) 이펙트 프리팹 연결 " +
                      "(3) 무기 메시 배정 — 콘솔 경고를 확인하세요.");
        }

        private static T CreateOrLoad<T>(string assetName) where T : ScriptableObject
        {
            if (!Directory.Exists(SoFolder))
            {
                Directory.CreateDirectory(SoFolder);
                AssetDatabase.Refresh();
            }

            string path = SoFolder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            Debug.Log("[TFW Setup] 생성: " + path);
            return created;
        }

        /// 애니메이터 진입 조건(AnimatorPatternId)·해금 페이즈·쿨타임을 한자리에서 배정한다.
        /// 쿨타임을 서로 어긋나게 둬 후보가 겹치지 않게 한다 — 가중 랜덤은 후보가 1개면 무의미해진다.
        /// ※ 전 패턴이 동시에 쿨다운이면 보스가 추격만 하며 멍때린다.
        ///   1페이즈는 섬단(30s)과 일도양단(11s) 둘뿐이라 이 구간이 생길 수 있다 —
        ///   실제로 겪으면 일도양단 쿨을 더 줄이거나 1페이즈 패턴을 늘려야 한다.
        /// 대검 패턴은 1~3페이즈(HP 100~50%), 검+방패 패턴은 4페이즈(50% 이하)에서만 나온다.
        /// LockPhase(상한)로 대검을 봉인하지 않으면 무기가 바뀐 뒤에도 대검 모션이 섞여 나온다.
        /// 쿨타임은 서로 어긋나게 둬 후보가 겹치지 않게 한다 — 가중 랜덤은 후보가 1개면 무의미해진다.
        private static void ConfigurePatterns(CleaveComboDefinition cleave, SunderingDashDefinition dash,
                                              RisingComboDefinition rising, TriBlastDefinition triBlast,
                                              FourStrikeComboDefinition fourStrike,
                                              SwordShieldComboDefinition swordShield,
                                              ShieldBashComboDefinition shieldBash,
                                              RushSixStrikeDefinition sixStrike,
                                              CascadeFieldComboDefinition cascade,
                                              LaserComboDefinition laser,
                                              MarkedStrikeDefinition marked,
                                              DonutFieldDefinition donut)
        {
            // 1페이즈 대검 — 3페이즈까지만
            Apply(cleave,     patternId: 1,  unlock: 1, lockPhase: 3, cooldown: 11f);
            Apply(dash,       patternId: 2,  unlock: 1, lockPhase: 3, cooldown: 30f);
            Apply(rising,     patternId: 3,  unlock: 1, lockPhase: 3, cooldown: 13f);
            Apply(triBlast,   patternId: 4,  unlock: 1, lockPhase: 3, cooldown: 17f);
            Apply(fourStrike, patternId: 5,  unlock: 1, lockPhase: 3, cooldown: 19f);

            // 2페이즈 검+방패 — 4페이즈부터
            Apply(swordShield, patternId: 11, unlock: 4, lockPhase: 99, cooldown: 12f);
            Apply(shieldBash,  patternId: 12, unlock: 4, lockPhase: 99, cooldown: 14f);
            Apply(sixStrike,   patternId: 13, unlock: 4, lockPhase: 99, cooldown: 16f);
            Apply(cascade,     patternId: 14, unlock: 4, lockPhase: 99, cooldown: 18f);
            Apply(laser,       patternId: 15, unlock: 4, lockPhase: 99, cooldown: 20f);
            Apply(marked,      patternId: 16, unlock: 4, lockPhase: 99, cooldown: 22f);
            Apply(donut,       patternId: 17, unlock: 4, lockPhase: 99, cooldown: 9f);   // 사이사이 끼는 마무리

            // 후방 타격 조각 — 진입할 때 보스를 180도 돌려 "앞으로 쓰는" 클립을 그대로 재활용한다.
            // 전용 후방 모션을 구할 필요가 없어지는 대신, 그 뒤 조각들도 돌아간 방향을 이어받는다.
            SetTurnAround(fourStrike, 2);   // 후방 일도양단
            SetTurnAround(swordShield, 3);  // 뒤돌기 베기
            SetTurnAround(laser, 2);        // 뒤돌아 레이저
        }

        private static void SetTurnAround(PatternDefinition def, params int[] segments)
        {
            var so = new SerializedObject(def);
            var p = so.FindProperty("TurnAroundSegments");
            if (p == null) return;

            p.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
                p.GetArrayElementAtIndex(i).intValue = segments[i];
            so.ApplyModifiedProperties();
        }

        private static void Apply(PatternDefinition def, int patternId, int unlock, int lockPhase, float cooldown)
        {
            var so = new SerializedObject(def);
            so.FindProperty(nameof(PatternDefinition.AnimatorPatternId)).intValue = patternId;
            so.FindProperty(nameof(PatternDefinition.UnlockPhase)).intValue = unlock;
            so.FindProperty(nameof(PatternDefinition.LockPhase)).intValue = lockPhase;
            so.FindProperty(nameof(PatternDefinition.CooldownSeconds)).floatValue = cooldown;
            so.ApplyModifiedProperties();
        }

        /// 연출 슬롯을 타격 시점에 맞춰 미리 깔아둔다 — 디자이너는 Prefab 칸만 채우면 된다.
        /// 이미 큐가 들어있으면 손대지 않는다(수동 편집 보존).
        private static void ConfigureEffectCues(CleaveComboDefinition cleave, SunderingDashDefinition dash,
                                                RisingComboDefinition rising, TriBlastDefinition triBlast,
                                                FourStrikeComboDefinition fourStrike)
        {
            FillCues(cleave, new[]
            {
                new CueSeed("1타 회전베기", cleave.SpinAtSeconds),
                new CueSeed("2타 칼등치기(스턴)", cleave.BackhandAtSeconds),
                new CueSeed("3타 일도양단", cleave.CleaveAtSeconds),
            });

            FillCues(dash, new[]
            {
                new CueSeed("돌진 시작", 0f),
                new CueSeed("마무리 참격", 0f),   // 돌진 종료 = Active 끝. 클립 보고 조정
            });

            FillCues(rising, new[]
            {
                new CueSeed("1타 우 올려베기", rising.RightRiseAtSeconds),
                new CueSeed("2타 좌 올려베기", rising.LeftRiseAtSeconds),
                // 백스텝은 타격이 없어 기준 시각이 없다 — 좌 올려베기와 일도양단 사이로 잡아둔다(먼지 등).
                new CueSeed("백스텝", (rising.LeftRiseAtSeconds + rising.CleaveAtSeconds) * 0.5f),
                new CueSeed("3타 일도양단", rising.CleaveAtSeconds),
            });

            FillCues(triBlast, new[]
            {
                new CueSeed("근거리 폭발", triBlast.NearAtSeconds),
                new CueSeed("중거리 폭발", triBlast.MidAtSeconds),
                new CueSeed("원거리 폭발", triBlast.FarAtSeconds),
            });

            FillCues(fourStrike, new[]
            {
                new CueSeed("1타 찌르기", fourStrike.ThrustAtSeconds),
                new CueSeed("2타 전방 일도양단", fourStrike.FrontCleaveAtSeconds),
                new CueSeed("3타 후방 일도양단", fourStrike.BackCleaveAtSeconds),
                new CueSeed("4타 마무리 참격", fourStrike.FinishAtSeconds),
            });
        }

        private struct CueSeed
        {
            public readonly string Label;
            public readonly float At;
            public CueSeed(string label, float at) { Label = label; At = at; }
        }

        private static void FillCues(ScriptableObject target, CueSeed[] cues)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty("_effectCues");
            if (array == null) return;
            if (array.arraySize > 0) return;   // 이미 채워둔 걸 덮지 않는다

            array.arraySize = cues.Length;
            for (int i = 0; i < cues.Length; i++)
            {
                var e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative(nameof(PatternEffectCue.Label)).stringValue = cues[i].Label;
                e.FindPropertyRelative(nameof(PatternEffectCue.AtSeconds)).floatValue = cues[i].At;
                e.FindPropertyRelative(nameof(PatternEffectCue.AttachToAnchor)).boolValue = false;
                e.FindPropertyRelative(nameof(PatternEffectCue.Lifetime)).floatValue = 3f;
            }
            so.ApplyModifiedProperties();
            Debug.Log("[TFW Setup] " + target.name + ": 연출 슬롯 " + cues.Length + "칸 생성 (Prefab 칸을 채우세요)");
        }

        private static void ConfigureDefinition(BossDefinition def, params PatternDefinition[] patterns)
        {
            var so = new SerializedObject(def);

            // 거합(즉사기) 3회 = 임계값 3개 → 페이즈 4개.
            // 마지막 임계값을 50%에 둬서 그 거합이 곧 무기 교체(대검 → 검+방패) 연출이 된다.
            SetIntArray(so, nameof(BossDefinition.PhaseThresholdPercents), new[] { 80, 65, 50 });
            SetFloatArray(so, nameof(BossDefinition.PhasePatternSpeedMultipliers),
                          new[] { 1f, 1.15f, 1.35f, 1.6f });

            var array = so.FindProperty(nameof(BossDefinition.Patterns));
            array.arraySize = patterns.Length;
            for (int i = 0; i < patterns.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = patterns[i];

            so.FindProperty(nameof(BossDefinition.PatternIntervalSeconds)).floatValue = 0.15f;   // 쉴틈없이
            so.ApplyModifiedProperties();

            var data = so.FindProperty("data");
            if (data != null && data.objectReferenceValue == null)
                Debug.LogWarning("[TFW Setup] BossDefinition의 BossRecord(data)가 비어 있습니다 — " +
                                 "체력·ID가 여기서 오므로 BossRecord_1003을 꽂아주세요.");
        }

        private static void ConfigurePrefab(BossDefinition definition)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[TFW Setup] 프리팹을 찾을 수 없음: " + PrefabPath);
                return;
            }

            try
            {
                Require<NetworkObject>(root);
                Require<CharacterController>(root);          // NetworkCharacterController의 전제
                Require<NetworkCharacterController>(root);
                Require<BossNetState>(root);
                var entity = Require<BossEntity>(root);
                Require<BossPhaseWeaponSwitcher>(root);
                var effects = Require<BossPatternEffectPlayer>(root);
                Require<BossRangeGizmos>(root);
                Require<BossPatternGizmos>(root);

                Bind(entity, "_definition", definition);
                Bind(effects, "_definition", definition);

                // 본체(CharacterController)는 BossBlocker 레이어다 — SkeletonKing과 같은 구성.
                // Boss 레이어에 두면 (1) 플레이어 공격이 HitBox와 함께 이걸도 잡아 데미지가 두 번 들어가고
                // (2) 플레이어와 물리 충돌해 서로 밀어내며 보스가 떨린다.
                root.layer = BlockerLayer;
                EnsureHitBox(root);
                EnsureBlocker(root);

                var animator = root.GetComponent<Animator>();
                if (animator == null)
                    Debug.LogWarning("[TFW Setup] 루트에 Animator가 없습니다 — BossView가 연출을 못 돌립니다.");
                else if (animator.runtimeAnimatorController == null)
                    animator.runtimeAnimatorController =
                        AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[TFW Setup] 프리팹 배선 완료.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// 피격 판정용 트리거 박스 + BossHitRelay. 크기는 렌더러 bounds에서 잡는다.
        /// SkeletonKing과 같은 구성이다 — 루트가 아니라 자식에 둬서 나중에 부위별로 늘릴 수 있게 한다.
        private static void EnsureHitBox(GameObject root)
        {
            if (root.GetComponentInChildren<BossHitRelay>() != null) return;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[TFW Setup] 렌더러가 없어 히트박스 크기를 잴 수 없습니다 — 수동으로 만들어주세요.");
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var hitBox = new GameObject("HitBox");
            hitBox.layer = BossLayer;
            hitBox.transform.SetParent(root.transform, false);
            // bounds는 월드 기준이라 루트 위치를 빼서 로컬로 옮긴다.
            hitBox.transform.localPosition = bounds.center - root.transform.position;

            var box = hitBox.AddComponent<BoxCollider>();
            box.size = bounds.size;
            box.isTrigger = true;
            hitBox.AddComponent<BossHitRelay>();

            Debug.Log("[TFW Setup] HitBox 생성 (크기 " + bounds.size + "). 모델에 비해 헐거우면 손으로 조여주세요.");
        }

        /// 플레이어를 막는 몸통 콜라이더. SkeletonKing과 같은 구성이다.
        /// 이게 없으면 플레이어가 보스의 CharacterController에 직접 부딪혀
        /// 겹침 해소로 보스가 밀려난다 — 막는 역할은 별도 레이어의 이 콜라이더가 맡는다.
        private static void EnsureBlocker(GameObject root)
        {
            var existing = root.transform.Find(BlockerName);
            if (existing != null) return;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[TFW Setup] 렌더러가 없어 BossBlocker 크기를 잴 수 없습니다 — 수동으로 만들어주세요.");
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var blocker = new GameObject(BlockerName);
            blocker.layer = BlockerLayer;
            blocker.transform.SetParent(root.transform, false);

            var capsule = blocker.AddComponent<CapsuleCollider>();
            capsule.height = bounds.size.y;
            capsule.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            capsule.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);   // 발밑 기준
            capsule.isTrigger = false;

            Debug.Log($"[TFW Setup] BossBlocker 생성 (반지름 {capsule.radius:F2}, 높이 {capsule.height:F2}). " +
                      "너무 뚱뚱하면 반지름을 줄이세요.");
        }

        private static T Require<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
            {
                c = go.AddComponent<T>();
                Debug.Log("[TFW Setup] 컴포넌트 추가: " + typeof(T).Name);
            }
            return c;
        }

        private static void Bind(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetIntArray(SerializedObject so, string field, int[] values)
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void SetFloatArray(SerializedObject so, string field, float[] values)
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }
}
