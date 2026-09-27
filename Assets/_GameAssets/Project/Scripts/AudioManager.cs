using System.Collections;
using UnityEngine;

public class AudioManager : MonoSingleton<AudioManager>
{
    [SerializeField] private AudioSource audioSource;
    public AudioClip[] audioClips;

    public void PlaySound(SoundType soundType)
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(audioClips[(int)soundType]);
    }

    public void PlayCount(int intensity, float duration)
    {
        StartCoroutine(PlayCountRoutine(intensity, duration));
    }

    public void StopCount()
    {
        StopAllCoroutines();
        audioSource.pitch = 1f;
    }

    private IEnumerator PlayCountRoutine(int intensity, float duration)
    {
        audioSource.pitch = 1f;
        if (intensity == 0) yield break;
        if (intensity > 100) intensity = 100;

        if (intensity / duration > 35)
        {
            intensity = (int)(duration * 35);
        }
        
        var wfs = new WaitForSeconds(duration / intensity);
        for (int i = 0; i < intensity; i++)
        {
            yield return wfs;
            audioSource.pitch += 0.02f;
            audioSource.PlayOneShot(audioClips[4]);
        }
    }
}

public enum SoundType
{
    Button,
    Swipe,
    Success,
    Fail,
    Count
}