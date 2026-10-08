using UnityEngine;

namespace Game.BossContent
{
    /// TFW 연계 패턴이 공유하는 골격. 조각 클립 체인(_Active → _Active2 → ...)을 전제로
    /// 세 가지 공통 동작을 제공한다:
    ///
    ///  1. 조준 — 예고 동안 타겟을 계속 바라본다(SkeletonKing 패턴들과 같은 방식).
    ///     Active에 들어가면 방향이 굳어, 그때부터는 옆으로 빠져 피할 수 있다.
    ///  2. 조각별 전진 — 조각마다 클립 루트모션에서 뽑은 속도로 등속 이동한다(음수면 후진).
    ///     루트모션을 켜지 않는 이유는 애니메이터가 transform을 직접 움직이면
    ///     서버가 계산한 위치와 싸워서 보스가 떨리기 때문이다.
    ///  3. 조각 경계 통지 — 자식이 조각이 바뀌는 시점을 알 수 있다.
    public abstract class TfwComboPattern : BossPatternBase
    {
        /// 조각의 순 이동이 이보다 작으면 제자리 모션으로 보고 아예 손대지 않는다.
        private const float MinSegmentTravel = 0.15f;

        /// 한 틱에 이 이상은 움직이지 않는다 — 곡선이 튀어도 순간이동으로 번지지 않게 하는 안전장치.
        private const float MaxDeltaPerTick = 0.5f;

        /// Slerp 계수가 1을 넘어 한 틱에 회전이 끝나는 값(BossMotor.FaceToward 참고).
        private const float SnapRotationSpeed = 999f;

        // ── 조각별 재조준 ──────────────────────────────────────────────
        // 상용 액션게임(Hades·소울류)이 공유하는 관용구를 따른다:
        // 조각마다 예비동작 동안에만 타겟을 향해 돌고, 타격 직전에 조준을 끊는다.
        //
        // 왜 이 형태인가:
        //  · 아예 안 돌면 — 예전 동작 — 첫 조각 방향으로 5타를 다 휘두른다. 한 번 옆으로 빠지면
        //    나머지가 전부 허공이라 4~5타 연계가 1타 연계로 전락한다.
        //  · 끝까지 돌면 회피가 불가능하다. "안 읽히게 만들어 어렵게 하기"는 나쁜 난이도다.
        //  · 몸이 도는 게 보이고, 어느 순간 멈추는 것도 보여야 한다. 그 멈춤이 곧 "지금 빠져라"다.

        /// 재조준 Slerp 계수. 추격 회전(SO의 RotationSpeed, 기본 5)보다 조금 느리게 잡아
        /// "노려보며 따라 도는" 게 눈에 보이게 한다. 스냅이면 텔레그래프가 성립하지 않는다.
        private const float TrackRotationSpeed = 4f;

        /// 타격 이 시간 전에 조준을 끊는다(초, 배속 1 기준). 이 여유가 곧 회피 창이다.
        /// 늘리면 쉬워지고 줄이면 매워진다 — 난이도 다이얼은 여기 하나다.
        private const float CommitLeadSeconds = 0.35f;

        /// 조각 하나에서 돌 수 있는 총 각도. 크게 우회한 플레이어를 끝까지 따라가지 않게 막는다.
        /// 이 상한이 있어서 "매 틱 조준하면 원을 그리며 빙글빙글 돈다"던 예전 문제가 재발하지 않는다.
        private const float MaxTurnPerSegmentDegrees = 75f;

        private TfwTelegraphPlan _telegraph;
        private int _telegraphElapsed;
        private readonly ITfwComboDefinition _combo;
        private readonly float _activeSeconds;

        /// 조각별 타격 시각. SO의 getter가 호출마다 배열을 새로 만들기 때문에 여기서 한 번만 받아둔다
        /// (재조준 판단이 매 틱 도는 코드라 그대로 쓰면 틱마다 할당이 생긴다).
        private readonly float[] _segmentHitSeconds;

        /// 이번 조각에서 지금까지 돈 각도. 조각이 바뀔 때 0으로 되돌린다.
        private float _turnedThisSegment;

        /// Active 시작 후 흐른 틱. 자식이 타격 시점 판단에 쓴다.
        protected int ElapsedTicks { get; private set; }

        /// 다단히트 설정. 공용 PatternDefinition에는 없고 TFW 정의에만 있으므로,
        /// 호출마다 캐스팅하지 않도록 생성 때 한 번만 받아둔다.
        /// TFW 패턴은 전부 TfwPatternDefinition을 쓰지만, 아닌 SO가 꽂혀도 죽지 않게 null을 허용한다.
        protected TfwPatternDefinition Repeats { get; }

        private int _segmentIndex;      // 지금 재생 중인 조각 (-1 = 아직 Active 전)

        // 180도 돌기 전의 정면. 회전 조각을 벗어날 때 이 방향으로 되돌린다.
        private Vector3 _facingBeforeTurn;
        private bool _isTurnedAround;

        // 조각별 이동 속도(m/s). 0이면 제자리, 음수면 후진. 틱마다 판단하면 경계에서 흔들린다.
        private float[] _segmentSpeeds = { 0f };

        protected TfwComboPattern(PatternDefinition def, ITfwComboDefinition combo, BossServices services)
            : base(def, services)
        {
            _combo = combo;
            Repeats = def as TfwPatternDefinition;
            _activeSeconds = def.ActiveSeconds;
            _segmentHitSeconds = combo.SegmentShakeSeconds;
            BuildSegmentMotion();
        }

        /// 조각마다 "얼마나, 어느 쪽으로" 움직일지를 등속으로 확정한다.
        ///
        /// 곡선의 가감속을 틱마다 따라가면 MoveInDirection이 매 틱 NCC의 maxSpeed를 바꾸게 되고,
        /// 그 값이 흔들리면 NCC 내부의 가속·감속 처리와 부딪혀 보스가 진동한다.
        /// 조각 안에서 속도를 고정하면 그 여지가 사라진다 — 완급은 조각 단위로 표현된다.
        private void BuildSegmentMotion()
        {
            var starts = _combo.SegmentStartSeconds;
            var curve = _combo.ForwardDistanceCurve;
            if (starts == null || starts.Length == 0 || curve == null || curve.length < 2) return;

            _segmentSpeeds = new float[starts.Length];

            for (int i = 0; i < starts.Length; i++)
            {
                float start = starts[i];
                float end = i + 1 < starts.Length ? starts[i + 1] : _activeSeconds;
                float duration = end - start;
                float net = curve.Evaluate(end) - curve.Evaluate(start);

                // 순 이동이 거의 없는 조각(제자리 회전 등)은 아예 손대지 않는다.
                _segmentSpeeds[i] = (duration <= 0.01f || Mathf.Abs(net) < MinSegmentTravel)
                    ? 0f
                    : net / duration;   // 음수면 후진(백스텝)
            }
        }

        protected sealed override void OnBegin()
        {
            ElapsedTicks = 0;
            _segmentIndex = -1;
            _isTurnedAround = false;
            _turnedThisSegment = 0f;
            _telegraph = new TfwTelegraphPlan(Definition, Services);
            _telegraphElapsed = -ToTicks(Definition.TelegraphSeconds);
            _telegraph.Tick(_telegraphElapsed, SpeedMultiplier);
            OnComboBegin();
        }

        /// 예고 동안 타겟을 계속 바라본다 — SkeletonKing 패턴들과 같다.
        /// 큰 방향 전환은 여기서 다 끝낸다. Active의 재조준은 그 위에 얹는 미세 보정이다.
        protected sealed override void OnTelegraphTick()
        {
            _telegraph.Tick(++_telegraphElapsed, SpeedMultiplier);
            OnComboTelegraphTick(_telegraphElapsed);
            if (Services.Sensor.TryGetTargetPosition(out var target))
                Services.Motor.FaceToward(target);
        }

        protected sealed override void OnActiveTick()
        {
            ElapsedTicks++;
            UpdateSegment();
            TrackTarget();
            ApplyForwardCurve();
            _telegraph.Tick(ElapsedTicks, SpeedMultiplier);
            OnComboActiveTick();
        }

        /// 이번 조각의 타격이 아직 남아 있으면 타겟 쪽으로 조금씩 돈다.
        ///
        /// 세 가지가 동시에 참일 때만 돈다:
        ///   1. 후방 조각이 아니다 — 180도 회전 조각은 그 회전이 방향을 소유한다.
        ///   2. 이번 조각의 회전 예산이 남았다.
        ///   3. 아직 커밋 시점 전이다 — 타격 직전엔 방향이 굳어야 회피가 성립한다.
        ///
        /// 이동은 Forward를 따르므로 이 회전이 돌진 경로도 함께 휘게 한다. 그게 의도다 —
        /// 옆으로 빠지는 플레이어를 몸으로 따라붙는 그림이 나온다. 예전 주석이 걱정하던
        /// "빙글빙글 도는" 실패는 무제한 회전일 때 얘기고, 여기는 예산과 창이 둘 다 막는다.
        private void TrackTarget()
        {
            if (_isTurnedAround) return;
            if (_turnedThisSegment >= MaxTurnPerSegmentDegrees) return;
            if (!IsBeforeCommitPoint()) return;
            if (!Services.Sensor.TryGetTargetPosition(out var target)) return;

            Vector3 before = Services.Motor.Forward;

            // 배속에 곱한다. FaceToward의 계수는 "틱당" 회전량인데 배속이 걸리면 Active 틱 수가
            // 줄어들기 때문에(ToTicks), 그대로 두면 빠른 페이즈일수록 덜 돌아 추적이 약해진다 —
            // 뒤 페이즈가 더 위협적이어야 하므로 방향이 거꾸로다.
            // SunderingDash가 DashSpeed에 같은 이유로 같은 값을 곱한다.
            Services.Motor.FaceToward(target, TrackRotationSpeed * SpeedMultiplier);

            _turnedThisSegment += Vector3.Angle(before, Services.Motor.Forward);
        }

        /// 이번 조각의 타격까지 아직 커밋 여유가 남았는가.
        ///
        /// 조각별 타격 시각(SegmentShakeSeconds)을 그대로 쓴다 — 화면 흔들림이 터지는 순간이
        /// 곧 칼이 닿는 순간이라, 조준을 끊을 기준으로 이미 맞는 값이다. 새로 채울 데이터가 없다.
        ///
        /// 타격이 조각 시작 직후인 조각(360도 회전베기 등)은 창이 0 이하로 계산돼 자연히 안 돈다 —
        /// 제자리에서 도는 모션이 타겟을 따라 또 도는 이중 회전을 막아준다.
        private bool IsBeforeCommitPoint()
        {
            if (_segmentHitSeconds == null ||
                _segmentIndex < 0 || _segmentIndex >= _segmentHitSeconds.Length) return false;

            // 커밋 여유만 ToTicks 밖에서 잰다 — 여기 넣으면 배속으로 나뉘어 함께 짧아진다.
            //
            // 타격 0.35초 전부터 추가 회전을 멈춘다. 회피 성공은 범위와 이동·무적 프레임에 달려 있다.
            // 회피 지속·무적 프레임은 배속과 무관한 실시간 값이다. 같이 줄이면 3페이즈에서
            // 그 여유가 0.175초로 무너져, 이미 시작한 회피를 보스가 따라와 잡는다.
            //
            // 타격 시각 자체는 ToTicks로 배속에 맞춘다. 그래서 배속이 오를수록 조준 창만 좁아지고
            // 회피 보장은 그대로다 — 뒤 페이즈가 매워지는 방향이 이쪽이 맞다.
            int leadTicks = Mathf.RoundToInt(CommitLeadSeconds * Services.Stats.TickRate);
            return ElapsedTicks < ToTicks(_segmentHitSeconds[_segmentIndex]) - leadTicks;
        }

        /// Active 종료. 타격은 전부 OnComboActiveTick이 하고, 여기선 남은 속도를 끊는다 —
        /// MoveInDirection이 NCC에 속도를 심으므로 안 끊으면 Recovery 내내 미끄러진다.
        protected sealed override void OnActiveHit()
        {
            RestoreFacing();
            Services.Motor.Stop();
        }

        /// 조각이 바뀌면 필요한 경우 보스를 뒤로 돌리고, 자식에게 알린다.
        private void UpdateSegment()
        {
            var starts = _combo.SegmentStartSeconds;
            if (starts == null || starts.Length == 0) return;

            int current = 0;
            for (int i = 0; i < starts.Length; i++)
            {
                if (ElapsedTicks < ToTicks(starts[i])) break;
                current = i;
            }

            if (current == _segmentIndex) return;

            _segmentIndex = current;
            _turnedThisSegment = 0f;   // 조각마다 회전 예산을 새로 준다

            if (IsTurnAroundSegment(current)) TurnAround();
            else RestoreFacing();

            OnSegmentEnter(current);
        }

        private bool IsTurnAroundSegment(int segmentIndex)
        {
            var turns = _combo.TurnAroundSegments;
            if (turns == null) return false;

            for (int i = 0; i < turns.Length; i++)
                if (turns[i] == segmentIndex) return true;
            return false;
        }

        /// 보스를 그 자리에서 180도 돌린다.
        ///
        /// 후방 타격 전용 모션을 따로 만들지 않아도 되게 하는 장치다 —
        /// 보스가 돌아버리면 "앞으로 베는" 클립이 그대로 뒤를 치는 그림이 된다.
        /// 판정도 그때부터 Forward 기준이라 코드가 방향을 뒤집을 필요가 없다.
        ///
        /// 회전 속도를 크게 줘 한 틱에 끝낸다(BossMotor.FaceToward 주석대로 Slerp가 스냅이 된다).
        /// 도는 모션이 없으니 어차피 순간 전환으로 보이는 게 자연스럽다.
        private void TurnAround()
        {
            if (_isTurnedAround) return;   // 회전 조각이 연달아 붙어 있으면 돈 채로 이어간다

            _facingBeforeTurn = Services.Motor.Forward;
            _isTurnedAround = true;

            Vector3 behind = Services.Motor.Position - Services.Motor.Forward * 10f;
            Services.Motor.FaceToward(behind, SnapRotationSpeed);
        }

        /// 회전 조각을 벗어나면 돌기 전 정면으로 되돌린다.
        ///
        /// 이게 없으면 후속 조각들이 돌아간 Forward를 그대로 물려받아
        /// "그 조각만 후방"이 아니라 연계 전체가 뒤를 치게 된다.
        private void RestoreFacing()
        {
            if (!_isTurnedAround) return;

            _isTurnedAround = false;
            Services.Motor.FaceToward(Services.Motor.Position + _facingBeforeTurn * 10f, SnapRotationSpeed);
        }

        /// 곡선이 이번 틱에 나아간 만큼만 옮긴다 — 클립이 한 걸음 가면 보스도 한 걸음 간다.
        /// 속도가 아니라 변위를 넘기므로 NCC의 가속 램프·maxSpeed 클램프를 타지 않고,
        /// 그래서 매 틱 값이 달라져도 진동하지 않는다.
        private void ApplyForwardCurve()
        {
            if (!_combo.UseForwardMotion) return;   // 꺼두면 제자리에서만 패턴을 친다

            var curve = _combo.ForwardDistanceCurve;
            if (curve == null || curve.length < 2) return;

            // 제자리 조각(360도 회전 등)은 손대지 않는다 — 곡선의 미세한 요동을 따라갈 이유가 없다.
            if (Mathf.Approximately(_segmentSpeeds[Mathf.Clamp(_segmentIndex, 0, _segmentSpeeds.Length - 1)], 0f))
                return;

            // 곡선은 배속 1 기준이다. 진행도(0~1)로 환산해 훑으면 배속이 얼마든 곡선 전체를 한 번 지나간다.
            int totalTicks = Mathf.Max(1, ToTicks(_activeSeconds));
            float now  = _activeSeconds * Mathf.Clamp01((float)ElapsedTicks / totalTicks);
            float prev = _activeSeconds * Mathf.Clamp01((float)(ElapsedTicks - 1) / totalTicks);

            float delta = Mathf.Clamp(curve.Evaluate(now) - curve.Evaluate(prev), -MaxDeltaPerTick, MaxDeltaPerTick);
            if (Mathf.Abs(delta) < 0.0001f) return;

            delta = LimitToTarget(delta);
            if (Mathf.Abs(delta) < 0.0001f) return;

            Services.Motor.MoveByDelta(Services.Motor.Forward * delta);
        }

        /// 타겟을 지나쳐 달려가지 않게 이번 틱의 전진량을 깎는다.
        ///
        /// 클립들은 무대를 가로지르라고 만들어진 모션이라 연계 하나가 최대 13m를 나아간다.
        /// 그런데 보스는 StopDistance(2m)까지 붙어서 패턴을 시작하므로, 그대로 두면
        /// 플레이어를 10m 넘게 지나쳐 등 뒤에서 허공을 때리고 다시 걸어 돌아온다.
        ///
        /// 곡선을 건드리지 않고 도달점만 막으므로 클립의 가감속·완급은 그대로 남는다.
        /// 후진(백스텝)과 타겟을 등지고 가는 조각은 제한하지 않는다 — 거리가 벌어지는 방향이라 지나칠 일이 없다.
        private float LimitToTarget(float delta)
        {
            if (delta <= 0f) return delta;
            if (!Services.Sensor.TryGetTargetPosition(out var target)) return delta;

            Vector3 toTarget = target - Services.Motor.Position;
            toTarget.y = 0f;

            if (Vector3.Dot(toTarget, Services.Motor.Forward) <= 0f) return delta;   // 등지고 가는 중

            float remaining = toTarget.magnitude - Services.Stats.StopDistance;
            return Mathf.Min(delta, Mathf.Max(0f, remaining));
        }

        /// 다단히트를 반영해 "지금 터져야 할 타격"을 한 번에 하나씩 돌려준다.
        ///
        /// 쓰는 쪽은 while로 돌린다 — 한 틱 안에 여러 타가 몰릴 수 있기 때문이다.
        /// 간격이 틱보다 짧거나 프레임이 밀리면 두 타 이상이 같은 틱에 도달하는데,
        /// if로 한 번만 처리하면 그 타들이 조용히 사라진다.
        ///
        ///   while (TryHit(ref _spin, nameof(_def.SpinAtSeconds), _def.SpinAtSeconds, _def.SpinDamage, out int dmg))
        ///       TfwHits.Sphere(center, radius, dmg, Definition.HitCC);
        ///
        /// 다단히트 설정이 없으면 총 1회만 true를 돌려주므로 기존 동작과 같다.
        /// fired는 자식이 들고 있는 "이 타격을 몇 번 쐈나" 카운터다(연계 시작 때 0으로 되돌린다).
        protected bool TryHit(ref int fired, string timingKey, float baseSeconds, int totalDamage, out int damage)
        {
            damage = 0;

            int count = Repeats.EffectiveHitCount(timingKey, baseSeconds);
            if (fired >= count) return false;

            float at = Repeats.RepeatTimeAt(timingKey, baseSeconds, fired);
            if (ElapsedTicks < ToTicks(at)) return false;

            damage = TfwPatternDefinition.DistributeDamage(totalDamage, count, fired);
            fired++;
            return true;
        }

        protected virtual void OnComboBegin() { }
        protected virtual void OnComboTelegraphTick(int elapsedTicks) { }

        /// 조각이 바뀐 첫 틱에 불린다(0-based). 자식이 조각별 연출·상태를 다룰 때 쓴다.
        protected virtual void OnSegmentEnter(int segmentIndex) { }

        /// Active 매 틱. 자식은 ElapsedTicks를 보고 타격 시점을 판단한다.
        protected abstract void OnComboActiveTick();
    }

    /// 조각 타이밍을 다루는 잔심부름. 각 SO가 SegmentShakeSeconds를 채울 때 쓴다.
    public static class TfwComboTiming
    {
        /// 타격이 없는 조각의 흔들림 시각 — 그 조각의 시작으로 대신한다.
        public static float SegmentStart(float[] starts, int index)
            => starts != null && index >= 0 && index < starts.Length ? starts[index] : 0f;
    }

    /// 연계 패턴 SO가 베이스에 넘겨주는 값들. PatternDefinition(공유 파일)을 건드리지 않으려고
    /// TFW 쪽에서 인터페이스로 얹었다.
    public interface ITfwComboDefinition
    {
        /// 클립 루트모션을 따라 앞뒤로 움직일지. 끄면 제자리에서만 패턴을 친다.
        bool UseForwardMotion { get; }

        /// 각 Active 조각이 시작하는 시각(초). 베이커가 클립 길이를 누적해 채운다.
        float[] SegmentStartSeconds { get; }

        /// 이 인덱스(0-based)의 조각에 들어갈 때 보스를 180도 돌린다.
        /// 후방 타격 모션을 따로 구하지 않고 앞으로 쓰는 클립을 그대로 재활용하기 위한 장치다.
        int[] TurnAroundSegments { get; }

        /// 조각별로 화면을 흔들 시각(초, Active 시작 기준). 인덱스는 SegmentStartSeconds와 나란하다.
        /// 조각 시작이 아니라 그 조각이 실제로 때리는 순간이다 — 칼을 드는 시점에 흔들면 한 박자 이르다.
        /// 타격이 없는 조각은 그 조각의 시작 시각을 넣어 순서만 지킨다(세기가 None이라 실제로 쓰이진 않는다).
        float[] SegmentShakeSeconds { get; }

        /// Active 시작 후 시각(x, 초) → 그때까지 나아간 누적 거리(y, m).
        /// 베이커가 조각 클립들의 루트모션 곡선을 이어붙여 만든다. 내려가는 구간은 후진(백스텝).
        AnimationCurve ForwardDistanceCurve { get; }
    }
}
