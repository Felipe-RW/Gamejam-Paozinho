// Grava o teaser em MP4 (1920x1080, 60 fps, com audio) quando a cena Teaser entra em Play
// e o menu Game > Teaser > Gravar video ao dar Play esta ligado. Para ao sair do Play
// (o TeaserDirector sai sozinho no fim do encerramento).
// So compila com o pacote com.unity.recorder instalado (define TEASER_RECORDER no asmdef).
#if TEASER_RECORDER
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class TeaserRecorder
{
    const string PrefKey = "Teaser.Record"; // mesmo valor de TeaserDirector.RecordPrefKey
    const string SceneName = "Teaser";

    static RecorderController controller;
    static string outputPath;

    static TeaserRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (EditorPrefs.GetBool(PrefKey, false) && SceneManager.GetActiveScene().name == SceneName)
                StartRecording();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && controller != null)
        {
            controller.StopRecording();
            controller = null;
            Debug.Log($"Teaser gravado em {outputPath}.mp4");
            EditorUtility.RevealInFinder(outputPath + ".mp4");
        }
    }

    static void StartRecording()
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
        Directory.CreateDirectory(folder);
        outputPath = Path.Combine(folder, $"SuitsOfDestiny_Teaser_{DateTime.Now:yyyyMMdd_HHmmss}").Replace('\\', '/');

        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controller = new RecorderController(controllerSettings);

        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Teaser";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            Codec = CoreEncoderSettings.OutputCodec.MP4
        };
        movie.CaptureAlpha = false;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = outputPath;

        controllerSettings.AddRecorderSettings(movie);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.FrameRate = 60f;
        controllerSettings.CapFrameRate = true;

        RecorderOptions.VerboseMode = false;
        controller.PrepareRecording();
        if (controller.StartRecording()) Debug.Log("Teaser: gravando...");
        else { Debug.LogError("Teaser: o Recorder nao conseguiu comecar a gravar."); controller = null; }
    }
}
#endif
