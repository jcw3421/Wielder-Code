using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>애니메이션 이벤트로 플레이어 SFX를 낸다. 이벤트의 Object 파라미터에 AudioClip을 꽂으면
    /// 그 시점에 그 클립이 재생된다 — 타이밍이 클립 타임라인에 박혀 있어 동작과 100% 동기다.
    /// (보스 VFX가 BossEffectSpawner에서 같은 방식으로 켜진다 — 같은 결의 배선이다.)
    ///
    /// 애니메이션 이벤트는 Animator가 있는 GameObject의 컴포넌트에서 함수를 찾으므로
    /// 이 컴포넌트는 아바타 루트(Animator와 같은 오브젝트)에 있어야 한다.
    ///
    /// 재생은 내 아바타에서만 한다. SoundManager의 SFX 소스는 2D(spatialBlend = 0)라
    /// 남의 아바타 소리도 거리와 무관하게 같은 크기로 들리고, 4인 난전에서 그대로 겹친다.
    ///
    /// 순수 로컬 연출이라 네트워크 상태를 건드리지 않는다 — 애니메이터는 모든 피어에서
    /// 같은 클립을 돌리므로, 서버가 소리를 알릴 필요도 없다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationSound : MonoBehaviour
    {
        private NetworkObject _networkObject;
        private bool _lookedUp;

        /// <summary>애니메이션 이벤트에서 호출한다. Object 파라미터 = 재생할 AudioClip.</summary>
        public void PlaySfx(AnimationEvent evt)
        {
            if (evt.objectReferenceParameter is not AudioClip clip) return;
            if (!IsLocalAvatar()) return;
            if (!ServiceLocator.TryGet<ISoundManager>(out var sound)) return;

            sound.Play(clip, SoundType.SFX);
        }

        /// 스폰 전에는 권한이 확정되지 않으므로 처음 이벤트가 올 때 찾는다.
        /// 세션 밖(로비·연습 씬 등 NetworkObject가 없는 아바타)에서는 내 것으로 본다.
        private bool IsLocalAvatar()
        {
            if (!_lookedUp)
            {
                _networkObject = GetComponentInParent<NetworkObject>();
                _lookedUp = true;
            }

            return _networkObject == null || _networkObject.HasInputAuthority;
        }
    }
}
