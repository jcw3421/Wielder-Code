using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 휴머노이드 클립의 <b>머슬 커브에 상수 오프셋</b>을 더해 포즈를 보정한다.
    ///
    /// 왜 이게 되는가: 휴머노이드 클립은 뼈 회전이 아니라 "머슬 값"(관절별 -1~1 정규화 값)을
    /// float 커브로 들고 있다. 무릎이 굽은 건 LowerLeg Stretch 값이 낮게 깔려 있다는 뜻이라,
    /// 그 커브 전체를 일정량 올리면 동작은 그대로 두고 무릎만 펼 수 있다.
    ///
    /// 주의: 무릎을 펴면 다리가 길어져 발이 땅에서 뜬다 — RootT.y로 같이 내려야 한다.
    /// 그래서 아래 프리셋에 키 높이를 같이 넣어 뒀다.
    /// </summary>
    public static class PoseOffsetBaker
    {
        /// 머슬 하나에 대한 편집 항목. 이름은 유니티 휴머노이드 머슬 이름 그대로여야 한다.
        public readonly struct MuscleKnob
        {
            public readonly string Label;
            public readonly string[] Muscles;   // 좌우 동시 조정이라 여러 개일 수 있다
            public readonly string Hint;

            public MuscleKnob(string label, string hint, params string[] muscles)
            {
                Label = label;
                Hint = hint;
                Muscles = muscles;
            }
        }

        /// <summary>무릎 굽힘 보정에 실제로 쓰이는 것들만 추렸다.
        /// 이름은 클립의 커브 attribute와 <b>글자 하나까지</b> 같아야 한다 — 띄어쓰기 포함
        /// ("Left Lower Leg Stretch"이지 "LeftLowerLeg Stretch"가 아니다). 틀리면 조용히 아무 일도 안 난다.</summary>
        public static readonly MuscleKnob[] LegKnobs =
        {
            new("무릎 펴기", "+ 하면 무릎이 펴집니다. 다리가 길어져 발이 뜨므로 '키 높이'로 같이 내리세요.",
                "Left Lower Leg Stretch", "Right Lower Leg Stretch"),
            new("허벅지 앞뒤", "+ 하면 허벅지가 앞으로 옵니다. 앉은 듯한 자세를 세울 때.",
                "Left Upper Leg Front-Back", "Right Upper Leg Front-Back"),
            new("다리 벌리기", "+ 하면 다리가 벌어집니다. 발 간격이 좁거나 넓을 때.",
                "Left Upper Leg In-Out", "Right Upper Leg In-Out"),
            new("발 각도", "+ 하면 발끝이 올라갑니다. 무릎을 편 뒤 발바닥 각도를 맞출 때.",
                "Left Foot Up-Down", "Right Foot Up-Down"),
            new("키 높이", "루트의 높이(RootT.y). 무릎을 편 만큼 - 로 내려 발을 땅에 붙입니다.",
                "RootT.y"),
        };

        /// 휴머노이드 머슬 커브는 path 없이 Animator에 float으로 붙는다.
        public static EditorCurveBinding BindingOf(string muscle)
            => EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);

        // ── 본별 편집 ────────────────────────────────────────────────────────

        /// 휴머노이드 본 하나와, 그 본의 회전을 이루는 머슬들(최대 3축).
        public readonly struct BoneEntry
        {
            public readonly string BoneName;
            public readonly string[] MuscleNames;
            public readonly int[] MuscleIndices;   // HumanTrait 인덱스 — 가동범위(도)를 얻는 데 쓴다

            public BoneEntry(string boneName, string[] muscleNames, int[] muscleIndices)
            {
                BoneName = boneName;
                MuscleNames = muscleNames;
                MuscleIndices = muscleIndices;
            }
        }

        /// <summary>이 클립이 실제로 애니메이션하는 휴머노이드 본 목록.
        /// 본→머슬 매핑은 문자열을 쪼개지 않고 HumanTrait에서 그대로 가져온다 —
        /// "Left Upper Leg Front-Back" 같은 이름을 파싱하면 손가락·눈에서 깨진다.</summary>
        public static List<BoneEntry> BonesIn(AnimationClip clip)
        {
            var result = new List<BoneEntry>();
            var present = MusclesIn(clip);
            if (present.Count == 0) return result;

            var muscleNames = HumanTrait.MuscleName;

            for (int bone = 0; bone < HumanTrait.BoneCount; bone++)
            {
                var names = new List<string>(3);
                var indices = new List<int>(3);

                for (int dof = 0; dof < 3; dof++)
                {
                    int muscle = HumanTrait.MuscleFromBone(bone, dof);
                    if (muscle < 0 || muscle >= muscleNames.Length) continue;
                    if (!present.Contains(muscleNames[muscle])) continue;

                    names.Add(muscleNames[muscle]);
                    indices.Add(muscle);
                }

                if (names.Count > 0)
                    result.Add(new BoneEntry(HumanTrait.BoneName[bone], names.ToArray(), indices.ToArray()));
            }

            return result;
        }

        /// 머슬 값(-1~1)이 실제로 몇 도인지. 슬라이더 숫자만으로는 감이 안 오므로 옆에 적어준다.
        public static float MuscleDegrees(int muscleIndex, float value)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount) return 0f;

            // 값이 +면 최대각 쪽, -면 최소각 쪽으로 간다(최소각은 보통 음수).
            float limit = value >= 0f
                ? HumanTrait.GetMuscleDefaultMax(muscleIndex)
                : -HumanTrait.GetMuscleDefaultMin(muscleIndex);

            return value * limit;
        }

        public static (float min, float max) MuscleRangeDegrees(int muscleIndex)
            => muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount
                ? (0f, 0f)
                : (HumanTrait.GetMuscleDefaultMin(muscleIndex), HumanTrait.GetMuscleDefaultMax(muscleIndex));

        // ── 위치 커브 ────────────────────────────────────────────────────────

        /// <summary>위치를 바꿀 수 있는 커브. 휴머노이드는 <b>일반 본의 위치를 저장하지 않으므로</b>
        /// 여기 나오는 것(루트·모션·IK 골)이 전부다.</summary>
        public static List<string> PositionCurvesIn(AnimationClip clip)
        {
            var result = new List<string>();
            if (clip == null) return result;

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(Animator) || !string.IsNullOrEmpty(b.path)) continue;
                if (IsTranslationCurve(b.propertyName)) result.Add(b.propertyName);
            }

            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        /// "RootT.y", "LeftFootT.x"처럼 위치 성분인가. 회전(Q)과 머슬은 제외한다.
        public static bool IsTranslationCurve(string propertyName)
            => propertyName != null
               && (propertyName.EndsWith("T.x", System.StringComparison.Ordinal)
                   || propertyName.EndsWith("T.y", System.StringComparison.Ordinal)
                   || propertyName.EndsWith("T.z", System.StringComparison.Ordinal));

        /// <summary>클립이 갖고 있는 Animator 커브 이름 전부 — 머슬뿐 아니라
        /// RootT/RootQ·MotionT/Q·IK 골(LeftFootT 등)도 같은 자리에 들어 있다.
        /// 없는 커브에 오프셋을 걸면 원래 없던 커브가 생겨 다른 동작을 덮어쓸 수 있어서,
        /// UI가 이 목록으로 걸러낸다.</summary>
        public static HashSet<string> MusclesIn(AnimationClip clip)
        {
            var set = new HashSet<string>();
            if (clip == null) return set;

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.type == typeof(Animator) && string.IsNullOrEmpty(b.path))
                    set.Add(b.propertyName);

            return set;
        }

        /// <summary>오프셋을 적용한 사본을 만든다. 저장하지 않는 메모리 클립이라 프리뷰에 쓴다.
        /// 원본은 건드리지 않는다.</summary>
        public static AnimationClip CreateOffsetCopy(AnimationClip source, IReadOnlyDictionary<string, float> offsets)
        {
            if (source == null) return null;

            // 통째 복제는 유니티 내부에서 처리돼 커브를 하나씩 옮기는 것보다 훨씬 빠르다.
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            ApplyOffsets(copy, offsets);
            return copy;
        }

        /// 사본에 오프셋을 실제로 더한다. 클립에 없는 머슬은 건너뛴다.
        private static void ApplyOffsets(AnimationClip clip, IReadOnlyDictionary<string, float> offsets)
        {
            foreach (var kv in offsets)
            {
                if (Mathf.Approximately(kv.Value, 0f)) continue;

                var binding = BindingOf(kv.Key);
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;   // 이 클립이 안 쓰는 관절 — 새로 만들지 않는다

                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                    keys[i].value += kv.Value;

                // 값만 옮기므로 기울기·탄젠트 모드는 그대로 둔다(모양이 안 변한다).
                var shifted = new AnimationCurve(keys)
                {
                    preWrapMode = curve.preWrapMode,
                    postWrapMode = curve.postWrapMode,
                };
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(shifted, i, AnimationUtility.GetKeyLeftTangentMode(curve, i));
                    AnimationUtility.SetKeyRightTangentMode(shifted, i, AnimationUtility.GetKeyRightTangentMode(curve, i));
                }

                AnimationUtility.SetEditorCurve(clip, binding, shifted);
            }
        }

        /// <summary>보정한 클립을 원본 옆에 새 에셋으로 저장한다. FBX 안의 클립도 .anim으로 떨어진다.</summary>
        public static AnimationClip Bake(AnimationClip source, IReadOnlyDictionary<string, float> offsets, out string message)
        {
            message = null;

            if (source == null)
            {
                message = "원본 클립이 없습니다.";
                return null;
            }

            bool any = false;
            foreach (var kv in offsets)
                if (!Mathf.Approximately(kv.Value, 0f)) { any = true; break; }

            if (!any)
            {
                message = "보정값이 전부 0이라 구울 것이 없습니다.";
                return null;
            }

            var muscles = MusclesIn(source);
            if (muscles.Count == 0)
            {
                message = $"'{source.name}'에서 휴머노이드 머슬 커브를 찾지 못했습니다. " +
                          "제네릭 리그이거나 압축된 클립이라 포즈 보정을 할 수 없습니다.";
                return null;
            }

            string path = TargetPath(source);
            if (path == null)
            {
                message = "원본 클립의 경로를 찾지 못했습니다.";
                return null;
            }

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("포즈 보정 굽기",
                    $"이미 있는 파일을 덮어씁니다:\n{path}\n\n계속할까요?", "덮어쓰기", "취소"))
            {
                message = "취소했습니다.";
                return null;
            }

            var baked = Object.Instantiate(source);
            baked.name = Path.GetFileNameWithoutExtension(path);
            ApplyOffsets(baked, offsets);
            baked.EnsureQuaternionContinuity();

            AssetDatabase.CreateAsset(baked, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            var applied = new List<string>();
            foreach (var kv in offsets)
            {
                if (Mathf.Approximately(kv.Value, 0f)) continue;
                applied.Add(muscles.Contains(kv.Key)
                    ? $"{kv.Key} {kv.Value:+0.###;-0.###}"
                    : $"{kv.Key} (클립에 없음 — 건너뜀)");
            }

            message = $"'{source.name}' → '{saved.name}'\n{string.Join("  ·  ", applied)}\n{path}";
            return saved;
        }

        private static string TargetPath(AnimationClip source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;

            string dir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) return null;

            // 이미 보정한 클립을 또 보정해도 접미사가 겹치지 않게 한 번만 붙인다.
            string baseName = source.name.EndsWith(" (pose)", System.StringComparison.Ordinal)
                ? source.name
                : source.name + " (pose)";

            return $"{dir}/{baseName}.anim";
        }
    }
}
