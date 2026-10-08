using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Player;
using Game.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 공격의 이펙트 목록(AttackRecord.Particles)을 씬뷰 핸들로 배치하는 툴.
    /// 한 공격에 여러 이펙트를 서로 다른 시점·위치로 둘 수 있고, 목록에서 고른 것이 핸들 대상이 된다.
    ///
    /// 앵커는 런타임과 같은 기준인 WeaponBase.transform이다. 무기 오브젝트는 아바타 루트의
    /// 직계 자식(본 아님)이라 애니메이션 영향을 받지 않으므로, 에디트 모드에서 잰 값이
    /// 런타임 값과 같다 — 세션을 띄울 필요가 없다.
    ///
    /// 값은 SO 에셋에 바로 쓴다(Undo 지원). 시트가 건드리지 않는 필드라 Sync에도 살아남는다.
    /// </summary>
    public class EffectOffsetWindow : EditorWindow
    {
        [MenuItem("Tools/Combat/이펙트 오프셋 편집기")]
        public static void Open()
        {
            var win = GetWindow<EffectOffsetWindow>();
            win.titleContent = new GUIContent("Effect Offset");
            win.minSize = new Vector2(360f, 300f);   // 스크롤이 있으므로 작게 줄여도 쓸 수 있다
        }

        // --- 대상 ---
        private GameObject _avatar;               // 씬에 놓은 플레이어 프리팹
        private WeaponBase[] _weapons = new WeaponBase[0];
        private string[] _weaponNames = new string[0];
        private int _weaponIdx;
        private Animator _animator;

        private AttackRecord[] _attacks = new AttackRecord[0];
        private float _weaponBaseDamage = 1f;
        private int _attackIdx = -1;
        private int _particleIdx;   // 편집 대상 이펙트

        // --- 프리뷰 ---
        // 애니메이션과 파티클은 하나의 타임라인(_time)을 공유한다. t=0이 공격 시작이고,
        // 파티클은 ParticleOccurDelay가 지난 뒤부터 재생된다 — 런타임 타이밍과 같은 관계.
        private GameObject _fxPreview;
        private AnimationClip _clip;

        private float _time;
        private bool _playing;
        private bool _loop = true;
        private float _speed = 1f;
        private float _fxLength = 2f;   // 프리뷰 파티클의 대략적 길이(타임라인 끝 계산용)

        // 전진기 프리뷰로 아바타를 실제로 옮기므로, 원래 위치를 기억해 뒀다가 되돌린다.
        private Vector3 _avatarHome;
        private bool _homeSet;

        private Vector2 _scroll;
        private Vector2 _listScroll;
        private float _listHeight = 110f;
        private double _lastTick;

        // 섹션 접기 상태. 창을 넘길 만큼 길어져서 필요한 것만 펴놓고 쓴다.
        private bool _showOffset = true;
        private bool _showSpeed = true;
        private bool _showMultiHit = true;
        private bool _showLunge = true;
        private bool _showTimeline = true;
        private bool _showTrim;
        private bool _showImportSettings;
        private bool _showBlendTree;
        private bool _showPose;
        private bool _showDash = true;

        // 시트의 대시·백대시 행 인덱스(-1 = 없음). 무기를 바꿀 때 이름으로 다시 찾는다.
        private int _dashIdx = -1;
        private int _backDashIdx = -1;

        // --- 사운드 ---
        // 대상 클립의 사운드 이벤트를 사본으로 들고 편집하다가 '저장'에서 한 번에 클립에 쓴다.
        // FBX 클립은 저장이 곧 재임포트라(느리다) 슬라이더를 움직일 때마다 쓸 수 없다.
        private bool _showSound = true;
        private readonly List<AnimationEvent> _soundEvents = new();
        private AnimationClip _soundClip;          // _soundEvents를 읽어온 클립
        private bool _soundDirty;
        private bool _soundPreview = true;         // 재생 중 소리 미리듣기
        private string _soundLog;

        // --- 포즈 보정 ---
        // 머슬 이름 → 오프셋. 프리뷰는 이 값을 얹은 임시 클립을 샘플링한다(원본은 안 건드린다).
        private readonly Dictionary<string, float> _poseOffsets = new();
        private AnimationClip _posePreview;
        private HashSet<string> _clipMuscles;
        private List<PoseOffsetBaker.BoneEntry> _clipBones;
        private List<string> _clipPositionCurves;
        private int _boneIdx;
        private string[] _boneNames = new string[0];
        private bool _showBoneDetail;
        private bool _showPosition;
        private string _poseLog;
        private bool _poseOnBlendTreeClip;   // 대상: 공격 클립(false) / 블렌드트리에서 고른 클립(true)
        private AnimationClip _poseTargetOverride;

        // --- 블렌드트리 배속 ---
        // 공격과 무관하다(이동 블렌드트리가 주 대상). 컨트롤러를 뒤져 캐시해 두고 무기가 바뀌면 다시 만든다.
        private BlendTreeEntry[] _blendTrees = new BlendTreeEntry[0];
        private string[] _blendTreeNames = new string[0];
        private int _blendTreeIdx;
        private bool _blendTreesDirty = true;
        private Dictionary<AnimationClip, AnimationClip> _overrideMap;
        private string _clipBakeLog;

        // --- 끝 자르기 ---
        // -1 = 아직 안 정했음(클립 길이로 초기화). 타임라인에서 가져오거나 손으로 넣는다.
        private float _trimTime = -1f;
        private string _trimLog;

        /// 컨트롤러 안에서 찾은 블렌드트리 State 하나.
        private readonly struct BlendTreeEntry
        {
            public readonly string Path;          // "Base Layer / Locomotion"
            public readonly AnimatorState State;
            public readonly BlendTree Tree;

            public BlendTreeEntry(string path, AnimatorState state, BlendTree tree)
            {
                Path = path;
                State = state;
                Tree = tree;
            }
        }

        /// 섹션 헤더. 접혀 있으면 false를 돌려주고 본문은 그리지 않는다.
        private static bool SectionHeader(string title, ref bool open)
        {
            open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            return open;
        }

        private WeaponBase Weapon =>
            _weapons.Length > 0 && _weaponIdx < _weapons.Length ? _weapons[_weaponIdx] : null;

        private AttackRecord Attack =>
            _attackIdx >= 0 && _attackIdx < _attacks.Length ? _attacks[_attackIdx] : null;

        private Transform Anchor => Weapon != null ? Weapon.transform : null;

        // =====================================================================

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            // 재컴파일·플레이 진입으로 창이 죽으면 StopAnimationMode가 안 불려 포즈가 씬에 굳는다.
            AssemblyReloadEvents.beforeAssemblyReload += CleanUp;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _lastTick = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanUp;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            CleanUp();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) CleanUp();
        }

        private void CleanUp()
        {
            EditorAudioPreview.Stop();
            StopPose();
            DestroyPreview();
            DestroyPosePreview();
            RestoreAvatarHome();
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTick);
            _lastTick = now;

            if (!_playing) return;

            float prev = _time;
            _time += dt * _speed;
            bool wrapped = false;
            if (_time > TotalLength)
            {
                _time = _loop ? 0f : TotalLength;
                wrapped = _loop;
            }

            // 소리는 '지나간 구간'으로 판정한다 — 프레임이 이벤트를 건너뛰어도 빠지지 않는다.
            TickSoundPreview(prev, _time, wrapped);
            ApplyTime();
            Repaint();
        }

        /// 실제로 씬에 샘플링되는 클립. 보정 프리뷰가 있으면 그것, 없으면 포즈 대상 클립.
        /// (포즈 대상은 공격 클립이거나 직접 지정한 클립이다 — 공격을 안 골라도 재생이 되어야 한다.)
        private AnimationClip SampledClip => _posePreview != null ? _posePreview : PoseTarget;

        /// 샘플링 시 시간에 곱하는 배속. 공격 클립은 시트의 애니 배속을 따르고,
        /// 직접 지정한 클립은 어디에 쓰이는지 모르므로 원본 속도로 본다.
        private float SampleSpeed =>
            _poseOnBlendTreeClip ? 1f : Mathf.Max(0.01f, AnimSpeedOf(Attack));

        /// 타임라인 총 길이 — 애니메이션과 (파티클 시작 + 파티클 길이) 중 긴 쪽.
        private float TotalLength
        {
            get
            {
                var sampled = SampledClip;
                float animLen = sampled != null ? sampled.length / SampleSpeed : 0f;
                float fxEnd = ParticleIdx >= 0
                    ? SelectedFxTime() + _fxLength / SelectedFxSpeed()
                    : 0f;
                return Mathf.Max(0.1f, Mathf.Max(animLen, fxEnd));
            }
        }

        /// 현재 _time을 전진·포즈·파티클에 동시에 반영한다.
        private void ApplyTime()
        {
            ApplyLungePreview();
            SamplePose();
            SimulateFx();
            SceneView.RepaintAll();
        }

        /// 타임라인 시점만큼 아바타를 실제로 전진시킨다(프리뷰 종료 시 원위치).
        private void ApplyLungePreview()
        {
            if (_avatar == null) return;

            var rec = Attack;
            if (rec == null || !rec.HasLunge)
            {
                RestoreAvatarHome();
                return;
            }

            if (!_homeSet)
            {
                _avatarHome = _avatar.transform.position;
                _homeSet = true;
            }
            _avatar.transform.position = _avatarHome + _avatar.transform.forward * LungeAmountAt(_time);
        }

        private void RestoreAvatarHome()
        {
            if (!_homeSet || _avatar == null) return;
            _avatar.transform.position = _avatarHome;
            _homeSet = false;
        }

        /// 이펙트는 발생 시점 위치에 남는다(플레이어를 따라가지 않는다 — EffectPool이 월드에 스냅샷으로 놓는다).
        /// 현재 전진량에서 발생 시점 전진량을 빼서 그때의 앵커 위치를 되돌려 구한다.
        private Vector3 AnchorPosAtSpawn()
        {
            if (Anchor == null) return Vector3.zero;

            var rec = Attack;
            if (rec == null || !rec.HasLunge || _avatar == null) return Anchor.position;

            float back = LungeAmountAt(_time) - LungeAmountAt(SelectedFxTime());
            return Anchor.position - _avatar.transform.forward * back;
        }

        // =====================================================================

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("이펙트 오프셋 편집기", EditorStyles.boldLabel);

            DrawTargetSection();

            // 프로젝트 창의 프리팹 에셋을 넣으면 아바타가 씬에 없어 아무것도 안 보인다.
            // (이펙트 프리뷰만 씬에 Instantiate되므로 그것만 보이는 증상이 난다.)
            if (_avatar != null && PrefabUtility.IsPartOfPrefabAsset(_avatar))
            {
                EditorGUILayout.HelpBox(
                    "프로젝트 창의 프리팹 에셋이 들어왔습니다. 씬에 배치된 인스턴스가 필요합니다.\n" +
                    "아바타가 씬에 없으면 캐릭터도 포즈도 보이지 않고 이펙트만 뜹니다.",
                    MessageType.Error);

                if (GUILayout.Button("씬에 배치하고 이걸로 쓰기"))
                    PlaceAvatarInScene();

                return;
            }

            if (Weapon == null)
            {
                EditorGUILayout.HelpBox(
                    "씬에 플레이어 프리팹(PlayerwAnimation)을 놓고, 그 씬 오브젝트를 여기에 드래그하세요.\n" +
                    "네트워크 세션은 필요 없습니다 — 에디트 모드로 충분합니다.",
                    MessageType.Info);
                return;
            }

            if (_attacks.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "이 무기의 WeaponRecord가 비어 있거나 Attacks가 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6f);

            // 공격 목록은 높이를 묶어둔다 — 무기마다 개수가 달라 아래 섹션을 밀어내면 안 된다.
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(_listHeight));
            DrawAttackList();
            EditorGUILayout.EndScrollView();

            // 목록 높이 조절 핸들
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("목록 높이", EditorStyles.miniLabel, GUILayout.Width(55f));
                _listHeight = EditorGUILayout.Slider(_listHeight, 60f, 300f);
            }

            EditorGUILayout.Space(4f);

            // 섹션이 여럿이라 창을 넘긴다. 전체를 스크롤로 감싸고 각 섹션은 접을 수 있게 한다.
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // 블렌드트리·포즈 보정은 이동 모션도 다루므로 특정 공격에 속하지 않는다 —
            // 공격을 안 골라도 쓸 수 있어야 한다.
            DrawBlendTreeSection();
            DrawPoseSection();
            DrawSoundSection();
            DrawDashSection();

            if (Attack != null)
            {
                DrawOffsetSection();
                DrawSpeedSection();
                DrawMultiHitSection();
                DrawLungeSection();
                DrawPreviewSection();
                DrawTrimSection();
                DrawClipImportSection();
            }
            else
            {
                EditorGUILayout.HelpBox("위 목록에서 공격을 고르면 이펙트·배속·다단히트 편집이 열립니다.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSection()
        {
            EditorGUI.BeginChangeCheck();
            _avatar = (GameObject)EditorGUILayout.ObjectField("아바타", _avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                RebuildWeapons();

            if (_weapons.Length == 0) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _weaponIdx = EditorGUILayout.Popup("무기", _weaponIdx, _weaponNames);
                if (EditorGUI.EndChangeCheck())
                {
                    RebuildAttacks();
                    DestroyPreview();
                }

                // 무기 메시는 런타임에 WeaponModelSelector가 토글한다 — 에디트 모드에선 손으로 맞춰야 한다.
                if (GUILayout.Button("이 무기 모델만 켜기", GUILayout.Width(130f)))
                    ShowOnlySelectedWeaponModel();
            }

            // 포즈 샘플링은 본 트랜스폼을 직접 건드린다. 재컴파일 등으로 복원이 누락되면 여기서 되돌린다.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("포즈 초기화", GUILayout.Width(70f));

                if (GUILayout.Button("프리팹 값으로 되돌리기"))
                    RevertPoseToPrefab();

                if (GUILayout.Button("T포즈(바인드포즈)로", GUILayout.Width(130f)))
                    RestoreBindPose();
            }
        }

        /// 본 트랜스폼의 프리팹 오버라이드만 되돌린다. 아바타 배치 위치(루트)는 건드리지 않는다.
        /// 프리팹 에셋이 멀쩡하면 이 방법이 가장 정확하다.
        private void RevertPoseToPrefab()
        {
            if (_avatar == null) return;
            StopPose();
            RestoreAvatarHome();

            if (!PrefabUtility.IsPartOfPrefabInstance(_avatar))
            {
                ShowNotification(new GUIContent("프리팹 인스턴스가 아닙니다. T포즈 복원을 쓰세요."));
                return;
            }

            int n = 0;
            foreach (var t in _avatar.GetComponentsInChildren<Transform>(true))
            {
                if (t == _avatar.transform) continue;             // 배치 위치는 유지
                if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;

                PrefabUtility.RevertObjectOverride(t, InteractionMode.UserAction);
                n++;
            }

            ShowNotification(new GUIContent($"본 {n}개를 프리팹 값으로 되돌렸습니다."));
            SceneView.RepaintAll();
        }

        /// 프리팹 에셋 자체가 오염된 경우용. 스킨 메시의 bindpose로 본을 되돌린다.
        private void RestoreBindPose()
        {
            if (_avatar == null) return;
            StopPose();
            RestoreAvatarHome();

            int n = 0;
            foreach (var smr in _avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var bones = smr.bones;
                var binds = mesh.bindposes;

                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                {
                    if (bones[i] == null) continue;

                    // bindpose는 (본의 worldToLocal × 렌더러의 localToWorld) → 역으로 본의 월드 행렬을 얻는다.
                    Matrix4x4 m = smr.transform.localToWorldMatrix * binds[i].inverse;
                    Undo.RecordObject(bones[i], "T포즈 복원");
                    bones[i].SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    n++;
                }
            }

            ShowNotification(new GUIContent(n > 0
                ? $"본 {n}개를 바인드포즈로 복원했습니다."
                : "스킨 메시를 찾지 못했습니다."));
            SceneView.RepaintAll();
        }

        /// WeaponModelSelector의 _models 배열을 읽어, 선택한 무기의 모델만 활성화한다.
        /// (런타임의 HandleWeaponEquipped와 같은 동작을 에디트 모드에서 재현)
        private void ShowOnlySelectedWeaponModel()
        {
            var selector = _avatar != null ? _avatar.GetComponentInChildren<WeaponModelSelector>(true) : null;
            if (selector == null)
            {
                ShowNotification(new GUIContent("WeaponModelSelector를 찾지 못했습니다."));
                return;
            }

            var weapon = Weapon;
            if (weapon == null) return;

            var so = new SerializedObject(selector);
            var models = so.FindProperty("_models");
            if (models == null || !models.isArray) return;

            int shown = 0;
            for (int i = 0; i < models.arraySize; i++)
            {
                var el = models.GetArrayElementAtIndex(i);
                int wid = el.FindPropertyRelative("WeaponId").intValue;
                var go = el.FindPropertyRelative("Model").objectReferenceValue as GameObject;
                if (go == null) continue;

                bool on = wid == weapon.ID;
                Undo.RecordObject(go, "무기 모델 토글");
                go.SetActive(on);
                EditorUtility.SetDirty(go);
                if (on) shown++;
            }

            ShowNotification(new GUIContent(shown > 0
                ? $"{_weaponNames[_weaponIdx]} 모델만 켰습니다."
                : $"이 무기(ID {weapon.ID})에 매칭되는 모델이 없습니다."));
            SceneView.RepaintAll();
        }

        private void DrawAttackList()
        {
            for (int i = 0; i < _attacks.Length; i++)
            {
                var rec = _attacks[i];
                if (rec == null) continue;

                bool on = i == _attackIdx;
                using (new EditorGUILayout.HorizontalScope(on ? EditorStyles.helpBox : GUIStyle.none))
                {
                    bool next = GUILayout.Toggle(on, $"{rec.Name}", EditorStyles.miniButton, GUILayout.Width(150f));
                    if (next && !on) SelectAttack(i);

                    EditorGUILayout.LabelField(
                        (i == _dashIdx ? "[대시] " : i == _backDashIdx ? "[백대시] " : "") +
                        (rec.ParticleCount > 0 ? $"이펙트 {rec.ParticleCount}" : "(이펙트 없음)"),
                        EditorStyles.miniLabel);
                }
            }
        }

        /// 현재 선택된 이펙트. 없으면 -1.
        private int ParticleIdx
        {
            get
            {
                int count = Attack != null ? Attack.ParticleCount : 0;
                return count == 0 ? -1 : Mathf.Clamp(_particleIdx, 0, count - 1);
            }
        }

        private void DrawOffsetSection()
        {
            var rec = Attack;
            if (!SectionHeader($"이펙트 — {rec.Name} ({rec.ParticleCount}개)", ref _showOffset)) return;

            var so = new SerializedObject(rec);
            var particles = so.FindProperty("_particles");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 목록 — 시점 순으로 무엇이 언제 터지는지 한눈에.
                for (int i = 0; i < particles.arraySize; i++)
                {
                    var e = particles.GetArrayElementAtIndex(i);
                    var prefab = e.FindPropertyRelative("Prefab").objectReferenceValue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool on = GUILayout.Toggle(i == ParticleIdx,
                            $"{i}. {(prefab != null ? prefab.name : "(없음)")}  @ {e.FindPropertyRelative("Time").floatValue:0.###}s",
                            EditorStyles.miniButton);

                        if (on && i != ParticleIdx)
                        {
                            _particleIdx = i;
                            DestroyPreview();
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("－", EditorStyles.miniButton, GUILayout.Width(24f)))
                        {
                            particles.DeleteArrayElementAtIndex(i);
                            so.ApplyModifiedProperties();
                            DestroyPreview();
                            return;
                        }
                    }
                }

                if (GUILayout.Button("＋ 이펙트 추가"))
                {
                    int i = particles.arraySize;
                    particles.arraySize = i + 1;
                    var e = particles.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Prefab").objectReferenceValue = null;
                    e.FindPropertyRelative("Time").floatValue = rec.ParticleOccurDelay;
                    e.FindPropertyRelative("Offset").vector3Value = Vector3.zero;
                    e.FindPropertyRelative("Euler").vector3Value = Vector3.zero;
                    e.FindPropertyRelative("Speed").floatValue = 1f;
                    _particleIdx = i;
                    so.ApplyModifiedProperties();
                    return;
                }

                int idx = ParticleIdx;
                if (idx < 0)
                {
                    EditorGUILayout.LabelField("이펙트가 없습니다. 추가하세요.", EditorStyles.miniLabel);
                    so.ApplyModifiedProperties();
                    return;
                }

                EditorGUILayout.Space(4f);
                var sel = particles.GetArrayElementAtIndex(idx);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(sel.FindPropertyRelative("Prefab"), new GUIContent("프리팹"));
                if (EditorGUI.EndChangeCheck()) DestroyPreview();

                EditorGUILayout.PropertyField(sel.FindPropertyRelative("Time"), new GUIContent("시점 (s)"));
                EditorGUILayout.PropertyField(sel.FindPropertyRelative("Offset"), new GUIContent("위치"));
                EditorGUILayout.PropertyField(sel.FindPropertyRelative("Euler"), new GUIContent("회전"));
                EditorGUILayout.PropertyField(sel.FindPropertyRelative("Speed"), new GUIContent("배속"));

                var follow = sel.FindPropertyRelative("FollowCaster");
                EditorGUILayout.PropertyField(follow, new GUIContent("시전자 따라가기"));
                EditorGUILayout.LabelField(
                    follow.boolValue
                        ? "재생 내내 시전자를 따라옵니다 — 차징·오라용."
                        : "발생 위치에 남습니다 — 베기 궤적·폭발용. 지속 이펙트라면 켜세요.",
                    EditorStyles.miniLabel);

                EditorGUILayout.PropertyField(
                    sel.FindPropertyRelative("Sustained"),
                    new GUIContent("차징 중 유지"));

                EditorGUILayout.LabelField(
                    $"앵커: {Anchor.name} — 핸들을 움직이면 선택된 이펙트에 즉시 반영됩니다 (Ctrl+Z 가능).",
                    EditorStyles.miniLabel);
            }

            so.ApplyModifiedProperties();
        }

        /// 애니 배속과 시트 타이밍은 서로 모른다. 여기서 어긋남을 눈에 보이게 한다.
        private void DrawSpeedSection()
        {
            var rec = Attack;
            var so = new SerializedObject(rec);
            if (!SectionHeader("배속 · 이펙트 구간", ref _showSpeed)) return;

            var speed = so.FindProperty("_animSpeed");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(speed, new GUIContent("애니 배속"));
                    if (GUILayout.Button("1로", GUILayout.Width(40f)))
                        speed.floatValue = 1f;
                }

                if (ParticleIdx >= 0)
                {
                    float fxPlay = _fxLength / SelectedFxSpeed();
                    float fxStart = SelectedFxTime();
                    EditorGUILayout.LabelField(
                        $"선택 이펙트 — 시작 {fxStart:0.###}s   끝 {fxStart + fxPlay:0.###}s   길이 {fxPlay:0.###}s" +
                        (_fxPreview == null ? "  (길이는 프리뷰를 켜야 측정됨)" : ""),
                        EditorStyles.miniLabel);
                }

                float s = speed.floatValue > 0f ? speed.floatValue : 1f;

                if (_clip == null)
                {
                    EditorGUILayout.LabelField("클립을 찾지 못해 길이 비교를 할 수 없습니다.", EditorStyles.miniLabel);
                }
                else
                {
                    float played = _clip.length / s;
                    float gap = played - rec.Duration;

                    EditorGUILayout.LabelField(
                        $"재생 길이 {played:0.###}s (원본 {_clip.length:0.###}s ÷ {s:0.##})   vs   Duration {rec.Duration:0.###}s",
                        EditorStyles.miniLabel);

                    // 0.05s는 틱(약 0.02s) 두어 개 분량 — 그 이하는 눈에 안 띈다.
                    if (gap > 0.05f)
                        EditorGUILayout.HelpBox(
                            $"애니가 Duration보다 {gap:0.###}s 깁니다 → {rec.Duration:0.###}s 지점에서 잘립니다.\n" +
                            "'종료' 마커로 이동해 그 시점 포즈를 확인하세요. 마무리·복귀 동작만 잘리는 거라면 " +
                            "의도된 것일 수 있으니 그대로 두면 됩니다.\n" +
                            $"핵심 동작이 잘린다면 배속을 {_clip.length / rec.Duration:0.###}로 올리거나 시트의 Duration을 늘리세요.",
                            MessageType.Info);
                    else if (gap < -0.05f)
                        EditorGUILayout.HelpBox(
                            $"애니가 Duration보다 {-gap:0.###}s 짧습니다 → 동작이 끝난 뒤 그만큼 굳어 있습니다.",
                            MessageType.Warning);
                }

                if (rec.HasLunge)
                    EditorGUILayout.LabelField(
                        "※ 전진 지속은 배속과 무관합니다 — 배속을 바꾸면 전진 지속도 같이 맞춰야 합니다.",
                        EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    "Base.controller가 무기 4종 공용이라, 배속은 State가 아니라 파라미터로 걸립니다.",
                    EditorStyles.miniLabel);
            }

            so.ApplyModifiedProperties();
        }

        /// 다단히트 시점 편집. 애니메이터가 클립에 찍어둔 Hit 이벤트를 그대로 구워올 수 있다.
        /// 이벤트는 렌더 타이밍이라 런타임 판정에 못 쓰지만, 오소링 입력으로는 가장 자연스럽다.
        private void DrawMultiHitSection()
        {
            var rec = Attack;
            if (!SectionHeader("다단히트", ref _showMultiHit)) return;

            var so = new SerializedObject(rec);
            var times = so.FindProperty("_hitTimes");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(times, new GUIContent("타격 시점 (s)"), true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_clip == null))
                    {
                        if (GUILayout.Button("클립 Hit 이벤트에서 굽기"))
                            BakeHitTimesFromEvents(times);
                    }
                    if (GUILayout.Button("1타로", GUILayout.Width(60f)))
                        times.arraySize = 0;
                }

                // 총 데미지가 어떻게 쪼개지는지 그대로 보여준다 — 합이 시트값과 같아야 한다.
                float total = _weaponBaseDamage * rec.Damage;
                int count = rec.HitCount;
                var parts = new int[count];
                int sum = 0;
                for (int i = 0; i < count; i++)
                {
                    parts[i] = AttackRecord.Split(total, i, count);
                    sum += parts[i];
                }

                EditorGUILayout.LabelField(
                    $"총 데미지 {total:0.##} → {count}타 [{string.Join(", ", parts)}]  합계 {sum}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    "누적 차분으로 나눠서 타수를 늘려도 총합이 줄지 않습니다(너프 아님).",
                    EditorStyles.miniLabel);

                if (count > 1 && rec.HitTime(count - 1) > rec.Duration + 0.05f)
                    EditorGUILayout.HelpBox(
                        $"마지막 타격({rec.HitTime(count - 1):0.###}s)이 공격 종료({rec.Duration:0.###}s)보다 뒤입니다. " +
                        "그 타격은 상태가 끝난 뒤에 들어갑니다.", MessageType.Warning);
            }

            so.ApplyModifiedProperties();
        }

        private void BakeHitTimesFromEvents(SerializedProperty times)
        {
            var events = AnimationUtility.GetAnimationEvents(_clip);
            var hits = new List<float>();

            foreach (var e in events)
                if (e.functionName == "Hit")
                    hits.Add(e.time);

            if (hits.Count == 0)
            {
                ShowNotification(new GUIContent($"'{_clip.name}'에 Hit 이벤트가 없습니다."));
                return;
            }

            hits.Sort();
            times.arraySize = hits.Count;
            for (int i = 0; i < hits.Count; i++)
                times.GetArrayElementAtIndex(i).floatValue = hits[i];

            ShowNotification(new GUIContent($"Hit 이벤트 {hits.Count}개를 가져왔습니다."));
        }

        private void DrawLungeSection()
        {
            var rec = Attack;
            if (!SectionHeader("전진기", ref _showLunge)) return;

            var so = new SerializedObject(rec);
            var dist = so.FindProperty("_lungeDistance");
            var delay = so.FindProperty("_lungeStartDelay");
            var dur = so.FindProperty("_lungeDuration");
            var curve = so.FindProperty("_lungeCurve");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("전진기", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(dist, new GUIContent("거리 (m)"));
                EditorGUILayout.PropertyField(delay, new GUIContent("시작 지연 (s)"));
                EditorGUILayout.PropertyField(dur, new GUIContent("지속 (s)"));
                EditorGUILayout.PropertyField(curve, new GUIContent("진행 곡선"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_clip == null))
                    {
                        if (GUILayout.Button("클립 루트모션에서 굽기"))
                        {
                            // 런타임은 Duration에서 상태를 끝낸다. 클립이 그보다 길면 뒤쪽은 재생되지
                            // 않으므로, 재생되는 구간까지만 굽는다 — 안 그러면 공격이 끝난 뒤에도 미끄러진다.
                            float speed = AnimSpeedOf(rec);
                            float played = _clip.length / speed;
                            // Duration이 아직 안 채워진(0) 레코드는 자를 근거가 없으니 클립 전체를 쓴다.
                            float useTime = rec.Duration > 0f ? Mathf.Min(played, rec.Duration) : played;

                            var (baked, shape) = BakeRootMotion(useTime * speed);
                            dist.floatValue = baked;
                            delay.floatValue = 0f;
                            dur.floatValue = useTime;
                            curve.animationCurveValue = shape;

                            string cut = useTime < played - 0.0001f
                                ? $" · Duration({rec.Duration:0.###}s)까지만 사용"
                                : string.Empty;
                            ShowNotification(new GUIContent($"전방 {baked:0.###}m · 곡선 {shape.length}키{cut}"));
                        }
                    }
                    if (GUILayout.Button("전진 없음", GUILayout.Width(80f)))
                    {
                        dist.floatValue = 0f;
                        delay.floatValue = 0f;
                        dur.floatValue = 0f;
                    }
                }

                // 클립에 루트모션이 없으면 곡선을 손으로 만들어야 한다. 자주 쓰는 모양을 버튼으로 둔다.
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("곡선 프리셋", GUILayout.Width(70f));

                    if (GUILayout.Button("등속"))
                        curve.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);

                    if (GUILayout.Button("감속"))
                        curve.animationCurveValue = new AnimationCurve(
                            new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

                    if (GUILayout.Button("가속"))
                        curve.animationCurveValue = new AnimationCurve(
                            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.5f, 0f));

                    if (GUILayout.Button("S자"))
                        curve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

                    if (GUILayout.Button("한 발"))
                        curve.animationCurveValue = new AnimationCurve(
                            new Keyframe(0f, 0f, 0f, 0f),
                            new Keyframe(0.25f, 0.05f, 0.4f, 0.4f),
                            new Keyframe(0.65f, 0.9f, 1.6f, 1.6f),
                            new Keyframe(1f, 1f, 0f, 0f));
                }

                EditorGUILayout.LabelField(
                    "곡선: X = 시간 진행률, Y = 이동 진행률. (0,0)에서 시작해 (1,1)로 끝나야 목표 거리에 정확히 닿습니다.",
                    EditorStyles.miniLabel);

                // 곡선이 직선이면 굽기가 실패한 것 — 발걸음이 아니라 등속으로 밀려 미끄러져 보인다.
                var cur = curve.animationCurveValue;
                bool isFlat = cur == null || cur.length <= 2;
                if (dist.floatValue != 0f && isFlat)
                    EditorGUILayout.HelpBox(
                        "진행 곡선이 직선입니다 → 발걸음과 무관하게 등속으로 밀립니다(Z값을 미는 느낌).\n" +
                        "'클립 루트모션에서 굽기'를 눌러 곡선을 채우세요. 눌러도 직선이면 그 클립엔 " +
                        "루트모션이 없다는 뜻이라, 전진을 빼거나 다른 클립을 써야 합니다.",
                        MessageType.Warning);
                else if (!isFlat)
                    EditorGUILayout.LabelField(
                        $"곡선 {cur.length}키 — 클립 루트모션에서 구워짐(발걸음 궤적 반영)",
                        EditorStyles.miniLabel);

                float lungeEnd = delay.floatValue + dur.floatValue;

                if (dist.floatValue == 0f && dur.floatValue > 0f)
                    EditorGUILayout.HelpBox(
                        "거리가 0이라 전진하지 않습니다. 굽기 결과가 0이면 이 클립엔 루트모션이 없다는 뜻입니다" +
                        "(제자리 클립이거나 임포트 설정이 Bake Into Pose). 원하는 거리를 직접 넣으세요.",
                        MessageType.Info);
                else if (dist.floatValue != 0f && lungeEnd > rec.Duration + 0.05f)
                    EditorGUILayout.HelpBox(
                        $"전진이 {lungeEnd:0.###}s에 끝나는데 공격 상태는 {rec.Duration:0.###}s에 끝납니다.\n" +
                        "전진은 공격 상태와 무관하게 계속되므로, 공격이 끝난 뒤에도 미끄러집니다. " +
                        "'시작 지연 + 지속'을 Duration 안으로 넣으세요.",
                        MessageType.Warning);

                EditorGUILayout.LabelField(
                    "런타임은 루트모션을 쓰지 않습니다(applyRootMotion = 0). 이 거리만큼만 서버가 틱 단위로 KCC에 먹입니다.",
                    EditorStyles.miniLabel);
            }

            so.ApplyModifiedProperties();
        }

        // =====================================================================
        // 대시 · 스테미너
        // =====================================================================

        /// <summary>대시는 시트의 "대시 행"(AttackRecord) 하나로 오소링한다 —
        /// 애니메이션(AnimName)·이펙트·전진·지속이 전부 평타와 같은 자리에 들어가므로,
        /// 위 목록에서 그 행을 고르면 기존 이펙트/전진/프리뷰 섹션을 그대로 쓸 수 있다.
        /// 여기서는 그 행으로 가는 길과, 행에 없는 값(스테미너)만 다룬다.</summary>
        // =====================================================================
        // 사운드 — 클립에 박는 애니메이션 이벤트
        // =====================================================================

        /// <summary>대상 클립에 사운드 이벤트를 박고, 프리뷰를 보면서 시점을 맞춘다.
        /// 런타임에는 아바타 루트의 PlayerAnimationSound.PlaySfx가 이 이벤트를 받아
        /// ISoundManager로 넘긴다 — 여기서 맞춘 시점이 그대로 게임 안 타이밍이다.
        ///
        /// 대상은 '포즈 보정'과 같은 클립이다: 고른 공격의 클립이거나, 직접 지정한 클립
        /// (이동·대시처럼 공격이 아닌 클립은 이쪽으로 넣는다).</summary>
        private void DrawSoundSection()
        {
            if (!SectionHeader("사운드 (애니메이션 이벤트)", ref _showSound)) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPoseTargetPicker();

                var target = PoseTarget;
                if (target == null)
                {
                    EditorGUILayout.HelpBox(
                        "대상 클립이 없습니다. 위 목록에서 공격을 고르거나, '직접 지정'으로 " +
                        "이동·대시 클립을 넣으세요.", MessageType.Info);
                    return;
                }

                EnsureSoundEvents(target);

                bool isModel = AnimationSoundEvents.IsModelClip(target);
                EditorGUILayout.LabelField(
                    $"대상: {target.name}   ({(isModel ? "FBX 내부 클립" : ".anim 에셋")}, 길이 {target.length:0.###}s)",
                    EditorStyles.miniLabel);

                if (_avatar != null && !HasSoundReceiver)
                {
                    EditorGUILayout.HelpBox(
                        "아바타에 PlayerAnimationSound가 없습니다 — 이벤트를 박아도 게임에서 " +
                        "아무 함수도 불리지 않습니다(콘솔 경고만 납니다).", MessageType.Warning);

                    if (GUILayout.Button("아바타에 PlayerAnimationSound 붙이기"))
                        AttachSoundReceiver();
                }

                EditorGUILayout.Space(2f);
                DrawClipPlayback(target);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _soundPreview = GUILayout.Toggle(_soundPreview, "재생 중 소리 듣기",
                        EditorStyles.miniButton, GUILayout.Width(110f));

                    if (GUILayout.Button("소리 멈추기", EditorStyles.miniButton, GUILayout.Width(80f)))
                        EditorAudioPreview.Stop();

                    GUILayout.FlexibleSpace();

                    // 소리는 대개 타격에서 시작한다 — 시트가 이미 아는 시점이라 손으로 찍을 이유가 없다.
                    using (new EditorGUI.DisabledScope(Attack == null || _poseOnBlendTreeClip))
                    {
                        if (GUILayout.Button(new GUIContent("타격 시점에 추가",
                                "시트의 타격 시점마다(다단이면 타마다) 빈 이벤트를 놓습니다."),
                                GUILayout.Width(110f)))
                            AddSoundEventsAtHits(target);
                    }

                    if (GUILayout.Button("현재 t에 추가", GUILayout.Width(100f)))
                        AddSoundEvent(target);
                }

                if (!EditorAudioPreview.Available)
                    EditorGUILayout.HelpBox(
                        "이 유니티 버전에서 에디터 미리듣기 API를 찾지 못했습니다. " +
                        "시점 편집은 되지만 소리는 나지 않습니다.", MessageType.Warning);

                if (_soundEvents.Count == 0)
                    EditorGUILayout.LabelField("이 클립에 사운드 이벤트가 없습니다.", EditorStyles.miniLabel);

                float speed = SampleSpeed;
                int removeAt = -1;

                for (int i = 0; i < _soundEvents.Count; i++)
                {
                    var e = _soundEvents[i];

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUI.BeginChangeCheck();
                            var audio = (AudioClip)EditorGUILayout.ObjectField(
                                $"♪{i}", e.objectReferenceParameter as AudioClip, typeof(AudioClip), false);
                            if (EditorGUI.EndChangeCheck())
                            {
                                e.objectReferenceParameter = audio;
                                _soundDirty = true;
                                EditorAudioPreview.Play(audio);
                            }

                            if (GUILayout.Button("듣기", EditorStyles.miniButton, GUILayout.Width(40f)))
                                EditorAudioPreview.Play(e.objectReferenceParameter as AudioClip);

                            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                                removeAt = i;
                        }

                        // 클립 시간으로 편집하고, 옆에 재생(타임라인) 시간을 같이 보여준다 —
                        // 배속이 걸린 공격은 두 값이 다르고, 타임라인 슬라이더는 재생 시간이다.
                        EditorGUI.BeginChangeCheck();
                        float t = EditorGUILayout.Slider("시점 (클립 s)", e.time, 0f, target.length);
                        if (EditorGUI.EndChangeCheck()) SetSoundTime(e, t, target);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"재생 t = {e.time / speed:0.000}s",
                                EditorStyles.miniLabel, GUILayout.Width(110f));

                            if (GUILayout.Button("−0.01", EditorStyles.miniButton, GUILayout.Width(50f)))
                                SetSoundTime(e, e.time - 0.01f, target);
                            if (GUILayout.Button("+0.01", EditorStyles.miniButton, GUILayout.Width(50f)))
                                SetSoundTime(e, e.time + 0.01f, target);

                            if (GUILayout.Button(new GUIContent("타임라인 t에서",
                                    "지금 보고 있는 시점을 이 이벤트의 시점으로 가져옵니다."),
                                    EditorStyles.miniButton, GUILayout.Width(90f)))
                                SetSoundTime(e, _time * speed, target);

                            if (GUILayout.Button(new GUIContent("이 시점으로 이동 ▶",
                                    "타임라인을 이 이벤트 시점으로 옮기고 소리를 들려줍니다."),
                                    EditorStyles.miniButton))
                                JumpToSound(e, speed);
                        }
                    }
                }

                if (removeAt >= 0)
                {
                    _soundEvents.RemoveAt(removeAt);
                    _soundDirty = true;
                }

                EditorGUILayout.Space(2f);

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(!_soundDirty))
                {
                    if (GUILayout.Button(isModel ? "클립에 저장 (FBX 재임포트)" : "클립에 저장"))
                        SaveSoundEvents(target);

                    if (GUILayout.Button("되돌리기", GUILayout.Width(80f)))
                        ReloadSoundEvents(target);
                }

                if (_soundDirty)
                    EditorGUILayout.HelpBox("저장하지 않은 변경이 있습니다. 저장해야 게임에 반영됩니다.",
                        MessageType.Warning);

                if (!string.IsNullOrEmpty(_soundLog))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(_soundLog, MessageType.None);
                        if (GUILayout.Button("×", GUILayout.Width(22f), GUILayout.Height(20f)))
                            _soundLog = null;
                    }
                }

                EditorGUILayout.LabelField(
                    $"저장 위치: {AnimationSoundEvents.OwnerPath(target)}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    "이 클립을 함께 쓰는 다른 무기·상태에서도 같은 소리가 납니다. 무기별로 다르게 하려면 " +
                    "그 무기의 오버라이드 클립에 박으세요.",
                    EditorStyles.miniLabel);
            }
        }

        /// <summary>클립 하나를 사운드·포즈 편집 대상으로 잡는다(공격 목록에 없는 이동 클립용).</summary>
        private void SelectSoundTarget(AnimationClip clip)
        {
            if (clip == null) return;

            _poseOnBlendTreeClip = true;
            _poseTargetOverride = clip;
            ResetPoseState();

            _showSound = true;
            _playing = false;
            _time = 0f;
            ApplyTime();
        }

        /// 아바타에 이벤트를 받을 컴포넌트가 있는가. 애니메이션 이벤트는 Animator가 있는
        /// 오브젝트의 컴포넌트에서만 함수를 찾으므로 아바타 루트를 본다.
        private bool HasSoundReceiver =>
            _avatar != null && _avatar.GetComponent<PlayerAnimationSound>() != null;

        /// 씬 인스턴스에만 붙이면 프리팹에는 안 남는다 — 프리팹 인스턴스면 추가분을 프리팹에 적용한다.
        private void AttachSoundReceiver()
        {
            if (_avatar == null) return;

            var added = Undo.AddComponent<PlayerAnimationSound>(_avatar);
            if (added == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(_avatar))
            {
                PrefabUtility.ApplyAddedComponent(added,
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_avatar),
                    InteractionMode.UserAction);
                _soundLog = "PlayerAnimationSound를 아바타와 프리팹에 추가했습니다.";
            }
            else
            {
                _soundLog = "PlayerAnimationSound를 씬 아바타에 추가했습니다. " +
                            "프리팹 인스턴스가 아니라 프리팹에는 반영되지 않았습니다.";
            }
        }

        /// 대상 클립이 바뀌면 이벤트 사본을 다시 읽는다.
        private void EnsureSoundEvents(AnimationClip clip)
        {
            if (_soundClip == clip) return;

            if (_soundDirty && _soundClip != null)
                _soundLog = $"'{_soundClip.name}'의 저장하지 않은 사운드 변경을 버렸습니다.";

            ReloadSoundEvents(clip);
        }

        private void ReloadSoundEvents(AnimationClip clip)
        {
            _soundEvents.Clear();
            _soundEvents.AddRange(AnimationSoundEvents.Sounds(clip));
            _soundClip = clip;
            _soundDirty = false;
        }

        private void AddSoundEvent(AnimationClip clip)
        {
            float clipTime = Mathf.Clamp(_time * SampleSpeed, 0f, clip.length);
            _soundEvents.Add(AnimationSoundEvents.New(clipTime, null));
            _soundEvents.Sort(CompareEventTime);
            _soundDirty = true;
        }

        /// <summary>시트의 타격 시점마다 빈 이벤트를 하나씩 놓는다.
        /// 타격 시점은 재생 시간이고 이벤트는 클립 시간이라 배속을 곱해 환산한다.</summary>
        private void AddSoundEventsAtHits(AnimationClip clip)
        {
            var rec = Attack;
            if (rec == null) return;

            float speed = SampleSpeed;
            for (int i = 0; i < rec.HitCount; i++)
                _soundEvents.Add(AnimationSoundEvents.New(
                    Mathf.Clamp(rec.HitTime(i) * speed, 0f, clip.length), null));

            _soundEvents.Sort(CompareEventTime);
            _soundDirty = true;
        }

        /// 시점을 옮기면 타임라인도 그리로 따라간다 — 포즈를 보면서 맞추라는 뜻이다.
        private void SetSoundTime(AnimationEvent e, float clipTime, AnimationClip clip)
        {
            e.time = Mathf.Clamp(clipTime, 0f, clip.length);
            _soundDirty = true;
            _soundEvents.Sort(CompareEventTime);
            JumpToSound(e, SampleSpeed);
        }

        private static int CompareEventTime(AnimationEvent a, AnimationEvent b)
            => a.time.CompareTo(b.time);

        private void JumpToSound(AnimationEvent e, float speed)
        {
            _playing = false;
            _time = Mathf.Clamp(e.time / speed, 0f, TotalLength);
            ApplyTime();

            if (_soundPreview)
                EditorAudioPreview.Play(e.objectReferenceParameter as AudioClip);
        }

        private void SaveSoundEvents(AnimationClip clip)
        {
            // FBX는 저장이 곧 재임포트라 clip 참조가 죽는다 — 경로·이름으로 다시 찾아온다.
            string path = AssetDatabase.GetAssetPath(clip);
            string clipName = clip.name;

            if (!AnimationSoundEvents.Write(clip, _soundEvents, out _soundLog))
                return;

            var saved = FindClipAt(path, clipName);
            if (saved != null && saved != clip)
            {
                if (_poseOnBlendTreeClip) _poseTargetOverride = saved;
                else RefreshClip();
            }

            ReloadSoundEvents(PoseTarget);
        }

        private static AnimationClip FindClipAt(string path, string clipName)
        {
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && c.name == clipName) return c;

            return null;
        }

        /// <summary>재생이 prev → cur로 지나간 구간에 걸린 사운드 이벤트를 울린다.
        /// 프레임이 이벤트를 건너뛰어도 빠지지 않고, 한 번 지나갈 때 한 번만 난다.</summary>
        private void TickSoundPreview(float prev, float cur, bool wrapped)
        {
            if (!_soundPreview) return;

            var clip = PoseTarget;
            if (clip == null) return;

            EnsureSoundEvents(clip);
            if (_soundEvents.Count == 0) return;

            float speed = SampleSpeed;

            // 루프로 되감겼으면 (prev, 끝]과 [0, cur] 두 구간이 한꺼번에 지나간 것이다.
            if (wrapped) EditorAudioPreview.Stop();

            foreach (var e in _soundEvents)
            {
                float t = e.time / speed;
                bool passed = wrapped ? t > prev || t <= cur : t > prev && t <= cur;
                if (passed) EditorAudioPreview.Play(e.objectReferenceParameter as AudioClip);
            }
        }

        private void DrawDashSection()
        {
            if (!SectionHeader("대시 · 스테미너", ref _showDash)) return;
            if (Weapon == null) return;

            var so = new SerializedObject(Weapon);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 이름을 고치면 가리키는 행이 바뀐다 — 목록을 다시 만들어야 [대시] 표시가 따라온다.
                if (DrawDashRow(so, "dashAttackName", "대시 행", _dashIdx,
                        "시트의 이 이름을 가진 공격 행이 대시의 애니·이펙트·전진·지속을 갖는다.")) return;

                EditorGUILayout.Space(4f);

                if (DrawDashRow(so, "backDashAttackName", "백대시 행", _backDashIdx,
                        "이동 입력 없이 스페이스를 누르면 이 행이 쓰인다. 없으면 대시 행으로 대체된다.")) return;

                EditorGUILayout.Space(4f);
                DrawStaminaFields();
            }

            so.ApplyModifiedProperties();
        }

        /// <summary>대시/백대시 한 줄: 행 이름 + 지금 매칭된 행 요약 + 편집 버튼 + 경고.
        /// 애니메이션·이펙트·전진 편집은 [이 행 편집]로 그 행을 고른 뒤 기존 섹션에서 한다.</summary>
        /// <returns>이름이 바뀌어 목록을 다시 만들었으면 true — 호출부는 이번 프레임 그리기를 멈춰야 한다.</returns>
        private bool DrawDashRow(SerializedObject so, string propertyName, string label, int idx, string tooltip)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty(propertyName), new GUIContent($"{label} 이름", tooltip));
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                RebuildAttacks();
                return true;
            }

            if (idx < 0)
            {
                EditorGUILayout.HelpBox(
                    $"이 무기의 시트에 {label}이 없습니다. 시트에 추가하고 Sync하면 위 목록에 뜹니다.",
                    MessageType.Warning);
                return false;
            }

            var rec = _attacks[idx];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{rec.Name}   State: {(string.IsNullOrEmpty(rec.AnimName) ? "(없음)" : rec.AnimName)}   " +
                    $"지속 {rec.Duration:0.###}s   전진 {rec.LungeDistance:0.##}m   이펙트 {rec.ParticleCount}개",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("이 행 편집", GUILayout.Width(70f))) SelectAttack(idx);
            }

            if (string.IsNullOrEmpty(rec.AnimName))
                EditorGUILayout.HelpBox("AnimName이 비어 있어 직업 공용 클립이 재생됩니다.", MessageType.Info);
            else if (_attackIdx == idx && _clip == null)
                EditorGUILayout.HelpBox(
                    $"컨트롤러에서 State \"{rec.AnimName}\"을 찾지 못했습니다.\n" +
                    "Base.controller에 그 이름의 State를 만들고, 무기 Override Controller에 클립을 배정하세요.",
                    MessageType.Warning);

            // 백대시는 몸을 돌리지 않고 바라보는 방향 기준으로 밀린다 — 거리가 음수여야 뒤로 간다.
            if (idx == _backDashIdx && rec.LungeDistance >= 0f)
                EditorGUILayout.HelpBox(
                    "전진 거리가 음수가 아닙니다 → 뒤로 안 가고 앞으로 갑니다. [전진기] 섹션에서 음수(예: -4)로 넣으세요.",
                    MessageType.Warning);

            if (rec.Damage != 0f)
                EditorGUILayout.HelpBox(
                    "데미지 값이 있지만 런타임은 대시의 타격을 건너뜁니다(판정 없는 회피). 시트 값은 0을 권장합니다.",
                    MessageType.Info);

            return false;
        }

        /// <summary>스테미너 수치는 시트가 아니라 무기 프리팹의 WeaponStamina 컴포넌트에 모여 있다.
        /// 무기는 "대시라는 행동"만 갖는다 — 여기서는 그 수치를 대시 지속과 함께 환산해 보여준다.</summary>
        private void DrawStaminaFields()
        {
            EditorGUILayout.LabelField("스테미너 (무기 프리팹에 저장)", EditorStyles.boldLabel);

            var stamina = Weapon.GetComponent<WeaponStamina>();
            if (stamina == null)
            {
                EditorGUILayout.HelpBox(
                    "이 무기에 WeaponStamina 컴포넌트가 없습니다 → 대시가 동작하지 않습니다. 무기 프리팹에 추가하세요.",
                    MessageType.Error);
                return;
            }

            var staminaSo = new SerializedObject(stamina);
            var max = staminaSo.FindProperty("maxStamina");
            var regen = staminaSo.FindProperty("regenPerSecond");
            var cost = staminaSo.FindProperty("dashCost");

            EditorGUILayout.PropertyField(max, new GUIContent("최대치"));
            EditorGUILayout.PropertyField(regen, new GUIContent("초당 회복"));
            EditorGUILayout.PropertyField(cost, new GUIContent("대시 소모"));
            staminaSo.ApplyModifiedProperties();

            if (cost.intValue <= 0)
            {
                EditorGUILayout.HelpBox("소모가 0이면 스테미너 제한 없이 무한 대시가 됩니다.", MessageType.Warning);
                return;
            }
            if (max.intValue <= 0) return;

            float dashDuration = _dashIdx >= 0 ? _attacks[_dashIdx].Duration : 0f;
            EditorGUILayout.LabelField(
                $"연속 {max.intValue / cost.intValue}회 → 이후 " +
                $"{cost.intValue / Mathf.Max(0.01f, regen.floatValue):0.##}s마다 1회   " +
                $"(대시 지속 {dashDuration:0.###}s = 무적 시간 = 최소 간격)",
                EditorStyles.miniLabel);
        }

        // =====================================================================
        // 블렌드트리 배속
        // =====================================================================

        /// 무기가 바뀌면 컨트롤러도 바뀐다. 매 프레임 컨트롤러를 훑지 않도록 캐시해 둔다.
        private void EnsureBlendTrees()
        {
            if (!_blendTreesDirty) return;
            _blendTreesDirty = false;

            // 굽기 직후처럼 목록만 새로 만드는 경우가 있어, 보고 있던 트리를 되찾을 수 있게 기억해 둔다.
            string previous = _blendTreeIdx >= 0 && _blendTreeIdx < _blendTreeNames.Length
                ? _blendTreeNames[_blendTreeIdx]
                : null;

            _blendTrees = new BlendTreeEntry[0];
            _blendTreeNames = new string[0];
            _overrideMap = null;
            _blendTreeIdx = 0;

            var rac = WeaponController;
            if (rac == null) return;

            var controller = ResolveBaseController(rac, out _overrideMap);
            if (controller == null) return;

            var found = new List<BlendTreeEntry>();
            foreach (var layer in controller.layers)
                CollectBlendTrees(layer.stateMachine, layer.name, found);

            _blendTrees = found.ToArray();
            _blendTreeNames = new string[_blendTrees.Length];
            for (int i = 0; i < _blendTrees.Length; i++)
            {
                _blendTreeNames[i] = _blendTrees[i].Path;
                if (previous != null && _blendTreeNames[i] == previous) _blendTreeIdx = i;
            }
        }

        /// 무기에 꽂힌 컨트롤러(Override). 없으면 아바타 Animator의 것을 쓴다.
        private RuntimeAnimatorController WeaponController
        {
            get
            {
                if (Weapon != null)
                {
                    var so = new SerializedObject(Weapon);
                    var rac = so.FindProperty("anim")?.objectReferenceValue as RuntimeAnimatorController;
                    if (rac != null) return rac;
                }
                return _animator != null ? _animator.runtimeAnimatorController : null;
            }
        }

        /// <summary>블렌드트리 안 클립들의 재생 배속(ChildMotion.timeScale) 편집.
        /// 걷기·뛰기처럼 한 트리에 섞인 클립들의 길이가 제각각일 때 발이 미끄러지는데,
        /// 그걸 맞추는 값이 여기 있다. 공격과 무관해서 공격을 안 골라도 쓸 수 있다.</summary>
        private void DrawBlendTreeSection()
        {
            if (!SectionHeader("블렌드트리 배속", ref _showBlendTree)) return;

            EnsureBlendTrees();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("대상 트리", GUILayout.Width(60f));
                    if (_blendTrees.Length > 0)
                        _blendTreeIdx = EditorGUILayout.Popup(_blendTreeIdx, _blendTreeNames);
                    if (GUILayout.Button("새로고침", GUILayout.Width(70f)))
                        _blendTreesDirty = true;
                }

                if (_blendTrees.Length == 0)
                {
                    EditorGUILayout.HelpBox(
                        "이 무기의 컨트롤러에서 블렌드트리를 찾지 못했습니다.\n" +
                        "무기의 Override Controller가 비어 있거나, 바탕 컨트롤러에 블렌드트리 State가 없습니다.",
                        MessageType.Info);
                    return;
                }

                _blendTreeIdx = Mathf.Clamp(_blendTreeIdx, 0, _blendTrees.Length - 1);
                var entry = _blendTrees[_blendTreeIdx];
                if (entry.Tree == null || entry.State == null) return;

                DrawBlendTreeStateSpeed(entry);

                EditorGUILayout.Space(4f);
                DrawBlendTreeChildren(entry.Tree, entry.State.speed, 0);

                if (!string.IsNullOrEmpty(_clipBakeLog))
                {
                    EditorGUILayout.Space(2f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(_clipBakeLog, MessageType.None);
                        if (GUILayout.Button("×", GUILayout.Width(22f), GUILayout.Height(20f)))
                            _clipBakeLog = null;
                    }
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "여기서 고친 배속은 무기 4종이 공유하는 바탕 컨트롤러(Base.controller)에 저장됩니다 — " +
                    "Override Controller는 클립만 갈아끼울 뿐 배속은 못 덮기 때문입니다.\n\n" +
                    "이 무기만 다른 속도로 움직여야 한다면 배속을 맞춰본 뒤 [클립에 굽기]를 누르세요. " +
                    "키프레임 시간을 줄인 새 클립이 원본 옆에 만들어지고, 이 무기의 오버라이드 슬롯만 " +
                    "그 클립을 보게 바뀝니다. 원본은 그대로 남습니다.",
                    MessageType.Info);
            }
        }

        /// State 자체의 배속. 트리 전체에 곱해지므로 자식 배속보다 먼저 본다.
        private void DrawBlendTreeStateSpeed(BlendTreeEntry entry)
        {
            var state = entry.State;

            EditorGUI.BeginChangeCheck();
            float stateSpeed = EditorGUILayout.FloatField(
                new GUIContent("State 배속", "트리 전체에 곱해집니다. 아래 자식 배속과 곱해져 최종 배속이 됩니다."),
                state.speed);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(state, "State 배속 편집");
                state.speed = stateSpeed;
                EditorUtility.SetDirty(state);
            }

            if (state.speedParameterActive)
                EditorGUILayout.LabelField(
                    $"※ 배속 파라미터 '{state.speedParameter}'가 켜져 있습니다 — 런타임에 이 값이 한 번 더 곱해집니다.",
                    EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                $"블렌드 타입: {entry.Tree.blendType}   파라미터: {entry.Tree.blendParameter}",
                EditorStyles.miniLabel);
        }

        /// <summary>트리의 자식 모션을 한 줄씩 그린다. 자식이 또 트리면 들여쓰기해서 재귀한다.</summary>
        /// <param name="inheritedSpeed">위에서 곱해져 내려온 배속(State 배속 × 상위 트리들의 자식 배속).
        /// 중첩 트리에서 실제 재생 길이를 맞게 보여주려면 계속 물고 내려가야 한다.</param>
        private void DrawBlendTreeChildren(BlendTree tree, float inheritedSpeed, int depth)
        {
            // children은 복사본을 돌려준다 — 원소만 고치면 반영되지 않으므로 배열째 다시 넣어야 한다.
            var children = tree.children;
            if (children == null || children.Length == 0)
            {
                EditorGUILayout.LabelField($"{Indent(depth)}(자식 모션이 없습니다)", EditorStyles.miniLabel);
                return;
            }

            bool changed = false;
            var subTrees = new List<(BlendTree tree, float speed)>();

            for (int i = 0; i < children.Length; i++)
            {
                var motion = children[i].motion;
                var subTree = motion as BlendTree;
                var baseClip = motion as AnimationClip;

                // 실제로 재생되는 건 이 무기의 오버라이드 클립이다 — 바탕 클립 이름을 보여주면 헷갈린다.
                var clip = baseClip;
                if (clip != null && _overrideMap != null && _overrideMap.TryGetValue(clip, out var o))
                    clip = o;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string label = motion == null
                        ? "(비어 있음)"
                        : subTree != null ? $"[하위 트리] {subTree.name}" : clip != null ? clip.name : motion.name;

                    EditorGUILayout.LabelField($"{Indent(depth)}{label}", EditorStyles.miniBoldLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        float ts = EditorGUILayout.FloatField(
                            new GUIContent("배속", "이 클립만의 재생 속도. 1 = 원본. 발이 미끄러지면 여기를 조정합니다."),
                            children[i].timeScale);
                        if (EditorGUI.EndChangeCheck())
                        {
                            children[i].timeScale = ts;
                            changed = true;
                        }

                        if (GUILayout.Button("1로", GUILayout.Width(40f)) &&
                            !Mathf.Approximately(children[i].timeScale, 1f))
                        {
                            children[i].timeScale = 1f;
                            changed = true;
                        }

                        // 여기 배속은 무기 4종이 공유한다. 이 무기만 다르게 하려면 클립에 구워야 한다.
                        using (new EditorGUI.DisabledScope(
                            clip == null || Mathf.Approximately(children[i].timeScale, 1f)))
                        {
                            if (GUILayout.Button(
                                new GUIContent("클립에 굽기",
                                    "이 배속을 키프레임 시간에 구운 새 클립을 만들고, 이 무기만 그 클립을 쓰게 합니다."),
                                GUILayout.Width(80f)))
                            {
                                if (BakeChildClipSpeed(clip, baseClip, children[i].timeScale))
                                {
                                    children[i].timeScale = 1f;   // 구웠으니 공유 배속은 되돌린다
                                    changed = true;
                                }
                            }
                        }

                        // 발소리는 이동 클립에 박는다 — 공격 목록에 없는 클립이라 여기서 바로 넘겨준다.
                        using (new EditorGUI.DisabledScope(clip == null))
                        {
                            if (GUILayout.Button(
                                new GUIContent("사운드", "이 클립을 사운드 섹션의 대상으로 잡습니다(발소리 등)."),
                                GUILayout.Width(50f)))
                                SelectSoundTarget(clip);
                        }
                    }

                    // 임계값(1D)이나 위치(2D)는 배속을 판단할 근거라 같이 보여준다(편집은 Animator 창에서).
                    string blendInfo = tree.blendType == BlendTreeType.Simple1D
                        ? $"임계값 {children[i].threshold:0.###}"
                        : $"위치 ({children[i].position.x:0.##}, {children[i].position.y:0.##})";

                    if (clip != null)
                    {
                        float total = children[i].timeScale * inheritedSpeed;
                        float played = Mathf.Approximately(total, 0f) ? 0f : clip.length / Mathf.Abs(total);
                        EditorGUILayout.LabelField(
                            $"{blendInfo}   원본 {clip.length:0.###}s → 재생 {played:0.###}s " +
                            $"(자식 {children[i].timeScale:0.##} × 상위 {inheritedSpeed:0.##})",
                            EditorStyles.miniLabel);

                        if (baseClip != clip)
                            EditorGUILayout.LabelField(
                                $"바탕 클립: {baseClip.name} → 이 무기에서는 {clip.name}",
                                EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(blendInfo, EditorStyles.miniLabel);
                    }
                }

                if (subTree != null) subTrees.Add((subTree, inheritedSpeed * children[i].timeScale));
            }

            if (changed)
            {
                Undo.RecordObject(tree, "블렌드트리 배속 편집");
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }

            // 하위 트리는 배열을 되돌려 넣은 뒤에 그린다 — 그리는 도중 children을 갈아끼우면 인덱스가 꼬인다.
            foreach (var sub in subTrees)
                DrawBlendTreeChildren(sub.tree, sub.speed, depth + 1);
        }

        // =====================================================================
        // 포즈 보정 (휴머노이드 머슬 오프셋)
        // =====================================================================

        /// 보정 대상 클립. 기본은 지금 고른 공격의 클립이고, 블렌드트리 쪽을 고칠 수도 있다.
        private AnimationClip PoseTarget => _poseOnBlendTreeClip ? _poseTargetOverride : _clip;

        /// <summary>무릎 굽힘 같은 리타게팅 오차를 클립의 머슬 값에 상수 오프셋으로 얹어 고친다.
        /// 씬에서 결과를 바로 보면서 맞추고, 확정되면 새 클립으로 굽는다.</summary>
        private void DrawPoseSection()
        {
            if (!SectionHeader("포즈 보정 (무릎·다리)", ref _showPose)) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPoseTargetPicker();

                var target = PoseTarget;
                if (target == null)
                {
                    EditorGUILayout.HelpBox(
                        "보정할 클립이 없습니다. 위에서 공격을 고르거나, 블렌드트리 클립을 대상으로 바꾸세요.",
                        MessageType.Info);
                    return;
                }

                _clipMuscles ??= PoseOffsetBaker.MusclesIn(target);

                if (_clipMuscles.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        $"'{target.name}'에서 휴머노이드 머슬 커브를 찾지 못했습니다.\n" +
                        "제네릭 리그이거나 압축된 클립이라 포즈 보정을 할 수 없습니다.",
                        MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField($"대상: {target.name}   (머슬 {_clipMuscles.Count}개)",
                    EditorStyles.miniLabel);

                EditorGUILayout.Space(2f);
                DrawClipPlayback(target);
                EditorGUILayout.Space(2f);

                bool changed = false;
                foreach (var knob in PoseOffsetBaker.LegKnobs)
                {
                    // 클립이 안 쓰는 관절은 슬라이더를 내려도 아무 일이 안 일어난다 — 아예 비활성으로 보여준다.
                    bool present = false;
                    foreach (var m in knob.Muscles)
                        if (_clipMuscles.Contains(m)) { present = true; break; }

                    using (new EditorGUI.DisabledScope(!present))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        float current = _poseOffsets.TryGetValue(knob.Muscles[0], out var v) ? v : 0f;

                        EditorGUI.BeginChangeCheck();
                        float next = EditorGUILayout.Slider(
                            new GUIContent(present ? knob.Label : $"{knob.Label} (없음)", knob.Hint),
                            current, -0.5f, 0.5f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            foreach (var m in knob.Muscles) _poseOffsets[m] = next;
                            changed = true;
                        }

                        if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(24f)) &&
                            !Mathf.Approximately(current, 0f))
                        {
                            foreach (var m in knob.Muscles) _poseOffsets[m] = 0f;
                            changed = true;
                        }
                    }
                }

                if (changed) RebuildPosePreview();

                EditorGUILayout.Space(4f);
                DrawBoneDetail(target);
                DrawPositionDetail(target);
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("전부 0으로"))
                    {
                        _poseOffsets.Clear();
                        RebuildPosePreview();
                    }

                    using (new EditorGUI.DisabledScope(!HasPoseOffset))
                    {
                        if (GUILayout.Button("새 클립으로 굽기"))
                            BakePoseOffsets(target);
                    }
                }

                if (!string.IsNullOrEmpty(_poseLog))
                    EditorGUILayout.HelpBox(_poseLog, MessageType.None);

                EditorGUILayout.LabelField(
                    "슬라이더를 움직이면 씬의 아바타 포즈가 바로 바뀝니다(원본 클립은 그대로).",
                    EditorStyles.miniLabel);

                EditorGUILayout.HelpBox(
                    "여러 클립에 같은 보정이 필요하다면 클립이 아니라 아바타(리타게팅) 문제입니다.\n" +
                    "이 프로젝트는 애니메이션이 서로 다른 원본 리그에서 P09_Body 아바타로 리타게팅됩니다 — " +
                    "원본 모델의 Rig > Configure에서 T포즈를 바로잡거나 Leg Stretch를 올리는 쪽이 " +
                    "클립을 40개 구워내는 것보다 낫습니다.",
                    MessageType.Info);
            }
        }

        private bool HasPoseOffset
        {
            get
            {
                foreach (var kv in _poseOffsets)
                    if (!Mathf.Approximately(kv.Value, 0f)) return true;
                return false;
            }
        }

        private void DrawPoseTargetPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("대상 클립", GUILayout.Width(60f));

                EditorGUI.BeginChangeCheck();
                int mode = GUILayout.Toolbar(_poseOnBlendTreeClip ? 1 : 0,
                    new[] { "고른 공격", "직접 지정" });
                if (EditorGUI.EndChangeCheck())
                {
                    _poseOnBlendTreeClip = mode == 1;
                    ResetPoseState();
                }
            }

            if (!_poseOnBlendTreeClip) return;

            EditorGUI.BeginChangeCheck();
            _poseTargetOverride = (AnimationClip)EditorGUILayout.ObjectField(
                "클립", _poseTargetOverride, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) ResetPoseState();
        }

        /// 대상이 바뀌면 머슬 목록과 프리뷰를 다시 만들어야 한다.
        private void ResetPoseState()
        {
            _clipMuscles = null;
            _clipBones = null;
            _clipPositionCurves = null;
            _boneNames = new string[0];
            _boneIdx = 0;
            _poseOffsets.Clear();
            DestroyPosePreview();
            _poseLog = null;
        }

        /// <summary>포즈 대상 클립을 씬뷰에서 재생·스크럽한다.
        /// 아래 '타임라인' 섹션은 공격을 골라야 열리므로, 이동 클립을 직접 지정해 본을 만지는 동안에는
        /// 재생할 방법이 없었다. 재생 상태(_time·_playing·_loop·_speed)는 타임라인과 공유하므로
        /// 어느 쪽에서 움직여도 같은 시점을 본다.</summary>
        private void DrawClipPlayback(AnimationClip target)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "■ 정지" : "▶ 재생", GUILayout.Width(70f)))
                {
                    _playing = !_playing;
                    ApplyTime();
                }

                if (GUILayout.Button("↺ 처음", GUILayout.Width(60f)))
                {
                    _playing = false;
                    _time = 0f;
                    ApplyTime();
                }

                _loop = GUILayout.Toggle(_loop, "루프", EditorStyles.miniButton, GUILayout.Width(45f));

                EditorGUILayout.LabelField("속도", GUILayout.Width(30f));
                _speed = EditorGUILayout.Slider(_speed, 0.1f, 1f);
            }

            EditorGUI.BeginChangeCheck();
            _time = EditorGUILayout.Slider($"t = {_time:0.000}s", _time, 0f, TotalLength);
            if (EditorGUI.EndChangeCheck())
            {
                _playing = false;
                ApplyTime();
            }

            float clipT = Mathf.Min(_time * SampleSpeed, target.length);
            EditorGUILayout.LabelField(
                _animator == null
                    ? "Animator가 없어 포즈가 안 움직입니다."
                    : $"클립 {clipT:0.00} / {target.length:0.00}s" +
                      (_poseOnBlendTreeClip ? "  (원본 속도로 봅니다)" : $"  ×{SampleSpeed:0.##}"),
                EditorStyles.miniLabel);
        }

        /// <summary>본 하나를 골라 그 본의 회전을 축별로 만진다.
        /// 휴머노이드는 뼈 회전을 "머슬 값"(-1~1)으로 들고 있어서, 한 본의 축이 최대 3개다.</summary>
        private void DrawBoneDetail(AnimationClip target)
        {
            if (!SectionHeader("본별 회전 (고급)", ref _showBoneDetail)) return;

            if (_clipBones == null)
            {
                _clipBones = PoseOffsetBaker.BonesIn(target);
                _boneNames = new string[_clipBones.Count];
                for (int i = 0; i < _clipBones.Count; i++) _boneNames[i] = _clipBones[i].BoneName;
                _boneIdx = Mathf.Clamp(_boneIdx, 0, Mathf.Max(0, _clipBones.Count - 1));
            }

            if (_clipBones.Count == 0)
            {
                EditorGUILayout.HelpBox("이 클립이 애니메이션하는 휴머노이드 본이 없습니다.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _boneIdx = EditorGUILayout.Popup("본", _boneIdx, _boneNames);
                var bone = _clipBones[_boneIdx];

                bool changed = false;
                for (int i = 0; i < bone.MuscleNames.Length; i++)
                {
                    string muscle = bone.MuscleNames[i];
                    int muscleIdx = bone.MuscleIndices[i];
                    float current = _poseOffsets.TryGetValue(muscle, out var v) ? v : 0f;

                    // 축 이름은 머슬 이름에서 본 이름을 뺀 나머지 — "Front-Back" 처럼 읽힌다.
                    string axis = muscle.StartsWith(bone.BoneName, System.StringComparison.Ordinal)
                        ? muscle.Substring(bone.BoneName.Length).Trim()
                        : muscle;

                    var (minDeg, maxDeg) = PoseOffsetBaker.MuscleRangeDegrees(muscleIdx);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        float next = EditorGUILayout.Slider(
                            new GUIContent(axis, $"{muscle}\n가동범위 {minDeg:0}° ~ {maxDeg:0}°"),
                            current, -1f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _poseOffsets[muscle] = next;
                            changed = true;
                        }

                        EditorGUILayout.LabelField(
                            $"≈ {PoseOffsetBaker.MuscleDegrees(muscleIdx, current):+0.#;-0.#;0}°",
                            EditorStyles.miniLabel, GUILayout.Width(52f));

                        if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(24f)) &&
                            !Mathf.Approximately(current, 0f))
                        {
                            _poseOffsets[muscle] = 0f;
                            changed = true;
                        }
                    }
                }

                if (changed) RebuildPosePreview();
            }
        }

        /// <summary>위치 보정. 휴머노이드 클립은 일반 본의 위치를 저장하지 않아
        /// 루트·모션·IK 골만 만질 수 있다 — 그 사실을 UI에서 분명히 해 둔다.</summary>
        private void DrawPositionDetail(AnimationClip target)
        {
            if (!SectionHeader("위치 (루트 · IK 골)", ref _showPosition)) return;

            _clipPositionCurves ??= PoseOffsetBaker.PositionCurvesIn(target);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "휴머노이드 클립은 뼈의 위치를 저장하지 않습니다 — 위치는 리타게팅 때 아바타의 뼈 길이로 " +
                    "계산됩니다. 그래서 본마다 포지션을 옮기는 건 포맷상 불가능하고, 아래 루트·모션·IK 골만 " +
                    "움직일 수 있습니다. 팔다리 위치를 바꾸려면 위의 '본별 회전'을 쓰세요.",
                    MessageType.Info);

                if (_clipPositionCurves.Count == 0)
                {
                    EditorGUILayout.LabelField("이 클립에는 위치 커브가 없습니다.", EditorStyles.miniLabel);
                    return;
                }

                bool changed = false;
                foreach (var curve in _clipPositionCurves)
                {
                    float current = _poseOffsets.TryGetValue(curve, out var v) ? v : 0f;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        // 위치는 미터 단위라 머슬(-1~1)과 범위가 다르다.
                        float next = EditorGUILayout.Slider(
                            new GUIContent(curve, "미터 단위. IK 골은 해당 IK가 켜져 있을 때만 반영됩니다."),
                            current, -0.5f, 0.5f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _poseOffsets[curve] = next;
                            changed = true;
                        }

                        if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(24f)) &&
                            !Mathf.Approximately(current, 0f))
                        {
                            _poseOffsets[curve] = 0f;
                            changed = true;
                        }
                    }
                }

                if (changed) RebuildPosePreview();
            }
        }

        /// 보정을 얹은 임시 클립을 만든다. 슬라이더를 놓을 때만 부르므로 매 프레임 복제하지 않는다.
        private void RebuildPosePreview()
        {
            DestroyPosePreview();

            var target = PoseTarget;
            if (target == null || !HasPoseOffset) { ApplyTime(); return; }

            _posePreview = PoseOffsetBaker.CreateOffsetCopy(target, _poseOffsets);
            if (_posePreview != null) _posePreview.hideFlags = HideFlags.HideAndDontSave;

            ApplyTime();
        }

        private void DestroyPosePreview()
        {
            if (_posePreview != null) DestroyImmediate(_posePreview);
            _posePreview = null;
        }

        private void BakePoseOffsets(AnimationClip target)
        {
            var baked = PoseOffsetBaker.Bake(target, _poseOffsets, out _poseLog);
            if (baked == null) return;

            // 공격 클립을 구웠으면 이 무기의 오버라이드 슬롯을 새 클립으로 돌려준다.
            // (직접 지정한 클립은 어디에 쓰이는지 알 수 없어 자동 교체하지 않는다)
            if (!_poseOnBlendTreeClip && RepointWeaponOverride(target, baked))
                _poseLog += "\n→ 이 무기의 오버라이드 슬롯을 새 클립으로 교체했습니다.";
            else
                _poseLog += "\n※ 오버라이드 슬롯 자동 교체는 하지 않았습니다 — " +
                            "Override Controller에서 직접 새 클립으로 바꿔주세요.";

            _poseOffsets.Clear();
            DestroyPosePreview();
            _clipMuscles = null;
            _clipBones = null;
            _clipPositionCurves = null;
            RefreshClip();
            ApplyTime();
        }

        /// 이 무기의 Override Controller에서 current를 가리키던 슬롯을 replacement로 바꾼다.
        private bool RepointWeaponOverride(AnimationClip current, AnimationClip replacement)
        {
            var aoc = WeaponController as AnimatorOverrideController;
            if (aoc == null || current == null || replacement == null) return false;

            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(list);

            // 인덱서의 키는 '바탕 클립'이라, 지금 클립을 값으로 갖는 슬롯을 되짚어야 한다.
            AnimationClip original = null;
            foreach (var kv in list)
                if (kv.Value == current) { original = kv.Key; break; }

            if (original == null) return false;

            Undo.RecordObject(aoc, "오버라이드 클립 교체");
            aoc[original] = replacement;
            EditorUtility.SetDirty(aoc);
            AssetDatabase.SaveAssets();
            _blendTreesDirty = true;
            return true;
        }

        /// <summary>이 무기가 쓰는 클립을 speed배로 구워 새 에셋을 만들고, 이 무기의 Override Controller가
        /// 그 클립을 가리키게 바꾼다. 성공하면 true — 호출부가 공유 배속을 1로 되돌린다.</summary>
        private bool BakeChildClipSpeed(AnimationClip currentClip, AnimationClip baseClip, float speed)
        {
            var baked = ClipSpeedBaker.Bake(currentClip, speed, out _clipBakeLog);
            if (baked == null) return false;

            var aoc = WeaponController as AnimatorOverrideController;
            if (aoc != null && baseClip != null)
            {
                // 인덱서의 키는 '바탕 클립'이다. 이 무기의 오버라이드 슬롯만 새 클립으로 갈아끼운다.
                Undo.RecordObject(aoc, "오버라이드 클립 교체");
                aoc[baseClip] = baked;
                EditorUtility.SetDirty(aoc);
                AssetDatabase.SaveAssets();

                _clipBakeLog += $"\n→ '{aoc.name}'의 '{baseClip.name}' 슬롯을 새 클립으로 교체했습니다. " +
                                "이 무기에만 적용됩니다.";
            }
            else
            {
                _clipBakeLog += "\n※ 이 무기는 Override Controller를 쓰지 않아 자동 교체를 못 했습니다. " +
                                "블렌드트리에서 모션을 직접 새 클립으로 바꿔주세요 " +
                                "(바꾸면 컨트롤러를 공유하는 다른 무기에도 적용됩니다).";
            }

            _blendTreesDirty = true;   // 오버라이드 대응표를 다시 만들어야 한다
            return true;
        }

        private static string Indent(int depth) => depth <= 0 ? "" : new string(' ', depth * 4) + "└ ";

        private static float AnimSpeedOf(AttackRecord rec) => rec != null ? rec.AnimSpeed : 1f;

        /// 프리뷰가 따라가는 이펙트의 시점·배속. 선택된 것이 없으면 0 / 1배속.
        private float SelectedFxTime() => ParticleIdx >= 0 ? Attack.Particle(ParticleIdx).Time : 0f;
        private float SelectedFxSpeed() => ParticleIdx >= 0 ? Attack.Particle(ParticleIdx).SafeSpeed : 1f;

        /// t 시점까지 누적된 전진 거리(m). 런타임의 틱 누적과 같은 선형 보간.
        private float LungeAmountAt(float t)
        {
            var rec = Attack;
            if (rec == null || !rec.HasLunge) return 0f;

            float p = Mathf.Clamp01((t - rec.LungeStartDelay) / rec.LungeDuration);
            // 런타임(PlayerStateMachine.LungeProgress)과 같은 식이어야 프리뷰가 실제와 일치한다.
            float shaped = rec.LungeCurve != null && rec.LungeCurve.length > 0
                ? rec.LungeCurve.Evaluate(p)
                : p;
            return rec.LungeDistance * shaped;
        }

        /// 클립의 FBX 임포트 설정(Root Transform) 편집.
        /// XZ의 Bake Into Pose가 켜져 있으면 루트모션이 본에 흡수돼 전진 굽기가 0으로 나온다 —
        /// 그걸 여기서 바로 끄고 재임포트할 수 있게 한다.
        private void DrawClipImportSection()
        {
            if (_clip == null) return;

            if (!SectionHeader("클립 임포트 설정 (Root Transform)", ref _showImportSettings)) return;

            string path = AssetDatabase.GetAssetPath(_clip);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                // .anim 에셋에는 임포터가 없지만, 같은 Root Transform 설정이 클립 자체에 들어 있다.
                DrawAnimAssetRootSettings(path);
                return;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            int idx = System.Array.FindIndex(clips, c => c != null && c.name == _clip.name);
            if (idx < 0)
            {
                EditorGUILayout.HelpBox($"'{_clip.name}' 항목을 이 FBX에서 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            var entry = clips[idx];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(System.IO.Path.GetFileName(path), EditorStyles.miniBoldLabel);

                EditorGUILayout.LabelField("Root Transform Position (XZ)", EditorStyles.boldLabel);
                entry.lockRootPositionXZ = EditorGUILayout.Toggle("Bake Into Pose", entry.lockRootPositionXZ);
                entry.keepOriginalPositionXZ = EditorGUILayout.Toggle("Based Upon = Original", entry.keepOriginalPositionXZ);
                if (entry.lockRootPositionXZ)
                    EditorGUILayout.HelpBox(
                        "이동이 트랜스폼이 아니라 '포즈'에 들어갑니다.\n" +
                        "→ 몸은 앞으로 가 보이지만 루트는 제자리 = 카메라가 안 따라오고, 히트 판정도 " +
                        "제자리 기준이며, 애니가 끝나면 툭 돌아옵니다. 네트워크로도 실제 이동이 아닙니다.\n" +
                        "이걸 끄고 아래 '클립 루트모션에서 굽기'를 눌러야 실제로 움직입니다.",
                        MessageType.Warning);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Root Transform Position (Y)", EditorStyles.boldLabel);
                entry.lockRootHeightY = EditorGUILayout.Toggle("Bake Into Pose", entry.lockRootHeightY);
                entry.keepOriginalPositionY = EditorGUILayout.Toggle("Based Upon = Original", entry.keepOriginalPositionY);
                entry.heightFromFeet = EditorGUILayout.Toggle("Based Upon = Feet", entry.heightFromFeet);
                entry.heightOffset = EditorGUILayout.FloatField("Offset", entry.heightOffset);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Root Transform Rotation", EditorStyles.boldLabel);
                entry.lockRootRotation = EditorGUILayout.Toggle("Bake Into Pose", entry.lockRootRotation);
                entry.keepOriginalOrientation = EditorGUILayout.Toggle("Based Upon = Original", entry.keepOriginalOrientation);
                entry.rotationOffset = EditorGUILayout.FloatField("Offset", entry.rotationOffset);

                EditorGUILayout.Space(4f);
                if (GUILayout.Button("적용 (재임포트)"))
                {
                    // 재임포트는 클립 참조를 무효화한다 — 프리뷰·포즈를 먼저 정리한다.
                    CleanUp();

                    clips[idx] = entry;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();

                    RefreshClip();
                    ShowNotification(new GUIContent("재임포트 완료"));
                }

                EditorGUILayout.LabelField(
                    "이 설정은 FBX 전체에 적용되며, 같은 클립을 쓰는 다른 공격에도 영향을 줍니다. " +
                    "외부 에셋 팩이면 패키지 갱신 시 되돌아갈 수 있습니다.",
                    EditorStyles.miniLabel);
            }
        }

        /// .anim 에셋용 Root Transform 설정. ModelImporter의 lock*/keepOriginal*와 같은 값이
        /// AnimationClipSettings에 loopBlend*/keepOriginal*라는 이름으로 들어 있다.
        /// 재임포트가 필요 없고 에셋에 바로 쓴다.
        private void DrawAnimAssetRootSettings(string path)
        {
            var s = AnimationUtility.GetAnimationClipSettings(_clip);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{System.IO.Path.GetFileName(path)} (.anim 에셋)", EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.LabelField("Root Transform Position (XZ)", EditorStyles.boldLabel);
                s.loopBlendPositionXZ = EditorGUILayout.Toggle("Bake Into Pose", s.loopBlendPositionXZ);
                s.keepOriginalPositionXZ = EditorGUILayout.Toggle("Based Upon = Original", s.keepOriginalPositionXZ);
                if (s.loopBlendPositionXZ)
                    EditorGUILayout.HelpBox(
                        "이동이 트랜스폼이 아니라 '포즈'에 들어갑니다.\n" +
                        "→ 몸은 앞으로 가 보이지만 루트는 제자리 = 카메라가 안 따라오고, 히트 판정도 " +
                        "제자리 기준이며, 애니가 끝나면 툭 돌아옵니다. 네트워크로도 실제 이동이 아닙니다.\n" +
                        "이걸 끄고 아래 '클립 루트모션에서 굽기'를 눌러야 실제로 움직입니다.",
                        MessageType.Warning);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Root Transform Position (Y)", EditorStyles.boldLabel);
                s.loopBlendPositionY = EditorGUILayout.Toggle("Bake Into Pose", s.loopBlendPositionY);
                s.keepOriginalPositionY = EditorGUILayout.Toggle("Based Upon = Original", s.keepOriginalPositionY);
                s.heightFromFeet = EditorGUILayout.Toggle("Based Upon = Feet", s.heightFromFeet);
                s.level = EditorGUILayout.FloatField("Offset", s.level);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Root Transform Rotation", EditorStyles.boldLabel);
                s.loopBlendOrientation = EditorGUILayout.Toggle("Bake Into Pose", s.loopBlendOrientation);
                s.keepOriginalOrientation = EditorGUILayout.Toggle("Based Upon = Original", s.keepOriginalOrientation);
                s.orientationOffsetY = EditorGUILayout.FloatField("Offset", s.orientationOffsetY);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_clip, "Root Transform 설정 변경");
                    AnimationUtility.SetAnimationClipSettings(_clip, s);
                    EditorUtility.SetDirty(_clip);
                    ApplyTime();   // 포즈 프리뷰에 즉시 반영
                }

                EditorGUILayout.Space(4f);
                if (GUILayout.Button("디스크에 저장"))
                    AssetDatabase.SaveAssets();

                EditorGUILayout.LabelField(
                    "재임포트가 필요 없어 바꾸면 바로 반영됩니다. 이 클립을 쓰는 다른 곳에도 영향을 줍니다.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawPreviewSection()
        {
            var rec = Attack;
            if (!SectionHeader("타임라인 (애니메이션 + 이펙트 동시)", ref _showTimeline)) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("재생", EditorStyles.boldLabel);
                    if (GUILayout.Button(_fxPreview == null ? "프리뷰 켜기" : "프리뷰 끄기", GUILayout.Width(90f)))
                    {
                        if (_fxPreview == null) { CreatePreview(); ApplyTime(); }
                        else { DestroyPreview(); StopPose(); }
                    }
                }

                // ---- 재생 컨트롤 ----
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_playing ? "■ 정지" : "▶ 재생", GUILayout.Width(70f)))
                    {
                        _playing = !_playing;
                        if (_playing && _fxPreview == null) CreatePreview();
                    }
                    if (GUILayout.Button("↺ 처음", GUILayout.Width(60f)))
                    {
                        _time = 0f;
                        ApplyTime();
                    }
                    _loop = GUILayout.Toggle(_loop, "루프", EditorStyles.miniButton, GUILayout.Width(45f));

                    EditorGUILayout.LabelField("속도", GUILayout.Width(30f));
                    _speed = EditorGUILayout.Slider(_speed, 0.1f, 1f);
                }

                // ---- 타임라인 슬라이더 ----
                EditorGUI.BeginChangeCheck();
                _time = EditorGUILayout.Slider($"t = {_time:0.000}s", _time, 0f, TotalLength);
                if (EditorGUI.EndChangeCheck())
                {
                    _playing = false;
                    if (_fxPreview == null) CreatePreview();
                    ApplyTime();
                }

                // ---- 마커 (시트 타이밍 값으로 점프) ----
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("점프", GUILayout.Width(30f));
                    for (int i = 0; i < rec.ParticleCount; i++) DrawMarker($"FX{i}", rec.Particle(i).Time);
                    
                    // 다단이면 타격마다 마커를 낸다.
                    for (int i = 0; i < rec.HitCount; i++)
                        DrawMarker(rec.HitCount > 1 ? $"{i + 1}타" : "판정", rec.HitTime(i));

                    // 사운드 이벤트도 같은 줄에 — 소리·타격·이펙트가 서로 얼마나 어긋났는지 한 줄에서 본다.
                    // 이벤트 시점은 클립 시간이라 재생 시간으로 환산한다(배속이 걸린 공격은 두 값이 다르다).
                    if (!_poseOnBlendTreeClip && _soundClip == _clip)
                        for (int i = 0; i < _soundEvents.Count; i++)
                            DrawMarker($"♪{i}", _soundEvents[i].time / Mathf.Max(0.01f, AnimSpeedOf(rec)));

                    DrawMarker("종료", rec.Duration);
                }

                // ---- 상태 표시 ----
                float fxT = _time - SelectedFxTime();
                string fxState = _fxPreview == null ? "꺼짐"
                    : fxT < 0f ? $"대기 ({-fxT:0.00}s 후)"
                    : $"재생 {fxT:0.00}s ×{SelectedFxSpeed():0.##}";
                string animState = _clip == null ? "클립 없음"
                    : _time > _clip.length ? "종료"
                    : $"{_time:0.00} / {_clip.length:0.00}s";

                EditorGUILayout.LabelField($"애니: {animState}    이펙트: {fxState}", EditorStyles.miniLabel);

                if (_animator == null)
                    EditorGUILayout.HelpBox("Animator가 없어 포즈가 안 움직입니다.", MessageType.Warning);
                else if (_clip == null)
                    EditorGUILayout.HelpBox(
                        $"'{rec.AnimName}' 상태의 클립을 찾지 못했습니다 (BlendTree이거나 이름 불일치). " +
                        "이펙트 편집은 정상 동작합니다.", MessageType.Warning);
                else if (EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("플레이 중에는 포즈가 고정되지 않습니다. 에디트 모드에서 쓰세요.", MessageType.Info);
            }
        }

        /// <summary>공격 클립의 꼬리를 잘라낸 새 클립을 만든다.
        /// 마무리·복귀 구간이 다음 동작(idle)과 안 이어질 때, 페이드 시간을 만지는 대신
        /// 그 구간을 아예 없애는 쪽이 맞는 경우가 있다 — 크로스페이드가 잘린 지점 포즈에서 시작한다.</summary>
        private void DrawTrimSection()
        {
            if (_clip == null) return;
            if (!SectionHeader("클립 끝 자르기", ref _showTrim)) return;

            var rec = Attack;
            float animSpeed = Mathf.Max(0.01f, AnimSpeedOf(rec));

            // 처음 펼쳤을 때는 자를 것이 없는 상태(=클립 끝)에서 시작한다.
            if (_trimTime < 0f) _trimTime = _clip.length;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"대상: {_clip.name}  (길이 {_clip.length:0.###}s)",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _trimTime = EditorGUILayout.Slider("자를 시점 (s)", _trimTime, 0f, _clip.length);

                    // 타임라인은 '재생 시간'이고 클립 키프레임은 '원본 시간'이다 — 배속만큼 환산해야 한다.
                    if (GUILayout.Button(new GUIContent("타임라인 t에서",
                            "위 타임라인에서 보고 있는 시점을 자를 시점으로 가져옵니다."),
                            GUILayout.Width(100f)))
                        _trimTime = Mathf.Clamp(_time * animSpeed, 0f, _clip.length);

                    if (GUILayout.Button("끝", GUILayout.Width(36f)))
                        _trimTime = _clip.length;
                }

                // 자를 시점을 정했으면 그 포즈를 씬에서 바로 확인할 수 있어야 한다.
                if (GUILayout.Button("이 시점 포즈 보기 (타임라인 이동)"))
                {
                    _playing = false;
                    _time = Mathf.Clamp(_trimTime / animSpeed, 0f, TotalLength);
                    if (_fxPreview == null) CreatePreview();
                    ApplyTime();
                }

                float cut = _clip.length - _trimTime;
                EditorGUILayout.LabelField(
                    $"남는 길이 {_trimTime:0.###}s   제거 {cut:0.###}s   " +
                    $"(재생 기준 {_trimTime / animSpeed:0.###}s, 배속 ×{animSpeed:0.##})",
                    EditorStyles.miniLabel);

                // 런타임은 Duration에서 상태를 끝낸다. 그보다 앞에서 자르면 동작이 실제로 짧아지고,
                // 뒤에서 자르면 어차피 안 보이던 구간만 없어진다 — 어느 쪽인지 알려준다.
                if (rec != null && cut > 0.005f)
                {
                    float playedAfter = _trimTime / animSpeed;
                    if (playedAfter < rec.Duration - 0.05f)
                        EditorGUILayout.HelpBox(
                            $"자른 뒤 재생 길이({playedAfter:0.###}s)가 Duration({rec.Duration:0.###}s)보다 짧습니다.\n" +
                            "→ 동작이 끝난 뒤 그만큼 마지막 포즈로 굳어 있다가 다음 동작으로 넘어갑니다. " +
                            "그게 싫으면 시트의 Duration도 같이 줄이세요.",
                            MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox(
                            $"Duration({rec.Duration:0.###}s) 이후 구간을 자릅니다 — 런타임에서 어차피 " +
                            "잘려 보이지 않던 부분입니다. 다음 동작으로의 크로스페이드가 " +
                            "잘린 지점 포즈에서 시작합니다.",
                            MessageType.Info);
                }

                EditorGUILayout.Space(2f);

                using (new EditorGUI.DisabledScope(cut <= 0.005f))
                {
                    if (GUILayout.Button("잘라서 새 클립 만들기"))
                        TrimClipTail();
                }

                if (!string.IsNullOrEmpty(_trimLog))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(_trimLog, MessageType.None);
                        if (GUILayout.Button("×", GUILayout.Width(22f), GUILayout.Height(20f)))
                            _trimLog = null;
                    }
                }

                EditorGUILayout.LabelField(
                    "원본은 그대로 두고 옆에 잘린 클립을 만든 뒤, 이 무기의 오버라이드 슬롯만 " +
                    "그 클립을 보게 바꿉니다. 컨트롤러를 공유하는 다른 무기는 영향받지 않습니다.",
                    EditorStyles.miniLabel);
            }
        }

        private void TrimClipTail()
        {
            var source = _clip;
            var trimmed = ClipTrimmer.TrimTail(source, _trimTime, out _trimLog);
            if (trimmed == null) return;

            // 클립이 바뀌면 샘플링 대상도 바뀐다 — 포즈 모드를 먼저 정리한다.
            StopPose();
            DestroyPosePreview();
            _poseOffsets.Clear();

            if (RepointWeaponOverride(source, trimmed))
                _trimLog += "\n→ 이 무기의 오버라이드 슬롯을 잘린 클립으로 교체했습니다.";
            else
                _trimLog += "\n※ 오버라이드 슬롯 자동 교체는 하지 않았습니다 — " +
                            "Override Controller에서 직접 새 클립으로 바꿔주세요.";

            RefreshClip();
            ApplyTime();
        }

        private void DrawMarker(string label, float t)
        {
            bool on = Mathf.Abs(_time - t) < 0.005f;
            var style = on ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button($"{label} {t:0.###}s", style))
            {
                _playing = false;
                _time = Mathf.Clamp(t, 0f, TotalLength);
                if (_fxPreview == null) CreatePreview();
                ApplyTime();
            }
        }

        // =====================================================================

        private void OnSceneGUI(SceneView view)
        {
            var rec = Attack;
            if (rec == null || Anchor == null || ParticleIdx < 0) return;

            // 앵커 표시 — 무기마다 높이가 달라(y=0 / y=1) 기준을 눈으로 확인해야 한다.
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Handles.DrawWireDisc(Anchor.position, Vector3.up, 0.25f);
            Handles.DrawLine(Anchor.position, Anchor.position + Anchor.rotation * Vector3.forward * 0.6f);

            // 전진기면 경로를 그린다 — 이펙트가 어디서 터져서 어디에 남는지 판단할 기준선.
            if (rec.HasLunge && _avatar != null)
            {
                Vector3 home = _homeSet ? _avatarHome : _avatar.transform.position;
                Vector3 end = home + _avatar.transform.forward * rec.LungeDistance;
                Handles.color = new Color(0.4f, 1f, 0.4f, 0.9f);
                Handles.DrawLine(home, end);
                Handles.DrawWireDisc(end, Vector3.up, 0.2f);
                Handles.Label(end, $"전진 {rec.LungeDistance:0.##}m");
            }

            Vector3 spawnAnchor = AnchorPosAtSpawn();
            Vector3 worldPos = spawnAnchor + Anchor.rotation * rec.Particle(ParticleIdx).Offset;
            Quaternion worldRot = Anchor.rotation * Quaternion.Euler(rec.Particle(ParticleIdx).Euler);

            Handles.color = new Color(0.3f, 0.9f, 1f, 0.7f);
            Handles.DrawDottedLine(spawnAnchor, worldPos, 3f);

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = worldPos;
            Quaternion newRot = worldRot;

            if (Tools.current == Tool.Rotate)
                newRot = Handles.RotationHandle(worldRot, worldPos);
            else
                newPos = Handles.PositionHandle(worldPos, worldRot);

            if (EditorGUI.EndChangeCheck())
            {
                var so = new SerializedObject(rec);
                var e = so.FindProperty("_particles").GetArrayElementAtIndex(ParticleIdx);
                e.FindPropertyRelative("Offset").vector3Value =
                    Quaternion.Inverse(Anchor.rotation) * (newPos - spawnAnchor);
                e.FindPropertyRelative("Euler").vector3Value =
                    (Quaternion.Inverse(Anchor.rotation) * newRot).eulerAngles;
                so.ApplyModifiedProperties();

                SyncPreviewTransform();
                Repaint();
            }

            Handles.Label(worldPos + Vector3.up * 0.15f, $"{rec.Name}");
        }

        // =====================================================================

        private void PlaceAvatarInScene()
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(_avatar);
            if (inst == null) return;

            Undo.RegisterCreatedObjectUndo(inst, "아바타 배치");
            _avatar = inst;
            RebuildWeapons();

            Selection.activeGameObject = inst;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void RebuildWeapons()
        {
            _homeSet = false; // 아바타가 바뀌면 이전 아바타의 원위치는 의미 없다
            _weapons = _avatar != null ? _avatar.GetComponentsInChildren<WeaponBase>(true) : new WeaponBase[0];
            _weaponNames = new string[_weapons.Length];
            for (int i = 0; i < _weapons.Length; i++)
                _weaponNames[i] = _weapons[i].gameObject.name;

            _animator = _avatar != null ? _avatar.GetComponentInChildren<Animator>(true) : null;
            _weaponIdx = 0;
            RebuildAttacks();
        }

        private void RebuildAttacks()
        {
            _attacks = new AttackRecord[0];
            _attackIdx = -1;
            _clip = null;
            _blendTreesDirty = true;   // 무기가 바뀌면 컨트롤러도 바뀐다

            var weapon = Weapon;
            if (weapon == null) return;

            // WeaponBase.data는 protected — SerializedObject로 읽는다(런타임 API 변경 없이).
            var so = new SerializedObject(weapon);
            var record = so.FindProperty("data")?.objectReferenceValue as WeaponRecord;
            if (record?.Attacks == null) return;

            _attacks = record.Attacks;
            _weaponBaseDamage = record.BaseDamage;   // 다단 분배 미리보기용

            // 행 찾기는 런타임 구현을 그대로 쓴다 — 규칙이 갈리면 여기서 "[대시]"라고 표시한 행과
            // 서버가 실제로 고르는 행이 달라진다.
            _dashIdx = WeaponBase.FindAttackIndex(_attacks, so.FindProperty("dashAttackName")?.stringValue);
            _backDashIdx = WeaponBase.FindAttackIndex(_attacks, so.FindProperty("backDashAttackName")?.stringValue);
        }

        /// 공격 목록에서 한 행을 고른다. 선택이 바뀔 때 따라와야 하는 것들을 한 곳에 모아 둔다.
        private void SelectAttack(int idx)
        {
            if (idx == _attackIdx) return;

            _attackIdx = idx;
            DestroyPreview();
            RefreshClip();
            SceneView.RepaintAll();
        }

        private void RefreshClip()
        {
            _clip = null;
            var rec = Attack;
            if (rec == null || _animator == null) return;

            var so = new SerializedObject(Weapon);
            var rac = so.FindProperty("anim")?.objectReferenceValue as RuntimeAnimatorController
                      ?? _animator.runtimeAnimatorController;
            if (rac == null) return;

            _clip = FindClipByStateName(rac, rec.AnimName);
            _time = 0f;
            _trimTime = -1f;   // 새 클립 길이로 다시 초기화
            // 클립이 바뀌면 머슬·본·위치 커브 목록을 전부 다시 읽어야 한다.
            _clipMuscles = null;
            _clipBones = null;
            _clipPositionCurves = null;
        }

        /// <summary>Override Controller를 벗겨 바탕이 되는 AnimatorController를 얻고,
        /// 같이 원본 클립 → 오버라이드 클립 대응표를 만든다.
        /// 블렌드트리·State는 바탕 컨트롤러(Base.controller)에만 있고, 실제로 재생되는 클립은
        /// 무기별 오버라이드다 — 이 둘을 잇는 유일한 통로라 클립 찾기와 트리 편집이 같이 쓴다.</summary>
        private static AnimatorController ResolveBaseController(
            RuntimeAnimatorController rac, out Dictionary<AnimationClip, AnimationClip> overrideMap)
        {
            overrideMap = null;

            while (rac is AnimatorOverrideController aoc)
            {
                var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(list);
                overrideMap ??= new Dictionary<AnimationClip, AnimationClip>();
                foreach (var kv in list)
                    if (kv.Key != null && kv.Value != null && !overrideMap.ContainsKey(kv.Key))
                        overrideMap[kv.Key] = kv.Value;

                rac = aoc.runtimeAnimatorController;
            }

            return rac as AnimatorController;
        }

        /// AnimName은 클립 이름이 아니라 Animator의 State 이름이다.
        /// Override Controller면 원본 클립 → 오버라이드 클립으로 바꿔 돌려준다.
        private static AnimationClip FindClipByStateName(RuntimeAnimatorController rac, string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return null;

            var controller = ResolveBaseController(rac, out var overrideMap);
            if (controller == null) return null;

            foreach (var layer in controller.layers)
            {
                var found = FindInStateMachine(layer.stateMachine, stateName);
                if (found == null) continue;

                var clip = found.motion as AnimationClip;
                if (clip != null && overrideMap != null && overrideMap.TryGetValue(clip, out var o))
                    clip = o;
                return clip;
            }
            return null;
        }

        /// 컨트롤러 전체를 훑어 모션이 BlendTree인 State를 모은다(하위 상태머신 포함).
        private static void CollectBlendTrees(AnimatorStateMachine sm, string path, List<BlendTreeEntry> outList)
        {
            if (sm == null) return;

            foreach (var cs in sm.states)
            {
                if (cs.state == null) continue;
                if (cs.state.motion is BlendTree tree)
                    outList.Add(new BlendTreeEntry($"{path}/{cs.state.name}", cs.state, tree));
            }

            foreach (var child in sm.stateMachines)
                CollectBlendTrees(child.stateMachine, $"{path}/{child.stateMachine?.name}", outList);
        }

        private static AnimatorState FindInStateMachine(AnimatorStateMachine sm, string stateName)
        {
            if (sm == null) return null;

            foreach (var cs in sm.states)
                if (cs.state != null && cs.state.name == stateName)
                    return cs.state;

            foreach (var child in sm.stateMachines)
            {
                var found = FindInStateMachine(child.stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        // =====================================================================

        private void CreatePreview()
        {
            DestroyPreview();

            var rec = Attack;
            if (ParticleIdx < 0) return;
            var prefab = rec.Particle(ParticleIdx).Prefab;
            if (prefab == null) return;

            _fxPreview = Instantiate(prefab);
            _fxPreview.hideFlags = HideFlags.HideAndDontSave;

            // 타임라인 끝을 정하려면 파티클이 얼마나 지속되는지 알아야 한다.
            var ps = _fxPreview.GetComponentInChildren<ParticleSystem>();
            _fxLength = ps != null
                ? Mathf.Max(0.2f, ps.main.duration + ps.main.startLifetime.constantMax)
                : 1f;

            SyncPreviewTransform();
            SimulateFx();
        }

        private void DestroyPreview()
        {
            if (_fxPreview != null) DestroyImmediate(_fxPreview);
            _fxPreview = null;
            _playing = false;
            RestoreAvatarHome();
        }

        private void SyncPreviewTransform()
        {
            var rec = Attack;
            if (_fxPreview == null || rec == null || Anchor == null) return;

            _fxPreview.transform.SetPositionAndRotation(
                AnchorPosAtSpawn() + Anchor.rotation * rec.Particle(ParticleIdx).Offset,
                Anchor.rotation * Quaternion.Euler(rec.Particle(ParticleIdx).Euler));
        }

        private void SimulateFx()
        {
            if (_fxPreview == null) return;

            SyncPreviewTransform();
            var ps = _fxPreview.GetComponentInChildren<ParticleSystem>();
            if (ps == null) return;

            // 파티클은 ParticleOccurDelay가 지나야 시작한다. 그 전엔 아무것도 안 보여야 한다.
            // 배속은 런타임에서 simulationSpeed로 걸리므로, 프리뷰는 경과 시간에 곱해 같은 결과를 낸다.
            var rec = Attack;
            float fxT = _time - SelectedFxTime();
            float fxSpeed = SelectedFxSpeed();
            ps.Simulate(Mathf.Max(0f, fxT) * fxSpeed, true, true);
        }

        private void SamplePose()
        {
            // 포즈 보정 중이면 보정을 얹은 임시 클립을 대신 샘플링한다 — 슬라이더 결과를 씬에서 바로 본다.
            // 공격을 안 골랐어도(직접 지정 모드) 프리뷰는 보여야 하므로 _clip보다 우선한다.
            var sampled = SampledClip;

            if (_animator == null || sampled == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // 포즈 고정은 에디트 모드 전용

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            // SampleAnimationClip은 클립의 루트모션을 트랜스폼에 그대로 적용한다. 하지만 런타임은
            // applyRootMotion = 0이라 루트모션이 트랜스폼에 반영되지 않는다 — 전진기만 움직인다.
            // 그대로 두면 전진 + 루트모션이 겹쳐 툴이 실제보다 멀리 간다. 그래서 샘플링 전 위치를
            // 기억했다가 되돌려 루트모션을 상쇄한다(포즈는 남고 이동만 제거).
            var root = _animator.transform;
            Vector3 keepPos = root.position;
            Quaternion keepRot = root.rotation;

            // 배속이 걸리면 t초 시점의 클립 위치는 t * 배속이다. 클립 끝을 넘으면 마지막 프레임으로 둔다
            // (이펙트 잔상 구간을 계속 볼 수 있게).
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_animator.gameObject, sampled,
                Mathf.Min(_time * SampleSpeed, sampled.length));
            AnimationMode.EndSampling();

            root.SetPositionAndRotation(keepPos, keepRot);

            SyncPreviewTransform();
        }

        /// 클립의 루트모션을 구간별로 샘플링해 (총 전방 거리, 진행 곡선)으로 굽는다.
        /// 런타임은 applyRootMotion을 켜지 않고 이 데이터를 서버가 틱마다 재생한다 —
        /// 결과는 애니메이션과 같은 이동인데 결정론은 유지된다.
        /// <param name="clipEndTime">샘플링을 끝낼 클립 시간(초). 0 이하면 클립 전체.
        /// 재생 도중 잘리는 클립은 잘리는 지점까지만 재서, 거리·곡선이 실제로 보이는 동작과 맞게 한다.</param>
        private (float distance, AnimationCurve curve) BakeRootMotion(float clipEndTime = 0f, int samples = 24)
        {
            var flat = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            if (_animator == null || _clip == null) return (0f, flat);

            float end = clipEndTime > 0f ? Mathf.Min(clipEndTime, _clip.length) : _clip.length;

            var root = _animator.transform;
            Vector3 keepPos = root.position;
            Quaternion keepRot = root.rotation;
            Vector3 fwd = keepRot * Vector3.forward;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            var forward = new float[samples + 1];

            for (int i = 0; i <= samples; i++)
            {
                float t = end * i / samples;

                // 매 샘플 전에 기준 자세로 되돌린다. SampleAnimationClip이 루트모션을 누적으로
                // 적용하는지 절대값으로 적용하는지에 의존하지 않도록, 항상 같은 기준에서 잰다.
                root.SetPositionAndRotation(keepPos, keepRot);

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_animator.gameObject, _clip, t);
                AnimationMode.EndSampling();

                forward[i] = Vector3.Dot(root.position - keepPos, fwd);
            }

            root.SetPositionAndRotation(keepPos, keepRot);

            // t=0 자체가 오프셋을 가질 수 있으므로 첫 샘플을 원점으로 옮긴다.
            float zero = forward[0];
            for (int i = 0; i <= samples; i++) forward[i] -= zero;

            float total = forward[samples];

            Debug.Log($"[BakeRootMotion] {_clip.name} (0~{end:0.###}s) 총 전방 {total:0.####}m · 구간별 " +
                      string.Join(", ", System.Array.ConvertAll(forward, v => v.ToString("0.##"))));

            if (Mathf.Abs(total) < 0.0001f)
            {
                Debug.LogWarning($"[BakeRootMotion] '{_clip.name}'에서 전방 이동이 측정되지 않았습니다. " +
                                 "제자리 클립이거나 FBX 임포트의 Root Transform Position(XZ)이 " +
                                 "Bake Into Pose로 잠겨 있습니다. 거리를 직접 넣어야 합니다.");
                return (0f, flat);
            }

            // 진행률(0~1) → 이동 진행률(0~1)로 정규화. 거리는 따로 들고 있으므로 곡선은 모양만 담는다.
            var curve = new AnimationCurve();
            for (int i = 0; i <= samples; i++)
                curve.AddKey(new Keyframe((float)i / samples, forward[i] / total));

            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);

            return (total, curve);
        }

        private void StopPose()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }
    }
}
