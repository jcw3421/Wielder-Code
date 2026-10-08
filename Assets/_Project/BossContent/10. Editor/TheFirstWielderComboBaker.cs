using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.BossContent.Editor
{
    /// TFW 전용 타이밍 베이커. 기본 BossTimingBaker는 _Active 스테이트 "하나"의 클립 길이를 재지만,
    /// TFW의 연계는 조각 클립 체인(_Active → _Active2 → ...)이라 합산이 필요하다.
    ///
    /// 하는 일:
    ///  1. Telegraph / Recovery 길이를 SO에 기입 (기본 베이커와 동일)
    ///  2. Active 조각 길이를 모두 더해 ActiveSeconds에 기입하고 ManualActiveSeconds를 켠다
    ///     (켜두지 않으면 기본 베이커가 조각 하나의 길이로 덮어버린다)
    ///  3. 각 조각의 시작 시각을 계산해 연계의 타격 시점 필드에 기본값으로 넣는다
    ///
    /// 3번은 어디까지나 출발점이다 — 실제 칼이 닿는 순간은 조각 중간일 수 있으니
    /// 인스펙터에서 미세조정하는 걸 전제로 한다.
    public static class TheFirstWielderComboBaker
    {
        private const string ControllerPath =
            "Assets/_Project/BossContent/05. Animators/TheFirstWielderBoss/TheFirstWielderBoss.controller";
        private const string BossDefinitionName = "TFWBossDefinition";

        /// 타격 시점 필드 하나. 조각의 시작이 아니라 조각 안의 비율 지점으로 잡는다 —
        /// 조각 시작은 아직 칼을 드는 순간이라 그때 판정을 내면 "닿지도 않았는데 맞는" 그림이 된다.
        private struct HitField
        {
            public readonly string Field;
            public readonly int Segment;   // 몇 번째 Active 조각인가 (0-based)
            public readonly float Ratio;   // 그 조각 안에서 타격이 나가는 지점 (0=시작, 1=끝)

            public HitField(string field, int segment, float ratio = 0.5f)
            {
                Field = field;
                Segment = segment;
                Ratio = ratio;
            }
        }

        private struct ComboSpec
        {
            public string Prefix;
            public System.Type SoType;
            public HitField[] HitFields;
        }

        private static readonly ComboSpec[] Combos =
        {
            new ComboSpec
            {
                Prefix = "CleaveCombo",
                SoType = typeof(CleaveComboDefinition),
                // 조각: 0=360도, 1=기모으기(타격 없음), 2=칼등치기, 3=일도양단
                HitFields = new[]
                {
                    new HitField(nameof(CleaveComboDefinition.SpinAtSeconds), 0, 0.55f),      // 한 바퀴 돈 뒤
                    new HitField(nameof(CleaveComboDefinition.BackhandAtSeconds), 2, 0.5f),
                    new HitField(nameof(CleaveComboDefinition.CleaveAtSeconds), 3, 0.45f),    // 내려치는 순간
                },
            },
            new ComboSpec
            {
                Prefix = "RisingCombo",
                SoType = typeof(RisingComboDefinition),
                // 조각: 0=우 올려베기, 1=좌 올려베기, 2=백스텝, 3=일도양단
                HitFields = new[]
                {
                    new HitField(nameof(RisingComboDefinition.RightRiseAtSeconds), 0, 0.5f),
                    new HitField(nameof(RisingComboDefinition.LeftRiseAtSeconds), 1, 0.5f),
                    // 조각2(백스텝)는 타격이 없다 — 이동은 ForwardDistanceCurve가 담당한다.
                    new HitField(nameof(RisingComboDefinition.CleaveAtSeconds), 3, 0.45f),
                },
            },
            new ComboSpec
            {
                Prefix = "FourStrikeCombo",
                SoType = typeof(FourStrikeComboDefinition),
                // 조각: 0=찌르기, 1=전방 일도양단, 2=후방 일도양단, 3=마무리 참격
                HitFields = new[]
                {
                    new HitField(nameof(FourStrikeComboDefinition.ThrustAtSeconds), 0, 0.45f),   // 찌르기는 빠르게
                    new HitField(nameof(FourStrikeComboDefinition.FrontCleaveAtSeconds), 1, 0.5f),
                    new HitField(nameof(FourStrikeComboDefinition.BackCleaveAtSeconds), 2, 0.5f),
                    new HitField(nameof(FourStrikeComboDefinition.FinishAtSeconds), 3, 0.5f),
                },
            },
            // 단발 패턴 — 조각이 1개라 타격 시점을 계산할 근거가 없다. 길이만 기입한다.
            new ComboSpec { Prefix = "SunderingDash", SoType = typeof(SunderingDashDefinition), HitFields = new HitField[0] },
            new ComboSpec { Prefix = "TriBlast",      SoType = typeof(TriBlastDefinition),      HitFields = new HitField[0] },
            new ComboSpec { Prefix = "MarkedStrike",  SoType = typeof(MarkedStrikeDefinition),  HitFields = new HitField[0] },
            new ComboSpec { Prefix = "DonutField",    SoType = typeof(DonutFieldDefinition),    HitFields = new HitField[0] },

            // ── 2페이즈 검+방패 ──────────────────────────────────
            new ComboSpec
            {
                Prefix = "SwordShieldCombo",
                SoType = typeof(SwordShieldComboDefinition),
                // 조각: 0=검, 1=방패, 2=검, 3=뒤돌기, 4=찌르기
                HitFields = new[]
                {
                    new HitField(nameof(SwordShieldComboDefinition.Slash1AtSeconds), 0, 0.5f),
                    new HitField(nameof(SwordShieldComboDefinition.ShieldAtSeconds), 1, 0.5f),
                    new HitField(nameof(SwordShieldComboDefinition.Slash2AtSeconds), 2, 0.5f),
                    new HitField(nameof(SwordShieldComboDefinition.BackTurnAtSeconds), 3, 0.55f),
                    new HitField(nameof(SwordShieldComboDefinition.ThrustAtSeconds), 4, 0.45f),
                },
            },
            new ComboSpec
            {
                Prefix = "ShieldBashCombo",
                SoType = typeof(ShieldBashComboDefinition),
                // 조각: 0=검, 1=방패, 2=X검, 3=방패찍기(스턴), 4=뒷발차기
                HitFields = new[]
                {
                    new HitField(nameof(ShieldBashComboDefinition.SlashAtSeconds), 0, 0.5f),
                    new HitField(nameof(ShieldBashComboDefinition.ShieldAtSeconds), 1, 0.5f),
                    new HitField(nameof(ShieldBashComboDefinition.CrossSlashAtSeconds), 2, 0.5f),
                    new HitField(nameof(ShieldBashComboDefinition.BashAtSeconds), 3, 0.5f),
                    new HitField(nameof(ShieldBashComboDefinition.KickAtSeconds), 4, 0.5f),
                },
            },
            new ComboSpec
            {
                Prefix = "RushSixStrike",
                SoType = typeof(RushSixStrikeDefinition),
                // 조각: 0=연속 6타(한 클립), 1=찌르기. 6타 간격은 SO에서 직접 튜닝한다.
                HitFields = new[]
                {
                    new HitField(nameof(RushSixStrikeDefinition.FirstStrikeAtSeconds), 0, 0.1f),
                    new HitField(nameof(RushSixStrikeDefinition.ThrustAtSeconds), 1, 0.5f),
                },
            },
            new ComboSpec
            {
                Prefix = "CascadeFieldCombo",
                SoType = typeof(CascadeFieldComboDefinition),
                // 조각: 0=방패, 1=검, 2=X검, 3=장판 시전
                HitFields = new[]
                {
                    new HitField(nameof(CascadeFieldComboDefinition.ShieldAtSeconds), 0, 0.5f),
                    new HitField(nameof(CascadeFieldComboDefinition.SlashAtSeconds), 1, 0.5f),
                    new HitField(nameof(CascadeFieldComboDefinition.CrossSlashAtSeconds), 2, 0.5f),
                    new HitField(nameof(CascadeFieldComboDefinition.FirstFieldAtSeconds), 3, 0.2f),
                },
            },
            new ComboSpec
            {
                Prefix = "LaserCombo",
                SoType = typeof(LaserComboDefinition),
                // 조각: 0=방패, 1=전방 레이저, 2=뒤돌기 레이저, 3=지목 강타
                HitFields = new[]
                {
                    new HitField(nameof(LaserComboDefinition.ShieldAtSeconds), 0, 0.5f),
                    new HitField(nameof(LaserComboDefinition.FrontLaserAtSeconds), 1, 0.5f),
                    new HitField(nameof(LaserComboDefinition.BackLaserAtSeconds), 2, 0.5f),
                    new HitField(nameof(LaserComboDefinition.MarkedAtSeconds), 3, 0.5f),
                },
            },
        };

        [MenuItem("Tools/Boss/Bake TheFirstWielder Timings")]
        private static void Bake()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[TFW Baker] 컨트롤러를 찾을 수 없음: {ControllerPath}");
                return;
            }

            int baked = 0;
            foreach (var combo in Combos)
                baked += BakeCombo(controller, combo);

            BakePhaseTransition(controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TFW Baker] 완료: {baked}개 패턴 갱신. " +
                      "타격 시점은 조각 시작 기준이니 인스펙터에서 실제 타격 순간으로 다듬으세요.");
        }

        private static int BakeCombo(AnimatorController controller, ComboSpec combo)
        {
            if (!TryGetSeconds(controller, $"{combo.Prefix}_Telegraph", out float telegraph)) return 0;
            if (!TryGetSeconds(controller, $"{combo.Prefix}_Recovery", out float recovery)) return 0;

            // Active 조각을 _Active, _Active2, ... 순으로 끝날 때까지 모은다.
            var segmentStarts = new System.Collections.Generic.List<float>();
            float total = 0f;
            for (int i = 0; ; i++)
            {
                string name = i == 0 ? $"{combo.Prefix}_Active" : $"{combo.Prefix}_Active{i + 1}";
                if (!TryGetSeconds(controller, name, out float length)) break;

                segmentStarts.Add(total);   // 이 조각이 시작하는 시각
                total += length;
            }

            var forwardCurve = BuildForwardCurve(controller, combo.Prefix);

            if (segmentStarts.Count == 0)
            {
                Debug.LogWarning($"[TFW Baker] {combo.Prefix}: Active 클립이 하나도 없음 — 건너뜀");
                return 0;
            }

            foreach (var guid in AssetDatabase.FindAssets($"t:{combo.SoType.Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<PatternDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                so.FindProperty(nameof(PatternDefinition.TelegraphSeconds)).floatValue = telegraph;
                so.FindProperty(nameof(PatternDefinition.RecoverySeconds)).floatValue = recovery;
                so.FindProperty(nameof(PatternDefinition.ActiveSeconds)).floatValue = total;
                // 조각 체인은 클립 하나가 지속시간을 못 정하므로 수동 모드로 고정한다.
                so.FindProperty(nameof(PatternDefinition.ManualActiveSeconds)).boolValue = segmentStarts.Count > 1;

                // 타격 시점 = 조각 시작 + 조각 길이 × 비율. 모션이 실제로 뻗는 지점에 판정을 맞춘다.
                foreach (var hit in combo.HitFields)
                {
                    if (hit.Segment >= segmentStarts.Count) continue;
                    var p = so.FindProperty(hit.Field);
                    if (p == null) continue;

                    float start = segmentStarts[hit.Segment];
                    float end = hit.Segment + 1 < segmentStarts.Count
                        ? segmentStarts[hit.Segment + 1]
                        : total;
                    p.floatValue = Mathf.Lerp(start, end, hit.Ratio);
                }

                // 루트모션 곡선. 없는 클립은 곡선이 평평해 제자리에 선다.
                float travelled = 0f;
                if (forwardCurve != null)
                {
                    var p = so.FindProperty("ForwardDistanceCurve");
                    if (p != null) p.animationCurveValue = forwardCurve;
                    travelled = forwardCurve.Evaluate(total) - forwardCurve.Evaluate(0f);
                }

                // 섬단은 조각이 하나뿐이라 SegmentStartSeconds가 없지만 곡선은 쓴다(돌진 거리 = 클립 거리).
                // FindProperty가 없는 필드에 null을 주므로 패턴별 분기 없이 그대로 지나간다.

                // 조각 경계 — 패턴이 조각이 바뀌는 순간마다 타겟을 다시 조준하는 데 쓴다.
                var starts = so.FindProperty("SegmentStartSeconds");
                if (starts != null)
                {
                    starts.arraySize = segmentStarts.Count;
                    for (int i = 0; i < segmentStarts.Count; i++)
                        starts.GetArrayElementAtIndex(i).floatValue = segmentStarts[i];
                }

                so.ApplyModifiedProperties();

                var detail = new System.Text.StringBuilder();
                detail.AppendLine($"[TFW Baker] {asset.name}");
                detail.AppendLine($"  Telegraph {telegraph:F2}s / Active {total:F2}s / Recovery {recovery:F2}s " +
                                  $"= 전체 {telegraph + total + recovery:F2}s");
                for (int i = 0; i < segmentStarts.Count; i++)
                {
                    float end = i + 1 < segmentStarts.Count ? segmentStarts[i + 1] : total;
                    detail.AppendLine($"  조각{i + 1}: {segmentStarts[i]:F2}s ~ {end:F2}s ({end - segmentStarts[i]:F2}s)");
                }
                foreach (var hit in combo.HitFields)
                    detail.AppendLine($"  타격 {hit.Field} = {new SerializedObject(asset).FindProperty(hit.Field).floatValue:F2}s");
                detail.Append($"  총 전진 {travelled:F2}m" +
                              (forwardCurve != null ? $" ({forwardCurve.length}키)" : " (루트모션 없음)"));
                Debug.Log(detail.ToString());
            }
            return 1;
        }

        /// 거합(즉사기). 페이즈 전환은 패턴 SO가 아니라 BossDefinition이 타이밍을 들고 있다.
        private static void BakePhaseTransition(AnimatorController controller)
        {
            if (!TryGetSeconds(controller, "PhaseTransition_Telegraph", out float telegraph)) return;
            if (!TryGetSeconds(controller, "PhaseTransition_Recovery", out float recovery)) return;
            TryGetSeconds(controller, "PhaseTransition_Active", out float active);

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(BossDefinition)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
                if (asset == null || asset.name != BossDefinitionName) continue;

                var so = new SerializedObject(asset);
                so.FindProperty(nameof(BossDefinition.PhaseTransitionTelegraphSeconds)).floatValue = telegraph;
                so.FindProperty(nameof(BossDefinition.PhaseTransitionRecoverySeconds)).floatValue = recovery;

                // 장판이 차오르는 동안 자세를 유지하려고 Active를 늘려둔 경우가 있어 수동 값을 존중한다.
                if (!asset.ManualPhaseTransitionActive)
                    so.FindProperty(nameof(BossDefinition.PhaseTransitionActiveSeconds)).floatValue = active;

                so.ApplyModifiedProperties();
                Debug.Log($"[TFW Baker] {asset.name} 거합: Telegraph {telegraph:F2}s / " +
                          $"Active {(asset.ManualPhaseTransitionActive ? "수동" : active.ToString("F2") + "s")} / " +
                          $"Recovery {recovery:F2}s");
            }
        }


        /// 조각 클립들의 루트 전진 곡선을 이어붙여 하나의 누적 거리 곡선으로 만든다.
        /// 평균 속도로 등속 이동시키면 런지 같은 가감속이 뭉개져 쑥 미끄러지므로,
        /// 실제 곡선을 촘촘히 샘플링해 원본의 완급을 그대로 살린다.
        /// (루트모션을 켜지 않는 이유는 애니메이터가 transform을 직접 움직이면
        ///  서버가 계산한 위치와 싸워서 보스가 떨리기 때문이다.)
        private const int SamplesPerSecond = 30;

        /// 조각의 순 이동이 이보다 작으면 제자리 모션으로 보고 아예 안 움직인다.
        /// 360도 회전베기처럼 루트가 원을 그리는 클립을 걸러낸다.
        private const float StillSegmentThreshold = 0.15f;

        private static AnimationCurve BuildForwardCurve(AnimatorController controller, string prefix)
        {
            var keys = new System.Collections.Generic.List<Keyframe>();
            float timeOffset = 0f;
            float distanceOffset = 0f;

            for (int i = 0; ; i++)
            {
                string name = i == 0 ? $"{prefix}_Active" : $"{prefix}_Active{i + 1}";
                var state = FindState(controller, name);
                var clip = state?.motion as AnimationClip;
                if (clip == null) break;

                float speed = state.speed <= 0f ? 1f : state.speed;
                float playLength = clip.length / speed;
                var root = RootMotionCurves.From(clip);

                if (root == null)
                {
                    // 루트 커브를 못 읽으면 평균 속도로 등속 근사(직선 두 점).
                    float travel = clip.averageSpeed.z * clip.length;
                    if (keys.Count == 0) keys.Add(new Keyframe(timeOffset, distanceOffset));
                    distanceOffset += travel;
                    keys.Add(new Keyframe(timeOffset + playLength, distanceOffset));
                }
                else
                {
                    // 조각의 이동량은 "시작점에서 끝점까지의 직선 거리"로 정한다.
                    // 매 샘플을 그 시점 정면에 투영해 누적하면, 회전이 섞인 클립에서 성분이 서로 상쇄돼
                    // 실제로 2~3m 나아가는 모션이 0에 가깝게 나온다. 제자리 회전은 시작=끝이라 자연히 0이 된다.
                    int samples = Mathf.Max(2, Mathf.CeilToInt(playLength * SamplesPerSecond));
                    var path = new float[samples + 1];        // 누적 경로 길이 (가감속을 여기서 얻는다)
                    Vector3 startPos = root.PositionAt(0f);
                    Vector3 prevPos = startPos;
                    Vector3 endPos = startPos;

                    for (int s = 1; s <= samples; s++)
                    {
                        float clipTime = playLength * s / samples * speed;
                        Vector3 pos = root.PositionAt(clipTime);
                        Vector3 step = pos - prevPos;
                        step.y = 0f;

                        path[s] = path[s - 1] + step.magnitude;
                        prevPos = pos;
                        endPos = pos;
                    }

                    Vector3 displacement = endPos - startPos;
                    displacement.y = 0f;

                    // 방향 부호는 시작 시점 정면 기준 — 앞으로 갔으면 +, 뒤로 물러났으면 -(백스텝).
                    float signedNet = Vector3.Dot(displacement, root.ForwardAt(0f)) >= 0f
                        ? displacement.magnitude
                        : -displacement.magnitude;
                    if (Mathf.Abs(signedNet) < StillSegmentThreshold) signedNet = 0f;

                    if (keys.Count == 0) keys.Add(new Keyframe(timeOffset, distanceOffset));

                    float totalPath = path[samples];
                    for (int s = 1; s <= samples; s++)
                    {
                        // 경로 진행률에 순 이동량을 실어 원본의 완급을 살린다.
                        float progress = totalPath > 0.0001f ? path[s] / totalPath : (float)s / samples;
                        keys.Add(new Keyframe(timeOffset + playLength * s / samples,
                                              distanceOffset + signedNet * progress));
                    }
                    distanceOffset += signedNet;
                }

                timeOffset += playLength;
            }

            if (keys.Count < 2) return null;

            var curve = new AnimationCurve(keys.ToArray());
            // 선형 탄젠트로 고정한다. 촘촘한 샘플에 스무딩을 걸면 키 사이가 오버슈트해서
            // 틱 간 이동량(delta)의 부호가 오락가락하고, 보스가 앞뒤로 떨린다.
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        /// 클립의 루트 위치·회전 커브 묶음. 위치만 보면 회전하는 클립에서 전진량을 잘못 읽으므로
        /// 회전까지 함께 들고 "그 순간 캐릭터가 향한 방향"을 알 수 있게 한다.
        private class RootMotionCurves
        {
            private AnimationCurve _px, _pz, _qx, _qy, _qz, _qw;

            public static RootMotionCurves From(AnimationClip clip)
            {
                var r = new RootMotionCurves();
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    // Humanoid는 RootT/RootQ, Generic은 루트 오브젝트의 Transform 커브로 들어온다.
                    bool isRoot = string.IsNullOrEmpty(binding.path);
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;

                    switch (binding.propertyName)
                    {
                        case "RootT.x": r._px = curve; break;
                        case "RootT.z": r._pz = curve; break;
                        case "RootQ.x": r._qx = curve; break;
                        case "RootQ.y": r._qy = curve; break;
                        case "RootQ.z": r._qz = curve; break;
                        case "RootQ.w": r._qw = curve; break;
                        case "m_LocalPosition.x": if (isRoot) r._px = curve; break;
                        case "m_LocalPosition.z": if (isRoot) r._pz = curve; break;
                        case "m_LocalRotation.x": if (isRoot) r._qx = curve; break;
                        case "m_LocalRotation.y": if (isRoot) r._qy = curve; break;
                        case "m_LocalRotation.z": if (isRoot) r._qz = curve; break;
                        case "m_LocalRotation.w": if (isRoot) r._qw = curve; break;
                    }
                }
                return (r._px != null || r._pz != null) ? r : null;
            }

            public Vector3 PositionAt(float t)
                => new Vector3(_px?.Evaluate(t) ?? 0f, 0f, _pz?.Evaluate(t) ?? 0f);

            /// 그 시점 루트가 향한 평면 방향. 회전 커브가 없으면 +z(제자리 정면)로 본다.
            public Vector3 ForwardAt(float t)
            {
                if (_qw == null) return Vector3.forward;

                var q = new Quaternion(_qx?.Evaluate(t) ?? 0f, _qy?.Evaluate(t) ?? 0f,
                                       _qz?.Evaluate(t) ?? 0f, _qw.Evaluate(t));
                if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) return Vector3.forward;

                Vector3 forward = q.normalized * Vector3.forward;
                forward.y = 0f;
                return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            }
        }

        private static AnimatorState FindState(AnimatorController controller, string stateName)
        {
            foreach (var childState in controller.layers[0].stateMachine.states)
                if (childState.state.name == stateName) return childState.state;
            return null;
        }

        private static bool TryGetSeconds(AnimatorController controller, string stateName, out float seconds)
        {
            seconds = 0f;
            foreach (var childState in controller.layers[0].stateMachine.states)
            {
                var state = childState.state;
                if (state.name != stateName) continue;

                var clip = state.motion as AnimationClip;
                if (clip == null) return false;          // 아직 클립을 안 넣은 스테이트 — 조용히 실패
                if (state.speed <= 0f)
                {
                    Debug.LogError($"[TFW Baker] '{stateName}'의 Speed가 {state.speed} — 0 이하로 둘 수 없음");
                    return false;
                }

                seconds = clip.length / state.speed;
                return true;
            }
            return false;
        }
    }
}
