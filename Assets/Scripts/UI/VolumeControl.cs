using UnityEngine;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    const string Key = "MasterVolume";

    public AudioSource music;
    public Slider slider;

    void Start()
    {
        float volume = PlayerPrefs.GetFloat(Key, 1f);

        if (music != null) music.volume = volume;
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(volume);
        slider.onValueChanged.AddListener(SetVolume);
    }

    void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(SetVolume);
    }

    void SetVolume(float value)
    {
        if (music != null) music.volume = value;
        PlayerPrefs.SetFloat(Key, value);
    }
}
