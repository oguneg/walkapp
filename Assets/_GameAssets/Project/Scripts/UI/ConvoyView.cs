using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// The Convoy tab. No convoy: create one or join with a code. In a convoy: invite code, rewards to claim,
    /// this week's goal, the members' steps, next week's vote and the month's goal. Everything shown comes from
    /// ConvoyManager.State (the server's view).
    /// </summary>
    public class ConvoyView : MonoBehaviour
    {
        [Header("No convoy")]
        [SerializeField] private GameObject noPartyGroup;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button createButton, joinButton;

        [Header("In a convoy")]
        [SerializeField] private GameObject partyGroup;
        [SerializeField] private TextMeshProUGUI nameText, codeText, membersCountText;
        [SerializeField] private Button copyButton, claimButton, leaveButton;
        [SerializeField] private TextMeshProUGUI claimLabel;

        [Header("This week")]
        [SerializeField] private TextMeshProUGUI weekTitleText, weekProgressText, weekDetailText;
        [SerializeField] private Image weekFill;
        [SerializeField] private TextMeshProUGUI membersText;

        [Header("Next week's vote")]
        [SerializeField] private TextMeshProUGUI voteTitleText, voteStatusText;
        [SerializeField] private Button[] voteButtons = new Button[4]; // chill, challenging, impossible, leader
        [SerializeField] private TextMeshProUGUI[] voteLabels = new TextMeshProUGUI[4];

        [Header("This month")]
        [SerializeField] private TextMeshProUGUI monthTitleText, monthProgressText, monthDetailText;
        [SerializeField] private Image monthFill;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Color votedColor = new Color32(0xFF, 0xB4, 0x01, 0xFF);
        [SerializeField] private Color voteColor = new Color32(0x0B, 0x25, 0x45, 0x73);

        private static readonly string[] VoteTiers = { "chill", "challenging", "impossible", "leader" };

        private ConvoyManager manager;

        private void Awake()
        {
            createButton.onClick.AddListener(OnCreate);
            joinButton.onClick.AddListener(OnJoin);
            copyButton.onClick.AddListener(OnCopy);
            claimButton.onClick.AddListener(OnClaim);
            leaveButton.onClick.AddListener(OnLeave);
            for (int i = 0; i < voteButtons.Length; i++)
            {
                string tier = VoteTiers[i];
                voteButtons[i].onClick.AddListener(() =>
                {
                    AudioManager.instance.PlaySound(SoundType.Button);
                    manager.Vote(tier);
                });
            }
            if (codeInput) codeInput.onValueChanged.AddListener(v =>
            {
                string upper = v.ToUpperInvariant();
                if (upper != v) codeInput.SetTextWithoutNotify(upper);
                joinButton.interactable = upper.Trim().Length == 6;
            });
        }

        private void OnEnable()
        {
            manager = ConvoyManager.instance;
            manager.OnChanged += Refresh;
            manager.RefreshSoon();
            Refresh();
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            bool busy = manager.Busy;
            statusText.text = manager.LastError != null ? ConvoyManager.Describe(manager.LastError)
                : manager.State == null ? "Connecting..." : "";

            bool has = manager.HasParty;
            noPartyGroup.SetActive(!has && manager.State != null);
            partyGroup.SetActive(has);
            createButton.interactable = !busy;
            joinButton.interactable = !busy && codeInput && codeInput.text.Trim().Length == 6;
            if (!has) return;

            PartyView p = manager.Party;
            nameText.text = p.name;
            codeText.text = $"Invite code: <b>{p.code}</b>";
            membersCountText.text = $"{p.memberCount}/{p.maxMembers} drivers";

            // Rewards: the first one waiting (this week, this month, or last week/month for players who were away).
            bool claimable = p.claimables != null && p.claimables.Length > 0;
            claimButton.gameObject.SetActive(claimable);
            if (claimable)
                claimLabel.text = $"CLAIM <sprite=0>{p.claimables[0].reward:N0} · {p.claimables[0].label}";
            claimButton.interactable = !busy;

            // This week.
            PartyWeek w = p.week;
            long nextTarget = w.goal;
            foreach (var s in w.stages) if (!s.reached) { nextTarget = s.steps; break; }
            bool allDone = w.steps >= w.stages[w.stages.Length - 1].steps;
            weekTitleText.text = $"THIS WEEK · {w.tierLabel.ToUpperInvariant()}";
            weekFill.fillAmount = Mathf.Clamp01(w.steps / (float)Mathf.Max(1, w.goal));
            weekProgressText.text = $"<sprite=1>{w.steps:N0} / {w.goal:N0}";
            weekDetailText.text = (allDone ? "Every bonus stage reached!"
                    : w.steps >= w.goal ? $"Goal reached! Bonus at {nextTarget:N0}"
                    : $"{w.goal - w.steps:N0} to go")
                + $" · {w.daysLeft} {(w.daysLeft == 1 ? "day" : "days")} left";

            var sb = new StringBuilder();
            for (int i = 0; i < p.members.Length; i++)
            {
                var m = p.members[i];
                string name = m.isMe ? $"<b>{m.name} (you)</b>" : m.name;
                string crown = m.isLeader ? " <size=80%><color=#FFE08A>LEADER</color></size>" : "";
                sb.Append($"{i + 1}. {name}{crown}<pos=72%><sprite=1>{m.weekSteps:N0}");
                if (i < p.members.Length - 1) sb.Append('\n');
            }
            membersText.text = sb.ToString();

            // Next week's vote.
            PartyVote v = p.vote;
            voteTitleText.text = "NEXT WEEK'S GOAL";
            for (int i = 0; i < voteButtons.Length; i++)
            {
                bool mine = v.myVote == VoteTiers[i];
                voteButtons[i].image.color = mine ? votedColor : voteColor;
                voteButtons[i].interactable = !busy;
                voteLabels[i].color = mine ? new Color32(0x0B, 0x25, 0x45, 0xFF) : Color.white;
                if (i < v.options.Length)
                {
                    var o = v.options[i];
                    voteLabels[i].text = $"{o.label.ToUpperInvariant()}\n<size=70%>{o.goal / 1000:N0}K · {o.votes} vote{(o.votes == 1 ? "" : "s")}</size>";
                }
                else
                {
                    voteLabels[i].text = $"LEADER DECIDES\n<size=70%>{v.leaderVotes} vote{(v.leaderVotes == 1 ? "" : "s")}</size>";
                }
            }
            voteStatusText.text = string.IsNullOrEmpty(v.myVote)
                ? $"Vote before Monday. {v.voters} of {p.memberCount} voted; the leader breaks ties."
                : $"You voted. {v.voters} of {p.memberCount} voted; the leader breaks ties.";

            // This month.
            PartyMonth mo = p.month;
            monthTitleText.text = $"THIS MONTH · {mo.theme.ToUpperInvariant()}";
            monthFill.fillAmount = Mathf.Clamp01(mo.steps / (float)Mathf.Max(1, mo.goal));
            monthProgressText.text = $"<sprite=1>{mo.steps:N0} / {mo.goal:N0}";
            var next = System.Array.Find(mo.milestones, x => !x.reached);
            monthDetailText.text = (next == null ? "Month complete!" : $"Next reward at {next.steps:N0} steps")
                + $" · {mo.daysLeft} {(mo.daysLeft == 1 ? "day" : "days")} left";

            leaveButton.interactable = !busy;
        }

        private void OnCreate()
        {
            AudioManager.instance.PlaySound(SoundType.Button);
            UIManager.instance.ShowConfirm("START A CONVOY?",
                "You'll get an invite code to share with up to 4 friends.\n\nYour convoy sees your name and your daily step totals, nothing else.",
                "START", "CANCEL", () => manager.Create(null));
        }

        private void OnJoin()
        {
            string code = codeInput.text.Trim();
            AudioManager.instance.PlaySound(SoundType.Button);
            UIManager.instance.ShowConfirm("JOIN THIS CONVOY?",
                $"Join convoy <b>{code}</b>?\n\nIts members will see your name and your daily step totals, nothing else.",
                "JOIN", "CANCEL", () => manager.Join(code, error => { if (error == null) codeInput.text = ""; }));
        }

        private void OnCopy()
        {
            if (!manager.HasParty) return;
            GUIUtility.systemCopyBuffer = manager.Party.code;
            AudioManager.instance.PlaySound(SoundType.Button);
            statusText.text = $"Code {manager.Party.code} copied. Send it to a friend!";
        }

        private void OnClaim()
        {
            if (!manager.HasParty || manager.Party.claimables.Length == 0) return;
            manager.Claim(manager.Party.claimables[0].id);
        }

        private void OnLeave()
        {
            AudioManager.instance.PlaySound(SoundType.Button);
            var p = manager.Party;
            string extra = p.isLeader && p.memberCount > 1 ? " The longest-standing member becomes the leader." : "";
            UIManager.instance.ShowConfirm("LEAVE CONVOY?",
                $"Leave <b>{p.name}</b>? Your steps stop counting for it, and you'll need the code to rejoin.{extra}",
                "LEAVE", "STAY", () => manager.Leave());
        }
    }
}
