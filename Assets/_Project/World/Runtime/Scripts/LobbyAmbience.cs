using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Dresses the lobby village at runtime: swaps the moat to the animated water
    /// material and spawns chimney smoke, ambient motes and portal beams. The chimney
    /// tops and scattered props themselves are authored in the scene, not here.
    /// Purely presentational and client-local — it never touches networked state, so it
    /// runs the same on host and clients without any authority handshake.
    /// Smoke placement is read from the "_chim" objects in the scene, so moving a
    /// chimney in the editor moves its plume with it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyAmbience : MonoBehaviour
    {
        const string GroundColliderName = "Collider_Ground";
        const string ChimneyNameFragment = "_chim";
        const string WaterNamePrefix = "ice_cube";
        const string PortalRootName = "PortalCorridor";
        const string PortalSignPrefix = "Sign_";

        [Header("Scene")]
        [Tooltip("CubeWorld_LobbyVillage. Left empty, it is looked up by name.")]
        [SerializeField] Transform villageRoot;
        [Tooltip("PortalCorridor. Left empty, it is looked up by name.")]
        [SerializeField] Transform portalRoot;
        [SerializeField] int seed = 20260829;

        [Header("Materials")]
        [SerializeField] Material waterMaterial;
        [SerializeField] Material smokeMaterial;
        [SerializeField] Material emberMaterial;

        [Header("Water")]
        [SerializeField] bool applyWater = true;

        [Header("Chimney Smoke")]
        [SerializeField] bool buildChimneySmoke = true;
        [Range(0f, 1f)]
        [SerializeField] float smokeBuildingRatio = 0.55f;
        [SerializeField] float smokeHeightOffset = 0.2f;
        [Tooltip("Multiplies puff size relative to the chimney width.")]
        [SerializeField] float smokeScale = 1.6f;
        [SerializeField] Color smokeColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] Vector2 smokeDrift = new Vector2(0.35f, 0.12f);

        [Header("Ambient Motes")]
        [SerializeField] bool buildAmbientMotes = true;
        [SerializeField] float moteRate = 18f;
        [SerializeField] Color moteColor = new Color(1f, 0.94f, 0.7f, 0.5f);

        [Header("Portal Beams")]
        [SerializeField] bool buildPortalBeams = true;
        [SerializeField] float portalBeamHeight = 4.5f;
        [SerializeField] Color[] portalBeamColors =
        {
            new Color(1f, 0.72f, 0.28f, 1f),
            new Color(0.35f, 0.85f, 1f, 1f),
            new Color(0.85f, 0.35f, 0.95f, 1f),
        };

        // guardrail:allow RNG — seed 필드로 고정된 결정론 RNG. 로비 연출 전용이라 네트워크 상태와 무관하다.
        System.Random _rng;
        Transform _root;
        BoxCollider _ground;
        float _groundY;

        void Awake()
        {
            // guardrail:allow RNG — seed 필드로 고정된 결정론 RNG. 로비 연출 전용이라 네트워크 상태와 무관하다.
            _rng = new System.Random(seed);

            if (villageRoot == null)
            {
                GameObject found = GameObject.Find("CubeWorld_LobbyVillage");
                villageRoot = found != null ? found.transform : null;
            }

            if (villageRoot == null)
            {
                Debug.LogWarning("[LobbyAmbience] CubeWorld_LobbyVillage not found; nothing to dress.", this);
                enabled = false;
                return;
            }

            if (portalRoot == null)
            {
                GameObject found = GameObject.Find(PortalRootName);
                portalRoot = found != null ? found.transform : null;
            }

            CollectColliders();

            _root = new GameObject("~LobbyAmbience (Runtime)").transform;
            _root.SetParent(transform, false);

            if (applyWater) ApplyMaterialByNamePrefix(WaterNamePrefix, waterMaterial);
            if (buildChimneySmoke) BuildChimneySmoke();
            if (buildAmbientMotes) BuildAmbientMotes();
            if (buildPortalBeams) BuildPortalBeams();
        }

        void CollectColliders()
        {
            BoxCollider[] boxes = villageRoot.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].name == GroundColliderName)
                {
                    _ground = boxes[i];
                    break;
                }
            }

            _groundY = _ground != null ? _ground.bounds.max.y : villageRoot.position.y;
        }

        /// <summary>
        /// Retargets every renderer under objects whose own name starts with
        /// <paramref name="namePrefix"/>. The prefix has to be matched on the prefab root
        /// rather than the renderer, because these assets keep their meshes on
        /// LOD children with unrelated names (ice_cube_1 -> Ice_block_1_LOD0).
        /// </summary>
        void ApplyMaterialByNamePrefix(string namePrefix, Material material)
        {
            if (material == null)
            {
                Debug.LogWarning($"[LobbyAmbience] No material assigned for '{namePrefix}'; skipping.", this);
                return;
            }

            int swapped = 0;
            Transform[] transforms = villageRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (!transforms[i].name.StartsWith(namePrefix)) continue;

                Renderer[] renderers = transforms[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] slots = renderers[r].sharedMaterials;
                    for (int s = 0; s < slots.Length; s++) slots[s] = material;
                    renderers[r].sharedMaterials = slots;
                    swapped++;
                }
            }

            if (swapped == 0)
            {
                Debug.LogWarning($"[LobbyAmbience] No objects named '{namePrefix}*' found.", this);
            }
        }

        void BuildChimneySmoke()
        {
            // Chimneys are authored as stacked pieces named "<building>_chim1/_chim2", so
            // group by the building prefix and keep whichever piece sits highest — smoke
            // has to leave the top of the stack, not the middle of it.
            // The "_chim" name is on the prefab root, which carries no renderer of its own
            // (the mesh is on block_*_LOD0/1/2 children), so this walks transforms and
            // measures their children rather than searching renderer names.
            Dictionary<string, Bounds> chimneyTops = new Dictionary<string, Bounds>();

            Transform[] transforms = villageRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string transformName = transforms[i].name;
                int fragment = transformName.IndexOf(ChimneyNameFragment, System.StringComparison.Ordinal);
                if (fragment < 0) continue;

                Bounds bounds = GetRendererBounds(transforms[i].gameObject);
                if (bounds.size == Vector3.zero) continue;

                string key = transformName.Substring(0, fragment);
                if (!chimneyTops.TryGetValue(key, out Bounds current) || bounds.max.y > current.max.y)
                {
                    chimneyTops[key] = bounds;
                }
            }

            if (chimneyTops.Count == 0)
            {
                Debug.LogWarning($"[LobbyAmbience] No '{ChimneyNameFragment}' objects found; no chimneys built.", this);
                return;
            }

            foreach (KeyValuePair<string, Bounds> chimney in chimneyTops)
            {
                Bounds bounds = chimney.Value;
                float unit = Mathf.Max(bounds.size.x, bounds.size.z);

                // Deliberately leaves some chimneys cold so the village does not read as
                // every single house burning at once.
                if (NextFloat() > smokeBuildingRatio) continue;

                Vector3 vent = new Vector3(
                    bounds.center.x,
                    bounds.max.y + smokeHeightOffset,
                    bounds.center.z);

                CreateSmoke($"Smoke_{chimney.Key}", vent, unit);
            }
        }

        /// <summary>
        /// Builds the chosen silhouette on top of the existing brick stub and returns the
        void CreateSmoke(string objectName, Vector3 position, float chimneyWidth)
        {
            ParticleSystem ps = CreateParticleSystem(objectName, position, smokeMaterial);

            float puffSize = Mathf.Max(chimneyWidth, 0.4f) * smokeScale;

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(puffSize * 0.45f, puffSize * 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = smokeColor;
            // Density is what separates a plume from a few floating blobs, so this runs a
            // high particle count at low per-particle alpha and lets them accumulate.
            main.maxParticles = 220;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 26f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f;
            shape.radius = puffSize * 0.18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 2.6f));

            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(SmokeGradient());

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // All three axes have to share a curve mode, so y is given an explicit
            // two-constant range as well even though it stays at zero.
            velocity.x = new ParticleSystem.MinMaxCurve(smokeDrift.x * 0.6f, smokeDrift.x * 1.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(smokeDrift.y * 0.6f, smokeDrift.y * 1.4f);

            // Turbulence, damped so puffs slow as they expand rather than shooting off.
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.55f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.35f;
            noise.octaveCount = 2;
            noise.damping = true;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            // The smoke shader needs each particle's age and a stable per-particle seed;
            // without these every puff would erode with the identical noise pattern.
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent,
                ParticleSystemVertexStream.StableRandomX,
            });

            ps.Play();
        }

        static Gradient SmokeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    // Hot and dark leaving the flue, cooling to pale as it disperses.
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.95f, 0.95f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.75f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        void BuildAmbientMotes()
        {
            if (_ground == null) return;

            Bounds bounds = _ground.bounds;
            Vector3 centre = new Vector3(bounds.center.x, _groundY + 1.6f, bounds.center.z);

            ParticleSystem ps = CreateParticleSystem("AmbientMotes", centre, emberMaterial);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = moteColor;
            main.maxParticles = Mathf.CeilToInt(moteRate * 9f);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = moteRate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x * 0.9f, 3f, bounds.size.z * 0.9f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeInOutGradient());

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.15f;

            ps.Play();
        }

        void BuildPortalBeams()
        {
            if (portalRoot == null) return;

            Transform[] children = portalRoot.GetComponentsInChildren<Transform>(true);
            int beamIndex = 0;

            for (int i = 0; i < children.Length; i++)
            {
                if (!children[i].name.StartsWith(PortalSignPrefix)) continue;

                Color beamColor = portalBeamColors != null && portalBeamColors.Length > 0
                    ? portalBeamColors[beamIndex % portalBeamColors.Length]
                    : Color.white;

                CreatePortalBeam($"PortalBeam_{children[i].name}", children[i].position, beamColor);
                beamIndex++;
            }
        }

        void CreatePortalBeam(string objectName, Vector3 position, Color beamColor)
        {
            position.y = _groundY;

            ParticleSystem ps = CreateParticleSystem(objectName, position, emberMaterial);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                portalBeamHeight / 3.4f, portalBeamHeight / 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            beamColor.a = 0.55f;
            main.startColor = beamColor;
            main.maxParticles = 28;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ps.Play();

            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(ps.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            light.type = LightType.Point;
            light.range = 5.5f;
            light.intensity = 0.9f;
            light.color = beamColor;
            light.shadows = LightShadows.None;
        }

        ParticleSystem CreateParticleSystem(string objectName, Vector3 position, Material material,
                                            Transform parent = null)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.position = position;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // AddComponent starts the system immediately, and modules such as duration
            // reject writes while it is playing. Callers re-Play once configured.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;

            return ps;
        }

        float NextFloat()
        {
            return (float)_rng.NextDouble();
        }

        static Bounds GetRendererBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static Gradient FadeInOutGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        static Gradient FadeOutGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }
    }
}
