using System.Collections.Generic;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 파티클 프리팹의 핵심 값(크기·색·수명·속도·방출량 등)을 씬에서 보면서 고치는 툴.
    ///
    /// 편집은 씬에 띄운 프리뷰 인스턴스에 먼저 적용되고, 만족스러우면 '프리팹에 저장'으로
    /// 에셋에 반영한다. 실수로 프리팹이 바뀌는 것을 막기 위해 저장은 명시적으로만 한다.
    ///
    /// 하나의 파티클 프리팹을 여러 AttackRecord가 공유하는 경우가 많아, 저장 전에
    /// 어떤 공격들이 영향을 받는지 같이 보여준다.
    /// </summary>
    public class ParticleEditWindow : EditorWindow
    {
        [MenuItem("Tools/Combat/파티클 편집기")]
        public static void Open()
        {
            var win = GetWindow<ParticleEditWindow>();
            win.titleContent = new GUIContent("Particle Edit");
            win.minSize = new Vector2(380f, 320f);
        }

        private GameObject _prefab;          // 편집 대상 프리팹 에셋
        private GameObject _preview;         // 씬에 띄운 프리뷰 인스턴스
        private ParticleSystem[] _systems;   // 프리뷰 안의 시스템들
        private int _systemIdx;
        private string[] _systemNames = new string[0];

        private float _time;
        private bool _playing;
        private float _speed = 1f;
        private double _lastTick;

        private Vector2 _scroll;
        private bool _showUsers;
        private readonly List<AttackRecord> _users = new();

        // =====================================================================

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            _lastTick = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreview();
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;

            if (!_playing || _preview == null) return;

            _time += dt * _speed;
            if (_time > 5f) _time = 0f;
            Simulate();
            Repaint();
        }

        // =====================================================================

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            _prefab = (GameObject)EditorGUILayout.ObjectField("파티클 프리팹", _prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                DestroyPreview();
                CollectUsers();
            }

            if (_prefab == null)
            {
                EditorGUILayout.HelpBox(
                    "편집할 파티클 프리팹을 넣으세요.\n" +
                    "AttackRecord의 Particle 필드에 꽂혀 있는 그 프리팹입니다.",
                    MessageType.Info);
                return;
            }

            DrawPreviewControls();

            if (_preview == null)
            {
                EditorGUILayout.HelpBox("'프리뷰 띄우기'를 눌러야 편집할 수 있습니다.", MessageType.Info);
                DrawUsers();
                return;
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawRootScale();
            DrawSystemSelector();
            DrawSystemFields();

            EditorGUILayout.Space(6f);
            DrawSaveBar();
            DrawUsers();

            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_preview == null ? "프리뷰 띄우기" : "프리뷰 끄기", GUILayout.Width(100f)))
                {
                    if (_preview == null) CreatePreview();
                    else DestroyPreview();
                }

                using (new EditorGUI.DisabledScope(_preview == null))
                {
                    if (GUILayout.Button(_playing ? "■ 정지" : "▶ 재생", GUILayout.Width(70f)))
                        _playing = !_playing;

                    if (GUILayout.Button("↺", GUILayout.Width(28f)))
                    {
                        _time = 0f;
                        Simulate();
                    }

                    EditorGUILayout.LabelField("속도", GUILayout.Width(30f));
                    _speed = EditorGUILayout.Slider(_speed, 0.1f, 2f);
                }
            }

            using (new EditorGUI.DisabledScope(_preview == null))
            {
                EditorGUI.BeginChangeCheck();
                _time = EditorGUILayout.Slider($"t = {_time:0.00}s", _time, 0f, 5f);
                if (EditorGUI.EndChangeCheck())
                {
                    _playing = false;
                    Simulate();
                }
            }
        }

        private void DrawRootScale()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("전체 크기", EditorStyles.boldLabel);

                Vector3 s = _preview.transform.localScale;
                EditorGUI.BeginChangeCheck();
                float uniform = EditorGUILayout.Slider("배율", s.x, 0.1f, 5f);
                if (EditorGUI.EndChangeCheck())
                {
                    _preview.transform.localScale = Vector3.one * uniform;
                    Simulate();
                }

                EditorGUILayout.LabelField(
                    "루트 스케일입니다. 파티클 하나하나의 크기는 아래 Start Size로 조절하세요.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawSystemSelector()
        {
            if (_systems == null || _systems.Length == 0) return;

            EditorGUILayout.Space(2f);
            if (_systems.Length > 1)
                _systemIdx = EditorGUILayout.Popup("시스템", _systemIdx, _systemNames);
            else
                EditorGUILayout.LabelField("시스템", _systemNames[0]);
        }

        private void DrawSystemFields()
        {
            if (_systems == null || _systemIdx >= _systems.Length) return;

            var ps = _systems[_systemIdx];
            if (ps == null) return;

            var main = ps.main;
            bool changed = false;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Main", EditorStyles.boldLabel);

                var size = main.startSize;
                if (DrawMinMax("Start Size", ref size)) { main.startSize = size; changed = true; }

                var life = main.startLifetime;
                if (DrawMinMax("Start Lifetime", ref life)) { main.startLifetime = life; changed = true; }

                var spd = main.startSpeed;
                if (DrawMinMax("Start Speed", ref spd)) { main.startSpeed = spd; changed = true; }

                var grav = main.gravityModifier;
                if (DrawMinMax("Gravity Modifier", ref grav)) { main.gravityModifier = grav; changed = true; }

                var col = main.startColor;
                if (DrawColor("Start Color", ref col)) { main.startColor = col; changed = true; }
            }

            var emission = ps.emission;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Emission", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                bool on = EditorGUILayout.Toggle("Enabled", emission.enabled);
                if (EditorGUI.EndChangeCheck()) { emission.enabled = on; changed = true; }

                var rate = emission.rateOverTime;
                if (DrawMinMax("Rate over Time", ref rate)) { emission.rateOverTime = rate; changed = true; }
            }

            var shape = ps.shape;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                bool sOn = EditorGUILayout.Toggle("Enabled", shape.enabled);
                float radius = EditorGUILayout.FloatField("Radius", shape.radius);
                float angle = EditorGUILayout.Slider("Angle", shape.angle, 0f, 90f);
                if (EditorGUI.EndChangeCheck())
                {
                    shape.enabled = sOn;
                    shape.radius = radius;
                    shape.angle = angle;
                    changed = true;
                }
            }

            if (changed) Simulate();
        }

        /// MinMaxCurve는 Constant/TwoConstants만 다룬다. 커브 모드는 여기서 건드리면
        /// 원본 커브가 날아가므로 인스펙터로 넘긴다.
        private static bool DrawMinMax(string label, ref ParticleSystem.MinMaxCurve v)
        {
            EditorGUI.BeginChangeCheck();

            switch (v.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    float c = EditorGUILayout.FloatField(label, v.constant);
                    if (EditorGUI.EndChangeCheck()) { v.constant = c; return true; }
                    return false;

                case ParticleSystemCurveMode.TwoConstants:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                        float lo = EditorGUILayout.FloatField(v.constantMin);
                        float hi = EditorGUILayout.FloatField(v.constantMax);
                        if (EditorGUI.EndChangeCheck())
                        {
                            v.constantMin = lo;
                            v.constantMax = hi;
                            return true;
                        }
                    }
                    return false;

                default:
                    EditorGUI.EndChangeCheck();
                    EditorGUILayout.LabelField(label, $"({v.mode}) — 인스펙터에서 편집");
                    return false;
            }
        }

        private static bool DrawColor(string label, ref ParticleSystem.MinMaxGradient v)
        {
            EditorGUI.BeginChangeCheck();

            switch (v.mode)
            {
                case ParticleSystemGradientMode.Color:
                    Color c = EditorGUILayout.ColorField(label, v.color);
                    if (EditorGUI.EndChangeCheck()) { v.color = c; return true; }
                    return false;

                case ParticleSystemGradientMode.TwoColors:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                        Color lo = EditorGUILayout.ColorField(v.colorMin);
                        Color hi = EditorGUILayout.ColorField(v.colorMax);
                        if (EditorGUI.EndChangeCheck())
                        {
                            v.colorMin = lo;
                            v.colorMax = hi;
                            return true;
                        }
                    }
                    return false;

                default:
                    EditorGUI.EndChangeCheck();
                    EditorGUILayout.LabelField(label, $"({v.mode}) — 인스펙터에서 편집");
                    return false;
            }
        }

        // =====================================================================

        private void DrawSaveBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("프리팹에 저장", GUILayout.Height(24f)))
                    SaveToPrefab();

                if (GUILayout.Button("되돌리기", GUILayout.Width(80f), GUILayout.Height(24f)))
                {
                    CreatePreview();   // 프리팹에서 다시 인스턴스화 = 편집 폐기
                    ShowNotification(new GUIContent("프리팹 값으로 되돌렸습니다."));
                }
            }

            EditorGUILayout.LabelField(
                "편집은 프리뷰 인스턴스에만 적용됩니다. 저장을 눌러야 프리팹에 반영됩니다.",
                EditorStyles.miniLabel);
        }

        private void SaveToPrefab()
        {
            if (_preview == null || _prefab == null) return;

            string path = AssetDatabase.GetAssetPath(_prefab);
            if (string.IsNullOrEmpty(path))
            {
                ShowNotification(new GUIContent("프리팹 경로를 찾지 못했습니다."));
                return;
            }

            if (!EditorUtility.DisplayDialog("프리팹에 저장",
                    $"{_prefab.name}에 현재 값을 덮어씁니다.\n\n" +
                    (_users.Count > 0
                        ? $"이 프리팹을 쓰는 공격 {_users.Count}개가 함께 바뀝니다."
                        : "이 프리팹을 참조하는 AttackRecord를 찾지 못했습니다."),
                    "저장", "취소"))
                return;

            PrefabUtility.SaveAsPrefabAsset(_preview, path, out bool ok);
            if (ok)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("저장했습니다."));
            }
            else
            {
                ShowNotification(new GUIContent("저장 실패 — Console을 확인하세요."));
            }
        }

        private void DrawUsers()
        {
            _showUsers = EditorGUILayout.Foldout(_showUsers, $"이 프리팹을 쓰는 공격 ({_users.Count})", true);
            if (!_showUsers) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_users.Count == 0)
                {
                    EditorGUILayout.LabelField("없음", EditorStyles.miniLabel);
                    return;
                }

                if (_users.Count > 1)
                    EditorGUILayout.HelpBox(
                        "여러 공격이 공유하는 프리팹입니다. 저장하면 전부 함께 바뀝니다.",
                        MessageType.Warning);

                foreach (var u in _users)
                {
                    if (u == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{u.Id}  {u.Name}", EditorStyles.miniLabel);
                        if (GUILayout.Button("선택", EditorStyles.miniButton, GUILayout.Width(45f)))
                            Selection.activeObject = u;
                    }
                }
            }
        }

        /// 한 공격이 이펙트를 여러 개 가질 수 있으므로 목록 전체를 훑는다.
        private bool UsesPrefab(AttackRecord rec)
        {
            for (int i = 0; i < rec.ParticleCount; i++)
                if (rec.Particle(i).Prefab == _prefab) return true;
            return false;
        }

        private void CollectUsers()
        {
            _users.Clear();
            if (_prefab == null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:AttackRecord"))
            {
                var rec = AssetDatabase.LoadAssetAtPath<AttackRecord>(AssetDatabase.GUIDToAssetPath(guid));
                if (rec != null && UsesPrefab(rec))
                    _users.Add(rec);
            }
        }

        // =====================================================================

        private void CreatePreview()
        {
            DestroyPreview();
            if (_prefab == null) return;

            _preview = (GameObject)PrefabUtility.InstantiatePrefab(_prefab);
            if (_preview == null) return;

            _preview.hideFlags = HideFlags.DontSave;
            Undo.RegisterCreatedObjectUndo(_preview, "파티클 프리뷰");

            var view = SceneView.lastActiveSceneView;
            _preview.transform.position = view != null ? view.pivot : Vector3.zero;

            _systems = _preview.GetComponentsInChildren<ParticleSystem>(true);
            _systemNames = new string[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
                _systemNames[i] = _systems[i].gameObject.name;

            _systemIdx = 0;
            _time = 0f;

            Selection.activeGameObject = _preview;
            if (view != null) view.FrameSelected();

            Simulate();
        }

        private void DestroyPreview()
        {
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
            _systems = null;
            _systemNames = new string[0];
            _playing = false;
        }

        private void Simulate()
        {
            if (_preview == null) return;

            var root = _preview.GetComponentInChildren<ParticleSystem>();
            if (root == null) return;

            root.Simulate(_time, true, true);
            SceneView.RepaintAll();
        }
    }
}
