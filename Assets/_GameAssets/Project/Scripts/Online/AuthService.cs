using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// The player's online account, through Firebase Authentication's REST API (no Firebase SDK in the build).
///   - First use: a guest account is created silently, so convoys work without signing in.
///   - "Sign in with Google" (settings) links Google to that guest, keeping the same player id; if that Google
///     account already belongs to another player (e.g. from an old phone), the game switches to that player.
///   - Sign out goes back to a fresh guest; delete removes the account (the server data goes first, see ApiClient).
/// Only the refresh token is stored on the phone; ID tokens are fetched when needed (they last an hour).
/// </summary>
public class AuthService : MonoSingleton<AuthService>
{
    private const string RefreshKey = "Auth.RefreshToken";
    private const string UidKey = "Auth.Uid";
    private const string GoogleKey = "Auth.Google";
    private const string NameKey = "Auth.Name";
    private const string EmailKey = "Auth.Email";

    public event Action OnChanged;

    public string Uid => PlayerPrefs.GetString(UidKey, "");
    public bool HasAccount => !string.IsNullOrEmpty(PlayerPrefs.GetString(RefreshKey, ""));
    public bool IsGoogle => PlayerPrefs.GetInt(GoogleKey, 0) == 1;
    /// <summary>The Google account's name, or empty for a guest (the server gives guests a "Driver XXXX" name).</summary>
    public string GoogleName => PlayerPrefs.GetString(NameKey, "");
    public string Email => PlayerPrefs.GetString(EmailKey, "");

    private string idToken;
    private float idTokenExpiresAt;
    private bool busy;

    private static BackendConfig Config => BackendConfig.Instance;

    // ---------------------------------------------------------------- tokens

    /// <summary>A valid ID token for API calls (creates a guest account if there is none). Null when offline.</summary>
    public IEnumerator GetIdToken(Action<string> done)
    {
        while (busy) yield return null;
        if (!string.IsNullOrEmpty(idToken) && Time.realtimeSinceStartup < idTokenExpiresAt)
        {
            done(idToken);
            yield break;
        }

        busy = true;
        try
        {
            if (HasAccount) yield return Refresh();
            if (string.IsNullOrEmpty(idToken)) yield return SignUpGuest();
        }
        finally { busy = false; }
        done(idToken);
    }

    private IEnumerator Refresh()
    {
        string form = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(PlayerPrefs.GetString(RefreshKey));
        using (var req = new UnityWebRequest($"{Config.TokenBase}/token?key={Config.ApiKey}", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(form));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // A revoked or deleted account can't be refreshed: start over as a guest. Offline: try again later.
                if (req.responseCode == 400) ClearAccount();
                yield break;
            }

            var r = JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
            SetTokens(r.id_token, r.refresh_token, r.user_id, r.expires_in);
        }
    }

    private IEnumerator SignUpGuest()
    {
        yield return PostJson($"{Config.AuthBase}/accounts:signUp?key={Config.ApiKey}", "{\"returnSecureToken\":true}",
            (ok, text) =>
            {
                if (!ok) return;
                var r = JsonUtility.FromJson<SignInResponse>(text);
                PlayerPrefs.SetInt(GoogleKey, 0);
                SetTokens(r.idToken, r.refreshToken, r.localId, r.expiresIn);
            });
    }

    private void SetTokens(string id, string refresh, string uid, string expiresIn)
    {
        idToken = id;
        idTokenExpiresAt = Time.realtimeSinceStartup + Mathf.Max(60, (int.TryParse(expiresIn, out int s) ? s : 3600) - 120);
        bool uidChanged = uid != Uid;
        PlayerPrefs.SetString(RefreshKey, refresh);
        PlayerPrefs.SetString(UidKey, uid);
        PlayerPrefs.Save();
        if (uidChanged) OnChanged?.Invoke();
    }

    private void ClearAccount()
    {
        idToken = null;
        foreach (var key in new[] { RefreshKey, UidKey, GoogleKey, NameKey, EmailKey }) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ---------------------------------------------------------------- Google

    /// <summary>
    /// Shows Google's account picker and links the account. done(ok, message): message is a short reason on failure
    /// ("cancelled" when the player closed the picker).
    /// </summary>
    public void SignInWithGoogle(Action<bool, string> done)
    {
        GoogleSignInBridge.Request((ok, googleIdToken, displayName, error) =>
        {
            if (!ok)
            {
                done(false, error);
                return;
            }
            StartCoroutine(LinkGoogle(googleIdToken, displayName, done));
        });
    }

    private IEnumerator LinkGoogle(string googleIdToken, string displayName, Action<bool, string> done)
    {
        string current = null;
        yield return GetIdToken(t => current = t);

        string postBody = $"id_token={UnityWebRequest.EscapeURL(googleIdToken)}&providerId=google.com";
        bool linked = false, alreadyUsed = false;
        SignInResponse response = null;

        // Link to the guest first, so the player keeps their id (and convoy).
        if (!string.IsNullOrEmpty(current))
        {
            yield return PostJson($"{Config.AuthBase}/accounts:signInWithIdp?key={Config.ApiKey}",
                JsonUtility.ToJson(new IdpRequest { idToken = current, postBody = postBody }),
                (ok, text) =>
                {
                    if (ok)
                    {
                        response = JsonUtility.FromJson<SignInResponse>(text);
                        linked = true;
                    }
                    else alreadyUsed = text.Contains("FEDERATED_USER_ID_ALREADY_LINKED");
                });
        }

        // That Google account is already a player (another phone, or a reinstall): switch to it.
        if (!linked && (alreadyUsed || string.IsNullOrEmpty(current)))
        {
            yield return PostJson($"{Config.AuthBase}/accounts:signInWithIdp?key={Config.ApiKey}",
                JsonUtility.ToJson(new IdpSignInRequest { postBody = postBody }),
                (ok, text) => { if (ok) response = JsonUtility.FromJson<SignInResponse>(text); });
        }

        if (response == null || string.IsNullOrEmpty(response.idToken))
        {
            done(false, "Couldn't reach the server. Check your connection and try again.");
            yield break;
        }

        string name = !string.IsNullOrEmpty(response.displayName) ? response.displayName : displayName;
        PlayerPrefs.SetInt(GoogleKey, 1);
        PlayerPrefs.SetString(NameKey, name ?? "");
        PlayerPrefs.SetString(EmailKey, response.email ?? "");
        SetTokens(response.idToken, response.refreshToken, response.localId, response.expiresIn);
        OnChanged?.Invoke();

        // The ID token doesn't carry the Google name after linking a guest, so the server gets it from here.
        yield return ApiClient.Post<ProfileResponse>("/me", new ProfileRequest { name = name, tzOffset = ApiClient.TzOffsetMinutes }, (_, __) => { });
        done(true, alreadyUsed ? "switched" : "linked");
    }

    /// <summary>Back to a fresh guest on this phone. The Google player (and their convoy) stays on the server.</summary>
    public void SignOut()
    {
        GoogleSignInBridge.SignOut();
        ClearAccount();
    }

    /// <summary>Deletes the account itself. Call after the server data is deleted (POST /me/delete).</summary>
    public IEnumerator DeleteAccount(Action<bool> done)
    {
        if (!HasAccount)
        {
            done(true);
            yield break;
        }

        string token = null;
        yield return GetIdToken(t => token = t);
        bool ok = false;
        if (!string.IsNullOrEmpty(token))
            yield return PostJson($"{Config.AuthBase}/accounts:delete?key={Config.ApiKey}",
                JsonUtility.ToJson(new DeleteRequest { idToken = token }), (success, _) => ok = success);
        GoogleSignInBridge.SignOut();
        ClearAccount();
        done(ok);
    }

    // ---------------------------------------------------------------- plumbing

    private static IEnumerator PostJson(string url, string json, Action<bool, string> done)
    {
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 20;
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success, req.downloadHandler?.text ?? "");
        }
    }

    [Serializable] private class RefreshResponse { public string id_token, refresh_token, user_id, expires_in; }
    [Serializable] private class SignInResponse { public string idToken, refreshToken, localId, expiresIn, displayName, email; }
    [Serializable] private class IdpRequest
    {
        public string idToken;
        public string postBody;
        public string requestUri = "http://localhost";
        public bool returnSecureToken = true;
        public bool returnIdpCredential = true;
    }
    // Same as IdpRequest without a current user: a plain sign-in, not a link.
    [Serializable] private class IdpSignInRequest
    {
        public string postBody;
        public string requestUri = "http://localhost";
        public bool returnSecureToken = true;
        public bool returnIdpCredential = true;
    }
    [Serializable] private class DeleteRequest { public string idToken; }
}
