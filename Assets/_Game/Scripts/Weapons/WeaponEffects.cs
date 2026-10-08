using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectElimination.Weapons
{
    // Resolve the visual muzzle after ADS, recoil and procedural animation have posed the gun.
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class WeaponEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Material effectsMaterial;

        [Header("Muzzle flash")]
        [SerializeField] private bool muzzleFlashEnabled = true;
        [SerializeField, Min(0.001f)] private float muzzleDuration = 0.045f;
        [SerializeField, Min(0.001f)] private float muzzleSize = 0.07f;
        [SerializeField, Min(0f)] private float muzzleIntensity = 2.5f;
        [SerializeField] private Color muzzleColor = new Color(1f, 0.65f, 0.15f, 1f);

        [Header("Tracer")]
        [SerializeField] private bool tracerEnabled = true;
        [SerializeField, Min(0.001f)] private float tracerDuration = 0.06f;
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.008f;
        [SerializeField, Range(1, 32)] private int tracerPoolSize = 4;
        [SerializeField] private Color tracerColor = new Color(1f, 0.8f, 0.35f, 0.7f);

        [Header("Impact")]
        [SerializeField] private bool impactEnabled = true;
        [SerializeField, Min(0.001f)] private float impactDuration = 0.2f;
        [SerializeField, Min(0.001f)] private float impactSize = 0.025f;
        [SerializeField, Range(1, 64)] private int impactPoolSize = 12;
        [SerializeField] private Color impactColor = new Color(1f, 0.85f, 0.5f, 1f);

        private sealed class Slot
        {
            public LineRenderer Line;
            public double StartedAt;
        }

        private GameObject poolRoot;
        private Slot flash;
        private Slot[] tracers;
        private Slot[] impacts;
        private int nextTracer;
        private int nextImpact;
        private bool shotPending;
        private HitscanWeapon.ShotResult pendingShot;
        private HitscanWeapon subscribedWeapon;
        private bool acceptingShots;
        private bool shuttingDown;
        private int poolSession = -1;
        private static int playSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartPlaySession()
        {
            // This callback also runs when Domain Reload is disabled.
            unchecked { playSession++; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeSceneEffects()
        {
            // Scene Reload may also be disabled: do not rely on Awake running again.
            foreach (WeaponEffects effects in FindObjectsByType<WeaponEffects>())
                if (effects.isActiveAndEnabled) effects.InitializePoolsAndSubscribe();
        }

        private void InitializePoolsAndSubscribe()
        {
            Unsubscribe();
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (weapon == null || muzzlePoint == null || effectsMaterial == null)
            {
                Debug.LogError("WeaponEffects requires a weapon, muzzle point and effects material.", this);
                ReleasePools();
                enabled = false;
                return;
            }

            shuttingDown = false;
            if (poolSession != playSession || !PoolsAreValid())
            {
                ReleasePools();
                CreatePools();
                poolSession = playSession;
            }
            subscribedWeapon = weapon;
            subscribedWeapon.ShotFired += OnShotFired;
            acceptingShots = true;
        }

        private void CreatePools()
        {
            ValidateSettings();
            // A world-space root prevents old tracers and impacts from following the player.
            poolRoot = new GameObject("Weapon Effects Pool");
            poolRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(poolRoot, gameObject.scene);
            flash = new Slot { Line = CreateLine("Muzzle Flash", 8, true, false) };
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                float radius = i % 2 == 0 ? 1f : 0.3f;
                flash.Line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            tracers = new Slot[tracerPoolSize];
            for (int i = 0; i < tracers.Length; i++)
                tracers[i] = new Slot { Line = CreateLine("Tracer " + i, 2, false, true) };
            impacts = new Slot[impactPoolSize];
            for (int i = 0; i < impacts.Length; i++)
            {
                impacts[i] = new Slot { Line = CreateLine("Impact " + i, 8, true, false) };
                for (int j = 0; j < 8; j++)
                {
                    float angle = j * Mathf.PI / 4f;
                    impacts[i].Line.SetPosition(j, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                }
            }
        }

        private LineRenderer CreateLine(string objectName, int points, bool loop, bool worldSpace)
        {
            GameObject go = new GameObject(objectName, typeof(LineRenderer));
            go.transform.SetParent(poolRoot.transform, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = effectsMaterial;
            line.positionCount = points;
            line.loop = loop;
            line.useWorldSpace = worldSpace;
            line.alignment = worldSpace ? LineAlignment.View : LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        private void OnEnable()
        {
            InitializePoolsAndSubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleasePools();
        }

        private void OnDestroy()
        {
            // Idempotent even if OnDisable already ran or Unity destroyed the pool first.
            Unsubscribe();
            ReleasePools();
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            acceptingShots = false;
            shotPending = false;
            pendingShot = default;
            // Event access is managed C#: remove it even if the publisher's native object is gone.
            if (!ReferenceEquals(subscribedWeapon, null)) subscribedWeapon.ShotFired -= OnShotFired;
            subscribedWeapon = null;
        }

        private void ReleasePools()
        {
            GameObject ownedRoot = poolRoot;
            // Drop every reference before cleanup; never touch individual renderers on shutdown.
            poolRoot = null;
            flash = null;
            tracers = null;
            impacts = null;
            nextTracer = nextImpact = 0;
            poolSession = -1;
            shotPending = false;
            pendingShot = default;

            // Unity's null check detects an already destroyed native root.
            if (ownedRoot == null) return;
            ownedRoot.SetActive(false);
            if (Application.isPlaying) Destroy(ownedRoot);
            else DestroyImmediate(ownedRoot);
        }

        private bool PoolsAreValid()
        {
            return poolRoot != null && flash != null && flash.Line != null &&
                PoolIsValid(tracers) && PoolIsValid(impacts);
        }

        private static bool PoolIsValid(Slot[] pool)
        {
            if (pool == null || pool.Length == 0) return false;
            foreach (Slot slot in pool)
                if (slot == null || slot.Line == null) return false;
            return true;
        }

        private void OnShotFired(HitscanWeapon.ShotResult shot)
        {
            if (!acceptingShots || shuttingDown || this == null || !isActiveAndEnabled || !Application.isPlaying) return;
            // The current semiautomatic weapon emits at most one shot per frame.
            pendingShot = shot;
            shotPending = true;
        }

        private void LateUpdate()
        {
            if (shuttingDown || !Application.isPlaying) return;
            if (weapon == null || muzzlePoint == null || effectsMaterial == null)
            {
                enabled = false;
                return;
            }
            if (!PoolsAreValid())
            {
                // Recover the entire owned pool after external destruction, never reuse dead slots.
                InitializePoolsAndSubscribe();
                return;
            }
            Fade(flash, muzzleFlashEnabled, muzzleDuration, muzzleColor, muzzleIntensity);
            foreach (Slot tracer in tracers) Fade(tracer, tracerEnabled, tracerDuration, tracerColor, 1f);
            foreach (Slot impact in impacts) Fade(impact, impactEnabled, impactDuration, impactColor, 1f);

            if (shotPending)
            {
                shotPending = false;
                ShowShot(pendingShot);
            }
            if (flash.Line.enabled)
                flash.Line.transform.SetPositionAndRotation(muzzlePoint.position, muzzlePoint.rotation);
        }

        private void ShowShot(HitscanWeapon.ShotResult shot)
        {
            if (muzzleFlashEnabled)
            {
                flash.Line.transform.localScale = Vector3.one * muzzleSize;
                flash.Line.startWidth = flash.Line.endWidth = muzzleSize * 0.3f;
                Activate(flash, muzzleColor, muzzleIntensity);
            }
            if (tracerEnabled)
            {
                Slot tracer = tracers[nextTracer];
                nextTracer = (nextTracer + 1) % tracers.Length;
                tracer.Line.SetPosition(0, muzzlePoint.position);
                tracer.Line.SetPosition(1, shot.EndPoint);
                tracer.Line.startWidth = tracerWidth;
                tracer.Line.endWidth = tracerWidth * 0.3f;
                Activate(tracer, tracerColor, 1f);
            }
            if (impactEnabled && shot.HasHit)
            {
                Slot impact = impacts[nextImpact];
                nextImpact = (nextImpact + 1) % impacts.Length;
                // Lift slightly off the surface to avoid z-fighting. Local XY faces the normal.
                impact.Line.transform.SetPositionAndRotation(shot.ImpactPosition + shot.ImpactNormal * 0.002f,
                    Quaternion.LookRotation(shot.ImpactNormal));
                impact.Line.transform.localScale = Vector3.one * impactSize;
                impact.Line.startWidth = impact.Line.endWidth = impactSize * 0.2f;
                Activate(impact, impactColor, 1f);
            }
        }

        private static void Activate(Slot slot, Color color, float intensity)
        {
            slot.StartedAt = Time.timeAsDouble;
            SetColor(slot.Line, color, intensity, 1f);
            slot.Line.enabled = true;
        }

        private static void Fade(Slot slot, bool effectEnabled, float duration, Color color, float intensity)
        {
            if (slot == null || slot.Line == null || !slot.Line.enabled) return;
            float remaining = 1f - (float)((Time.timeAsDouble - slot.StartedAt) / duration);
            if (!effectEnabled || remaining <= 0f) slot.Line.enabled = false;
            else SetColor(slot.Line, color, intensity, remaining);
        }

        private static void SetColor(LineRenderer line, Color color, float intensity, float alpha)
        {
            color.r *= intensity;
            color.g *= intensity;
            color.b *= intensity;
            color.a *= alpha;
            line.startColor = line.endColor = color;
        }

        private void OnValidate() { ValidateSettings(); }

        private void ValidateSettings()
        {
            muzzleDuration = Positive(muzzleDuration, 0.045f);
            muzzleSize = Positive(muzzleSize, 0.07f);
            muzzleIntensity = float.IsNaN(muzzleIntensity) || float.IsInfinity(muzzleIntensity)
                ? 2.5f : Mathf.Max(0f, muzzleIntensity);
            tracerDuration = Positive(tracerDuration, 0.06f);
            tracerWidth = Positive(tracerWidth, 0.008f);
            impactDuration = Positive(impactDuration, 0.2f);
            impactSize = Positive(impactSize, 0.025f);
            tracerPoolSize = Mathf.Clamp(tracerPoolSize, 1, 32);
            impactPoolSize = Mathf.Clamp(impactPoolSize, 1, 64);
        }

        private static float Positive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0.001f, value);
        }
    }
}
