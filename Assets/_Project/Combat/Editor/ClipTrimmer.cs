using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 클립의 끝을 잘라낸 새 .anim 에셋을 만든다.
    ///
    /// 왜 필요한가: 공격 클립의 마무리·복귀 구간이 다음 동작(Locomotion의 idle)과 안 이어져
    /// 크로스페이드가 튀는 경우가 있다. 이때 필요한 건 배속이나 페이드 시간이 아니라
    /// "그 꼬리를 아예 없애는" 것이다 — 크로스페이드가 꼬리 대신 잘린 지점의 포즈에서 시작한다.
    ///
    /// 원본은 건드리지 않고 옆에 새 에셋을 만든다. FBX 안의 클립도 .anim으로 떨어져 나온다.
    /// (FBX 임포터의 lastFrame을 줄이는 방법도 있지만, 그건 FBX를 공유하는 다른 공격까지
    ///  같이 잘린다 — 무기별로 다르게 자르려면 클립을 따로 떼야 한다.)
    /// </summary>
    public static class ClipTrimmer
    {
        /// <summary>cutTime 이후를 잘라낸 클립을 원본 옆에 저장한다. 실패하면 null과 사유를 돌려준다.</summary>
        public static AnimationClip TrimTail(AnimationClip source, float cutTime, out string message)
        {
            message = null;

            if (source == null)
            {
                message = "원본 클립이 없습니다.";
                return null;
            }

            if (cutTime <= 0f)
            {
                message = "자를 시점이 0보다 커야 합니다 — 클립이 통째로 비어버립니다.";
                return null;
            }

            if (cutTime >= source.length)
            {
                message = $"자를 시점({cutTime:0.###}s)이 클립 길이({source.length:0.###}s) 이상입니다. " +
                          "잘릴 구간이 없습니다.";
                return null;
            }

            var bindings = AnimationUtility.GetCurveBindings(source);
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);

            // 휴머노이드 클립은 데이터가 머슬 커브에 들어 있다. 여기가 비면 빈 클립이 만들어져
            // 캐릭터가 T포즈로 굳는다 — 조용히 넘어가지 말고 멈춘다. (ClipSpeedBaker와 같은 이유)
            if ((bindings == null || bindings.Length == 0) &&
                (objBindings == null || objBindings.Length == 0))
            {
                message = $"'{source.name}'에서 커브를 읽지 못했습니다. 압축된 클립이거나 " +
                          "에디터가 접근할 수 없는 형식이라 자르기를 할 수 없습니다.";
                return null;
            }

            string path = TargetPath(source, cutTime);
            if (path == null)
            {
                message = "원본 클립의 경로를 찾지 못했습니다(씬 안의 임시 클립?).";
                return null;
            }

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("클립 끝 자르기",
                    $"이미 있는 파일을 덮어씁니다:\n{path}\n\n계속할까요?", "덮어쓰기", "취소"))
            {
                message = "취소했습니다.";
                return null;
            }

            var trimmed = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(path),
                frameRate = source.frameRate,
                wrapMode = source.wrapMode,
                localBounds = source.localBounds,
            };

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null) continue;
                AnimationUtility.SetEditorCurve(trimmed, binding, TrimCurve(curve, cutTime));
            }

            foreach (var binding in objBindings)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                if (keys == null) continue;
                AnimationUtility.SetObjectReferenceCurve(trimmed, binding, TrimObjectCurve(keys, cutTime));
            }

            CopySettings(source, trimmed, cutTime);
            CopyEvents(source, trimmed, cutTime, out int droppedEvents);

            // 회전 커브가 쿼터니언 뒤집힘 없이 이어지도록 정리한다. 안 하면 특정 프레임에서 팔이 홱 돈다.
            trimmed.EnsureQuaternionContinuity();

            AssetDatabase.CreateAsset(trimmed, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            message = $"'{source.name}'을 {cutTime:0.###}s에서 잘랐습니다 → '{saved.name}'  " +
                      $"({source.length:0.###}s → {saved.length:0.###}s, " +
                      $"{source.length - cutTime:0.###}s 제거)\n{path}";

            if (droppedEvents > 0)
                message += $"\n※ 잘린 구간에 있던 애니메이션 이벤트 {droppedEvents}개가 빠졌습니다.";

            return saved;
        }

        /// <summary>cutTime 이후 키를 버리고, 정확히 cutTime에 마지막 키를 세운다.
        /// 그 키가 없으면 클립이 마지막 남은 키 시점에서 끝나 의도한 포즈보다 앞에서 멈춘다.</summary>
        private static AnimationCurve TrimCurve(AnimationCurve curve, float cutTime)
        {
            var src = curve.keys;
            var keys = new List<Keyframe>(src.Length);
            var leftModes = new List<AnimationUtility.TangentMode>(src.Length);
            var rightModes = new List<AnimationUtility.TangentMode>(src.Length);

            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].time > cutTime) break;
                keys.Add(src[i]);
                leftModes.Add(AnimationUtility.GetKeyLeftTangentMode(curve, i));
                rightModes.Add(AnimationUtility.GetKeyRightTangentMode(curve, i));
            }

            // cutTime에 키가 없으면 원본을 그 시점에서 평가해 하나 세운다.
            // 들어오는 기울기는 원본 곡선의 것을 쓰고(직전 구간의 흐름 유지), 나가는 기울기는
            // 0으로 둔다 — 클립이 여기서 끝나므로 뒤로 뻗을 곡선이 없다.
            bool hasKeyAtCut = keys.Count > 0 &&
                               Mathf.Approximately(keys[keys.Count - 1].time, cutTime);

            if (!hasKeyAtCut)
            {
                float value = curve.Evaluate(cutTime);
                keys.Add(new Keyframe(cutTime, value, SlopeBefore(curve, cutTime), 0f));
                leftModes.Add(AnimationUtility.TangentMode.Free);
                rightModes.Add(AnimationUtility.TangentMode.Free);
            }

            var result = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode,
            };

            // 탄젠트 모드는 keys 배열에 안 실려 오므로 인덱스별로 옮겨준다.
            for (int i = 0; i < keys.Count; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(result, i, leftModes[i]);
                AnimationUtility.SetKeyRightTangentMode(result, i, rightModes[i]);
            }

            return result;
        }

        /// cutTime 직전 구간의 기울기를 수치미분으로 잰다. 커브의 탄젠트 모드가 무엇이든
        /// 실제 재생되는 값 기준이라 이어짐이 자연스럽다.
        private static float SlopeBefore(AnimationCurve curve, float cutTime)
        {
            const float eps = 0.001f;
            float back = Mathf.Max(0f, cutTime - eps);
            if (Mathf.Approximately(back, cutTime)) return 0f;
            return (curve.Evaluate(cutTime) - curve.Evaluate(back)) / (cutTime - back);
        }

        private static ObjectReferenceKeyframe[] TrimObjectCurve(
            ObjectReferenceKeyframe[] keys, float cutTime)
        {
            var kept = new List<ObjectReferenceKeyframe>(keys.Length);
            foreach (var k in keys)
            {
                if (k.time > cutTime) break;
                kept.Add(k);
            }
            return kept.ToArray();
        }

        /// 루프·루트 트랜스폼 설정은 그대로 물려주고, 끝 시점만 자른 곳으로 옮긴다.
        private static void CopySettings(AnimationClip source, AnimationClip trimmed, float cutTime)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(source);

            settings.stopTime = cutTime;
            settings.startTime = Mathf.Min(settings.startTime, cutTime);
            settings.additiveReferencePoseTime = Mathf.Min(settings.additiveReferencePoseTime, cutTime);

            // 꼬리를 자르면 첫 프레임과 끝 프레임이 더는 맞지 않는다. 루프가 켜진 채로 두면
            // 마지막에서 처음으로 튄다 — 공격 클립을 자르는 상황에서 루프는 의미가 없다.
            settings.loopTime = false;

            AnimationUtility.SetAnimationClipSettings(trimmed, settings);
        }

        /// 잘린 구간의 이벤트는 버린다. 남기면 클립 끝 뒤에 있는 이벤트가 되어 발동하지 않는다.
        private static void CopyEvents(
            AnimationClip source, AnimationClip trimmed, float cutTime, out int dropped)
        {
            dropped = 0;

            var events = AnimationUtility.GetAnimationEvents(source);
            if (events == null || events.Length == 0) return;

            var kept = new List<AnimationEvent>(events.Length);
            foreach (var e in events)
            {
                if (e.time > cutTime) dropped++;
                else kept.Add(e);
            }

            if (kept.Count > 0)
                AnimationUtility.SetAnimationEvents(trimmed, kept.ToArray());
        }

        /// 원본 옆에 "이름 (cut 0.53s).anim"으로 만든다. FBX 안의 클립이면 FBX가 있는 폴더에 떨어진다.
        private static string TargetPath(AnimationClip source, float cutTime)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;

            string dir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) return null;

            // 이미 자른 클립을 또 자르면 "(cut ..) (cut ..)"이 된다 — 접미사를 떼고 다시 붙인다.
            string baseName = StripSuffix(source.name);
            return $"{dir}/{baseName} (cut {cutTime:0.00}s).anim";
        }

        private static string StripSuffix(string name)
        {
            int open = name.LastIndexOf(" (cut ", System.StringComparison.Ordinal);
            if (open < 0 || !name.EndsWith(")", System.StringComparison.Ordinal)) return name;
            return name.Substring(0, open);
        }
    }
}
