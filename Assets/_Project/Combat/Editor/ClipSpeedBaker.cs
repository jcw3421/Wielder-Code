using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 클립의 재생 속도를 <b>키프레임 시간 자체에 구워</b> 새 .anim 에셋으로 만든다.
    ///
    /// 왜 필요한가: 애니메이션 클립에는 "배속" 필드가 없다. 키프레임 시간이 초 단위로 박혀 있고
    /// m_SampleRate는 애니메이션 창 눈금용 표시값이라 바꿔도 재생 속도가 변하지 않는다.
    /// 배속을 걸 수 있는 자리(State.speed, BlendTree의 ChildMotion.timeScale)는 전부
    /// 바탕 컨트롤러(Base.controller)에 있어서 무기 4종이 공유한다 —
    /// 무기마다 다른 배속이 필요하면 클립 쪽에 구워야 한다.
    ///
    /// 원본은 건드리지 않고 옆에 새 에셋을 만든다. FBX 안의 클립도 .anim으로 떨어져 나온다.
    /// </summary>
    public static class ClipSpeedBaker
    {
        /// <summary>speed배 빠른 클립을 만들어 원본 옆에 저장한다. 실패하면 null과 사유를 돌려준다.</summary>
        public static AnimationClip Bake(AnimationClip source, float speed, out string message)
        {
            message = null;

            if (source == null)
            {
                message = "원본 클립이 없습니다.";
                return null;
            }

            if (speed <= 0f)
            {
                message = "배속은 0보다 커야 합니다.";
                return null;
            }

            if (Mathf.Approximately(speed, 1f))
            {
                message = "배속이 1이라 구울 것이 없습니다.";
                return null;
            }

            var bindings = AnimationUtility.GetCurveBindings(source);
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);

            // 휴머노이드 클립은 데이터가 머슬 커브(m_FloatCurves)에 들어 있다. 여기가 비면
            // 빈 클립이 만들어져 캐릭터가 T포즈로 굳는다 — 조용히 넘어가지 말고 멈춘다.
            if ((bindings == null || bindings.Length == 0) &&
                (objBindings == null || objBindings.Length == 0))
            {
                message = $"'{source.name}'에서 커브를 읽지 못했습니다. 압축된 클립이거나 " +
                          "에디터가 접근할 수 없는 형식이라 배속 굽기를 할 수 없습니다.";
                return null;
            }

            string path = TargetPath(source, speed);
            if (path == null)
            {
                message = "원본 클립의 경로를 찾지 못했습니다(씬 안의 임시 클립?).";
                return null;
            }

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("클립 배속 굽기",
                    $"이미 있는 파일을 덮어씁니다:\n{path}\n\n계속할까요?", "덮어쓰기", "취소"))
            {
                message = "취소했습니다.";
                return null;
            }

            var baked = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(path),
                // 시간이 1/speed로 줄었으니 샘플레이트를 같이 올려야 키가 눈금에 떨어진다(표시 편의).
                frameRate = source.frameRate * speed,
                wrapMode = source.wrapMode,
                localBounds = source.localBounds,
            };

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null) continue;
                AnimationUtility.SetEditorCurve(baked, binding, ScaleCurve(curve, speed));
            }

            foreach (var binding in objBindings)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                if (keys == null) continue;

                for (int i = 0; i < keys.Length; i++)
                    keys[i].time /= speed;

                AnimationUtility.SetObjectReferenceCurve(baked, binding, keys);
            }

            CopySettings(source, baked, speed);
            CopyEvents(source, baked, speed);

            // 회전 커브가 쿼터니언 뒤집힘 없이 이어지도록 정리한다. 안 하면 특정 프레임에서 팔이 홱 돈다.
            baked.EnsureQuaternionContinuity();

            AssetDatabase.CreateAsset(baked, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            message = $"'{source.name}' ×{speed:0.##} → '{saved.name}'  " +
                      $"({source.length:0.###}s → {saved.length:0.###}s)\n{path}";
            return saved;
        }

        /// <summary>시간을 1/speed로 줄인 커브. 기울기는 "값/시간"이라 시간이 줄면 그만큼 가팔라진다.
        /// 이걸 빼먹으면 키 위치는 맞는데 사이 구간이 출렁인다.</summary>
        private static AnimationCurve ScaleCurve(AnimationCurve curve, float speed)
        {
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time /= speed;
                keys[i].inTangent *= speed;
                keys[i].outTangent *= speed;
                // inWeight/outWeight는 구간 길이에 대한 비율이라 시간이 줄어도 그대로다.
            }

            var scaled = new AnimationCurve(keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode,
            };

            // 탄젠트 모드(Constant/Linear 등)는 keys 배열에 안 실려 오므로 인덱스별로 옮겨준다.
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(scaled, i, AnimationUtility.GetKeyLeftTangentMode(curve, i));
                AnimationUtility.SetKeyRightTangentMode(scaled, i, AnimationUtility.GetKeyRightTangentMode(curve, i));
            }

            return scaled;
        }

        /// 루프·루트 트랜스폼 설정은 그대로 물려주고, 시간 성격을 가진 값만 줄인다.
        private static void CopySettings(AnimationClip source, AnimationClip baked, float speed)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(source);

            settings.startTime /= speed;
            settings.stopTime /= speed;
            settings.additiveReferencePoseTime /= speed;

            AnimationUtility.SetAnimationClipSettings(baked, settings);
        }

        /// 애니메이션 이벤트도 같은 비율로 당겨야 타격·이펙트 마커가 동작과 안 어긋난다.
        private static void CopyEvents(AnimationClip source, AnimationClip baked, float speed)
        {
            var events = AnimationUtility.GetAnimationEvents(source);
            if (events == null || events.Length == 0) return;

            for (int i = 0; i < events.Length; i++)
                events[i].time /= speed;

            AnimationUtility.SetAnimationEvents(baked, events);
        }

        /// 원본 옆에 "이름 (x1.30).anim"으로 만든다. FBX 안의 클립이면 FBX가 있는 폴더에 떨어진다.
        private static string TargetPath(AnimationClip source, float speed)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;

            string dir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) return null;

            // 이미 구운 클립을 또 구우면 "(x1.30) (x1.30)"이 된다 — 접미사를 떼고 다시 붙인다.
            string baseName = StripSuffix(source.name);
            return $"{dir}/{baseName} (x{speed:0.00}).anim";
        }

        private static string StripSuffix(string name)
        {
            int open = name.LastIndexOf(" (x", System.StringComparison.Ordinal);
            if (open < 0 || !name.EndsWith(")", System.StringComparison.Ordinal)) return name;
            return name.Substring(0, open);
        }
    }
}
