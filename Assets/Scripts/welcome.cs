using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class welcome : MonoBehaviour
{
    public AudioSource audioSource;
    public Slider progressSlider;
    public TMP_Text durationText;

    private void Update()
    {
        progressSlider.value = audioSource.time;
    }

    private void Start()
    {
        progressSlider.maxValue = audioSource.clip.length;
    }

    public void PlayAudio()
    {
        audioSource.Play();
    }

    public void SetAudioProgress()
    {
        audioSource.time = progressSlider.value;
    }


    private void UpdateDurationText()
    {
        float duration = audioSource.clip.length;
        int minutes = Mathf.FloorToInt(duration / 60);
        int seconds = Mathf.FloorToInt(duration % 60);
        durationText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
