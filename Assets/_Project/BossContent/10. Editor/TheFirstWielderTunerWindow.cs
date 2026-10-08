using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Game.BossContent.Editor
{
    /// TFW PatternDefinition 원본을 직접 편집하는 씬 기반 제작 도구.
    public sealed class TheFirstWielderTunerWindow : EditorWindow
    {
        private const string ControllerPath = "Assets/_Project/BossContent/05. Animators/TheFirstWielderBoss/TheFirstWielderBoss.controller";
        private const float Fps = 60f;

        private sealed class Segment
        {
            public string Name; public AnimationClip Clip; public AnimatorState State;
            public float Start, Duration, Speed;
        }

        [MenuItem("도구/보스/더 퍼스트 윌더 패턴 제작소")]
        public static void Open()
        {
            var w = GetWindow<TheFirstWielderTunerWindow>();
            w.titleContent = new GUIContent("TFW 패턴 제작소");
            w.minSize = new Vector2(560f, 620f);
        }

        private PatternDefinition _pattern;
        private GameObject _bossPrefab;
        private BossDefinition _bossDefinition;
        private AnimatorController _controller;
        private readonly List<PatternDefinition> _patterns = new();
        private int _patternIndex;
        private Animator _boss;
        private SerializedObject _so;
        private readonly List<Segment> _segments = new();
        private readonly Dictionary<int, bool> _segmentFoldouts = new();
        private readonly Queue<Action> _pendingGuiActions = new();
        private readonly List<string> _timings = new(), _ranges = new();
        private string _effectsPath;
        private float _time, _duration, _playSpeed = 1f;
        private bool _playing, _loop = true;
        private bool _showAdvanced;
        private double _lastUpdate;
        private int _effectIndex, _rangeIndex;
        private int _timingIndex;
        private int _segmentIndex = -1;
        private bool _phaseTransitionSelected;
        private bool _hasPhaseTransitionCues;
        private readonly List<string> _selectedHitGeometry = new();
        private float _selectedHitAbsoluteTime = float.NaN;
        private enum EditTarget { None, Segment, Timing, Range, Effect }
        private EditTarget _editTarget;
        private Vector2 _scroll;
        private GameObject _effectPreview;
        private Scene _previewScene;
        private GameObject _previewBossRoot;
        private Animator _previewBossAnimator;
        private readonly Dictionary<Renderer, Mesh> _bakedPreviewMeshes = new();

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (_bossPrefab == null)
            {
                _bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/BossContent/03. Prefabs/Boss/Boss_TheFirstWielder.prefab");
                CollectPatterns();
            }
        }
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI; EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            StopPreview(); DestroyEffectPreview(); ClosePreviewScene();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("더 퍼스트 윌더 패턴 제작소", EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("플레이 모드에서는 편집과 미리보기가 비활성화됩니다.", MessageType.Info);
                return;
            }
            DrawSaveToolbar();
            EditorGUILayout.HelpBox("보스 프리팹에 연결된 모든 TFW 패턴 원본을 자동으로 찾아 직접 편집합니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var prefab = (GameObject)EditorGUILayout.ObjectField("보스 프리팹", _bossPrefab, typeof(GameObject), false);
            Animator boss = _boss;
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "고급 설정", true);
            if (_showAdvanced) boss = (Animator)EditorGUILayout.ObjectField("씬 미리보기 보스", _boss, typeof(Animator), true);
            if (EditorGUI.EndChangeCheck())
            {
                _boss = boss;
                if (prefab != _bossPrefab) { _bossPrefab = prefab; CollectPatterns(); }
                else Rebuild();
            }
            if (_patterns.Count == 0) { EditorGUILayout.HelpBox("Boss_TheFirstWielder 프리팹을 지정하세요.", MessageType.Warning); return; }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
            _scroll = scroll.scrollPosition;
            DrawPatternList();
            if (_phaseTransitionSelected)
                EditorGUILayout.HelpBox("페이즈 전환 이펙트 큐는 Boss_TheFirstWielder 프리팹의 BossPatternEffectPlayer._phaseTransitionCues에 저장됩니다. 프리팹 전체 재직렬화 위험을 피하기 위해 현재 제작소에서는 읽기 전용으로 표시합니다. 프리팹 인스펙터에서 해당 배열을 편집하세요.", MessageType.Info);
            else { DrawTransport(); DrawSegments(); }
            }
            FlushPendingGuiActions();
        }

        private void DrawSaveToolbar()
        {
            bool dirty = (_pattern != null && EditorUtility.IsDirty(_pattern)) || (_controller != null && EditorUtility.IsDirty(_controller));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(dirty ? "● 저장되지 않은 변경 있음" : "✓ 저장됨", GUILayout.Width(180));
                if (GUILayout.Button("저장", EditorStyles.toolbarButton)) SaveCurrent();
                if (GUILayout.Button("모두 저장", EditorStyles.toolbarButton)) { AssetDatabase.SaveAssets(); ShowNotification(new GUIContent("모든 변경을 저장했습니다.")); }
                if (GUILayout.Button("현재 패턴 변경 되돌리기", EditorStyles.toolbarButton)) ReloadCurrent();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.modifiers == EventModifiers.Control && Event.current.keyCode == KeyCode.S)
            { if (SaveCurrent()) Event.current.Use(); }
        }
        private bool SaveCurrent()
        {
            if (_pattern == null) { ShowNotification(new GUIContent("저장할 패턴이 없습니다.")); return false; }
            try
            {
                EditorUtility.SetDirty(_pattern); AssetDatabase.SaveAssetIfDirty(_pattern);
                if (_controller != null) AssetDatabase.SaveAssetIfDirty(_controller);
                ShowNotification(new GUIContent("현재 패턴과 애니메이터 컨트롤러를 저장했습니다.")); Repaint(); return true;
            }
            catch (Exception exception) { Debug.LogException(exception); ShowNotification(new GUIContent("저장에 실패했습니다. 콘솔을 확인하세요.")); return false; }
        }
        private void ReloadCurrent()
        {
            if (_pattern == null || !EditorUtility.IsDirty(_pattern)) return;
            string path = AssetDatabase.GetAssetPath(_pattern); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            _pattern = AssetDatabase.LoadAssetAtPath<PatternDefinition>(path); Rebuild();
            ShowNotification(new GUIContent("현재 패턴 SO만 저장된 상태로 되돌렸습니다. 컨트롤러 변경은 유지됩니다."));
        }

        private void DrawTransport()
        {
            Header("애니메이션 미리보기");
            SelectedBounds(out float sliderStart, out float sliderEnd);
            EditorGUI.BeginChangeCheck();
            _time = EditorGUILayout.Slider("시간", _time, sliderStart, Mathf.Max(sliderStart + .001f, sliderEnd));
            if (EditorGUI.EndChangeCheck()) Sample();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "일시정지" : "재생")) { _playing = !_playing; _lastUpdate = EditorApplication.timeSinceStartup; Sample(); }
                if (GUILayout.Button("정지")) { _playing = false; SelectedBounds(out float stopStart, out _); _time = stopStart; Sample(); }
                if (GUILayout.Button("|<")) { SelectedBounds(out float first, out _); _time = first; Sample(); }
                if (GUILayout.Button("이전 프레임")) Step(-1);
                if (GUILayout.Button("다음 프레임")) Step(1);
                if (GUILayout.Button(">|")) { SelectedBounds(out _, out float last); _time = last; Sample(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _loop = EditorGUILayout.ToggleLeft("반복", _loop, GUILayout.Width(65));
                _playSpeed = EditorGUILayout.Slider("재생 속도", _playSpeed, .1f, 3f);
                GUILayout.Label($"{_time:F3}s / {_duration:F3}s ({Mathf.RoundToInt(_time * Fps)}f)", GUILayout.Width(165));
            }
            if (_boss == null) EditorGUILayout.HelpBox("씬의 Boss_TheFirstWielder Animator를 지정하면 포즈와 이펙트를 미리 봅니다.", MessageType.None);
        }

        private void DrawPatternList()
        {
            Header("패턴 목록");
            int columns = Mathf.Max(1, Mathf.FloorToInt(position.width / 170f));
            for (int i = 0; i < _patterns.Count; i += columns)
                using (new EditorGUILayout.HorizontalScope())
                    for (int j = i; j < Mathf.Min(i + columns, _patterns.Count); j++)
                    {
                        bool selected = !_phaseTransitionSelected && j == _patternIndex;
                        if (GUILayout.Toggle(selected, $"{_patterns[j].name}  (페이즈 {_patterns[j].UnlockPhase}~{_patterns[j].LockPhase})", "Button") && !selected)
                        { _phaseTransitionSelected = false; _patternIndex = j; _pattern = _patterns[j]; SelectTarget(EditTarget.None); Rebuild(); }
                    }
            if (_hasPhaseTransitionCues && GUILayout.Toggle(_phaseTransitionSelected, "페이즈 전환  (특수 항목 · 읽기 전용)", "Button") && !_phaseTransitionSelected)
            { _phaseTransitionSelected = true; SelectTarget(EditTarget.None); }
            EditorGUILayout.LabelField("현재 패턴", _phaseTransitionSelected ? "페이즈 전환" : _pattern == null ? "없음" : _pattern.name);
        }

        private void DrawSegments()
        {
            Header("애니메이션 조각 슬롯");
            if (_segments.Count == 0) { EditorGUILayout.HelpBox("{패턴}_Active 이름의 애니메이터 상태를 찾지 못했습니다.", MessageType.Warning); return; }
            if (_so.FindProperty("SegmentStartSeconds") != null)
            {
                if (GUILayout.Button("현재 배속 기준 타임라인 재동기화"))
                    _pendingGuiActions.Enqueue(SynchronizeTimelineToCurrentSpeeds);
                EditorGUILayout.LabelField("저장된 조각 경계를 기준으로 타격·이펙트·이동 곡선을 현재 클립 배속에 맞춥니다. 반복 실행해도 값이 달라지지 않습니다.", EditorStyles.wordWrappedMiniLabel);
            }
            for (int segmentIndex = 0; segmentIndex < _segments.Count; segmentIndex++)
            {
                var s = _segments[segmentIndex];
                bool segmentSelected = _segmentIndex == segmentIndex && _editTarget != EditTarget.None;
                bool expanded = !_segmentFoldouts.TryGetValue(segmentIndex, out bool stored) || stored;
                var cardStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    margin = new RectOffset(8, 8, 12, 12),
                    padding = new RectOffset(12, 12, 10, 12)
                };
                if (segmentSelected) cardStyle.normal.background = EditorStyles.selectionRect.normal.background;
                using (new EditorGUILayout.VerticalScope(cardStyle))
                {
                    Rect accent = EditorGUILayout.GetControlRect(false, segmentSelected ? 4f : 2f);
                    EditorGUI.DrawRect(accent, segmentSelected ? new Color(.2f, .65f, 1f, 1f) : new Color(.35f, .35f, .35f, .8f));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect foldoutRect = GUILayoutUtility.GetRect(16f, 18f, GUILayout.Width(16f));
                        bool nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none);
                        if (nextExpanded != expanded)
                        {
                            int pendingSegment = segmentIndex; bool pendingExpanded = nextExpanded;
                            _pendingGuiActions.Enqueue(() => { _segmentFoldouts[pendingSegment] = pendingExpanded; Repaint(); });
                        }
                        var headerButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                        if (GUILayout.Button($"클립 {segmentIndex + 1} / {_segments.Count} · {s.Clip.name}", headerButton))
                        {
                            int pendingSegment = segmentIndex;
                            _pendingGuiActions.Enqueue(() => SelectSegment(pendingSegment, FirstCueInSegment(pendingSegment)));
                        }
                        GUILayout.Label(segmentSelected ? "선택됨" : string.Empty, EditorStyles.miniButton, GUILayout.Width(58));
                    }
                    var cues = CuesInSegment(segmentIndex);
                    EditorGUILayout.LabelField($"시간 범위  {s.Start:F3}초 – {s.Start + s.Duration:F3}초   |   이펙트 {cues.Count}개   |   타격 {HitCountInSegment(segmentIndex)}개", EditorStyles.miniLabel);
                    if (!expanded) continue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("클립 설정", EditorStyles.boldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("재생 속도", GUILayout.Width(75));
                            EditorGUI.BeginChangeCheck(); float speed = EditorGUILayout.DelayedFloatField(s.Speed);
                            if (EditorGUI.EndChangeCheck() && speed > 0)
                            {
                                AnimatorState pendingState = s.State; float pendingSpeed = speed;
                                _pendingGuiActions.Enqueue(() => RebakeSegmentSpeed(pendingState, pendingSpeed));
                            }
                            if (GUILayout.Button("클립 자산 선택", GUILayout.Width(100))) Selection.activeObject = s.Clip;
                        }
                    }

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"이펙트 · {cues.Count}개", EditorStyles.boldLabel);
                        if (cues.Count == 0)
                        {
                            EditorGUILayout.LabelField("이펙트 없음", EditorStyles.miniLabel);
                            if (GUILayout.Button("이 클립에 이펙트 추가"))
                            { int pendingSegment = segmentIndex; _pendingGuiActions.Enqueue(() => AddCueToSegment(pendingSegment)); }
                        }
                        else foreach (int cueIndex in cues)
                        {
                            var cue = _so.FindProperty(_effectsPath).GetArrayElementAtIndex(cueIndex);
                            string label = cue.FindPropertyRelative("Label").stringValue;
                            bool cueSelected = _editTarget == EditTarget.Segment && _segmentIndex == segmentIndex && _effectIndex == cueIndex;
                            if (GUILayout.Toggle(cueSelected, $"이펙트 {cueIndex + 1}: {(string.IsNullOrEmpty(label) ? "이름 없음" : label)}", "Button") && !cueSelected)
                            {
                                int pendingSegment = segmentIndex, pendingCue = cueIndex;
                                _pendingGuiActions.Enqueue(() => SelectSegment(pendingSegment, pendingCue));
                            }
                        }
                        if (_editTarget == EditTarget.Segment && _segmentIndex == segmentIndex && _effectIndex >= 0)
                            DrawSelectedCueFields();
                    }

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("타격 시점 · 판정 범위 · 다단히트", EditorStyles.boldLabel);
                        DrawSegmentHits(segmentIndex);
                        DrawGeneratedHits(segmentIndex);
                        DrawGenericRepeatHits(segmentIndex);
                    }
                }
            }
        }

        private void FlushPendingGuiActions()
        {
            while (_pendingGuiActions.Count > 0)
            {
                try { _pendingGuiActions.Dequeue()?.Invoke(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private int HitCountInSegment(int segmentIndex)
        {
            int count = TimingsInSegment(segmentIndex).Count;
            var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            foreach (string path in _timings)
            {
                var timing = _so.FindProperty(path); string key = timing.name;
                if (TimingKind(key) != "타격") continue;
                int repeats;
                float interval;
                if (_pattern is RushSixStrikeDefinition && key == "FirstStrikeAtSeconds")
                { repeats = _so.FindProperty("StrikeCount").intValue; interval = _so.FindProperty("StrikeIntervalSeconds").floatValue; }
                else if (_pattern is CascadeFieldComboDefinition && key == "FirstFieldAtSeconds")
                { repeats = _so.FindProperty("FieldWaveCount").intValue; interval = _so.FindProperty("FieldIntervalSeconds").floatValue; }
                else
                {
                    repeats = GenericRepeatCount(key);
                    if (repeats <= 1) continue;
                    interval = GenericRepeatInterval(key);
                }
                for (int i = 0; i < Mathf.Max(0, repeats); i++)
                {
                    float at = IsNativeRepeatKey(key)
                        ? timing.floatValue + Mathf.Max(0f, interval) * i
                        : GenericRepeatTimeAt(key, timing.floatValue, i);
                    if (at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end)) count++;
                }
            }
            return count;
        }

        private void RebakeSegmentSpeed(AnimatorState state, float newSpeed)
        {
            int changed = _segments.FindIndex(segment => segment.State == state);
            if (changed < 0 || newSpeed <= 0f || Mathf.Approximately(_segments[changed].Speed, newSpeed)) return;

            Undo.RecordObject(_pattern, "TFW 애니메이션 배속 타임라인 재계산");
            Undo.RecordObject(state, "TFW 애니메이션 조각 속도 변경");
            if (_controller != null) Undo.RecordObject(_controller, "TFW 애니메이션 배속 타임라인 재계산");

            int count = _segments.Count;
            var oldStarts = new float[count]; var oldDurations = new float[count]; var newStarts = new float[count]; var newDurations = new float[count];
            float oldTotal = 0f, newTotal = 0f;
            for (int i = 0; i < count; i++)
            {
                oldStarts[i] = _segments[i].Start; oldDurations[i] = _segments[i].Duration; oldTotal += oldDurations[i];
                newStarts[i] = newTotal; newDurations[i] = i == changed ? _segments[i].Clip.length / newSpeed : oldDurations[i]; newTotal += newDurations[i];
            }
            float MapTime(float value)
            {
                if (value >= oldTotal) return newTotal + (value - oldTotal);
                for (int i = 0; i < count; i++)
                    if (value < oldStarts[i] + oldDurations[i] || i == count - 1)
                        return newStarts[i] + Mathf.Clamp01((value - oldStarts[i]) / Mathf.Max(.0001f, oldDurations[i])) * newDurations[i];
                return value;
            }

            _so.Update();
            var oldTiming = new Dictionary<string, float>();
            foreach (string path in _timings) { var property = _so.FindProperty(path); oldTiming[property.name] = property.floatValue; }
            foreach (string path in _timings) { var property = _so.FindProperty(path); property.floatValue = MapTime(property.floatValue); }
            var cues = string.IsNullOrEmpty(_effectsPath) ? null : _so.FindProperty(_effectsPath);
            if (cues != null) for (int i = 0; i < cues.arraySize; i++)
            {
                var at = cues.GetArrayElementAtIndex(i).FindPropertyRelative("AtSeconds"); at.floatValue = MapTime(at.floatValue);
            }
            var starts = _so.FindProperty("SegmentStartSeconds");
            if (starts != null && starts.isArray)
            {
                starts.arraySize = count;
                for (int i = 0; i < count; i++) starts.GetArrayElementAtIndex(i).floatValue = newStarts[i];
            }
            var active = _so.FindProperty("ActiveSeconds"); if (active != null) active.floatValue = newTotal;

            var repeats = _so.FindProperty("_hitRepeats");
            if (repeats != null) for (int i = 0; i < repeats.arraySize; i++)
            {
                var repeat = repeats.GetArrayElementAtIndex(i); string key = repeat.FindPropertyRelative("TimingKey").stringValue;
                if (!oldTiming.TryGetValue(key, out float baseAt)) continue;
                if (RemapRepeatTimes(repeat, MapTime)) continue;
                var interval = repeat.FindPropertyRelative("IntervalSeconds");
                interval.floatValue = Mathf.Max(0f, MapTime(baseAt + interval.floatValue) - MapTime(baseAt));
            }
            RebakeNativeInterval("FirstStrikeAtSeconds", "StrikeIntervalSeconds", oldTiming, MapTime);
            RebakeNativeInterval("FirstFieldAtSeconds", "FieldIntervalSeconds", oldTiming, MapTime);

            var curve = _so.FindProperty("ForwardDistanceCurve");
            if (curve != null && curve.propertyType == SerializedPropertyType.AnimationCurve)
            {
                var source = curve.animationCurveValue; var keys = source.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    float oldTime = keys[k].time; int segmentIndex = Mathf.Clamp(SegmentIndexFor(oldTime, oldStarts, oldDurations), 0, count - 1);
                    float tangentScale = oldDurations[segmentIndex] / Mathf.Max(.0001f, newDurations[segmentIndex]);
                    keys[k].time = MapTime(oldTime); keys[k].inTangent *= tangentScale; keys[k].outTangent *= tangentScale;
                }
                source.keys = keys; curve.animationCurveValue = source;
            }
            state.speed = newSpeed;
            _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern); EditorUtility.SetDirty(state);
            if (_controller != null) EditorUtility.SetDirty(_controller);
            Rebuild(); Repaint(); SceneView.RepaintAll();
        }

        private void SynchronizeTimelineToCurrentSpeeds()
        {
            _so.Update(); int count = _segments.Count;
            var storedStarts = _so.FindProperty("SegmentStartSeconds"); var active = _so.FindProperty("ActiveSeconds");
            if (storedStarts == null || !storedStarts.isArray || storedStarts.arraySize != count || active == null)
            { ShowNotification(new GUIContent("저장된 조각 경계가 없어 재동기화할 수 없습니다.")); return; }

            var oldStarts = new float[count]; var oldDurations = new float[count]; var newStarts = new float[count]; var newDurations = new float[count];
            float newTotal = 0f;
            for (int i = 0; i < count; i++)
            {
                oldStarts[i] = storedStarts.GetArrayElementAtIndex(i).floatValue;
                float oldEnd = i + 1 < count ? storedStarts.GetArrayElementAtIndex(i + 1).floatValue : active.floatValue;
                oldDurations[i] = oldEnd - oldStarts[i];
                if (oldDurations[i] <= .0001f) { ShowNotification(new GUIContent("저장된 조각 경계가 올바르지 않아 중단했습니다.")); return; }
                newStarts[i] = newTotal; newDurations[i] = _segments[i].Clip.length / Mathf.Max(.01f, _segments[i].State.speed); newTotal += newDurations[i];
            }
            bool alreadySynchronized = Mathf.Approximately(active.floatValue, newTotal);
            for (int i = 0; i < count && alreadySynchronized; i++)
                alreadySynchronized = Mathf.Approximately(oldStarts[i], newStarts[i]) && Mathf.Approximately(oldDurations[i], newDurations[i]);
            if (alreadySynchronized) { ShowNotification(new GUIContent("이미 현재 배속과 동기화되어 있습니다.")); return; }

            float oldTotal = active.floatValue;
            float MapTime(float value)
            {
                if (value >= oldTotal) return newTotal + (value - oldTotal);
                int segment = SegmentIndexFor(value, oldStarts, oldDurations);
                float normalized = Mathf.Clamp01((value - oldStarts[segment]) / oldDurations[segment]);
                return newStarts[segment] + normalized * newDurations[segment];
            }

            Undo.RecordObject(_pattern, "TFW 현재 배속 타임라인 재동기화");
            var oldTiming = new Dictionary<string, float>();
            foreach (string path in _timings) { var property = _so.FindProperty(path); oldTiming[property.name] = property.floatValue; }
            foreach (string path in _timings) { var property = _so.FindProperty(path); property.floatValue = MapTime(property.floatValue); }
            var cues = string.IsNullOrEmpty(_effectsPath) ? null : _so.FindProperty(_effectsPath);
            if (cues != null) for (int i = 0; i < cues.arraySize; i++)
            { var at = cues.GetArrayElementAtIndex(i).FindPropertyRelative("AtSeconds"); at.floatValue = MapTime(at.floatValue); }
            var repeats = _so.FindProperty("_hitRepeats");
            if (repeats != null) for (int i = 0; i < repeats.arraySize; i++)
            {
                var repeat = repeats.GetArrayElementAtIndex(i); string key = repeat.FindPropertyRelative("TimingKey").stringValue;
                if (!oldTiming.TryGetValue(key, out float baseAt)) continue;
                if (RemapRepeatTimes(repeat, MapTime)) continue;
                var interval = repeat.FindPropertyRelative("IntervalSeconds"); interval.floatValue = Mathf.Max(0f, MapTime(baseAt + interval.floatValue) - MapTime(baseAt));
            }
            RebakeNativeInterval("FirstStrikeAtSeconds", "StrikeIntervalSeconds", oldTiming, MapTime);
            RebakeNativeInterval("FirstFieldAtSeconds", "FieldIntervalSeconds", oldTiming, MapTime);
            var curve = _so.FindProperty("ForwardDistanceCurve");
            if (curve != null && curve.propertyType == SerializedPropertyType.AnimationCurve)
            {
                var source = curve.animationCurveValue; var keys = source.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    float oldTime = keys[k].time; int segment = SegmentIndexFor(oldTime, oldStarts, oldDurations);
                    float tangentScale = oldDurations[segment] / Mathf.Max(.0001f, newDurations[segment]);
                    keys[k].time = MapTime(oldTime); keys[k].inTangent *= tangentScale; keys[k].outTangent *= tangentScale;
                }
                source.keys = keys; curve.animationCurveValue = source;
            }
            for (int i = 0; i < count; i++) storedStarts.GetArrayElementAtIndex(i).floatValue = newStarts[i];
            active.floatValue = newTotal;
            _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern); Rebuild(); Repaint(); SceneView.RepaintAll();
            ShowNotification(new GUIContent("현재 배속 기준으로 타임라인을 재동기화했습니다."));
        }

        private void RebakeNativeInterval(string timingName, string intervalName, Dictionary<string, float> oldTiming, Func<float, float> mapTime)
        {
            var interval = _so.FindProperty(intervalName);
            if (interval == null || !oldTiming.TryGetValue(timingName, out float baseAt)) return;
            interval.floatValue = Mathf.Max(0f, mapTime(baseAt + interval.floatValue) - mapTime(baseAt));
        }

        private static int SegmentIndexFor(float time, float[] starts, float[] durations)
        {
            for (int i = 0; i < starts.Length; i++) if (time < starts[i] + durations[i] || i == starts.Length - 1) return i;
            return starts.Length - 1;
        }

        private void DrawTimings()
        {
            Header("타격 시점"); _so.Update();
            for (int timingIndex = 0; timingIndex < _timings.Count; timingIndex++)
            {
                string path = _timings[timingIndex];
                var p = _so.FindProperty(path); if (p == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = _editTarget == EditTarget.Timing && _timingIndex == timingIndex;
                    if (GUILayout.Toggle(selected, "선택", "Button", GUILayout.Width(42))) SelectTarget(EditTarget.Timing, timingIndex);
                    EditorGUILayout.PropertyField(p, new GUIContent(KoreanField(p.name)));
                    if (GUILayout.Button("현재 시점", GUILayout.Width(70))) p.floatValue = _time;
                    if (GUILayout.Button("이동", GUILayout.Width(34))) { _time = Mathf.Clamp(p.floatValue, 0, _duration); Sample(); }
                }
                if (p.floatValue > _duration) EditorGUILayout.HelpBox(KoreanField(p.name) + "이 활성 애니메이션 범위를 벗어났습니다.", MessageType.Error);
            }
            Apply("TFW 타격 시점 변경");
        }

        private void DrawSegmentHits(int segmentIndex)
        {
            var hits = TimingsInSegment(segmentIndex);
            if (hits.Count == 0)
            {
                if (!HasGeneratedHitInSegment(segmentIndex)) { EditorGUILayout.LabelField("타격 없음", EditorStyles.miniLabel); EditorGUILayout.LabelField("타격 범위 없음", EditorStyles.miniLabel); }
                return;
            }
            foreach (int timingIndex in hits)
            {
                var timing = _so.FindProperty(_timings[timingIndex]);
                var geometry = GeometryForTiming(timing.name);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string kind = TimingKind(timing.name);
                    bool selected = _editTarget == EditTarget.Timing && _timingIndex == timingIndex;
                    if (GUILayout.Toggle(selected, kind + ": " + KoreanField(timing.name), "Button") && !selected)
                        QueueSelectHit(segmentIndex, timingIndex, geometry);
                    EditorGUILayout.PropertyField(timing, new GUIContent("타격 시점(초)"));
                    if (geometry.Count == 0) EditorGUILayout.LabelField("타격 범위 없음", EditorStyles.miniLabel);
                    else foreach (string rangeName in geometry)
                    {
                        var range = _so.FindProperty(rangeName); if (range != null) EditorGUILayout.PropertyField(range, new GUIContent("핸들 · " + KoreanField(range.name)));
                    }
                }
            }
            if (_pattern is LaserComboDefinition && hits.Exists(index => _so.FindProperty(_timings[index]).name == "FrontLaserAtSeconds"))
            {
                EditorGUILayout.LabelField("연속 발사 설정 · 전체 발리에 적용", EditorStyles.boldLabel);
                var beams = _so.FindProperty("LaserCount"); var volleys = _so.FindProperty("LaserVolleys"); var offset = _so.FindProperty("SecondVolleyOffset");
                EditorGUILayout.PropertyField(beams, new GUIContent("발리당 레이저 줄기 수")); EditorGUILayout.PropertyField(volleys, new GUIContent("전체 발리 횟수"));
                if (volleys.intValue >= 2) EditorGUILayout.PropertyField(offset, new GUIContent("두 번째 발리 회전 오프셋"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("발리 추가")) volleys.intValue = Mathf.Min(2, volleys.intValue + 1);
                    if (GUILayout.Button("발리 삭제")) volleys.intValue = Mathf.Max(1, volleys.intValue - 1);
                }
            }
            if (_so.hasModifiedProperties)
            {
                int selectedTiming = _timingIndex; _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern); _so.Update();
                if (selectedTiming >= 0 && selectedTiming < _timings.Count)
                {
                    int newSegment = SegmentIndexForTime(_so.FindProperty(_timings[selectedTiming]).floatValue);
                    if (newSegment >= 0 && newSegment != _segmentIndex) QueueSelectHit(newSegment, selectedTiming, GeometryForTiming(_so.FindProperty(_timings[selectedTiming]).name));
                }
            }
        }

        private List<int> TimingsInSegment(int segmentIndex)
        {
            var result = new List<int>(); var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            for (int i = 0; i < _timings.Count; i++)
            {
                var timing = _so.FindProperty(_timings[i]);
                if ((_pattern is RushSixStrikeDefinition && timing.name == "FirstStrikeAtSeconds")
                    || (_pattern is CascadeFieldComboDefinition && timing.name == "FirstFieldAtSeconds")) continue;
                if (GenericRepeatCount(timing.name) > 1) continue;
                float at = timing.floatValue;
                if (at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end)) result.Add(i);
            }
            return result;
        }

        private void DrawGeneratedHits(int segmentIndex)
        {
            string baseName = null, countName = null, intervalName = null, geometryTiming = null;
            if (_pattern is RushSixStrikeDefinition) { baseName = "FirstStrikeAtSeconds"; countName = "StrikeCount"; intervalName = "StrikeIntervalSeconds"; geometryTiming = baseName; }
            else if (_pattern is CascadeFieldComboDefinition) { baseName = "FirstFieldAtSeconds"; countName = "FieldWaveCount"; intervalName = "FieldIntervalSeconds"; geometryTiming = baseName; }
            if (baseName == null) return;
            var baseTime = _so.FindProperty(baseName); var count = _so.FindProperty(countName); var interval = _so.FindProperty(intervalName);
            var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            bool any = false;
            for (int i = 0; i < Mathf.Max(0, count.intValue); i++)
            {
                float at = baseTime.floatValue + interval.floatValue * i;
                if (!(at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end))) continue;
                any = true; var geometry = GeometryForTiming(geometryTiming);
                bool selected = _editTarget == EditTarget.Timing && Mathf.Approximately(_selectedHitAbsoluteTime, at);
                string shared = geometry.Count > 0 ? "공유 판정 범위" : "실행 중 대상 위치";
                if (GUILayout.Toggle(selected, $"{i + 1}타 · {at:F3}초  ({shared})", "Button") && !selected)
                    QueueSelectGeneratedHit(segmentIndex, _timings.FindIndex(path => _so.FindProperty(path).name == baseName), at, geometry);
            }
            if (!any) return;
            bool ownsSettings = baseTime.floatValue >= segment.Start && (segmentIndex == _segments.Count - 1
                ? baseTime.floatValue <= end : baseTime.floatValue < end);
            if (ownsSettings)
            {
                EditorGUILayout.LabelField("다단히트 설정 · 전체 타격에 적용", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(count, new GUIContent("전체 타격 횟수")); EditorGUILayout.PropertyField(interval, new GUIContent("공유 타격 간격(초)"));
                    if (GUILayout.Button("타격 추가")) count.intValue++;
                    if (GUILayout.Button("타격 삭제") && count.intValue > 1) count.intValue--;
                }
                Apply("TFW 다단 타격 변경");
            }
            else EditorGUILayout.LabelField("다단히트 횟수와 간격은 1타가 있는 조각에서 공유 설정합니다.", EditorStyles.miniLabel);
        }
        private bool HasGeneratedHitInSegment(int segmentIndex)
        {
            if (!(_pattern is RushSixStrikeDefinition) && !(_pattern is CascadeFieldComboDefinition)) return false;
            string baseName = _pattern is RushSixStrikeDefinition ? "FirstStrikeAtSeconds" : "FirstFieldAtSeconds";
            string countName = _pattern is RushSixStrikeDefinition ? "StrikeCount" : "FieldWaveCount";
            string intervalName = _pattern is RushSixStrikeDefinition ? "StrikeIntervalSeconds" : "FieldIntervalSeconds";
            float first = _so.FindProperty(baseName).floatValue, interval = _so.FindProperty(intervalName).floatValue;
            var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            for (int i = 0; i < _so.FindProperty(countName).intValue; i++) { float at = first + interval * i; if (at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end)) return true; }
            return false;
        }
        private void DrawGenericRepeatHits(int segmentIndex)
        {
            var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            foreach (string timingPath in _timings)
            {
                var timing = _so.FindProperty(timingPath); string key = timing.name;
                if (TimingKind(key) != "타격" || IsNativeRepeatKey(key)) continue;
                int requestedCount = GenericRepeatCount(key); float interval = GenericRepeatInterval(key);
                if (requestedCount <= 1)
                {
                    if (timing.floatValue >= segment.Start && (segmentIndex == _segments.Count - 1 ? timing.floatValue <= end : timing.floatValue < end))
                        if (GUILayout.Button(KoreanField(key) + " · 다단히트로 전환"))
                        { string pendingKey = key; float pendingTime = timing.floatValue; _pendingGuiActions.Enqueue(() => CreateGenericRepeat(pendingKey, pendingTime)); }
                    continue;
                }
                var geometry = GeometryForTiming(key);
                int effectiveCount = GenericRepeatCountWithinActive(key, timing.floatValue, requestedCount, interval);
                int totalDamage = TotalDamageForTiming(key);
                int distributedTotal = 0;
                for (int i = 0; i < effectiveCount; i++)
                {
                    float at = GenericRepeatTimeAt(key, timing.floatValue, i);
                    int hitDamage = DistributedDamageForDisplay(totalDamage, effectiveCount, i);
                    distributedTotal += hitDamage;
                    if (!(at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end))) continue;
                    bool selected = _editTarget == EditTarget.Timing && _timingIndex == _timings.IndexOf(timingPath) && Mathf.Approximately(_selectedHitAbsoluteTime, at);
                    if (GUILayout.Toggle(selected,
                            $"{KoreanField(key)} · {i + 1}타 · {at:F3}초 · {i + 1}타 피해: {hitDamage} (총 피해 {totalDamage} 유지, 공유 판정 범위)",
                            "Button") && !selected)
                        QueueSelectGeneratedHit(segmentIndex, _timings.IndexOf(timingPath), at, geometry);
                }
                if (distributedTotal != totalDamage)
                    EditorGUILayout.HelpBox($"피해 분배 합계({distributedTotal})가 총 피해({totalDamage})와 다릅니다.", MessageType.Error);
                if (requestedCount != effectiveCount && timing.floatValue >= segment.Start && (segmentIndex == _segments.Count - 1 ? timing.floatValue <= end : timing.floatValue < end))
                    EditorGUILayout.HelpBox($"요청 {requestedCount}타 / 실행 {effectiveCount}타. 실행되는 {effectiveCount}타에 총 피해 {totalDamage}를 나누어 보존합니다.", MessageType.Warning);
                if (timing.floatValue >= segment.Start && (segmentIndex == _segments.Count - 1 ? timing.floatValue <= end : timing.floatValue < end))
                    DrawGenericRepeatSettings(timingPath, key, geometry);
            }
        }

        private int TotalDamageForTiming(string key)
        {
            string damagePath = "Damage";
            if (_pattern is CleaveComboDefinition)
                damagePath = key == "SpinAtSeconds" ? "SpinDamage" : key == "BackhandAtSeconds" ? "BackhandDamage" : "Damage";
            else if (_pattern is FourStrikeComboDefinition)
                damagePath = key == "ThrustAtSeconds" ? "ThrustDamage" : key.Contains("Cleave") ? "CleaveDamage" : "Damage";
            else if (_pattern is CascadeFieldComboDefinition)
                damagePath = key == "ShieldAtSeconds" ? "ShieldDamage" : key == "SlashAtSeconds" ? "SlashDamage" : "Damage";
            else if (_pattern is LaserComboDefinition)
                damagePath = key == "ShieldAtSeconds" ? "ShieldDamage" : key.Contains("Laser") ? "LaserDamage" : "Damage";
            else if (_pattern is RisingComboDefinition)
                damagePath = key.Contains("Rise") ? "RiseDamage" : "Damage";
            else if (_pattern is ShieldBashComboDefinition)
            {
                if (key == "SlashAtSeconds") damagePath = "SlashDamage";
                else if (key == "ShieldAtSeconds") damagePath = "ShieldDamage";
                else if (key == "CrossSlashAtSeconds") damagePath = "CrossDamage";
                else if (key == "BashAtSeconds") damagePath = "BashDamage";
            }
            else if (_pattern is SwordShieldComboDefinition)
            {
                if (key.StartsWith("Slash", StringComparison.Ordinal)) damagePath = "SlashDamage";
                else if (key == "ShieldAtSeconds") damagePath = "ShieldDamage";
                else if (key == "BackTurnAtSeconds") damagePath = "BackTurnDamage";
            }
            else if (_pattern is TriBlastDefinition)
                damagePath = key == "NearAtSeconds" ? "NearDamage" : key == "MidAtSeconds" ? "MidDamage" : key == "FarAtSeconds" ? "FarDamage" : "Damage";
            else if (_pattern is RushSixStrikeDefinition && key == "FirstStrikeAtSeconds")
                damagePath = "StrikeDamage";
            else if (_pattern is SunderingDashDefinition)
                damagePath = "PathDamage";

            var damage = _so.FindProperty(damagePath) ?? _so.FindProperty("Damage");
            return Mathf.Max(0, damage?.intValue ?? 0);
        }

        /// 서버가 실제로 주는 값과 반드시 같아야 하므로 런타임 계산을 그대로 부른다.
        private static int DistributedDamageForDisplay(int totalDamage, int hitCount, int hitIndex)
            => TfwPatternDefinition.DistributeDamage(totalDamage, hitCount, hitIndex);
        /// 다단히트로 바꾼 타격의 편집 UI.
        ///
        /// 기준 시점과 판정 범위를 여기서 같이 그린다. 단발 타격은 DrawSegmentHits가 그려주지만,
        /// 다단히트가 걸린 타격은 TimingsInSegment가 목록에서 빼버려서 그쪽을 타지 않는다.
        /// 여기서 안 그리면 다단히트로 바꾸는 순간 시점도 범위도 영영 못 고치게 된다.
        private void DrawGenericRepeatSettings(string timingPath, string key, List<string> geometry)
        {
            var array = _so.FindProperty("_hitRepeats"); int index = FindRepeatIndex(array, key); if (index < 0) return;
            var config = array.GetArrayElementAtIndex(index);
            var count = config.FindPropertyRelative("Count");
            var interval = config.FindPropertyRelative("IntervalSeconds");
            var times = config.FindPropertyRelative("Times");

            var timing = _so.FindProperty(timingPath);
            EditorGUILayout.PropertyField(timing, new GUIContent("타격 시점(초) · 1타 기준"));
            foreach (string rangeName in geometry)
            {
                var range = _so.FindProperty(rangeName);
                if (range != null) EditorGUILayout.PropertyField(range, new GUIContent("핸들 · " + KoreanField(range.name)));
            }

            float active = _so.FindProperty("ActiveSeconds").floatValue;
            bool useTimes = times != null && times.arraySize > 0;

            EditorGUILayout.LabelField("다단히트 설정 · 이 타격 전체에 적용", EditorStyles.boldLabel);

            if (useTimes)
            {
                // 타마다 시각을 직접 적는다. 클립의 스윙 간격은 원래 고르지 않아서
                // 등간격으로는 3타를 2.5초와 2.7초에 두는 식의 배치를 아예 표현할 수 없다.
                for (int i = 0; i < times.arraySize; i++)
                {
                    var slot = times.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(slot, new GUIContent($"{i + 1}타 시각(초)"));
                        EditorGUILayout.LabelField(WarningLabel(slot.floatValue), WarningStyle(slot.floatValue),
                                                   GUILayout.Width(118));
                        if (GUILayout.Button("삭제", GUILayout.Width(48)) && times.arraySize > 1)
                        { times.DeleteArrayElementAtIndex(i); break; }
                    }
                }

                DrawWarningRunwayHelp(times);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("타격 추가"))
                    {
                        int last = times.arraySize - 1;
                        float previous = times.GetArrayElementAtIndex(last).floatValue;
                        times.InsertArrayElementAtIndex(times.arraySize);
                        times.GetArrayElementAtIndex(times.arraySize - 1).floatValue = previous + 0.2f;
                    }
                    if (GUILayout.Button("등간격으로 되돌리기"))
                    {
                        interval.floatValue = times.arraySize > 1
                            ? Mathf.Max(0f, (times.GetArrayElementAtIndex(times.arraySize - 1).floatValue
                                           - times.GetArrayElementAtIndex(0).floatValue) / (times.arraySize - 1))
                            : 0f;
                        count.intValue = times.arraySize;
                        times.ClearArray();
                    }
                }

                if (times.arraySize <= 1) array.DeleteArrayElementAtIndex(index);
                else
                {
                    // 1타 시각이 곧 이 타격의 자리다 — 조각 소속·이펙트 큐 베이킹이 전부 이 값을 본다.
                    // 목록과 어긋나면 기즈모는 여기, 실제 타격은 저기가 된다.
                    timing.floatValue = times.GetArrayElementAtIndex(0).floatValue;

                    int outside = 0;
                    for (int i = 0; i < times.arraySize; i++)
                        if (times.GetArrayElementAtIndex(i).floatValue > active) outside++;

                    if (outside > 0)
                        EditorGUILayout.HelpBox(
                            $"{outside}개 타격이 활성 시간({active:F3}초) 밖이라 실행되지 않습니다. " +
                            "시각을 앞당기세요. 애니메이션 길이를 늘리려면 클립 배속을 조절합니다.",
                            MessageType.Warning);
                }
            }
            else
            {
                float baseTime = timing.floatValue;
                EditorGUILayout.PropertyField(count, new GUIContent("전체 타격 횟수"));
                EditorGUILayout.PropertyField(interval, new GUIContent("공유 타격 간격(초)"));

                // 등간격에서는 1타가 가장 앞이라 그 타격만 확인하면 나머지는 자동으로 여유가 늘어난다.
                if (WarningRunway(baseTime) + 0.0005f < TfwTelegraphPlan.LeadSeconds)
                    EditorGUILayout.HelpBox(
                        $"1타의 전조가 {TfwTelegraphPlan.LeadSeconds:F2}초를 못 채웁니다" +
                        $"(확보 {WarningRunway(baseTime):F2}초). 위험 범위 표시가 그만큼 짧게 뜹니다.",
                        MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("타격 추가")) count.intValue++;
                    if (GUILayout.Button("타격 삭제")) count.intValue--;
                    if (GUILayout.Button("시각 직접 지정으로 전환"))
                    {
                        int n = Mathf.Max(2, count.intValue);
                        times.arraySize = n;
                        for (int i = 0; i < n; i++)
                            times.GetArrayElementAtIndex(i).floatValue =
                                baseTime + Mathf.Max(0f, interval.floatValue) * i;
                    }
                }

                if (count.intValue <= 1) array.DeleteArrayElementAtIndex(index);
                else
                {
                    // ActiveSeconds를 늘리지 않는다. 저건 Active 클립 길이의 합이라 베이커가 소유한다.
                    // 여기서 늘리면 애니메이션과 어긋나고, 이후 타임라인 재동기화가 그 어긋난 총 길이를
                    // 기준으로 모든 타격 시점을 비율 재매핑해서 최초 타격이 뒤로 밀린다.
                    // 창 밖으로 나가는 타격은 EffectiveHitCount가 잘라내고 총 피해는 보존된다.
                    float last = baseTime + Mathf.Max(0f, interval.floatValue) * (count.intValue - 1);
                    if (last > active)
                        EditorGUILayout.HelpBox(
                            $"마지막 타격({last:F3}초)이 활성 시간({active:F3}초) 밖입니다. 그 타격은 실행되지 않습니다. " +
                            "간격을 줄이거나 타격 시점을 앞당기세요. 애니메이션 길이를 늘리려면 클립 배속을 조절합니다.",
                            MessageType.Warning);
                }
            }

            Apply("TFW 다단히트 변경");
        }
        private void CreateGenericRepeat(string key, float baseTime)
        {
            _so.Update(); var array = _so.FindProperty("_hitRepeats"); if (array == null || FindRepeatIndex(array, key) >= 0) return;
            int index = array.arraySize; array.InsertArrayElementAtIndex(index); var config = array.GetArrayElementAtIndex(index);
            config.FindPropertyRelative("TimingKey").stringValue = key;
            config.FindPropertyRelative("Count").intValue = 2;
            config.FindPropertyRelative("IntervalSeconds").floatValue = 0f;

            // 시각 직접 지정으로 시작한다 — 클립의 스윙 간격이 고른 경우가 거의 없어서
            // 등간격은 결국 손으로 다시 흩어야 한다. 두 칸을 깔아 주고 값만 고치게 한다.
            var times = config.FindPropertyRelative("Times");
            times.arraySize = 2;
            times.GetArrayElementAtIndex(0).floatValue = baseTime;
            times.GetArrayElementAtIndex(1).floatValue = baseTime + .15f;

            Apply("TFW 다단히트로 전환");
        }
        // ── 전조(위험 범위 표시) 여유 ─────────────────────────────────────
        //
        // 런타임은 타격마다 TfwTelegraphPlan.LeadSeconds(0.8초) 전부터 바닥에 판정 형상을 그린다.
        // 그 경고는 Active 시작 전(예고 구간)까지 거슬러 올라갈 수 있지만 패턴 시작보다 앞설 수는 없다.
        // 그래서 한 타격이 확보할 수 있는 경고 시간은 예고 길이 + 그 타격 시각이다.
        //
        // 이 값을 제작소가 안 보여주면, 타격을 앞쪽에 몰아둔 뒤 인게임에서야
        // "경고가 안 뜨는데?"를 발견하게 된다 — 시각 자체는 서버와 어긋나지 않으므로
        // 숫자만 봐서는 원인을 알 수 없는 종류의 어긋남이다.

        /// 이 타격이 실제로 확보하는 경고 시간(초, 배속 1 기준).
        private float WarningRunway(float hitAtSeconds)
        {
            var telegraph = _so?.FindProperty("TelegraphSeconds");
            return (telegraph?.floatValue ?? 0f) + hitAtSeconds;
        }

        private string WarningLabel(float hitAtSeconds)
        {
            float runway = WarningRunway(hitAtSeconds);
            return runway + 0.0005f >= TfwTelegraphPlan.LeadSeconds
                ? $"전조 {TfwTelegraphPlan.LeadSeconds:F2}s"
                : $"전조 {runway:F2}s ▼";
        }

        private GUIStyle WarningStyle(float hitAtSeconds)
        {
            bool clipped = WarningRunway(hitAtSeconds) + 0.0005f < TfwTelegraphPlan.LeadSeconds;
            var style = new GUIStyle(EditorStyles.miniLabel);
            if (clipped) style.normal.textColor = new Color(0.85f, 0.45f, 0.2f);
            return style;
        }

        /// 경고가 잘리는 타격이 있으면 무엇을 얼마나 밀어야 하는지 알려준다.
        private void DrawWarningRunwayHelp(SerializedProperty times)
        {
            float worst = float.MaxValue;
            int clipped = 0;
            for (int i = 0; i < times.arraySize; i++)
            {
                float runway = WarningRunway(times.GetArrayElementAtIndex(i).floatValue);
                if (runway + 0.0005f >= TfwTelegraphPlan.LeadSeconds) continue;
                clipped++;
                worst = Mathf.Min(worst, runway);
            }
            if (clipped == 0) return;

            EditorGUILayout.HelpBox(
                $"{clipped}개 타격의 전조가 {TfwTelegraphPlan.LeadSeconds:F2}초를 못 채웁니다(최소 {worst:F2}초). " +
                $"그만큼 위험 범위 표시가 짧게 떠서 회피 시간이 줄어듭니다. " +
                $"타격 시각을 뒤로 밀거나 예고(Telegraph) 클립을 늘리세요.",
                MessageType.Warning);
        }

        /// 시각을 직접 적어둔 배열. 안 쓰면 null. 아직 저장 전 값을 봐야 해서 SerializedProperty로 읽는다.
        private SerializedProperty GenericRepeatTimes(string key)
        {
            var array = _so?.FindProperty("_hitRepeats"); int index = FindRepeatIndex(array, key);
            if (index < 0) return null;
            var times = array.GetArrayElementAtIndex(index).FindPropertyRelative("Times");
            return times != null && times.arraySize > 0 ? times : null;
        }
        private int GenericRepeatCount(string key)
        {
            var times = GenericRepeatTimes(key);
            if (times != null) return times.arraySize;

            var array = _so?.FindProperty("_hitRepeats"); int index = FindRepeatIndex(array, key);
            return index < 0 ? 1 : Mathf.Max(1, array.GetArrayElementAtIndex(index).FindPropertyRelative("Count").intValue);
        }
        private float GenericRepeatInterval(string key)
        {
            if (GenericRepeatTimes(key) != null) return 0f;

            var array = _so?.FindProperty("_hitRepeats"); int index = FindRepeatIndex(array, key);
            return index < 0 ? 0f : Mathf.Max(0f, array.GetArrayElementAtIndex(index).FindPropertyRelative("IntervalSeconds").floatValue);
        }
        /// i번째 타격 시각. 서버의 RepeatTimeAt과 같은 분기를 여기서도 태운다 —
        /// 제작소가 그리는 시각과 서버가 때리는 시각이 달라지면 튜닝이 거짓말이 된다.
        private float GenericRepeatTimeAt(string key, float baseTime, int hitIndex)
        {
            var times = GenericRepeatTimes(key);
            if (times == null) return baseTime + Mathf.Max(0f, GenericRepeatInterval(key)) * hitIndex;

            return times.GetArrayElementAtIndex(Mathf.Clamp(hitIndex, 0, times.arraySize - 1)).floatValue;
        }
        /// 아직 저장 전인 ActiveSeconds로 미리보기를 그려야 해서 값을 직접 넘긴다.
        private int GenericRepeatCountWithinActive(string key, float baseTime, int requestedCount, float interval)
        {
            float active = _so.FindProperty("ActiveSeconds").floatValue;
            var times = GenericRepeatTimes(key);
            if (times == null)
                return TfwPatternDefinition.EffectiveHitCount(active, baseTime, requestedCount, interval);

            int fits = 0;
            while (fits < times.arraySize && times.GetArrayElementAtIndex(fits).floatValue <= active) fits++;
            return fits;
        }
        /// 클립 배속이 바뀌어 타임라인이 늘거나 줄면 적어둔 타격 시각도 같이 옮긴다.
        /// 안 옮기면 조각은 움직였는데 타격만 제자리에 남아 싱크가 깨진다.
        /// 시각을 안 쓰는(등간격) 설정이면 false를 돌려 호출부가 간격을 재매핑하게 둔다.
        private static bool RemapRepeatTimes(SerializedProperty repeat, System.Func<float, float> mapTime)
        {
            var times = repeat.FindPropertyRelative("Times");
            if (times == null || times.arraySize == 0) return false;

            for (int i = 0; i < times.arraySize; i++)
            {
                var slot = times.GetArrayElementAtIndex(i);
                slot.floatValue = mapTime(slot.floatValue);
            }
            return true;
        }

        private static int FindRepeatIndex(SerializedProperty array, string key)
        {
            if (array == null) return -1;
            for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).FindPropertyRelative("TimingKey").stringValue == key) return i;
            return -1;
        }
        private bool IsNativeRepeatKey(string key)
            => (_pattern is RushSixStrikeDefinition && key == "FirstStrikeAtSeconds")
               || (_pattern is CascadeFieldComboDefinition && key == "FirstFieldAtSeconds")
               || (_pattern is LaserComboDefinition && (key == "FrontLaserAtSeconds" || key == "BackLaserAtSeconds"));

        private List<string> GeometryForTiming(string timingName)
        {
            if (_pattern is MarkedStrikeDefinition)
                return timingName == "ImpactAtSeconds" ? ExistingRanges("ImpactRadius") : new List<string>();
            if (_pattern is LaserComboDefinition)
            {
                if (timingName == "ShieldAtSeconds") return ExistingRanges("ShieldReach", "ShieldRadius");
                if (timingName == "FrontLaserAtSeconds") return ExistingRanges("LaserLength", "LaserWidth", "LaserHeight");
                if (timingName == "BackLaserAtSeconds") return _so.FindProperty("LaserVolleys").intValue >= 2 ? ExistingRanges("LaserLength", "LaserWidth", "LaserHeight") : new List<string>();
                if (timingName == "MarkedAtSeconds") return ExistingRanges("MarkedRadius");
                return new List<string>();
            }
            string prefix = timingName.Contains("Cross", StringComparison.Ordinal) ? "Cross" :
                timingName.Contains("BackTurn", StringComparison.Ordinal) ? "BackTurn" :
                timingName.Contains("Backhand", StringComparison.Ordinal) ? "Backhand" :
                timingName.Contains("Shield", StringComparison.Ordinal) ? "Shield" : timingName.Contains("Bash", StringComparison.Ordinal) ? "Bash" :
                timingName.Contains("Kick", StringComparison.Ordinal) ? "Kick" : timingName.Contains("Thrust", StringComparison.Ordinal) ? "Thrust" :
                timingName.Contains("Cleave", StringComparison.Ordinal) ? "Cleave" : timingName.Contains("Finish", StringComparison.Ordinal) ? "Finish" :
                timingName.Contains("Spin", StringComparison.Ordinal) ? "Spin" : timingName.Contains("Rise", StringComparison.Ordinal) ? "Rise" :
                timingName.Contains("Strike", StringComparison.Ordinal) ? "Strike" : timingName.Contains("Laser", StringComparison.Ordinal) ? "Laser" :
                timingName.Contains("Slash", StringComparison.Ordinal) ? "Slash" : timingName.Contains("Near", StringComparison.Ordinal) ? "Near" :
                timingName.Contains("Mid", StringComparison.Ordinal) ? "Mid" : timingName.Contains("Far", StringComparison.Ordinal) ? "Far" :
                timingName.Contains("Burst", StringComparison.Ordinal) ? "" : timingName.Contains("Impact", StringComparison.Ordinal) || timingName.Contains("Marked", StringComparison.Ordinal) ? "Impact" : string.Empty;
            var result = new List<string>();
            foreach (string path in _ranges)
            {
                string name = _so.FindProperty(path).name;
                bool match = prefix.Length > 0 && name.StartsWith(prefix, StringComparison.Ordinal);
                if (timingName.Contains("Burst", StringComparison.Ordinal)) match = name == "InnerRadius" || name == "OuterRadius" || name == "BurstHeight";
                if (timingName.Contains("Laser", StringComparison.Ordinal)) match |= name.StartsWith("Laser", StringComparison.Ordinal);
                if (match || ((prefix == "Thrust" || prefix == "Strike") && name == "StrikeHeight")) result.Add(path);
            }
            return result;
        }
        private List<string> ExistingRanges(params string[] names)
        {
            var result = new List<string>(); foreach (string name in names) { int index = _ranges.FindIndex(path => _so.FindProperty(path)?.name == name); if (index >= 0) result.Add(_ranges[index]); }
            return result;
        }
        private string TimingKind(string timingName)
        {
            if (_pattern is MarkedStrikeDefinition && timingName == "MarkAtSeconds") return "표식";
            return GeometryForTiming(timingName).Count == 0 ? "이벤트" : "타격";
        }

        private void DrawEffects()
        {
            if (string.IsNullOrEmpty(_effectsPath)) return;
            Header("이펙트 큐"); _so.Update(); var a = _so.FindProperty(_effectsPath);
            if (a == null || !a.isArray) return;
            if (a.arraySize == 0 && GUILayout.Button("이펙트 큐 추가")) a.InsertArrayElementAtIndex(0);
            int shownEffect = Mathf.Clamp(_effectIndex, 0, Mathf.Max(0, a.arraySize - 1));
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < a.arraySize; i++)
                {
                    string cueLabel = a.GetArrayElementAtIndex(i).FindPropertyRelative("Label").stringValue;
                    if (GUILayout.Toggle(_editTarget == EditTarget.Effect && i == _effectIndex, string.IsNullOrEmpty(cueLabel) ? "큐 " + i : cueLabel, "Button")) SelectTarget(EditTarget.Effect, i);
                }
            }
            if (a.arraySize > 0)
            {
                var cue = a.GetArrayElementAtIndex(shownEffect);
                string[] cueFields = { "Label", "AtSeconds", "Prefab", "AnchorBoneName", "AttachToAnchor", "Lifetime", "LocalOffset", "Scale", "ManualOverride" };
                EditorGUI.BeginChangeCheck();
                foreach (string field in cueFields)
                {
                    var property = cue.FindPropertyRelative(field);
                    if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(KoreanField(field)));
                }
                if (EditorGUI.EndChangeCheck()) MarkManual(cue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("위로") && shownEffect > 0) { a.MoveArrayElement(shownEffect, shownEffect - 1); SelectTarget(EditTarget.Effect, shownEffect - 1); }
                    if (GUILayout.Button("아래로") && shownEffect + 1 < a.arraySize) { a.MoveArrayElement(shownEffect, shownEffect + 1); SelectTarget(EditTarget.Effect, shownEffect + 1); }
                    if (GUILayout.Button("추가")) { a.InsertArrayElementAtIndex(shownEffect); SelectTarget(EditTarget.Effect, shownEffect + 1); }
                    if (GUILayout.Button("삭제")) { a.DeleteArrayElementAtIndex(shownEffect); SelectTarget(EditTarget.None); }
                }
            }
            bool clampedNegativeCue = false;
            for (int i = 0; i < a.arraySize; i++)
            {
                var at = a.GetArrayElementAtIndex(i).FindPropertyRelative("AtSeconds");
                if (at != null && at.floatValue < 0f) { at.floatValue = 0f; clampedNegativeCue = true; }
            }
            Apply("TFW 이펙트 큐 변경");
            if (clampedNegativeCue) EditorGUILayout.HelpBox("이펙트 시점은 음수가 될 수 없어 0초로 보정했습니다.", MessageType.Warning);
            _so.Update(); a = _so.FindProperty(_effectsPath); if (a == null || a.arraySize == 0) return;
            shownEffect = Mathf.Clamp(_effectIndex, 0, a.arraySize - 1);
            var labels = new string[a.arraySize];
            for (int i = 0; i < labels.Length; i++)
            {
                string label = a.GetArrayElementAtIndex(i).FindPropertyRelative("Label").stringValue;
                labels[i] = string.IsNullOrEmpty(label) ? "큐 " + i : label;
            }
            EditorGUI.BeginChangeCheck(); int effect = EditorGUILayout.Popup("씬 핸들 대상", shownEffect, labels);
            if (EditorGUI.EndChangeCheck()) SelectTarget(EditTarget.Effect, effect);
            if (GUILayout.Button("선택한 이펙트 편집")) SelectTarget(EditTarget.Effect, shownEffect);
            EditorGUILayout.HelpBox("씬 뷰의 이동 / 크기 핸들이 이펙트의 로컬 위치와 크기를 수정합니다.", MessageType.None);
            EditorGUILayout.HelpBox("이펙트는 별도 미리보기 씬에서만 생성하고 메시 결과만 씬 뷰에 그립니다. 편집 씬이나 네트워크 오브젝트에는 추가되지 않습니다. 단, 프리팹 초기화 함수의 정적 부작용까지 에디터가 차단할 수는 없습니다.", MessageType.None);
        }

        private void DrawRanges()
        {
            Header("타격 범위");
            if (_ranges.Count == 0) { GUILayout.Label("편집 가능한 범위 필드가 없습니다."); return; }
            var labels = new string[_ranges.Count]; for (int i = 0; i < labels.Length; i++) labels[i] = ObjectNames.NicifyVariableName(_ranges[i]);
            int shownRange = Mathf.Clamp(_rangeIndex, 0, labels.Length - 1);
            EditorGUI.BeginChangeCheck(); int range = EditorGUILayout.Popup("씬 핸들 대상", shownRange, labels);
            if (EditorGUI.EndChangeCheck()) SelectTarget(EditTarget.Range, range);
            if (GUILayout.Button("선택한 범위 편집")) SelectTarget(EditTarget.Range, shownRange);
            _so.Update(); foreach (string path in _ranges) { var p = _so.FindProperty(path); EditorGUILayout.PropertyField(p, new GUIContent(KoreanField(p.name))); }
            Apply("TFW 타격 범위 변경");
            EditorGUILayout.HelpBox("씬 뷰에서 반경/길이/너비/높이는 거리 핸들, 각도는 호 핸들로 편집합니다.", MessageType.None);
        }

        private void OnSceneGUI(SceneView view)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || _pattern == null || _so == null) return;
            EnsurePreviewBoss();
            if (_boss == null && _previewBossRoot == null) return;
            _so.Update();
            if (_editTarget == EditTarget.Range) DrawRangeHandles();
            else if (_editTarget == EditTarget.Timing) { DrawTimeMarkers(); if (_rangeIndex >= 0) DrawRangeHandles(); }
            else if (_editTarget == EditTarget.Effect) { DrawEffectHandle(); UpdateEffectPreview(); DrawEffectPreview(view); }
            else if (_editTarget == EditTarget.Segment)
            {
                DrawPreviewObject(view, _previewBossRoot);
                if (_effectIndex >= 0) { DrawEffectHandle(); UpdateEffectPreview(); DrawEffectPreview(view); }
            }
            else DestroyEffectPreview();
            Handles.BeginGUI();
            try
            {
                GUI.Box(new Rect(12, 12, 360, 24), $"현재 선택: {(_phaseTransitionSelected ? "페이즈 전환" : _pattern.name)} / {SelectedTargetName()}");
            }
            finally { Handles.EndGUI(); }
        }

        private void DrawEffectHandle()
        {
            if (!TryGetSelectedCue(out _, out var cue)) return;
            var offset = cue.FindPropertyRelative("LocalOffset");
            var scale = cue.FindPropertyRelative("Scale");
            var anchorName = cue.FindPropertyRelative("AnchorBoneName");
            if (offset == null || scale == null || anchorName == null) return;
            Transform anchor = Anchor(anchorName.stringValue);
            if (anchor == null) return;
            ResolveEffectWorldPose(anchor, offset.vector3Value, Vector3.zero, out Vector3 position, out Quaternion rotation);
            EditorGUI.BeginChangeCheck();
            position = Handles.PositionHandle(position, rotation);
            float size = Handles.ScaleSlider(scale.floatValue, position, anchor.up, rotation, HandleUtility.GetHandleSize(position), .1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_pattern, "TFW 이펙트 위치 변경"); offset.vector3Value = Quaternion.Inverse(anchor.rotation) * (position - anchor.position);
                scale.floatValue = Mathf.Max(.01f, size);
                MarkManual(cue);   // 손으로 맞춘 값 — 이펙트 셋업 재실행이 덮지 않게 표시한다
                _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern);
            }
            Handles.Label(position + Vector3.up * .3f, "이펙트 " + _effectIndex);
        }

        private bool TryGetSelectedCue(out SerializedProperty array, out SerializedProperty cue)
        {
            array = null;
            cue = null;
            if (_pattern == null || string.IsNullOrEmpty(_effectsPath) || _effectIndex < 0) return false;

            // Setup Effects가 같은 에셋의 큐 배열을 재직렬화하면 창이 들고 있던
            // SerializedObject snapshot은 더 이상 현재 target과 일치하지 않을 수 있다.
            if (_so == null || _so.targetObject == null || _so.targetObject != _pattern)
                Rebuild();
            if (_so == null || _so.targetObject != _pattern) return false;

            _so.UpdateIfRequiredOrScript();
            array = _so.FindProperty(_effectsPath);
            if (array == null || !array.isArray || _effectIndex >= array.arraySize) return false;
            cue = array.GetArrayElementAtIndex(_effectIndex);
            return cue != null;
        }

        private void DrawRangeHandles()
        {
            Transform bossTransform = BossTransform(); Vector3 origin = bossTransform.position, fwd = Vector3.ProjectOnPlane(bossTransform.forward, Vector3.up).normalized;
            Handles.color = new Color(.1f, .9f, .9f, .8f);
            if (_pattern is DonutFieldDefinition) { Disc("InnerRadius", origin + Vector3.up * V("BurstHeight")); Disc("OuterRadius", origin + Vector3.up * V("BurstHeight")); }
            else if (_pattern is TriBlastDefinition) { Vector3 c = origin + Vector3.up * V("BlastHeight"); Sphere("NearRadius", c); Sphere("MidRadius", c); Sphere("FarRadius", c); }
            else if (_pattern is CleaveComboDefinition)
            { Sphere("SpinRadius", origin + Vector3.up * V("SpinHeight")); Sphere("BackhandRadius", origin + fwd * V("BackhandReach") + Vector3.up * V("BackhandHeight")); Box("Cleave", origin, fwd, "CleaveHeight"); }
            else if (_pattern is RisingComboDefinition)
            { Fan("Rise", origin, Quaternion.AngleAxis(V("RiseYawOffset"), Vector3.up) * fwd); Fan("Rise", origin, Quaternion.AngleAxis(-V("RiseYawOffset"), Vector3.up) * fwd); Box("Cleave", origin, fwd, "CleaveHeight"); }
            else if (_pattern is RushSixStrikeDefinition)
            { Fan("Strike", origin, fwd); Box("Thrust", origin, fwd, "StrikeHeight"); }
            else if (_pattern is FourStrikeComboDefinition)
            { Box("Thrust", origin, fwd, "StrikeHeight"); Box("Cleave", origin, fwd, "StrikeHeight"); Fan("Finish", origin, fwd); }
            else if (_pattern is SwordShieldComboDefinition)
            { Fan("Slash", origin, fwd); Sphere("ShieldRadius", origin + fwd * V("ShieldReach") + Vector3.up); Fan("BackTurn", origin, fwd); Box("Thrust", origin, fwd, "StrikeHeight"); }
            else if (_pattern is ShieldBashComboDefinition)
            { Fan("Slash", origin, fwd); Sphere("ShieldRadius", origin + fwd * V("ShieldReach") + Vector3.up); Fan("Cross", origin, fwd); Sphere("BashRadius", origin + fwd * V("BashReach") + Vector3.up); Sphere("KickRadius", origin + fwd * V("KickReach") + Vector3.up); }
            else if (_pattern is CascadeFieldComboDefinition)
            { Sphere("ShieldRadius", origin + fwd * V("ShieldReach") + Vector3.up); Fan("Slash", origin, fwd); Fan("Cross", origin, fwd); Handles.Label(origin + Vector3.up * 4, "장판 파동은 실행 중 대상 위치를 사용하므로 씬 미리보기를 지원하지 않습니다."); }
            else if (_pattern is LaserComboDefinition)
            {
                Sphere("ShieldRadius", origin + fwd * V("ShieldReach") + Vector3.up);
                if (SelectedGeometryHasPrefix("Laser"))
                {
                    int count = Mathf.Max(2, _so.FindProperty("LaserCount").intValue);
                    float step = 360f / count;
                    string timing = _timingIndex >= 0 && _timingIndex < _timings.Count ? _so.FindProperty(_timings[_timingIndex]).name : string.Empty;
                    float offset = timing == "BackLaserAtSeconds" ? _so.FindProperty("SecondVolleyOffset").floatValue : 0f;
                    bool draw = timing == "FrontLaserAtSeconds" || (timing == "BackLaserAtSeconds" && _so.FindProperty("LaserVolleys").intValue >= 2);
                    if (draw)
                    {
                        for (int i = 0; i < count; i++)
                            BoxAt(origin, Quaternion.AngleAxis((i + offset) * step, Vector3.up) * fwd, "LaserLength", "LaserWidth", "LaserHeight", V("LaserLength") * .5f);
                    }
                }
                if (SelectedRangeName() == "MarkedRadius") Handles.Label(origin + Vector3.up * 4, "지목 강타: 실행 중 플레이어 위치를 사용하므로 씬 핸들을 지원하지 않습니다.");
            }
            else if (_pattern is MarkedStrikeDefinition) Handles.Label(origin + Vector3.up * 4, "지목 강타: 실행 중 플레이어 위치를 사용하므로 씬 핸들을 지원하지 않습니다.");
            else if (_pattern is SunderingDashDefinition) Handles.Label(origin + Vector3.up * 4, "돌진 경로: 실행 중 이동 경로를 사용하므로 씬 핸들을 지원하지 않습니다.");
            else Handles.Label(origin + Vector3.up * 4, "이 패턴의 씬 범위 편집은 지원하지 않습니다.");

            // 선택한 실제 property는 해당 기하의 대표 축에서 직접 write-back한다.
            var editable = _editTarget == EditTarget.Timing ? _selectedHitGeometry :
                _rangeIndex >= 0 && _rangeIndex < _ranges.Count ? new List<string> { _ranges[_rangeIndex] } : new List<string>();
            foreach (string path in editable)
            {
                var p = _so.FindProperty(path); if (p == null) continue;
                if (_pattern is MarkedStrikeDefinition || _pattern is SunderingDashDefinition || (_pattern is LaserComboDefinition && p.name == "MarkedRadius")) continue;
                Vector3 handleOrigin = ActualHandleOrigin(p.name, origin, fwd);
                Vector3 axis = p.name.EndsWith("Width", StringComparison.Ordinal) ? bossTransform.right : p.name.EndsWith("Height", StringComparison.Ordinal) ? Vector3.up : fwd;
                Handles.color = Color.yellow;
                if (p.name.EndsWith("HalfAngle", StringComparison.Ordinal)) AngleHandle(p, handleOrigin, fwd, RelatedRadius(p.name));
                else if (p.name == "RiseYawOffset") AngleHandle(p, origin, fwd, Mathf.Max(.1f, V("RiseRadius")));
                else DistanceHandle(p, handleOrigin, axis);
            }
        }

        private void Disc(string radius, Vector3 center)
        {
            if (!ShouldDraw(radius)) return;
            var p = _so.FindProperty(radius); if (p != null) Handles.DrawWireDisc(center, Vector3.up, Mathf.Max(0, p.floatValue));
        }

        private void Sphere(string radius, Vector3 center)
        {
            if (!ShouldDraw(radius)) return;
            var p = _so.FindProperty(radius); if (p == null) return; float r = Mathf.Max(0, p.floatValue);
            Handles.DrawWireDisc(center, Vector3.up, r); Handles.DrawWireDisc(center, Vector3.right, r); Handles.DrawWireDisc(center, Vector3.forward, r);
        }

        private void Fan(string prefix, Vector3 center, Vector3 forward)
        {
            if (!SelectedGeometryHasPrefix(prefix)) return;
            var radius = _so.FindProperty(prefix + "Radius"); var angle = _so.FindProperty(prefix + "HalfAngle"); if (radius == null || angle == null) return;
            Handles.DrawWireArc(center, Vector3.up, Quaternion.AngleAxis(-angle.floatValue, Vector3.up) * forward, angle.floatValue * 2, radius.floatValue);
            Handles.DrawLine(center, center + Quaternion.AngleAxis(-angle.floatValue, Vector3.up) * forward * radius.floatValue);
            Handles.DrawLine(center, center + Quaternion.AngleAxis(angle.floatValue, Vector3.up) * forward * radius.floatValue);
        }

        private void Box(string prefix, Vector3 origin, Vector3 forward, string height)
        {
            if (!SelectedGeometryHasPrefix(prefix) && !ShouldDraw(height)) return;
            BoxAt(origin, forward, prefix + "Length", prefix + "Width", height, V(prefix + "Reach"));
        }

        private void BoxAt(Vector3 origin, Vector3 forward, string lengthName, string widthName, string heightName, float reach)
        {
            float length = V(lengthName), width = V(widthName), height = V(heightName);
            Vector3 center = origin + forward * reach + Vector3.up * (height * .5f);
            using (new Handles.DrawingScope(Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one)))
                Handles.DrawWireCube(Vector3.zero, new Vector3(width, height, length));
        }

        private Vector3 ActualHandleOrigin(string name, Vector3 origin, Vector3 fwd)
        {
            string prefix = name.Replace("Radius", "").Replace("HalfAngle", "").Replace("Reach", "").Replace("Length", "").Replace("Width", "").Replace("Height", "");
            float reach = V(prefix + "Reach");
            if (name.EndsWith("Reach", StringComparison.Ordinal)) return origin;
            if (name.EndsWith("Height", StringComparison.Ordinal)) return origin + fwd * reach;
            float height = V(prefix + "Height");
            if (_pattern is TriBlastDefinition && name.EndsWith("Radius", StringComparison.Ordinal)) height = V("BlastHeight");
            else if (_pattern is DonutFieldDefinition && name.EndsWith("Radius", StringComparison.Ordinal)) height = V("BurstHeight");
            else if (_pattern is CleaveComboDefinition && prefix == "Spin") height = V("SpinHeight");
            if (height <= 0 && (prefix == "Shield" || prefix == "Bash" || prefix == "Kick")) height = 1f;
            return origin + fwd * reach + Vector3.up * height;
        }

        private void AngleHandle(SerializedProperty p, Vector3 origin, Vector3 forward, float radius)
        {
            Vector3 dir = Quaternion.AngleAxis(p.floatValue, Vector3.up) * forward;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
            EditorGUI.BeginChangeCheck();
            Quaternion changed = Handles.Disc(rotation, origin, Vector3.up, radius, false, 1f);
            if (EditorGUI.EndChangeCheck())
                SetFloat(p, Mathf.Clamp(Mathf.Abs(Vector3.SignedAngle(forward, changed * Vector3.forward, Vector3.up)), 0, 180));
        }

        private float V(string property)
        {
            var p = _so.FindProperty(property); return p == null ? 0f : Mathf.Max(0f, p.floatValue);
        }

        private void DistanceHandle(SerializedProperty p, Vector3 origin, Vector3 axis)
        {
            EditorGUI.BeginChangeCheck(); float value = Handles.ScaleSlider(p.floatValue, origin, axis, Quaternion.identity, HandleUtility.GetHandleSize(origin), .1f);
            if (EditorGUI.EndChangeCheck()) SetFloat(p, Mathf.Max(0, value));
        }

        private void SetFloat(SerializedProperty p, float value)
        {
            Undo.RecordObject(_pattern, "TFW 타격 범위 변경"); p.floatValue = value; _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern);
        }

        private void DrawTimeMarkers()
        {
            Transform bossTransform = BossTransform(); Handles.color = new Color(1, .3f, .2f); Vector3 origin = bossTransform.position;
            if (_timingIndex < 0 || _timingIndex >= _timings.Count) return;
            {
                var p = _so.FindProperty(_timings[_timingIndex]); float markerTime = float.IsNaN(_selectedHitAbsoluteTime) ? p.floatValue : _selectedHitAbsoluteTime;
                float angle = _duration <= 0 ? 0 : markerTime / _duration * 360;
                Vector3 pos = origin + Quaternion.AngleAxis(angle, Vector3.up) * bossTransform.forward * 2.5f;
                Handles.SphereHandleCap(0, pos, Quaternion.identity, .15f, EventType.Repaint); Handles.Label(pos + Vector3.up * .2f, $"{KoreanField(p.name)} {markerTime:F2}초");
            }
        }

        private void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !_playing) return; double now = EditorApplication.timeSinceStartup; _time += (float)(now - _lastUpdate) * _playSpeed; _lastUpdate = now;
            SelectedBounds(out float start, out float end);
            if (_time >= end) { if (_loop && end > start) _time = start + (_time - start) % (end - start); else { _time = end; _playing = false; } }
            Sample(); Repaint();
        }

        private void Step(int direction)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SelectedBounds(out float start, out float end);
            _time = Mathf.Clamp(_time + direction / Fps, start, end); Sample();
        }
        private void SelectedBounds(out float start, out float end)
        {
            start = 0f; end = _duration;
            if ((_editTarget == EditTarget.Segment || _editTarget == EditTarget.Timing) && _segmentIndex >= 0 && _segmentIndex < _segments.Count)
            { start = _segments[_segmentIndex].Start; end = start + _segments[_segmentIndex].Duration; }
        }
        private float ClipSampleTime(Segment segment)
        {
            float local = Mathf.Clamp(_time - segment.Start, 0f, segment.Duration) * segment.Speed;
            return Mathf.Min(local, Mathf.Max(0f, segment.Clip.length - .0001f));
        }
        private void Sample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_segments.Count == 0) return;
            Segment s = (_editTarget == EditTarget.Segment || _editTarget == EditTarget.Timing) && _segmentIndex >= 0 && _segmentIndex < _segments.Count
                ? _segments[_segmentIndex] : SegmentAt(_time);
            if (s == null) return;
            EnsurePreviewBoss();
            if (_boss == null && _previewBossRoot == null) return;
            if (_boss == null)
            {
                s.Clip.SampleAnimation(_previewBossAnimator != null ? _previewBossAnimator.gameObject : _previewBossRoot, ClipSampleTime(s));
                SceneView.RepaintAll(); return;
            }
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            try { AnimationMode.SampleAnimationClip(_boss.gameObject, s.Clip, ClipSampleTime(s)); }
            finally { AnimationMode.EndSampling(); }
            SceneView.RepaintAll();
        }

        private void Rebuild()
        {
            _playing = false; StopPreview(); DestroyEffectPreview(); ClosePreviewScene(); _segments.Clear(); _timings.Clear(); _ranges.Clear(); _effectsPath = null; _duration = 0; _so = null;
            if (_pattern == null) return; _so = new SerializedObject(_pattern); var it = _so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.Float && it.name.EndsWith("AtSeconds", StringComparison.Ordinal)) _timings.Add(it.propertyPath);
                if (it.propertyType == SerializedPropertyType.Float && IsRange(it.name)) _ranges.Add(it.propertyPath);
                else if (it.propertyType == SerializedPropertyType.Float && (it.name == "RiseYawOffset" || it.name == "HitFrontOffset")) _ranges.Add(it.propertyPath);
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.name == "_effectCues") _effectsPath = it.propertyPath;
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath); _controller = controller;
            string prefix = _pattern.name.Replace("Def", string.Empty);
            for (int i = 0; controller != null; i++)
            {
                string name = i == 0 ? prefix + "_Active" : prefix + "_Active" + (i + 1); var state = FindState(controller, name); if (!(state?.motion is AnimationClip baseClip)) break;
                AnimationClip clip = ResolveOverrideClip(baseClip);
                float speed = Mathf.Max(.01f, state.speed), duration = clip.length / speed;
                _segments.Add(new Segment { Name = name, State = state, Clip = clip, Start = _duration, Duration = duration, Speed = speed }); _duration += duration;
            }
            if (_duration <= 0) _duration = Mathf.Max(.01f, _pattern.ActiveSeconds); _time = Mathf.Clamp(_time, 0, _duration);
        }

        private AnimationClip ResolveOverrideClip(AnimationClip baseClip)
        {
            var prefabAnimator = _bossPrefab == null ? null : _bossPrefab.GetComponentInChildren<Animator>(true);
            if (!(prefabAnimator?.runtimeAnimatorController is AnimatorOverrideController overrides)) return baseClip;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); overrides.GetOverrides(pairs);
            foreach (var pair in pairs) if (pair.Key == baseClip && pair.Value != null) return pair.Value;
            return baseClip;
        }

        private void CollectPatterns()
        {
            _patterns.Clear(); _bossDefinition = null; _pattern = null; _patternIndex = 0; _hasPhaseTransitionCues = false; _phaseTransitionSelected = false;
            if (_bossPrefab == null) { Rebuild(); return; }
            if (_boss == null)
                foreach (var animator in FindObjectsByType<Animator>(FindObjectsInactive.Include))
                    if (animator.name.Contains("TheFirstWielder", StringComparison.OrdinalIgnoreCase) || animator.name.Contains("The First Wielder", StringComparison.OrdinalIgnoreCase)) { _boss = animator; break; }
            var entity = _bossPrefab.GetComponent<BossEntity>();
            if (entity != null)
            {
                var entitySo = new SerializedObject(entity);
                _bossDefinition = entitySo.FindProperty("_definition")?.objectReferenceValue as BossDefinition;
            }
            var effectPlayer = _bossPrefab.GetComponent<BossPatternEffectPlayer>();
            if (effectPlayer != null)
            {
                var playerSo = new SerializedObject(effectPlayer);
                var phaseCues = playerSo.FindProperty("_phaseTransitionCues");
                _hasPhaseTransitionCues = phaseCues != null;
            }
            if (_bossDefinition == null)
            {
                foreach (var component in _bossPrefab.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var componentSo = new SerializedObject(component);
                    var definition = componentSo.FindProperty("_definition")?.objectReferenceValue as BossDefinition;
                    if (definition != null) { _bossDefinition = definition; break; }
                }
            }
            var seen = new HashSet<PatternDefinition>();
            if (_bossDefinition?.Patterns != null)
                foreach (var pattern in _bossDefinition.Patterns)
                    if (pattern is IPatternEffectSource && seen.Add(pattern)) _patterns.Add(pattern);
            if (_patterns.Count > 0) _pattern = _patterns[0];
            Rebuild();
        }

        private void UpdateEffectPreview()
        {
            var a = string.IsNullOrEmpty(_effectsPath) ? null : _so.FindProperty(_effectsPath); if (a == null || _effectIndex >= a.arraySize) { DestroyEffectPreview(); return; }
            var cue = a.GetArrayElementAtIndex(_effectIndex); var prefab = cue.FindPropertyRelative("Prefab").objectReferenceValue as GameObject; if (prefab == null) { DestroyEffectPreview(); return; }
            if (_effectPreview == null || PrefabUtility.GetCorrespondingObjectFromSource(_effectPreview) != prefab)
            {
                DestroyEffectPreview();
                EnsurePreviewScene();
                _effectPreview = PrefabUtility.InstantiatePrefab(prefab, _previewScene) as GameObject;
                if (_effectPreview != null) _effectPreview.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_effectPreview == null) return; Transform anchor = Anchor(cue.FindPropertyRelative("AnchorBoneName").stringValue);
            bool attach = cue.FindPropertyRelative("AttachToAnchor").boolValue;
            // PreviewScene 객체를 편집 씬 앵커에 parent하지 않고, 런타임 parent 결과의 월드 TRS만 복제한다.
            if (_effectPreview.transform.parent != null) _effectPreview.transform.SetParent(null, true);
            var localOffset = cue.FindPropertyRelative("LocalOffset");
            if (anchor == null || localOffset == null) { DestroyEffectPreview(); return; }
            ResolveEffectWorldPose(anchor, localOffset.vector3Value,
                Vector3.zero, out Vector3 position, out Quaternion rotation);
            _effectPreview.transform.SetPositionAndRotation(position, rotation);
            Vector3 parentScale = attach ? anchor.lossyScale : Vector3.one;
            _effectPreview.transform.localScale = Vector3.Scale(parentScale, prefab.transform.localScale)
                                                    * Mathf.Max(.01f, cue.FindPropertyRelative("Scale").floatValue);
            float t = Mathf.Max(0, _time - cue.FindPropertyRelative("AtSeconds").floatValue);
            float previewAge = !_playing && t < .05f ? .15f : t;
            foreach (var particle in _effectPreview.GetComponentsInChildren<ParticleSystem>(true)) particle.Simulate(previewAge, true, true, true);
            foreach (var decal in _effectPreview.GetComponentsInChildren<PatternFieldDecal>(true))
            {
                var renderer = decal.GetComponent<MeshRenderer>(); if (renderer == null) continue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetFloat("_Progress", decal.FillSeconds > .001f ? Mathf.Clamp01(previewAge / decal.FillSeconds) : 1f);
                block.SetFloat("_InnerRadius", decal.InnerRadius); block.SetFloat("_Fade", 1f); renderer.SetPropertyBlock(block);
            }
        }

        /// PreviewScene은 일반 SceneView가 렌더하지 않으므로, 안전하게 격리된 renderer의 mesh만
        /// 현재 SceneView에 즉시 그린다. 편집 씬에는 GameObject를 추가하지 않는다.
        private void DrawEffectPreview(SceneView view)
        {
            DrawPreviewObject(view, _effectPreview);
        }

        private void DrawPreviewObject(SceneView view, GameObject root)
        {
            if (root == null || Event.current.type != EventType.Repaint || view.camera == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                Mesh mesh = null;
                try
                {
                    if (renderer is ParticleSystemRenderer particles)
                    {
                        mesh = PreviewMesh(renderer);
                        particles.BakeMesh(mesh, view.camera, ParticleSystemBakeMeshOptions.Default);
                    }
                    else if (renderer is SkinnedMeshRenderer skinned)
                    {
                        mesh = PreviewMesh(renderer); skinned.BakeMesh(mesh);
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var filter = renderer.GetComponent<MeshFilter>(); mesh = filter == null ? null : filter.sharedMesh;
                    }
                    if (mesh == null || mesh.vertexCount == 0) continue;
                    var materials = renderer.sharedMaterials;
                    int subMeshes = Mathf.Min(mesh.subMeshCount, materials.Length);
                    var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                    for (int sub = 0; sub < subMeshes; sub++)
                    {
                        var material = materials[sub];
                        if (material == null) continue;
                        Graphics.DrawMesh(mesh, renderer.localToWorldMatrix, material, renderer.gameObject.layer,
                            view.camera, sub, properties, ShadowCastingMode.Off, false, null, false);
                    }
                }
                catch (Exception)
                {
                    // 지원하지 않는 shader pass/BakeMesh renderer 하나가 전체 SceneView를 깨지 않게 한다.
                }
            }
        }

        private Mesh PreviewMesh(Renderer renderer)
        {
            if (_bakedPreviewMeshes.TryGetValue(renderer, out var mesh) && mesh != null) return mesh;
            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "TFW 이펙트 미리보기 메시" };
            _bakedPreviewMeshes[renderer] = mesh;
            return mesh;
        }

        private Transform Anchor(string name)
        {
            Transform root = BossTransformOrNull();
            if (root == null) return null;
            if (string.IsNullOrEmpty(name)) return root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return root;
        }
        private static void ResolveEffectWorldPose(Transform anchor, Vector3 localOffset, Vector3 localEulerAngles,
            out Vector3 position, out Quaternion rotation)
        {
            position = anchor.position + anchor.rotation * localOffset;
            rotation = anchor.rotation * Quaternion.Euler(localEulerAngles);
        }
        private Transform BossTransformOrNull()
        {
            if (_boss != null)
            {
                var effectPlayer = _boss.GetComponentInParent<BossPatternEffectPlayer>(true);
                return effectPlayer != null ? effectPlayer.transform : _boss.transform.root;
            }
            return _previewBossRoot != null ? _previewBossRoot.transform : null;
        }
        private Transform BossTransform() => BossTransformOrNull();
        private void DestroyEffectPreview()
        {
            foreach (var mesh in _bakedPreviewMeshes.Values) if (mesh != null) DestroyImmediate(mesh);
            _bakedPreviewMeshes.Clear();
            if (_effectPreview != null) DestroyImmediate(_effectPreview); _effectPreview = null;
        }
        private void EnsurePreviewScene()
        {
            if (!_previewScene.IsValid()) _previewScene = EditorSceneManager.NewPreviewScene();
        }
        private void EnsurePreviewBoss()
        {
            if (_boss != null || _bossPrefab == null || _previewBossRoot != null) return;
            EnsurePreviewScene();
            var map = new Dictionary<Transform, Transform>();
            _previewBossRoot = CloneVisualTransform(_bossPrefab.transform, null, map).gameObject;
            SceneManager.MoveGameObjectToScene(_previewBossRoot, _previewScene);
            foreach (var pair in map) CopyVisualComponents(pair.Key, pair.Value, map);
            _previewBossAnimator = _previewBossRoot.GetComponentInChildren<Animator>(true);
        }
        private static Transform CloneVisualTransform(Transform source, Transform parent, Dictionary<Transform, Transform> map)
        {
            var clone = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave };
            Transform target = clone.transform; target.SetParent(parent, false);
            target.localPosition = source.localPosition; target.localRotation = source.localRotation; target.localScale = source.localScale;
            map[source] = target;
            for (int i = 0; i < source.childCount; i++) CloneVisualTransform(source.GetChild(i), target, map);
            return target;
        }
        private static void CopyVisualComponents(Transform source, Transform target, Dictionary<Transform, Transform> map)
        {
            var sourceFilter = source.GetComponent<MeshFilter>(); var sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceRenderer != null)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                var renderer = target.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.enabled = sourceRenderer.enabled; renderer.shadowCastingMode = sourceRenderer.shadowCastingMode; renderer.receiveShadows = sourceRenderer.receiveShadows;
            }
            var sourceSkin = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkin != null)
            {
                var skin = target.gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = sourceSkin.sharedMesh; skin.sharedMaterials = sourceSkin.sharedMaterials;
                skin.bones = Array.ConvertAll(sourceSkin.bones, bone => bone != null && map.TryGetValue(bone, out var clone) ? clone : null);
                skin.rootBone = sourceSkin.rootBone != null && map.TryGetValue(sourceSkin.rootBone, out var root) ? root : null;
                skin.localBounds = sourceSkin.localBounds; skin.updateWhenOffscreen = true; skin.enabled = sourceSkin.enabled;
            }
            var sourceAnimator = source.GetComponent<Animator>();
            if (sourceAnimator != null)
            {
                var animator = target.gameObject.AddComponent<Animator>(); animator.avatar = sourceAnimator.avatar; animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false; animator.updateMode = sourceAnimator.updateMode; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
        private void ClosePreviewScene()
        {
            _previewBossRoot = null; _previewBossAnimator = null; _effectPreview = null;
            foreach (var mesh in _bakedPreviewMeshes.Values) if (mesh != null) DestroyImmediate(mesh);
            _bakedPreviewMeshes.Clear();
            if (_previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(_previewScene);
            _previewScene = default;
        }
        private void StopPreview() { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); }
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _playing = false; StopPreview(); DestroyEffectPreview(); ClosePreviewScene(); Repaint();
        }
        /// 제작소에서 값을 건드린 큐에 "수동 고정" 표시를 남긴다.
        /// 이 표시가 있는 큐는 Tools/Boss/Setup TheFirstWielder Effects가 다시 계산해도
        /// 시점·위치·크기·프리팹을 덮어쓰지 않는다(TheFirstWielderEffectSetup.WriteCues).
        /// 체크박스를 직접 끄면 다시 셋업의 계산값을 따른다.
        private static void MarkManual(SerializedProperty cue)
        {
            var flag = cue.FindPropertyRelative("ManualOverride");
            if (flag != null) flag.boolValue = true;
        }

        private void Apply(string name)
        {
            if (!_so.hasModifiedProperties) return;
            // SerializedObject가 변경 전 snapshot과 배열 reorder를 포함한 Undo를 기록한다.
            _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern); SceneView.RepaintAll(); Repaint();
        }
        private void SelectTarget(EditTarget target, int index = -1)
        {
            if (_editTarget == EditTarget.Effect || target != EditTarget.Effect || (target == EditTarget.Effect && index != _effectIndex))
                DestroyEffectPreview();
            _editTarget = target;
            _selectedHitGeometry.Clear();
            _selectedHitAbsoluteTime = float.NaN;
            _segmentIndex = _timingIndex = _rangeIndex = _effectIndex = -1;
            if (target == EditTarget.Segment) _segmentIndex = index;
            else if (target == EditTarget.Timing) _timingIndex = index;
            else if (target == EditTarget.Range) _rangeIndex = index;
            else if (target == EditTarget.Effect) _effectIndex = index;
            SceneView.RepaintAll(); Repaint();
        }
        private void SelectSegment(int segmentIndex, int cueIndex)
        {
            _segmentFoldouts[segmentIndex] = true;
            DestroyEffectPreview();
            _editTarget = EditTarget.Segment; _segmentIndex = segmentIndex; _timingIndex = _rangeIndex = -1; _effectIndex = cueIndex;
            var segment = _segments[segmentIndex];
            _time = cueIndex >= 0 ? Mathf.Clamp(_so.FindProperty(_effectsPath).GetArrayElementAtIndex(cueIndex).FindPropertyRelative("AtSeconds").floatValue,
                segment.Start, segment.Start + segment.Duration) : segment.Start;
            Sample(); SceneView.RepaintAll(); Repaint();
        }
        private void QueueSelectHit(int segmentIndex, int timingIndex, List<string> geometry)
        {
            var pendingGeometry = new List<string>(geometry);
            _pendingGuiActions.Enqueue(() => SelectHit(segmentIndex, timingIndex, pendingGeometry));
        }
        private void QueueSelectGeneratedHit(int segmentIndex, int timingIndex, float absoluteTime, List<string> geometry)
        {
            var pendingGeometry = new List<string>(geometry);
            _pendingGuiActions.Enqueue(() => SelectGeneratedHit(segmentIndex, timingIndex, absoluteTime, pendingGeometry));
        }
        private void SelectHit(int segmentIndex, int timingIndex, List<string> geometry)
        {
            _segmentFoldouts[segmentIndex] = true;
            DestroyEffectPreview(); _editTarget = EditTarget.Timing; _segmentIndex = segmentIndex; _timingIndex = timingIndex; _effectIndex = -1;
            _selectedHitGeometry.Clear(); _selectedHitGeometry.AddRange(geometry);
            _selectedHitAbsoluteTime = _so.FindProperty(_timings[timingIndex]).floatValue;
            _rangeIndex = geometry.Count == 0 ? -1 : _ranges.IndexOf(geometry[0]);
            _time = Mathf.Clamp(_so.FindProperty(_timings[timingIndex]).floatValue, _segments[segmentIndex].Start,
                _segments[segmentIndex].Start + _segments[segmentIndex].Duration);
            Sample(); SceneView.RepaintAll(); Repaint();
        }
        private void SelectGeneratedHit(int segmentIndex, int timingIndex, float absoluteTime, List<string> geometry)
        {
            SelectHit(segmentIndex, timingIndex, geometry); _selectedHitAbsoluteTime = absoluteTime; _time = absoluteTime; Sample();
        }

        private List<int> CuesInSegment(int segmentIndex)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(_effectsPath) || segmentIndex < 0 || segmentIndex >= _segments.Count) return result;
            var array = _so.FindProperty(_effectsPath); if (array == null) return result;
            var segment = _segments[segmentIndex]; float end = segment.Start + segment.Duration;
            for (int i = 0; i < array.arraySize; i++)
            {
                float at = Mathf.Max(0f, array.GetArrayElementAtIndex(i).FindPropertyRelative("AtSeconds").floatValue);
                bool inside = at >= segment.Start && (segmentIndex == _segments.Count - 1 ? at <= end : at < end);
                if (inside) result.Add(i);
            }
            return result;
        }

        private int FirstCueInSegment(int segmentIndex)
        {
            var cues = CuesInSegment(segmentIndex); return cues.Count == 0 ? -1 : cues[0];
        }

        private void AddCueToSegment(int segmentIndex)
        {
            _so.Update(); var array = _so.FindProperty(_effectsPath); int index = array.arraySize; array.InsertArrayElementAtIndex(index);
            var cue = array.GetArrayElementAtIndex(index); cue.FindPropertyRelative("Label").stringValue = $"조각 {segmentIndex + 1} 이펙트";
            cue.FindPropertyRelative("AtSeconds").floatValue = _segments[segmentIndex].Start;
            cue.FindPropertyRelative("Prefab").objectReferenceValue = null; cue.FindPropertyRelative("AnchorBoneName").stringValue = string.Empty;
            cue.FindPropertyRelative("AttachToAnchor").boolValue = true; cue.FindPropertyRelative("Lifetime").floatValue = 3f;
            cue.FindPropertyRelative("LocalOffset").vector3Value = Vector3.zero;
            cue.FindPropertyRelative("Scale").floatValue = 1f;
            cue.FindPropertyRelative("ManualOverride").boolValue = true;
            _so.ApplyModifiedProperties(); EditorUtility.SetDirty(_pattern); SelectSegment(segmentIndex, index);
        }

        private void DrawSelectedCueFields()
        {
            if (!TryGetSelectedCue(out var array, out var cue)) return;
            int cueIndex = _effectIndex;
            string[] fields = { "Label", "AtSeconds", "Prefab", "AnchorBoneName", "AttachToAnchor", "Lifetime", "LocalOffset", "Scale", "ManualOverride" };
            EditorGUI.BeginChangeCheck();
            foreach (string field in fields)
            {
                var property = cue.FindPropertyRelative(field);
                if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(KoreanField(field)));
            }
            if (EditorGUI.EndChangeCheck()) MarkManual(cue);
            var at = cue.FindPropertyRelative("AtSeconds");
            if (at != null && at.floatValue < 0f) at.floatValue = 0f;
            Apply("TFW 조각 이펙트 변경");
            _so.Update();
            var refreshedArray = _so.FindProperty(_effectsPath);
            if (refreshedArray == null || cueIndex >= refreshedArray.arraySize) return;
            int mappedSegment = SegmentIndexForTime(refreshedArray.GetArrayElementAtIndex(cueIndex).FindPropertyRelative("AtSeconds").floatValue);
            if (mappedSegment >= 0 && mappedSegment != _segmentIndex) SelectSegment(mappedSegment, cueIndex);
        }
        private int SegmentIndexForTime(float time)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                float end = _segments[i].Start + _segments[i].Duration;
                if (time >= _segments[i].Start && (i == _segments.Count - 1 ? time <= end : time < end)) return i;
            }
            return -1;
        }
        private SerializedProperty Find(params string[] names) { foreach (string n in names) { var p = _so.FindProperty(n); if (p != null) return p; } return null; }
        private string SelectedRangeName() => _rangeIndex >= 0 && _rangeIndex < _ranges.Count ? _so.FindProperty(_ranges[_rangeIndex])?.name ?? string.Empty : string.Empty;
        private bool ShouldDraw(string property)
        {
            if (_editTarget == EditTarget.Timing)
                foreach (string path in _selectedHitGeometry) if (_so.FindProperty(path)?.name == property) return true;
            return SelectedRangeName() == property;
        }
        private bool SelectedGeometryHasPrefix(string prefix)
        {
            if (_editTarget == EditTarget.Timing)
                foreach (string path in _selectedHitGeometry) if ((_so.FindProperty(path)?.name ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) return true;
            return SelectedRangeName().StartsWith(prefix, StringComparison.Ordinal);
        }
        private string SelectedTargetName()
        {
            if (_editTarget == EditTarget.Effect) return "이펙트 / " + _effectIndex;
            if (_editTarget == EditTarget.Range) return "타격 범위 / " + KoreanField(SelectedRangeName());
            if (_editTarget == EditTarget.Timing && _timingIndex < _timings.Count) return "타격 시점 / " + KoreanField(_so.FindProperty(_timings[_timingIndex]).name);
            if (_editTarget == EditTarget.Segment && _segmentIndex >= 0 && _segmentIndex < _segments.Count) return "애니메이션 조각 / " + _segments[_segmentIndex].Name;
            return "선택 없음";
        }
        private static string KoreanField(string name)
        {
            return name.Replace("AtSeconds", " 시점(초)").Replace("First", "첫 ").Replace("Second", "두 번째 ")
                .Replace("Label", "이름").Replace("Prefab", "프리팹").Replace("AnchorBoneName", "기준 본 이름").Replace("AttachToAnchor", "기준 본에 부착")
                .Replace("Lifetime", "지속시간").Replace("LocalOffset", "로컬 위치").Replace("LocalEulerAngles", "로컬 회전").Replace("ManualOverride", "수동 고정(셋업 보호)").Replace("Scale", "크기")
                .Replace("Strike", "타격 ").Replace("Slash", "베기 ").Replace("Cleave", "내려베기 ").Replace("Thrust", "찌르기 ")
                .Replace("Shield", "방패 ").Replace("Back", "후방 ").Replace("Front", "전방 ").Replace("Cross", "교차 ")
                .Replace("Laser", "레이저 ").Replace("Field", "장판 ").Replace("Wave", "파동 ").Replace("Volley", "연속 발사 ")
                .Replace("Radius", "반경").Replace("HalfAngle", "반각").Replace("Reach", "도달 거리").Replace("Length", "길이")
                .Replace("Width", "너비").Replace("Height", "높이").Replace("Count", "횟수").Replace("IntervalSeconds", "간격(초)")
                .Replace("Offset", "오프셋").Replace("Rise", "올려베기 ").Replace("Spin", "회전 ").Replace("Finish", "마무리 ")
                .Replace("Near", "근거리 ").Replace("Mid", "중거리 ").Replace("Far", "원거리 ").Replace("Impact", "충격 ").Trim();
        }
        private float RelatedRadius(string angle) { var p = _so.FindProperty(angle.Substring(0, angle.Length - "HalfAngle".Length) + "Radius"); return p == null ? 4 : Mathf.Max(.1f, p.floatValue); }
        private Segment SegmentAt(float time) { for (int i = _segments.Count - 1; i >= 0; i--) if (time >= _segments[i].Start) return _segments[i]; return _segments.Count > 0 ? _segments[0] : null; }
        private static void Header(string text) { EditorGUILayout.Space(); EditorGUILayout.LabelField(text, EditorStyles.boldLabel); }
        private static bool IsRange(string n) => n.EndsWith("Radius", StringComparison.Ordinal) || n.EndsWith("HalfAngle", StringComparison.Ordinal) || n.EndsWith("Reach", StringComparison.Ordinal) || n.EndsWith("Length", StringComparison.Ordinal) || n.EndsWith("Width", StringComparison.Ordinal) || n.EndsWith("Height", StringComparison.Ordinal);
        private static AnimatorState FindState(AnimatorController c, string name)
        {
            foreach (var layer in c.layers) { var found = FindState(layer.stateMachine, name); if (found != null) return found; }
            return null;
        }
        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states) if (child.state.name == name) return child.state;
            foreach (var child in machine.stateMachines) { var found = FindState(child.stateMachine, name); if (found != null) return found; }
            return null;
        }
    }
}
