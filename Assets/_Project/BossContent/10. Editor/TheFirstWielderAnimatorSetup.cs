using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.BossContent.Editor
{
    /// TFW 컨트롤러에 스테이트·전이를 한 번에 깔아준다. 클립(Motion)은 비워두므로 디자이너가 채운다.
    /// 스테이트 이름은 BossTimingBaker의 규칙({패턴명}_Telegraph/_Active/_Recovery)을 그대로 따른다.
    /// 여러 번 실행해도 안전하다 — 이미 있는 스테이트·파라미터는 건너뛴다.
    public static class TheFirstWielderAnimatorSetup
    {
        private const string ControllerPath =
            "Assets/_Project/BossContent/05. Animators/TheFirstWielderBoss/TheFirstWielderBoss.controller";

        // SO의 AnimatorPatternId와 맞춰야 하는 값 — 어긋나면 패턴이 시작돼도 클립이 안 바뀐다.
        // TheFirstWielderSetup.ConfigurePatterns가 SO에 같은 값을 써넣는다.
        private const int CleaveComboId = 1;
        private const int SunderingDashId = 2;
        private const int RisingComboId = 3;
        private const int TriBlastId = 4;
        private const int FourStrikeComboId = 5;

        // 2페이즈 검+방패 — 대검(1~5)과 겹치지 않게 11부터 쓴다.
        private const int SwordShieldComboId = 11;
        private const int ShieldBashComboId = 12;
        private const int RushSixStrikeId = 13;
        private const int CascadeFieldComboId = 14;
        private const int LaserComboId = 15;
        private const int MarkedStrikeId = 16;
        private const int DonutFieldId = 17;

        private const int PhaseTransitionId = 99;   // BossPhaseTransitionState가 쓰는 고정값

        [MenuItem("Tools/Boss/Setup TheFirstWielder Animator")]
        private static void Setup()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[TFW Animator] 컨트롤러를 찾을 수 없음: {ControllerPath}");
                return;
            }

            EnsureParameters(controller);
            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");

            var sm = controller.layers[0].stateMachine;

            var locomotion = EnsureLocomotion(controller, sm);
            sm.defaultState = locomotion;

            EnsureDie(sm);
            // activeSegments = 조각 클립 개수. 연계는 조각을 이어붙이고, 단발 패턴은 1개.
            // 1페이즈 대검
            EnsurePattern(sm, locomotion, "CleaveCombo", CleaveComboId, y: 200f, activeSegments: 4);
            EnsurePattern(sm, locomotion, "SunderingDash", SunderingDashId, y: 400f, activeSegments: 1);
            EnsurePattern(sm, locomotion, "RisingCombo", RisingComboId, y: 600f, activeSegments: 4);
            EnsurePattern(sm, locomotion, "TriBlast", TriBlastId, y: 800f, activeSegments: 1);
            EnsurePattern(sm, locomotion, "FourStrikeCombo", FourStrikeComboId, y: 1000f, activeSegments: 4);
            EnsurePattern(sm, locomotion, "PhaseTransition", PhaseTransitionId, y: 1200f, activeSegments: 1);

            // 2페이즈 검+방패 (HP 50% 이하)
            EnsurePattern(sm, locomotion, "SwordShieldCombo", SwordShieldComboId, y: 1500f, activeSegments: 5);
            EnsurePattern(sm, locomotion, "ShieldBashCombo", ShieldBashComboId, y: 1700f, activeSegments: 5);
            EnsurePattern(sm, locomotion, "RushSixStrike", RushSixStrikeId, y: 1900f, activeSegments: 2);
            EnsurePattern(sm, locomotion, "CascadeFieldCombo", CascadeFieldComboId, y: 2100f, activeSegments: 4);
            EnsurePattern(sm, locomotion, "LaserCombo", LaserComboId, y: 2300f, activeSegments: 4);
            EnsurePattern(sm, locomotion, "MarkedStrike", MarkedStrikeId, y: 2500f, activeSegments: 1);
            EnsurePattern(sm, locomotion, "DonutField", DonutFieldId, y: 2700f, activeSegments: 1);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[TFW Animator] 스테이트·전이 생성 완료. 각 스테이트에 클립을 넣으세요. " +
                      "PhaseTransition은 거합(즉사기)입니다.");
        }

        private static void EnsureParameters(AnimatorController controller)
        {
            AddIfMissing(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
            AddIfMissing(controller, "BossState", AnimatorControllerParameterType.Int);
            AddIfMissing(controller, "AnimatorPatternId", AnimatorControllerParameterType.Int);
            AddIfMissing(controller, "PatternSpeedMultiplier", AnimatorControllerParameterType.Float);
            AddIfMissing(controller, "StartPattern", AnimatorControllerParameterType.Trigger);
            AddIfMissing(controller, "PatternStep", AnimatorControllerParameterType.Int);
        }

        private static void AddIfMissing(AnimatorController controller, string name,
                                         AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
                if (p.name == name) return;
            controller.AddParameter(name, type);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var child in sm.states)
                if (child.state.name == name) return child.state;
            return null;
        }

        /// 이동 블렌드 트리. MoveSpeed로 정지↔이동을 섞는다 — 자식 Motion은 비어 있으니 클립을 꽂으면 된다.
        private static AnimatorState EnsureLocomotion(AnimatorController controller, AnimatorStateMachine sm)
        {
            var existing = FindState(sm, "Locomotion");
            if (existing != null) return existing;

            var state = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendParameter = "MoveSpeed";
            tree.AddChild(null, 0f);   // Idle 자리
            tree.AddChild(null, 1f);   // Walk 자리
            return state;
        }

        /// 페이즈 전환 패턴만 AnyState에서도 들어올 수 있게 한다.
        ///
        /// 페이즈 전환은 피격으로 트리거되므로 보스가 다른 패턴을 재생하는 도중에 걸린다.
        /// 로직(BossFSM)은 그 자리에서 패턴을 Cancel하고 거합 단계를 세기 시작하는데,
        /// 진입 전이가 Locomotion에만 달려 있으면 애니메이터는 재생 중이던 패턴의
        /// Recovery가 끝나 Locomotion으로 돌아올 때까지 StartPattern 트리거를 물고 기다린다.
        /// 그만큼 전환 애니가 통째로 밀려 판정이 먼저 나간다 — Die와 같은 이유로 AnyState가 맞다.
        ///
        /// 여러 번 실행해도 안전하다 — 이미 깔려 있으면 건너뛴다.
        [MenuItem("Tools/Boss/Fix TheFirstWielder PhaseTransition Entry")]
        private static void FixPhaseTransitionEntry()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[TFW Animator] 컨트롤러를 찾을 수 없음: {ControllerPath}");
                return;
            }

            var sm = controller.layers[0].stateMachine;
            var telegraph = FindState(sm, "PhaseTransition_Telegraph");
            if (telegraph == null)
            {
                Debug.LogError("[TFW Animator] PhaseTransition_Telegraph 스테이트가 없음 — Setup을 먼저 실행하세요.");
                return;
            }

            foreach (var existing in sm.anyStateTransitions)
                if (existing.destinationState == telegraph)
                {
                    Debug.Log("[TFW Animator] 페이즈 전환 AnyState 전이가 이미 있음 — 변경 없음.");
                    return;
                }

            var t = sm.AddAnyStateTransition(telegraph);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0f;            // 로직은 즉시 세기 시작한다 — 블렌드로 늦추면 그만큼 어긋난다
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Equals, PhaseTransitionId, "AnimatorPatternId");
            t.AddCondition(AnimatorConditionMode.If, 0f, "StartPattern");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[TFW Animator] 페이즈 전환 진입을 AnyState로 승격했습니다 — 패턴 도중에도 즉시 끊고 들어갑니다.");
        }

        private static void EnsureDie(AnimatorStateMachine sm)
        {
            if (FindState(sm, "Die") != null) return;

            var die = sm.AddState("Die", new Vector3(500f, -100f, 0f));
            // 사망은 어느 상태에서든 즉시 끊고 들어간다. BossAnimState.Dead = 3.
            var t = sm.AddAnyStateTransition(die);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Equals, 3, "BossState");
        }

        /// 패턴 하나의 스테이트와 전이를 만든다. 진입은 AnimatorPatternId + StartPattern 트리거,
        /// 단계 전환은 클립 종료(ExitTime).
        ///
        /// activeSegments가 2 이상이면 Active를 조각 클립 체인으로 깐다
        /// (_Active → _Active2 → ...). 연계 모션을 통짜로 합치지 않고 조각을 이어붙일 때 쓴다.
        /// 이 경우 SO의 ManualActiveSeconds를 켜고 전체 길이를 넣어야 하는데,
        /// TheFirstWielderComboBaker가 조각 길이를 합산해 자동으로 해준다.
        ///
        /// 재실행 시 스테이트는 그대로 두고 전이만 다시 깐다 — 조각 수를 바꿔도 수렴한다.
        private static void EnsurePattern(AnimatorStateMachine sm, AnimatorState locomotion,
                                          string prefix, int patternId, float y, int activeSegments = 1)
        {
            var telegraph = EnsureState(sm, $"{prefix}_Telegraph", 350f, y);
            var recovery  = EnsureState(sm, $"{prefix}_Recovery",  350f + 250f * (activeSegments + 1), y);

            var segments = new AnimatorState[Mathf.Max(1, activeSegments)];
            for (int i = 0; i < segments.Length; i++)
            {
                string name = i == 0 ? $"{prefix}_Active" : $"{prefix}_Active{i + 1}";
                segments[i] = EnsureState(sm, name, 600f + 250f * i, y);
            }

            // 페이즈 배속을 클립 재생에도 걸어야 한다. 코드는 ToTicks에서 배속만큼 시간을 줄이는데
            // 애니가 이걸 모르면 2페이즈부터 판정과 모션이 통째로 어긋난다(SkeletonKing과 동일한 설정).
            BindSpeedParameter(telegraph);
            BindSpeedParameter(recovery);
            foreach (var s in segments) BindSpeedParameter(s);

            // 전조의 기준 속도는 1이다. 위 BindSpeedParameter가 그대로 살아 있으므로
            // 실제 재생 속도는 1 × PatternSpeedMultiplier — 즉 전조도 배속을 탄다.
            //
            // 그게 맞다. 서버도 EnterStage(Telegraph, ToTicks(TelegraphSeconds))로 같은 배속에 나누므로,
            // 여기만 1로 고정하면 3페이즈에서 애니는 그대로인데 판정만 빨라져 어긋난다.
            // (베이커가 TelegraphSeconds를 클립길이/speed로 넣어주므로 배속 1에서는 정확히 클립 길이다.)
            telegraph.speed = 1f;

            // 전이를 처음부터 다시 깐다(중복 방지). 스테이트에 넣은 클립은 유지된다.
            ClearTransitions(telegraph);
            ClearTransitions(recovery);
            foreach (var s in segments) ClearTransitions(s);
            RemoveTransitionsTo(locomotion, telegraph);

            var enter = locomotion.AddTransition(telegraph);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            // 블렌드 시간은 0이다. Fixed Duration은 초 단위 고정이라 배속을 따라가지 않는데,
            // 코드는 ToTicks에서 배속만큼 시간을 줄인다 — 블렌드가 남아 있으면 배속이 오를수록
            // 애니가 코드보다 뒤로 밀려 "이동은 이미 했는데 모션은 나중에 뜨는" 어긋남이 생긴다.
            enter.duration = 0f;
            enter.AddCondition(AnimatorConditionMode.Equals, patternId, "AnimatorPatternId");
            enter.AddCondition(AnimatorConditionMode.If, 0f, "StartPattern");

            ChainOnExit(telegraph, segments[0]);
            for (int i = 0; i < segments.Length - 1; i++)
                ChainOnExit(segments[i], segments[i + 1]);
            ChainOnExit(segments[segments.Length - 1], recovery);
            ChainOnExit(recovery, locomotion);
        }

        /// 스테이트 재생 속도를 PatternSpeedMultiplier 파라미터에 연결한다.
        /// BossView가 매 프레임 이 값을 써넣으므로, 페이즈 배속이 그대로 클립 속도가 된다.
        private static void BindSpeedParameter(AnimatorState state)
        {
            state.speedParameter = "PatternSpeedMultiplier";
            state.speedParameterActive = true;
        }

        /// 스테이트를 찾거나 만들고, 위치는 항상 최신 레이아웃으로 맞춘다.
        /// (조각 수가 늘면 기존 스테이트가 새 조각과 같은 자리에 겹쳐 보이지 않게 된다)
        private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, float x, float y)
        {
            var position = new Vector3(x, y, 0f);
            var found = FindState(sm, name);
            if (found == null) return sm.AddState(name, position);

            // ChildAnimatorState는 struct라 배열을 고쳐서 통째로 되돌려줘야 반영된다.
            var states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != found) continue;
                states[i].position = position;
                sm.states = states;
                break;
            }
            return found;
        }

        /// 이 스테이트의 전이를 전부 걷어낸다.
        ///
        /// <c>state.transitions = new AnimatorStateTransition[0]</c> 만으로는 부족하다. 그건 참조만
        /// 끊고 전이 오브젝트 자체는 .controller 안에 서브에셋으로 남긴다 — Setup을 돌릴 때마다
        /// 한 세트씩 쌓여서, 실제로 이 컨트롤러는 전이 294개 중 216개가 아무 데도 연결되지 않은
        /// 죽은 오브젝트였다. 실행에는 영향이 없지만 파일이 부풀고 diff·정적 분석이 못 쓰게 된다.
        /// 참조를 먼저 끊고 나서 파괴하므로 이중 해제는 없다.
        private static void ClearTransitions(AnimatorState state)
        {
            var existing = state.transitions;
            state.transitions = new AnimatorStateTransition[0];

            foreach (var t in existing)
                if (t != null) UnityEngine.Object.DestroyImmediate(t, allowDestroyingAssets: true);
        }

        /// locomotion에서 이 패턴으로 가는 기존 전이를 걷어낸다(다른 패턴 전이는 건드리지 않는다).
        private static void RemoveTransitionsTo(AnimatorState from, AnimatorState target)
        {
            foreach (var t in from.transitions)
                if (t.destinationState == target)
                    from.RemoveTransition(t);
        }

        /// 컨트롤러를 점검하고, 아무 데도 연결되지 않은 전이 오브젝트를 걷어낸다.
        ///
        /// 두 가지를 본다:
        ///  1. 조각 체인의 블렌드 시간이 0인가 — 크로스페이드가 붙은 만큼 애니가 코드보다 뒤로 밀리고,
        ///     조각이 4~5개인 연계에서는 그 오차가 누적된다(자세한 건 ChainOnExit 주석).
        ///     손으로 값을 넣었다면 여기서 잡힌다.
        ///  2. 고아 전이 — 예전 Setup이 참조만 끊고 오브젝트를 남겨서 .controller에 쌓인 것들.
        ///     실행에는 영향이 없다(Unity는 연결된 그래프만 훑는다). 다만 파일이 부풀고,
        ///     에셋을 텍스트로 읽을 때 살아있는 전이와 구분이 안 돼 사람을 헷갈리게 한다.
        ///
        /// 지우기 전에 무엇을 지우는지 보여주고 확인을 받는다. 지우는 대상은 전이뿐이라
        /// 스테이트·블렌드트리·비헤이비어는 어떤 경우에도 건드리지 않는다.
        [MenuItem("Tools/Boss/Verify TheFirstWielder Animator")]
        private static void VerifyAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[TFW Animator] 컨트롤러를 찾을 수 없음: {ControllerPath}");
                return;
            }

            var live = CollectLiveObjects(controller);
            var report = new System.Text.StringBuilder("[TFW Animator] 점검\n");

            // ── 1. 블렌드 시간 ──
            int blendFixed = 0;
            foreach (var obj in live)
            {
                if (!(obj is AnimatorStateTransition t) || t.duration <= 0f) continue;
                if (!IsStepChainTransition(live, t)) continue;

                report.AppendLine($"  블렌드 {t.duration:F3}s → 0s : → " +
                                  $"{(t.destinationState != null ? t.destinationState.name : "?")}");
                t.duration = 0f;
                EditorUtility.SetDirty(t);
                blendFixed++;
            }

            // ── 2. 고아 전이 ──
            var orphans = new System.Collections.Generic.List<UnityEngine.Object>();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub == null || !(sub is AnimatorTransitionBase)) continue;   // 전이만 대상
                if (!live.Contains(sub)) orphans.Add(sub);
            }

            int liveTransitions = 0;
            foreach (var o in live) if (o is AnimatorTransitionBase) liveTransitions++;

            report.AppendLine($"  전이: 연결 {liveTransitions}개 / 고아 {orphans.Count}개");
            report.AppendLine($"  블렌드: {(blendFixed == 0 ? "체인 전부 0 — 이상 없음" : blendFixed + "개 수정")}");

            if (orphans.Count > 0 &&
                EditorUtility.DisplayDialog("TFW 애니메이터 정리",
                    $"아무 데도 연결되지 않은 전이 {orphans.Count}개를 지웁니다.\n" +
                    $"(연결된 전이 {liveTransitions}개와 스테이트는 건드리지 않습니다)\n\n" +
                    "되돌리려면 git으로 .controller를 복원하세요.",
                    "지운다", "그대로 둔다"))
            {
                foreach (var o in orphans) UnityEngine.Object.DestroyImmediate(o, allowDestroyingAssets: true);
                report.AppendLine($"  고아 {orphans.Count}개 삭제됨");
                blendFixed++;   // 저장이 필요하다는 표시
            }

            if (blendFixed > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log(report.ToString());
        }

        /// 지금 그래프에 실제로 연결돼 있는 스테이트·스테이트머신·전이를 모은다.
        /// 여기 없는 전이는 Unity가 평가하지 않는 죽은 오브젝트다.
        private static System.Collections.Generic.HashSet<UnityEngine.Object> CollectLiveObjects(
            AnimatorController controller)
        {
            var live = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            foreach (var layer in controller.layers)
                CollectStateMachine(layer.stateMachine, live);
            return live;
        }

        private static void CollectStateMachine(AnimatorStateMachine sm,
                                                System.Collections.Generic.HashSet<UnityEngine.Object> live)
        {
            if (sm == null || !live.Add(sm)) return;   // Add가 false면 이미 훑은 것 — 순환 방지도 겸한다

            foreach (var t in sm.anyStateTransitions) if (t != null) live.Add(t);
            foreach (var t in sm.entryTransitions) if (t != null) live.Add(t);

            foreach (var child in sm.states)
            {
                if (child.state == null) continue;
                live.Add(child.state);
                foreach (var t in child.state.transitions) if (t != null) live.Add(t);
            }

            foreach (var child in sm.stateMachines)
            {
                if (child.stateMachine == null) continue;
                foreach (var t in sm.GetStateMachineTransitions(child.stateMachine))
                    if (t != null) live.Add(t);
                CollectStateMachine(child.stateMachine, live);
            }
        }

        /// 이 전이가 "판정 시간 안"에 있는가 — 전조와 Active 조각에서 나가는 것들이 그렇다.
        /// 소유 스테이트를 역으로 찾아 이름으로 판단한다(전이는 자기 출발점을 모른다).
        /// Recovery → locomotion은 판정이 끝난 뒤라 블렌드가 있어야 자연스럽다.
        private static bool IsStepChainTransition(
            System.Collections.Generic.HashSet<UnityEngine.Object> live, AnimatorStateTransition transition)
        {
            foreach (var obj in live)
            {
                if (!(obj is AnimatorState state)) continue;
                if (!state.name.EndsWith("_Telegraph") && !state.name.Contains("_Active")) continue;

                foreach (var t in state.transitions)
                    if (t == transition) return true;
            }
            return false;
        }

        /// 클립이 끝나면 다음 단계로. 지속시간은 로직(BossPatternBase)이 SO의 초로 재므로
        /// 클립 길이와 SO 값은 베이커로 맞춘다.
        ///
        /// 블렌드 시간은 0이다. 크로스페이드가 있으면 (1) 클립이 끝나기 전에 다음으로 넘어가 실제
        /// 재생 시간이 짧아지고 (2) Fixed Duration이 초 단위 고정이라 배속을 안 따라간다.
        /// 조각이 4개면 이 오차가 누적돼 코드와 애니가 통째로 어긋난다.
        private static void ChainOnExit(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.canTransitionToSelf = false;
        }
    }
}
