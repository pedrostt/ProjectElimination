using ProjectElimination.Weapons;
using UnityEngine;

namespace ProjectElimination.Audio
{
    [DisallowMultipleComponent]
    public sealed class WeaponAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [Header("References")]
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private WeaponAmmo ammo;

        [Header("Clips")]
        [Tooltip("Leave empty to use the generated placeholder. Assign a WAV later.")]
        [SerializeField] private AudioClip fireClip;
        [Tooltip("Leave empty to use the generated placeholder. Assign a WAV later.")]
        [SerializeField] private AudioClip reloadClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float fireVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float reloadVolume = 0.5f;

        [Header("Fire")]
        [SerializeField, Range(0f, 0.25f)] private float firePitchVariation = 0.08f;

        private AudioSource fireSource;
        private AudioSource reloadSource;
        private AudioClip placeholderFire;
        private AudioClip placeholderReload;
        private HitscanWeapon subscribedWeapon;
        private WeaponAmmo subscribedAmmo;

        private void Awake()
        {
            if (weapon == null || ammo == null)
            {
                Debug.LogError("WeaponAudio requires HitscanWeapon and WeaponAmmo.", this);
                enabled = false;
                return;
            }

            EnsureSources();
            EnsurePlaceholders();
        }

        private void OnEnable()
        {
            if (weapon == null || ammo == null) return;
            EnsureSources();
            EnsurePlaceholders();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopPlayback();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopPlayback();
            ReleasePlaceholder(ref placeholderFire);
            ReleasePlaceholder(ref placeholderReload);
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (!isActiveAndEnabled) return;

            subscribedWeapon = weapon;
            subscribedAmmo = ammo;
            if (subscribedWeapon != null) subscribedWeapon.Fired += OnFired;
            if (subscribedAmmo != null)
            {
                subscribedAmmo.ReloadStarted += OnReloadStarted;
                subscribedAmmo.ReloadFinished += OnReloadStopped;
                subscribedAmmo.ReloadCancelled += OnReloadStopped;
            }
        }

        private void Unsubscribe()
        {
            if (!ReferenceEquals(subscribedWeapon, null))
                subscribedWeapon.Fired -= OnFired;
            if (!ReferenceEquals(subscribedAmmo, null))
            {
                subscribedAmmo.ReloadStarted -= OnReloadStarted;
                subscribedAmmo.ReloadFinished -= OnReloadStopped;
                subscribedAmmo.ReloadCancelled -= OnReloadStopped;
            }

            subscribedWeapon = null;
            subscribedAmmo = null;
        }

        private void OnFired()
        {
            // Fired is raised only after ammo is consumed, so empty magazines, reloads
            // and cursor recapture never reach this callback.
            AudioClip clip = fireClip != null ? fireClip : placeholderFire;
            if (clip == null || fireSource == null) return;

            fireSource.pitch = 1f + Random.Range(-firePitchVariation, firePitchVariation);
            fireSource.PlayOneShot(clip, fireVolume);
        }

        private void OnReloadStarted()
        {
            AudioClip clip = reloadClip != null ? reloadClip : placeholderReload;
            if (clip == null || reloadSource == null) return;

            reloadSource.Stop();
            reloadSource.clip = clip;
            reloadSource.volume = reloadVolume;
            reloadSource.pitch = 1f;
            reloadSource.Play();
        }

        private void OnReloadStopped()
        {
            if (reloadSource != null) reloadSource.Stop();
        }

        private void EnsureSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            fireSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            reloadSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            ConfigureSource(fireSource);
            ConfigureSource(reloadSource);
        }

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.pitch = 1f;
        }

        private void EnsurePlaceholders()
        {
            if (placeholderFire == null) placeholderFire = CreatePlaceholderFire();
            if (placeholderReload == null) placeholderReload = CreatePlaceholderReload();
        }

        private static AudioClip CreatePlaceholderFire()
        {
            const float duration = 0.09f;
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint noise = 2463534242u;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 58f);
                float thump = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.45f;
                float crack = Mathf.Sin(2f * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 90f) * 0.2f;
                noise = noise * 1664525u + 1013904223u;
                float hiss = (noise / (float)uint.MaxValue) * 2f - 1f;
                data[i] = Mathf.Clamp((thump + crack + hiss * 0.65f) * env, -1f, 1f);
            }

            return CreateClip("PlaceholderFire", data);
        }

        private static AudioClip CreatePlaceholderReload()
        {
            const float duration = 1.5f;
            int samples = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[samples];
            uint noise = 374761393u;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                noise = noise * 1664525u + 1013904223u;
                float hiss = (noise / (float)uint.MaxValue) * 2f - 1f;
                float sample = 0f;

                sample += Click(t, 0.02f, 1800f, 0.55f);
                sample += hiss * Scrape(t, 0.18f, 1.05f) * 0.12f;
                sample += Click(t, 1.12f, 900f, 0.4f);
                sample += Click(t, 1.32f, 700f, 0.35f);
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return CreateClip("PlaceholderReload", data);
        }

        private static float Click(float t, float start, float hz, float amplitude)
        {
            float local = t - start;
            if (local < 0f || local > 0.06f) return 0f;
            float env = Mathf.Exp(-local * 70f);
            return (Mathf.Sin(2f * Mathf.PI * hz * local) * 0.6f + Mathf.Sin(2f * Mathf.PI * hz * 0.5f * local) * 0.4f)
                * env * amplitude;
        }

        private static float Scrape(float t, float start, float end)
        {
            if (t < start || t > end) return 0f;
            float u = (t - start) / (end - start);
            return Mathf.Sin(u * Mathf.PI);
        }

        private static AudioClip CreateClip(string clipName, float[] data)
        {
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.name = clipName;
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(data, 0);
            return clip;
        }

        private void StopPlayback()
        {
            if (fireSource != null) fireSource.Stop();
            if (reloadSource != null) reloadSource.Stop();
        }

        private static void ReleasePlaceholder(ref AudioClip clip)
        {
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip);
            else DestroyImmediate(clip);
            clip = null;
        }

        private void OnValidate()
        {
            fireVolume = Mathf.Clamp01(Valid(fireVolume, 0.7f));
            reloadVolume = Mathf.Clamp01(Valid(reloadVolume, 0.5f));
            firePitchVariation = Mathf.Clamp(Valid(firePitchVariation, 0.08f), 0f, 0.25f);
        }

        private static float Valid(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
