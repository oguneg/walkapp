using System;
using UnityEngine;

/// <summary>
/// Google's account picker on Android (Plugins/Android/GoogleSignIn, Android Credential Manager). Returns a Google
/// ID token for AuthService to hand to Firebase. In the Editor with the emulator, a fake Google account is used so
/// the whole flow can be tested without a phone.
/// </summary>
public class GoogleSignInBridge : MonoBehaviour
{
    public delegate void Result(bool ok, string idToken, string displayName, string error);

    private static GoogleSignInBridge receiver;
    private static Result pending;

    public static void Request(Result done)
    {
        var config = BackendConfig.Instance;
        if (config.UseEmulator)
        {
            // The Auth emulator accepts an unsigned JSON "token" for any provider.
            string fake = "{\"sub\":\"editor-google-user\",\"email\":\"driver@example.com\",\"name\":\"Editor Driver\"}";
            done(true, fake, "Editor Driver", null);
            return;
        }

        if (Application.platform != RuntimePlatform.Android)
        {
            done(false, null, null, "Google sign-in is only available on Android for now.");
            return;
        }

        if (string.IsNullOrEmpty(config.googleWebClientId))
        {
            done(false, null, null, "Google sign-in isn't set up yet (missing web client id).");
            return;
        }

        EnsureReceiver();
        pending = done;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var plugin = new AndroidJavaClass("com.ogunworks.walkapp.GoogleSignIn"))
                plugin.CallStatic("signIn", activity, config.googleWebClientId, receiver.gameObject.name, nameof(OnGoogleSignInResult));
        }
        catch (Exception e)
        {
            pending = null;
            done(false, null, null, e.Message);
        }
    }

    /// <summary>Forget the chosen Google account, so the next sign-in shows the picker again.</summary>
    public static void SignOut()
    {
        if (Application.platform != RuntimePlatform.Android) return;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var plugin = new AndroidJavaClass("com.ogunworks.walkapp.GoogleSignIn"))
                plugin.CallStatic("signOut", activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Google sign-out failed: " + e.Message);
        }
    }

    private static void EnsureReceiver()
    {
        if (receiver != null) return;
        var go = new GameObject("GoogleSignInReceiver");
        DontDestroyOnLoad(go);
        receiver = go.AddComponent<GoogleSignInBridge>();
    }

    // Called by the Java plugin: {"ok":true,"idToken":"...","name":"..."} or {"ok":false,"error":"..."}.
    private void OnGoogleSignInResult(string json)
    {
        var done = pending;
        pending = null;
        var r = JsonUtility.FromJson<NativeResult>(json);
        done?.Invoke(r.ok, r.idToken, r.name, r.ok ? null : r.error);
    }

    [Serializable] private class NativeResult { public bool ok; public string idToken, name, error; }
}
