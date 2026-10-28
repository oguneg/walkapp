package com.ogunworks.walkapp;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import org.json.JSONObject;

import java.util.concurrent.Executors;

/**
 * "Sign in with Google" through Android's Credential Manager: shows Google's account picker and returns a
 * Google ID token, which the game exchanges for a Firebase account (AuthService.cs).
 * Results go back to Unity with UnitySendMessage as JSON: {"ok":true,"idToken":"...","name":"..."}
 * or {"ok":false,"error":"..."} ("cancelled" when the player closes the picker).
 */
public final class GoogleSignIn {
    private static final String TAG = "WalkappGoogleSignIn";

    private GoogleSignIn() {}

    public static void signIn(final Activity activity, String webClientId, final String gameObject, final String method) {
        try {
            GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();

            CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(),
                    Executors.newSingleThreadExecutor(),
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override
                        public void onResult(GetCredentialResponse response) {
                            Credential credential = response.getCredential();
                            if (credential instanceof CustomCredential
                                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());
                                send(gameObject, method, true, google.getIdToken(), google.getDisplayName(), null);
                            } else {
                                send(gameObject, method, false, null, null, "Unexpected sign-in result.");
                            }
                        }

                        @Override
                        public void onError(GetCredentialException e) {
                            boolean cancelled = e instanceof GetCredentialCancellationException;
                            if (!cancelled) Log.w(TAG, "sign-in failed", e);
                            send(gameObject, method, false, null, null, cancelled ? "cancelled" : String.valueOf(e.getMessage()));
                        }
                    });
        } catch (Throwable t) {
            Log.e(TAG, "sign-in could not start", t);
            send(gameObject, method, false, null, null, String.valueOf(t.getMessage()));
        }
    }

    public static void signOut(Activity activity) {
        try {
            CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), new CancellationSignal(),
                    Executors.newSingleThreadExecutor(),
                    new CredentialManagerCallback<Void, ClearCredentialException>() {
                        @Override public void onResult(Void result) {}
                        @Override public void onError(ClearCredentialException e) { Log.w(TAG, "sign-out failed", e); }
                    });
        } catch (Throwable t) {
            Log.w(TAG, "sign-out could not start", t);
        }
    }

    private static void send(String gameObject, String method, boolean ok, String idToken, String name, String error) {
        try {
            JSONObject json = new JSONObject();
            json.put("ok", ok);
            if (idToken != null) json.put("idToken", idToken);
            if (name != null) json.put("name", name);
            if (error != null) json.put("error", error);
            UnityPlayer.UnitySendMessage(gameObject, method, json.toString());
        } catch (Throwable t) {
            Log.e(TAG, "could not report the result to Unity", t);
        }
    }
}
