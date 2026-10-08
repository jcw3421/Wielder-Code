using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public sealed class DamageMeterPresenter : MonoBehaviour
    {
        private const float RefreshInterval = 0.1f;
        private const int MaxRows = 4;
        private static readonly int TypeCount = System.Enum.GetValues(typeof(DamageType)).Length;
        private static readonly string[] TypeNames = { "평타", "스킬", "궁극기", "풀체인", "환경" };

        [SerializeField] private DamageMeterView _view;
        private readonly Dictionary<int, Summary> _damage = new Dictionary<int, Summary>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private readonly List<KeyValuePair<int, Summary>> _sorted = new List<KeyValuePair<int, Summary>>(4);
        private readonly List<DamageMeterRow> _rows = new List<DamageMeterRow>(MaxRows);
        private readonly List<DamageMeterDetailRow> _detailRows = new List<DamageMeterDetailRow>(32);
        private bool _showDetails;
        private string _bossName;
        private int _currentBossId = -1;
        private double _now;
        private float _nextRefresh;

        private sealed class Summary
        {
            public long Total;
            public int Hits;
            public double FirstHitTime;
            public readonly long[] TypeTotals = new long[TypeCount];
            public readonly int[] TypeHits = new int[TypeCount];
            public readonly long[] TypeMax = new long[TypeCount];
        }

        private void Awake() { if (_view == null) _view = GetComponentInChildren<DamageMeterView>(true); }
        private void OnEnable()
        {
            GameplayEvents.DamageDealt += OnDamage;
            GameplayEvents.NicknameChanged += OnNickname;
            GameplayEvents.OnBossSpawned += OnBossSpawned;
            GameplayEvents.OnBossDespawned += OnBossDespawned;
            _view.CollapseRequested += ToggleCollapsed;
            _view.DetailRequested += ToggleDetails;
            _view.ResetRequested += ResetMeter;
        }
        private void OnDisable()
        {
            GameplayEvents.DamageDealt -= OnDamage;
            GameplayEvents.NicknameChanged -= OnNickname;
            GameplayEvents.OnBossSpawned -= OnBossSpawned;
            GameplayEvents.OnBossDespawned -= OnBossDespawned;
            _view.CollapseRequested -= ToggleCollapsed;
            _view.DetailRequested -= ToggleDetails;
            _view.ResetRequested -= ResetMeter;
        }

        private void LateUpdate()
        {
            if (_view.IsCollapsed) return;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private void OnDamage(int targetId, int amount, Vector3 position, DamageType type, int sourceId)
        {
            if (sourceId < 0 || amount <= 0) return;
            string targetBossName;
            if (ServiceLocator.TryGet<IBossDB>(out var database) && database.IsLoaded)
            {
                if (!database.TryGet(targetId, out var boss)) return;
                targetBossName = boss.Name;
            }
            else
            {
                if (targetId != _currentBossId) return;
                targetBossName = $"BOSS #{targetId}";
            }

            // 표적이 바뀌었을 때만 판을 비운다. 허수아비처럼 체력바가 숨으면서 Despawn을 쏘는 대상은
            // _currentBossId를 -1로 되돌리는데, 그걸 "새 표적"으로 치면 다음 타격에 기록이 통째로 날아간다.
            if (targetId != _currentBossId)
            {
                if (_currentBossId >= 0) _damage.Clear();
                _currentBossId = targetId;
            }
            _bossName = targetBossName;

            Summary summary = Ensure(sourceId);
            summary.Total += amount;
            summary.Hits++;
            int index = (int)type;
            if (index < 0 || index >= TypeCount) return;
            summary.TypeTotals[index] += amount;
            summary.TypeHits[index]++;
            if (amount > summary.TypeMax[index]) summary.TypeMax[index] = amount;
        }

        private Summary Ensure(int playerId)
        {
            if (_damage.TryGetValue(playerId, out var summary)) return summary;
            summary = new Summary { FirstHitTime = Time.timeAsDouble };
            _damage.Add(playerId, summary);
            return summary;
        }

        private void OnNickname(int playerId, string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) _names.Remove(playerId);
            else _names[playerId] = nickname;
        }
        private void OnBossSpawned(int bossId)
        {
            _damage.Clear();
            _currentBossId = bossId;
            _bossName = ResolveBossName(bossId);
        }
        private void OnBossDespawned(int bossId)
        {
            if (bossId != _currentBossId) return;
            _currentBossId = -1;
        }
        private void ToggleCollapsed() => _view.SetCollapsed(!_view.IsCollapsed);
        // 상세/간단은 보기 설정이다. 전투 데이터가 어떻게 바뀌든 여기서만 뒤집힌다.
        private void ToggleDetails() { _showDetails = !_showDetails; Refresh(); }
        private void ResetMeter()
        {
            _damage.Clear();
            if (_currentBossId >= 0) _bossName = ResolveBossName(_currentBossId);
            Refresh();
        }

        private void Refresh()
        {
            _now = Time.timeAsDouble;
            _sorted.Clear();
            foreach (var pair in _damage) _sorted.Add(pair);
            _sorted.Sort((a, b) => Dps(b.Value).CompareTo(Dps(a.Value)));

            long partyTotal = 0;
            double firstHit = _now;
            foreach (var pair in _sorted)
            {
                partyTotal += pair.Value.Total;
                if (pair.Value.FirstHitTime < firstHit) firstHit = pair.Value.FirstHitTime;
            }

            _rows.Clear();
            _detailRows.Clear();
            for (int i = 0; i < _sorted.Count && i < MaxRows; i++)
            {
                int id = _sorted[i].Key;
                Summary summary = _sorted[i].Value;
                string nickname = _names.TryGetValue(id, out var value) ? value : $"Player {id}";
                float share = partyTotal > 0 ? summary.Total / (float)partyTotal : 0f;

                _rows.Add(new DamageMeterRow
                {
                    Rank = (i + 1).ToString(CultureInfo.InvariantCulture),
                    Name = nickname,
                    Percent = $"{Mathf.RoundToInt(share * 100f)}%",
                    Dps = Compact((long)Dps(summary)),
                    Damage = Compact(summary.Total),
                    Share = share,
                });

                if (_showDetails) AppendDetails(i, nickname, summary);
            }

            _view.SetContent(BuildTitle(_now - firstHit), _rows, _detailRows, _showDetails);
        }

        private void AppendDetails(int colorIndex, string nickname, Summary summary)
        {
            _detailRows.Add(new DamageMeterDetailRow
            {
                IsHeader = true,
                ColorIndex = colorIndex,
                Label = nickname,
                Amount = Compact(summary.Total),
                Share = "100%",
                Average = Compact(Average(summary.Total, summary.Hits)),
                Max = string.Empty,
                Hits = summary.Hits.ToString(CultureInfo.InvariantCulture),
                Fill = 1f,
            });

            for (int type = 0; type < TypeCount; type++)
            {
                if (summary.TypeHits[type] == 0) continue;
                float share = summary.Total > 0 ? summary.TypeTotals[type] / (float)summary.Total : 0f;
                _detailRows.Add(new DamageMeterDetailRow
                {
                    ColorIndex = -1,
                    Label = type < TypeNames.Length ? TypeNames[type] : ((DamageType)type).ToString(),
                    Amount = Compact(summary.TypeTotals[type]),
                    Share = $"{Mathf.RoundToInt(share * 100f)}%",
                    Average = Compact(Average(summary.TypeTotals[type], summary.TypeHits[type])),
                    Max = Compact(summary.TypeMax[type]),
                    Hits = summary.TypeHits[type].ToString(CultureInfo.InvariantCulture),
                    Fill = share,
                });
            }
        }

        private string BuildTitle(double elapsed)
        {
            string name = string.IsNullOrWhiteSpace(_bossName) ? "DAMAGE" : _bossName;
            int seconds = Mathf.Max(0, Mathf.FloorToInt((float)elapsed));
            return $"{name} · {seconds / 60:00}:{seconds % 60:00}";
        }

        private double Dps(Summary summary) => summary.Total / System.Math.Max(1d, _now - summary.FirstHitTime);
        private static long Average(long total, int count) => count > 0 ? total / count : 0L;

        /// 좁은 패널에 들어가도록 자릿수를 접는다. 1.2k / 34.5M 꼴.
        private static string Compact(long value)
        {
            if (value < 10000L) return value.ToString("N0", CultureInfo.InvariantCulture);
            if (value < 1000000L) return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            if (value < 1000000000L) return (value / 1000000d).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            return (value / 1000000000d).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        }

        private static string ResolveBossName(int bossId)
        {
            if (ServiceLocator.TryGet<IBossDB>(out var database) && database.IsLoaded &&
                database.TryGet(bossId, out var boss))
                return boss.Name;
            return $"BOSS #{bossId}";
        }
    }
}
