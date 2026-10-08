using Fusion;
using Game.Networking;
using UnityEngine;

namespace Game.BossContent
{
    public class BossMotor
    {
        private readonly NetworkCharacterController _ncc;
        private readonly Transform _transform;
        private readonly NetworkRunner _runner;
        private readonly BossStats _stats;
        public Vector3 Position => _transform.position;
        public Vector3 Forward => _transform.forward;
        public Vector3 Up => _transform.up;

        public BossMotor(NetworkCharacterController ncc, Transform transform, NetworkRunner runner, BossStats stats)
        {
            _ncc = ncc;
            _transform = transform;
            _runner = runner;
            _stats = stats;
        }

        // 반환값: 실제로 이동했는가 (false = 정지거리 안이라 멈춤)
        public bool MoveToward(Vector3 targetPosition)
        {
            var dir = targetPosition - _transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude <= _stats.StopDistance * _stats.StopDistance)
            {
                Stop();
                if (dir.sqrMagnitude > 0.001f)
                    RotateToward(dir.normalized, _stats.RotationSpeed);
                return false;
            }

            dir.Normalize();
            _ncc.Move(dir * _stats.MoveSpeedMultiplier);
            RotateToward(dir, _stats.RotationSpeed);
            return true;
        }
        /// 지정 방향으로 직진(돌진 등) — 타겟 추적·정지거리 없다.
        /// NCC.Move는 인자를 정규화해 크기를 버리고 acceleration으로 램프업하므로, 그대로 두면
        /// 첫 틱부터 최고속이 아니라 서서히 붙어 사거리 = 속도×시간 이 어긋난다. 매 틱 Velocity를
        /// 상한값으로 미리 심어 램프를 건너뛴다(벽에 막혀 속도가 0이 돼도 다음 틱에 다시 최고속).
        /// maxSpeed 원복은 추격 상한(기본 2)에 영향을 주지 않기 위함.
        public void MoveInDirection(Vector3 direction, float maxSpeed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            Vector3 unit = direction.normalized;
            float previousMaxSpeed = _ncc.maxSpeed;
            _ncc.maxSpeed = maxSpeed;
            _ncc.Velocity = unit * maxSpeed;
            _ncc.Move(unit);
            _ncc.maxSpeed = previousMaxSpeed;
        }
        public void FaceToward(Vector3 targetPosition)
        {
            var dir = targetPosition - _transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                RotateToward(dir.normalized, _stats.RotationSpeed);   // 추격용 기본 속도
        }
        /// 회전 속도를 오버라이드해 조준(돌진 전조처럼 추격보다 빠르게 붙어야 할 때).
        /// rotationSpeed * DeltaTime 이 1을 넘으면 Slerp가 사실상 스냅이 된다.
        public void FaceToward(Vector3 targetPosition, float rotationSpeed)
        {
            var dir = targetPosition - _transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                RotateToward(dir.normalized, rotationSpeed);
        }
        public void Stop()
        {
            _ncc.Velocity = default;
            _ncc.Move(Vector3.zero);
        }

        public bool IsInRange(Vector3 targetPosition, float range)
        {
            var dir = targetPosition - _transform.position;
            dir.y = 0f;
            return dir.sqrMagnitude <= range * range;
        }

        /// 이번 틱의 이동량을 그대로 적용한다 — 클립 루트모션을 그대로 재현할 때 쓴다.
        ///
        /// MoveInDirection은 NCC의 Velocity·maxSpeed를 매 틱 덮어쓰고 가속 램프를 태우기 때문에,
        /// 매 틱 다른 거리를 요구하면 값이 흔들려 진동하고 등속으로 맞추면 미끄러진다.
        /// 이쪽은 CharacterController에 변위를 직접 넘겨 "한 걸음만큼 한 걸음" 나아가게 한다.
        /// 수평 성분만 쓰고, 지면에서 뜨지 않도록 살짝 아래로 눌러준다.
        public void MoveByDelta(Vector3 worldDelta)
        {
            worldDelta.y = 0f;
            if (worldDelta.sqrMagnitude < 0.0000001f) return;

            if (_characterController == null)
                _characterController = _ncc.GetComponent<CharacterController>();
            if (_characterController == null) return;

            _characterController.Move(worldDelta + GroundStick * Vector3.down);
        }

        private const float GroundStick = 0.02f;   // 경사·계단에서 붕 뜨지 않게 매 틱 눌러주는 양
        private CharacterController _characterController;

        private void RotateToward(Vector3 direction, float rotationSpeed)
        {
            _transform.rotation = Quaternion.Slerp(_transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * _runner.DeltaTime);
        }
    }
}