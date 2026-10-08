using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>클립에 박힌 사운드용 애니메이션 이벤트(PlayerAnimationSound.PlaySfx)를 읽고 쓴다.
    ///
    /// 이벤트가 어디에 저장되는지가 클립 종류마다 다르다:
    ///  - .anim 에셋: 클립 자체에 들어간다 (AnimationUtility).
    ///  - FBX 안의 클립: 클립은 임포트 결과물이라 직접 쓰면 재임포트 때 날아간다 —
    ///    ModelImporter의 클립 설정에 넣고 다시 임포트해야 남는다.
    /// 호출부가 이 차이를 몰라도 되게 여기서 갈라 준다.</summary>
    public static class AnimationSoundEvents
    {
        /// PlayerAnimationSound의 메서드 이름. 이름이 바뀌면 기존 클립의 이벤트가 전부 죽는다.
        public const string FunctionName = "PlaySfx";

        public static AnimationEvent[] All(AnimationClip clip)
            => clip == null ? Array.Empty<AnimationEvent>() : AnimationUtility.GetAnimationEvents(clip);

        /// 사운드 이벤트만, 시간순으로. 반환된 배열은 복사본이라 고쳐도 클립에 반영되지 않는다.
        public static List<AnimationEvent> Sounds(AnimationClip clip)
        {
            var list = new List<AnimationEvent>();
            foreach (var e in All(clip))
                if (e.functionName == FunctionName) list.Add(e);

            list.Sort((a, b) => a.time.CompareTo(b.time));
            return list;
        }

        public static AnimationEvent New(float time, AudioClip audio) => new()
        {
            functionName = FunctionName,
            time = time,
            objectReferenceParameter = audio,
        };

        /// <summary>사운드 이벤트 목록을 클립에 확정한다. 사운드가 아닌 이벤트(Hit 등)는 그대로 둔다.
        /// 성공하면 true, log에 무슨 일이 있었는지 담는다.</summary>
        public static bool Write(AnimationClip clip, List<AnimationEvent> sounds, out string log)
        {
            log = null;
            if (clip == null) { log = "클립이 없습니다."; return false; }

            var merged = new List<AnimationEvent>();
            foreach (var e in All(clip))
                if (e.functionName != FunctionName) merged.Add(e);
            merged.AddRange(sounds);
            merged.Sort((a, b) => a.time.CompareTo(b.time));

            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path))
            {
                log = "에셋이 아닌 클립에는 이벤트를 저장할 수 없습니다.";
                return false;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            return importer != null
                ? WriteToModel(importer, clip, merged, out log)
                : WriteToAnimAsset(clip, merged, out log);
        }

        public static bool IsModelClip(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            return !string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter;
        }

        /// 이 클립을 쓰는 곳이 여럿일 수 있다 — 공유 클립에 소리를 박으면 다 같이 난다.
        public static string OwnerPath(AnimationClip clip) => AssetDatabase.GetAssetPath(clip);

        private static bool WriteToAnimAsset(AnimationClip clip, List<AnimationEvent> events, out string log)
        {
            Undo.RecordObject(clip, "사운드 이벤트 편집");
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            log = $"'{clip.name}'에 이벤트 {events.Count}개를 저장했습니다.";
            return true;
        }

        /// <summary>FBX 클립. ModelImporter의 클립 설정에 이벤트를 넣고 재임포트한다.
        /// clipAnimations가 비어 있으면 임포터가 기본값(defaultClipAnimations)으로 도는 상태라,
        /// 먼저 그 기본값을 그대로 복사해 넣어야 나머지 설정(루프·루트모션 등)이 안 바뀐다.</summary>
        private static bool WriteToModel(ModelImporter importer, AnimationClip clip,
            List<AnimationEvent> events, out string log)
        {
            var clips = importer.clipAnimations;
            bool wasDefault = clips == null || clips.Length == 0;
            if (wasDefault) clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0)
            {
                log = $"'{importer.assetPath}'에서 클립 설정을 찾지 못했습니다.";
                return false;
            }

            ModelImporterClipAnimation target = null;
            foreach (var c in clips)
                if (c.name == clip.name) { target = c; break; }

            if (target == null)
            {
                log = $"'{importer.assetPath}'의 클립 목록에 '{clip.name}'이 없습니다.";
                return false;
            }

            target.events = events.ToArray();
            importer.clipAnimations = clips;   // 배열째 다시 넣어야 반영된다
            importer.SaveAndReimport();

            log = $"'{clip.name}'(FBX)에 이벤트 {events.Count}개를 저장하고 재임포트했습니다." +
                  (wasDefault ? "\n※ 이 FBX의 클립 설정이 '기본값'에서 '직접 지정'으로 바뀌었습니다(값은 그대로)." : "");
            return true;
        }
    }

    /// <summary>에디터에서 AudioClip을 즉석에서 들려준다. 플레이 모드 없이 소리를 내는 공개 API가 없어
    /// UnityEditor 내부의 AudioUtil을 리플렉션으로 부른다 — 버전에 따라 이름이 달라 후보를 차례로 찾는다.
    /// (모듈 경계를 우회하는 용도가 아니라, 에디터 전용 미리듣기다.)</summary>
    public static class EditorAudioPreview
    {
        private static MethodInfo _play;
        private static MethodInfo _stop;
        private static bool _resolved;

        /// 리플렉션이 실패했으면 false — 호출부가 "미리듣기 안 됨"을 표시한다.
        public static bool Available
        {
            get { Resolve(); return _play != null; }
        }

        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            Resolve();
            if (_play == null) return;

            Stop();

            // PlayPreviewClip(clip, startSample, loop) / 구버전 PlayClip(clip, startSample, loop)
            var ps = _play.GetParameters();
            object[] args = ps.Length == 1
                ? new object[] { clip }
                : ps.Length == 2
                    ? new object[] { clip, 0 }
                    : new object[] { clip, 0, false };

            _play.Invoke(null, args);
        }

        public static void Stop()
        {
            Resolve();
            _stop?.Invoke(null, null);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (type == null) return;

            const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in new[] { "PlayPreviewClip", "PlayClip" })
            {
                foreach (var m in type.GetMethods(Flags))
                {
                    if (m.Name != name) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 0 || ps[0].ParameterType != typeof(AudioClip)) continue;
                    _play = m;
                    break;
                }
                if (_play != null) break;
            }

            foreach (var name in new[] { "StopAllPreviewClips", "StopAllClips" })
            {
                var m = type.GetMethod(name, Flags, null, Type.EmptyTypes, null);
                if (m != null) { _stop = m; break; }
            }
        }
    }
}
