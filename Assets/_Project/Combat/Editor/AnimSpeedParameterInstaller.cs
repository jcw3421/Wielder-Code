using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Base.controller에 배속 파라미터를 설치한다.
    ///
    /// 무기별 Override Controller는 "클립"만 덮을 수 있고 State의 speed는 못 바꾼다.
    /// 그래서 State speed를 직접 건드리면 4개 무기가 전부 같이 바뀐다.
    /// 대신 SpeedParameter를 켜두면 런타임에 무기·상황별로 배속을 나눌 수 있다.
    ///
    /// - Attack* → AttackSpeed (공격 발동 시 AttackRecord.AnimSpeed로 설정)
    /// - Locomotion/Die/Flinch/Stun은 건드리지 않는다. 이동은 보폭 동기화를 철회했고,
    ///   CC 모션에 직전 공격의 배속이 묻어나면 안 되기 때문.
    /// </summary>
    public static class AnimSpeedParameterInstaller
    {
        private const string ControllerPath = "Assets/_Project/Combat/Ani/Base.controller";
        private const string AttackSpeedParam = "AttackSpeed";
        private const string MoveSpeedParam = "MoveSpeed";

        [MenuItem("Tools/Combat/Base.controller에 배속 파라미터 설치")]
        private static void Install()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("배속 파라미터 설치",
                    $"컨트롤러를 찾지 못했습니다:\n{ControllerPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("배속 파라미터 설치",
                    $"{controller.name}에 '{AttackSpeedParam}' / '{MoveSpeedParam}' 파라미터를 추가하고\n" +
                    "Attack* 및 Locomotion State의 Speed Parameter를 켭니다.\n\n" +
                    "Die/Flinch/Stun은 건드리지 않습니다.",
                    "설치", "취소"))
                return;

            EnsureFloatParameter(controller, AttackSpeedParam);
            EnsureFloatParameter(controller, MoveSpeedParam);

            int wired = 0, skipped = 0;
            var log = new StringBuilder();

            foreach (var layer in controller.layers)
                Walk(layer.stateMachine, ref wired, ref skipped, log);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AnimSpeedParameterInstaller] 배선 {wired} / 건드리지 않음 {skipped}\n{log}");
        }

        private static void Walk(AnimatorStateMachine sm, ref int wired, ref int skipped, StringBuilder log)
        {
            if (sm == null) return;

            foreach (var cs in sm.states)
            {
                var state = cs.state;
                if (state == null) continue;

                string param = ParamFor(state.name);
                if (param == null)
                {
                    // 예전 버전이 Locomotion에 MoveSpeed를 걸었다면 되돌린다(보폭 동기화는 철회됨).
                    if (state.speedParameterActive && state.speedParameter == MoveSpeedParam)
                    {
                        state.speedParameterActive = false;
                        state.speedParameter = "";
                        log.AppendLine($"  [해제] {state.name} — MoveSpeed 배선 제거");
                    }
                    else
                    {
                        log.AppendLine($"  [유지] {state.name}");
                    }
                    skipped++;
                    continue;
                }

                if (state.speedParameterActive && state.speedParameter == param)
                {
                    log.AppendLine($"  [이미됨] {state.name} ← {param}");
                    continue;
                }

                state.speedParameter = param;
                state.speedParameterActive = true;
                wired++;
                log.AppendLine($"  [배선] {state.name} ← {param}");
            }

            foreach (var child in sm.stateMachines)
                Walk(child.stateMachine, ref wired, ref skipped, log);
        }

        /// 상태 이름으로 어떤 파라미터를 쓸지 정한다. null이면 건드리지 않는다.
        /// Locomotion은 제외한다 — 보폭 동기화는 속도 배율로 해결되는 문제가 아니라 철회했다.
        private static string ParamFor(string stateName)
        {
            if (stateName.StartsWith("Attack")) return AttackSpeedParam;
            return null;
        }

        private static void EnsureFloatParameter(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
                if (p.name == name) return;

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,   // 설치 직후 동작이 바뀌지 않도록 1배속
            });
        }
    }
}
