using System.Collections.Generic;
using UnityEngine;

/// <summary>Plays the shared 3D gameplay sounds used by items, shoppers, and carts.</summary>
[DisallowMultipleComponent]
public sealed class GameplaySfxPlayer : MonoBehaviour
{
    [Header("Items")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip cashierScanClip;
    
[SerializeField] private AudioClip throwWhooshClip;
    [SerializeField] private AudioClip itemImpactClip;

    [Header("Carts")]
    [SerializeField] private AudioClip cartRollClip;
    [SerializeField] private AudioClip cartCrashClip;

    [Header("Shopper falls")]
    [SerializeField] private AudioClip cartoonFallClip;
    [SerializeField] private AudioClip fallScreamClipA;
    [SerializeField] private AudioClip fallScreamClipB;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float cashierScanVolume = 0.9f;
    
[SerializeField, Range(0f, 1f)] private float throwVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float itemImpactVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float cartRollVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float cartCrashVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float fallVolume = 0.8f;

    private static GameplaySfxPlayer instance;
    private readonly Dictionary<Transform, AudioSource> cartSources = new Dictionary<Transform, AudioSource>();

    public static GameplaySfxPlayer Instance => instance;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void PlayPickup(Vector3 position) => PlayAt(pickupClip, position, pickupVolume);

    public void PlayThrow(Vector3 position) => PlayAt(throwWhooshClip, position, throwVolume);

public void PlayCashierScan(Vector3 position) => PlayAt(cashierScanClip, position, cashierScanVolume);


    public void PlayItemImpact(Vector3 position, float impactSpeed)
    {
        float strength = Mathf.InverseLerp(1f, 12f, impactSpeed);
        PlayAt(itemImpactClip, position, itemImpactVolume * Mathf.Lerp(0.45f, 1f, strength));
    }

    public void PlayCartCrash(Vector3 position, float impactSpeed)
    {
        float strength = Mathf.InverseLerp(2f, 12f, impactSpeed);
        PlayAt(cartCrashClip, position, cartCrashVolume * Mathf.Lerp(0.5f, 1f, strength));
    }

    public void PlayProductBreak(Vector3 position, float impactSpeed)
    {
        float strength = Mathf.InverseLerp(16.5f, 25f, impactSpeed);
        PlayAt(cartCrashClip, position, cartCrashVolume * Mathf.Lerp(0.78f, 1.1f, strength));
    }

    public void PlayShopperFall(Vector3 position)
    {
        PlayAt(cartoonFallClip, position, fallVolume);

        AudioClip scream = Random.value < 0.5f ? fallScreamClipA : fallScreamClipB;
        if (scream == null)
            scream = fallScreamClipA != null ? fallScreamClipA : fallScreamClipB;
        PlayAt(scream, position, fallVolume * 0.5f);
    }

    public void SetCartRolling(Transform cart, bool rolling, float speed)
    {
        if (cart == null || cartRollClip == null)
            return;

        AudioSource source;
        if (!cartSources.TryGetValue(cart, out source) || source == null)
        {
            source = cart.GetComponent<AudioSource>();
            if (source == null)
                source = cart.gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 18f;
            cartSources[cart] = source;
        }

        if (source.clip != cartRollClip)
        {
            source.Stop();
            source.clip = cartRollClip;
        }

        source.loop = true;
        float speedStrength = Mathf.InverseLerp(0.15f, 1.5f, speed);
        source.volume = cartRollVolume * Mathf.Lerp(0.55f, 1f, speedStrength);

        if (rolling)
        {
            if (!source.isPlaying)
                source.Play();
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }

    private static void PlayAt(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null && volume > 0f)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
