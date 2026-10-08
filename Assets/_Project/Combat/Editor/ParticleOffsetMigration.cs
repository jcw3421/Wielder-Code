using System.Text;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 이펙트 데이터를 "공격당 1개(평면 필드)"에서 "공격당 N개(AttackParticle 배열)"로 옮긴다.
    ///
    /// 구버전 필드(_particle / _particleOffsetPos / _particleOffsetEuler / _effectSpeed /
    /// _effectStartOffset)는 스크립트에서 제거됐지만 .asset 파일에는 값이 남아 있다.
    /// SerializedObject로 그 값을 읽어 첫 번째 AttackParticle로 접어 넣는다.
    ///
    /// 한 번 돌리면 끝이고, 이미 이관된 레코드는 건너뛴다.
    /// </summary>
    public static class ParticleOffsetMigration
    {
        [MenuItem("Tools/Combat/이펙트 데이터 마이그레이션 (구필드 → Particles 배열)")]
        private static void Migrate()
        {
            string[] guids = AssetDatabase.FindAssets("t:AttackRecord");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("이펙트 마이그레이션", "AttackRecord 에셋을 찾지 못했습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("이펙트 마이그레이션",
                    $"AttackRecord {guids.Length}개를 검사해 구버전 이펙트 필드를 Particles 배열로 옮깁니다.\n\n" +
                    "이미 Particles가 채워진 레코드는 건너뜁니다.",
                    "실행", "취소"))
                return;

            int moved = 0, skipped = 0, empty = 0;
            var log = new StringBuilder();

            foreach (string guid in guids)
            {
                var record = AssetDatabase.LoadAssetAtPath<AttackRecord>(AssetDatabase.GUIDToAssetPath(guid));
                if (record == null) continue;

                var so = new SerializedObject(record);
                var particles = so.FindProperty("_particles");

                if (particles.arraySize > 0)
                {
                    skipped++;
                    log.AppendLine($"  [건너뜀] {record.name} ({record.Name}) — 이미 {particles.arraySize}개");
                    continue;
                }

                var oldPrefab = so.FindProperty("_particle");
                if (oldPrefab?.objectReferenceValue == null)
                {
                    empty++;
                    log.AppendLine($"  [없음] {record.name} ({record.Name}) — 옮길 이펙트 없음");
                    continue;
                }

                float startOffset = so.FindProperty("_effectStartOffset")?.floatValue ?? 0f;
                float speed = so.FindProperty("_effectSpeed")?.floatValue ?? 1f;
                float time = Mathf.Max(0f, record.ParticleOccurDelay + startOffset);

                particles.arraySize = 1;
                var e = particles.GetArrayElementAtIndex(0);
                e.FindPropertyRelative("Prefab").objectReferenceValue = oldPrefab.objectReferenceValue;
                e.FindPropertyRelative("Time").floatValue = time;
                e.FindPropertyRelative("Offset").vector3Value = so.FindProperty("_particleOffsetPos")?.vector3Value ?? Vector3.zero;
                e.FindPropertyRelative("Euler").vector3Value = so.FindProperty("_particleOffsetEuler")?.vector3Value ?? Vector3.zero;
                e.FindPropertyRelative("Speed").floatValue = speed > 0f ? speed : 1f;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(record);

                moved++;
                log.AppendLine($"  [이관] {record.name} ({record.Name}) ← " +
                               $"{oldPrefab.objectReferenceValue.name} @ {time:0.###}s");
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[이펙트 마이그레이션] 검사 {guids.Length} = 이관 {moved} / " +
                      $"건너뜀 {skipped} / 옮길 것 없음 {empty}\n{log}");
        }
    }
}
