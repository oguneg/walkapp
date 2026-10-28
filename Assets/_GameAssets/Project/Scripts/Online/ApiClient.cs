using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Calls the Walkapp API (server/functions/index.js) with the player's ID token. JSON in and out.
/// done(response, error): error is null on success, otherwise the server's error code ("party_full", ...)
/// or "offline".
/// </summary>
public static class ApiClient
{
    /// <summary>Minutes east of UTC, so the server knows which day "today" is for this player.</summary>
    public static int TzOffsetMinutes => (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes);

    public static IEnumerator Get<T>(string path, Action<T, string> done) => Send("GET", path, null, done);

    public static IEnumerator Post<T>(string path, object body, Action<T, string> done) =>
        Send("POST", path, body == null ? "{}" : JsonUtility.ToJson(body), done);

    private static IEnumerator Send<T>(string method, string path, string json, Action<T, string> done)
    {
        if (!BackendConfig.Instance.IsConfigured)
        {
            done(default, "not_configured");
            yield break;
        }

        string token = null;
        yield return AuthService.instance.GetIdToken(t => token = t);
        if (string.IsNullOrEmpty(token))
        {
            done(default, "offline");
            yield break;
        }

        using (var req = new UnityWebRequest(BackendConfig.Instance.ApiBase + path, method))
        {
            if (json != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 20;
            yield return req.SendWebRequest();

            string text = req.downloadHandler?.text ?? "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                done(JsonUtility.FromJson<T>(text), null);
                yield break;
            }

            string code = "offline";
            if (req.responseCode > 0 && text.StartsWith("{"))
            {
                var error = JsonUtility.FromJson<ErrorResponse>(text);
                if (!string.IsNullOrEmpty(error.error)) code = error.error;
            }
            done(default, code);
        }
    }

    [Serializable] private class ErrorResponse { public string error; }
}

// ---------------------------------------------------------------- request / response shapes (match index.js)

[Serializable] public class ProfileRequest { public string name; public int tzOffset; }
[Serializable] public class ProfileResponse { public string uid, name, partyId; }
[Serializable] public class OkResponse { public bool ok, deleted; }

[Serializable] public class StepDay { public string date; public long steps; }
[Serializable] public class StepsRequest { public int tzOffset; public StepDay[] days; }

[Serializable] public class CreatePartyRequest { public string name; public int tzOffset; }
[Serializable] public class JoinPartyRequest { public string code; }
[Serializable] public class VoteRequest { public string tier; }
[Serializable] public class ClaimRequest { public string id; }

[Serializable]
public class PartyResponse
{
    public bool hasParty;
    public long coins; // only on claim
    public PartyView party;
}

[Serializable]
public class PartyView
{
    public string id, name, code;
    public bool isLeader;
    public int memberCount, maxMembers;
    public PartyMember[] members;
    public PartyWeek week;
    public PartyVote vote;
    public PartyMonth month;
    public PartyClaimable[] claimables;
}

[Serializable] public class PartyMember { public string uid, name; public bool isLeader, isMe; public long weekSteps, monthSteps; }

[Serializable]
public class PartyWeek
{
    public string key, start, end, tier, tierLabel;
    public int daysLeft;
    public long goal, steps;
    public PartyStage[] stages;
}

[Serializable] public class PartyStage { public string id; public long steps, reward; public bool reached, claimed; }

[Serializable]
public class PartyVote
{
    public string nextStart, myVote, leaderPick;
    public int leaderVotes, voters;
    public PartyVoteOption[] options;
}

[Serializable] public class PartyVoteOption { public string tier, label; public long perMember, goal, reward; public int votes; }

[Serializable]
public class PartyMonth
{
    public string key, theme;
    public int daysLeft;
    public long goal, steps;
    public PartyStage[] milestones;
}

[Serializable] public class PartyClaimable { public string id, label; public long reward; }
