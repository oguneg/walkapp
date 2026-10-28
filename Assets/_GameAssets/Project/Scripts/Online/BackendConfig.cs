using UnityEngine;

/// <summary>
/// Where the online features live. One asset at Resources/BackendConfig.
/// Fill in the Firebase values once the project exists (Firebase console > Project settings):
///   apiKey           Web API key
///   projectId        the project id
///   googleWebClientId  Authentication > Sign-in method > Google > Web SDK configuration > Web client ID
/// In the Editor the game talks to the local emulators (server/: npm run emulators) unless useEmulatorInEditor is off.
/// </summary>
[CreateAssetMenu(fileName = "BackendConfig", menuName = "Walkapp/Backend Config")]
public class BackendConfig : ScriptableObject
{
    public string apiKey = "";
    public string projectId = "demo-walkapp";
    public string region = "europe-north1";
    public string googleWebClientId = "";

    [Header("Editor")]
    public bool useEmulatorInEditor = true;
    public string emulatorHost = "127.0.0.1";

    private static BackendConfig cached;

    public static BackendConfig Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<BackendConfig>("BackendConfig");
            if (cached == null) cached = CreateInstance<BackendConfig>();
            return cached;
        }
    }

    public bool UseEmulator => Application.isEditor && useEmulatorInEditor;

    /// <summary>A real project is filled in (or the emulator is used), so online features can run.</summary>
    public bool IsConfigured => UseEmulator || (!string.IsNullOrEmpty(apiKey) && !projectId.StartsWith("demo-"));

    public string ApiKey => UseEmulator ? "fake-api-key" : apiKey;

    public string AuthBase => UseEmulator
        ? $"http://{emulatorHost}:9099/identitytoolkit.googleapis.com/v1"
        : "https://identitytoolkit.googleapis.com/v1";

    public string TokenBase => UseEmulator
        ? $"http://{emulatorHost}:9099/securetoken.googleapis.com/v1"
        : "https://securetoken.googleapis.com/v1";

    public string ApiBase => UseEmulator
        ? $"http://{emulatorHost}:5001/demo-walkapp/{region}/api" // the emulators always run the offline demo project
        : $"https://{region}-{projectId}.cloudfunctions.net/api";
}
