using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's convoy (party): 2-5 friends walking toward a weekly and a monthly goal together.
/// The server (server/functions) owns the rules; this keeps the latest view, reports the player's daily step totals,
/// and sends the player's actions. Rewards come back as coins and are added here.
/// Privacy: only the display name and daily step totals leave the phone, and only convoy members see them.
/// </summary>
public class ConvoyManager : MonoSingleton<ConvoyManager>
{
    [Tooltip("While the app is open, re-sync this often (seconds).")]
    [SerializeField] private float refreshSeconds = 300f;
    [Tooltip("Days of step totals sent with each report (the step history keeps about a week).")]
    [SerializeField] private int reportDays = 7;

    /// <summary>Latest server view; null until the first successful refresh.</summary>
    public PartyResponse State { get; private set; }
    public bool HasParty => State != null && State.hasParty && State.party != null;
    public PartyView Party => HasParty ? State.party : null;
    public bool Busy { get; private set; }
    /// <summary>Last error code from the server ("offline", "party_full", "code_not_found", ...), or null.</summary>
    public string LastError { get; private set; }

    public event Action OnChanged;

    private bool profileSent;
    private string lastUid;
    private float nextRefresh;

    private bool Active => BackendConfig.Instance.IsConfigured && ProgressionManager.instance.IsUnlocked(Feature.Convoy);

    private void Start()
    {
        lastUid = AuthService.instance.Uid;
        AuthService.instance.OnChanged += () =>
        {
            // Linking Google keeps the player: just resend the name. A different player
            // (Google account from another phone, signed out, deleted): start over.
            profileSent = false;
            nextRefresh = 0f;
            if (AuthService.instance.Uid == lastUid) return;
            lastUid = AuthService.instance.Uid;
            State = null;
            nextRefresh = 0f;
            OnChanged?.Invoke();
        };
        ProgressionManager.instance.OnUnlocksChanged += () => nextRefresh = 0f;
    }

    private void Update()
    {
        if (!Active || Busy || Time.realtimeSinceStartup < nextRefresh) return;
        if (!StepManager.instance.IsReady) return;
        nextRefresh = Time.realtimeSinceStartup + refreshSeconds;
        StartCoroutine(Sync());
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) nextRefresh = 0f; // back from the background: fresh steps to report
    }

    /// <summary>Refresh now (the Convoy tab opened).</summary>
    public void RefreshSoon() => nextRefresh = Mathf.Min(nextRefresh, Time.realtimeSinceStartup + 0.5f);

    // ---------------------------------------------------------------- sync

    private IEnumerator Sync()
    {
        Busy = true;
        if (!profileSent)
        {
            string name = AuthService.instance.IsGoogle ? AuthService.instance.GoogleName : null;
            string error = null;
            yield return ApiClient.Post<ProfileResponse>("/me", new ProfileRequest { name = name, tzOffset = ApiClient.TzOffsetMinutes },
                (_, e) => error = e);
            profileSent = error == null;
        }

        yield return ApiClient.Post<OkResponse>("/steps", BuildStepReport(), (_, __) => { });
        yield return ApiClient.Get<PartyResponse>("/party", Apply);
        Busy = false;
        OnChanged?.Invoke(); // again, now that buttons can be enabled
    }

    private StepsRequest BuildStepReport()
    {
        var daily = DailyBonusManager.instance;
        var days = new List<StepDay>();
        DateTime today = daily.TodayLocal;
        for (int i = 0; i < reportDays; i++)
        {
            DateTime date = today.AddDays(-i);
            long steps = daily.StepsOn(date);
            if (steps > 0) days.Add(new StepDay { date = date.ToString("yyyy-MM-dd"), steps = steps });
        }
        return new StepsRequest { tzOffset = ApiClient.TzOffsetMinutes, days = days.ToArray() };
    }

    private void Apply(PartyResponse response, string error)
    {
        LastError = error;
        if (error == null) State = response;
        OnChanged?.Invoke();
    }

    // ---------------------------------------------------------------- actions

    public void Create(string name, Action<string> done = null) =>
        Run(ApiClient.Post<PartyResponse>("/party/create", new CreatePartyRequest { name = name, tzOffset = ApiClient.TzOffsetMinutes }, Wrap(done)));

    public void Join(string code, Action<string> done = null) =>
        Run(ApiClient.Post<PartyResponse>("/party/join", new JoinPartyRequest { code = code }, Wrap(done)));

    public void Leave(Action<string> done = null) =>
        Run(ApiClient.Post<PartyResponse>("/party/leave", null, Wrap(done)));

    public void Vote(string tier, Action<string> done = null) =>
        Run(ApiClient.Post<PartyResponse>("/party/vote", new VoteRequest { tier = tier }, Wrap(done)));

    public void Claim(string id, Action<string> done = null) =>
        Run(ApiClient.Post<PartyResponse>("/party/claim", new ClaimRequest { id = id }, (response, error) =>
        {
            if (error == null && response.coins > 0)
            {
                CurrencyManager.instance.AddCurrency(CurrencyType.Coin, response.coins);
                SaveSystem.SaveNow();
                AudioManager.instance.PlaySound(SoundType.Success);
            }
            Wrap(done)(response, error);
        }));

    /// <summary>GDPR: leave the convoy and delete everything the server keeps about this player, then the account.</summary>
    public void DeleteOnlineData(Action<bool> done)
    {
        StartCoroutine(DeleteRoutine(done));
    }

    private IEnumerator DeleteRoutine(Action<bool> done)
    {
        Busy = true;
        string error = null;
        if (BackendConfig.Instance.IsConfigured && AuthService.instance.HasAccount)
            yield return ApiClient.Post<OkResponse>("/me/delete", null, (_, e) => error = e);
        if (error == null) yield return AuthService.instance.DeleteAccount(_ => { });
        Busy = false;
        State = null;
        OnChanged?.Invoke();
        done(error == null);
    }

    private Action<PartyResponse, string> Wrap(Action<string> done) => (response, error) =>
    {
        Apply(response, error);
        done?.Invoke(error);
    };

    private void Run(IEnumerator call)
    {
        StartCoroutine(RunRoutine(call));
    }

    // Waits for a background sync to finish instead of dropping the player's tap.
    private IEnumerator RunRoutine(IEnumerator call)
    {
        while (Busy) yield return null;
        Busy = true;
        yield return call;
        Busy = false;
        OnChanged?.Invoke();
    }

    /// <summary>Player-facing text for a server error code.</summary>
    public static string Describe(string error)
    {
        switch (error)
        {
            case null: return "";
            case "offline": return "Can't reach the convoy server. Check your connection.";
            case "not_configured": return "Convoys aren't set up in this build yet.";
            case "code_not_found": return "No convoy with that code. Check the letters and try again.";
            case "party_full": return "That convoy is full (5 drivers).";
            case "already_in_party": return "You're already in a convoy. Leave it first.";
            case "not_claimable": return "That reward isn't ready, or you've already claimed it.";
            default: return "Something went wrong. Try again in a moment.";
        }
    }
}
