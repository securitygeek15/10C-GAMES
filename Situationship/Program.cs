using GenZ;

Term.Setup();
var (W, H) = Term.Fit(100, 34, 58, 22);
if (W == 0)
{
    Term.Restore();
    Console.WriteLine("Terminal too small - need at least 58x22.");
    return;
}

var cv = new Canvas(W, H);
Settings.Apply(cv);
var fx = new Fx();
var rng = new Random();
var best = Store.Best("situationship");
var messages = new List<Msg>();
const int Title = 0;
const int Chat = 1;
const int Ending = 2;
const int Paused = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("catch feelings", "how to play", "quit");
var endMenu = new Menu("text again", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var phase = 0;          // 0 typing incoming, 1 choice, 2 typing reply, 3 ended
var round = 0;
var typed = 0.0;
var peace = 0;
var heart = 0;
var rizz = 0;
var points = 0;
var typedMsg = "";
var options = Content.Rounds[0].Options;
var endingTitle = "";
var endingBody = "";
var newRecord = false;
var stateT = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var hue = 0.0;

void Start()
{
    messages.Clear();
    phase = 0;
    round = 0;
    typed = 0;
    peace = 0;
    heart = 0;
    rizz = 0;
    points = 0;
    newRecord = false;
    stateT = 0;
    BeginRound(0);
}

void BeginRound(int idx)
{
    round = idx;
    var r = Content.Rounds[idx];
    options = r.Options;
    typedMsg = r.Incoming;
    messages.Add(new Msg(r.Incoming, false));
    typed = 0;
    phase = 0;
}

void Choose(int i)
{
    if (phase != 1 || i < 0 || i >= options.Length) return;
    var o = options[i];
    peace = Math.Clamp(peace + o.Peace, 0, 12);
    heart = Math.Clamp(heart + o.Heart, 0, 12);
    rizz = Math.Clamp(rizz + o.Rizz, 0, 12);
    messages.Add(new Msg(o.Text, true));
    typedMsg = o.Reply;
    messages.Add(new Msg(o.Reply, false));
    typed = 0;
    phase = 2;
    fx.Spawn(W / 2.0, H / 2.0, 0, -1, 0.5, 0xff8fc8, 0x4a1a34, '♡', 0, 0);
    flash = 0.08;
    flashCol = 0xffc2df;
}

void Finish()
{
    phase = 3;
    state = 2;
    stateT = 0;
    (endingTitle, endingBody) = ComputeEnding();
    points = peace * 120 + heart * 40 + rizz * 90;
    if (peace >= 12 || heart >= 12 || rizz >= 12)
        toasts.Push("you maxed a stat. that's growth.", 0xff8fc8);
    if (points > best)
    {
        best = points;
        Store.Save("situationship", points);
        newRecord = true;
    }
    fx.Burst(W / 2.0, H / 2.0, 26, 0xff8fc8, 0x7ff5e3, 14, 1.0, 3, 2, '♡');
}

(string, string) ComputeEnding()
{
    if (peace >= 7 && heart >= 7 && rizz >= 5)
        return ("secure attachment. frame it.",
            "you like them, you like yourself, and you said so out loud. the group chat has never been quieter or more impressed.");
    if (peace >= 9 && peace >= heart + 2)
        return ("you chose peace.",
            "you logged off, drank water, and did not text back. the group chat is stunned. your skin is glowing.");
    if (heart >= 9 && heart >= rizz)
        return ("situationship: year five.",
            "you have a shared playlist, a toothbrush at their place, and absolutely no label. your friends stopped asking.");
    if (rizz >= 8 && rizz >= heart && rizz >= peace)
        return ("unbothered. moisturized. booked.",
            "you have three options, zero clarity, and a suspiciously full calendar. the group chat is invested.");
    return ("it's complicated.",
        "you set a boundary, then sent a meme at 2am. growth is not linear, but you're still growing.");
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 20) % 360;
    stateT += dt;
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    if (rng.NextDouble() < 0.08)
        fx.Spawn(rng.Next(W), H, (rng.NextDouble() - 0.5) * 1.5, -1.2 - rng.NextDouble() * 2,
            5, 0xff8fc8, 0x3a1428, rng.Next(3) == 0 ? '♡' : '·', 0, 0.05);

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Chat; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
    }
    else if (state == Help)
    {
        if (Input.Confirm || Input.Back || Input.Quit) { state = Title; stateT = 0; }
    }
    else if (state == Paused)
    {
        var pick = pauseMenu.Update();
        if (pick >= 0)
        {
            if (pick == 0) state = Chat;
            else if (pick == 1) { Start(); state = Chat; }
            else if (pick == 5) { state = Title; stateT = 0; }
            else Settings.PauseAction(pick);
        }
        if (Input.Pause) state = Chat;
        if (Input.Quit) return false;
        Settings.RefreshPauseLabels(pauseMenu);
    }
    else if (state == Chat)
    {
        if (Input.Quit) return false;
        if (Input.Pause) { state = Paused; pauseMenu.Index = 0; stateT = 0; }
        if (phase is 0 or 2)
        {
            if (Input.Hit(ConsoleKey.Spacebar)) typed = 9999;
            typed += 42 * dt;
            if (typed >= typedMsg.Length)
            {
                typed = typedMsg.Length;
                if (phase == 0) phase = 1;
                else if (round >= Content.Rounds.Length - 1) Finish();
                else BeginRound(round + 1);
            }
        }
        else if (phase == 1)
        {
            if (Input.Hit(ConsoleKey.D1) || Input.Hit(ConsoleKey.NumPad1) || Input.Hit(ConsoleKey.A)) Choose(0);
            else if (Input.Hit(ConsoleKey.D2) || Input.Hit(ConsoleKey.NumPad2) || Input.Hit(ConsoleKey.B)) Choose(1);
            else if (Input.Hit(ConsoleKey.D3) || Input.Hit(ConsoleKey.NumPad3) || Input.Hit(ConsoleKey.C)) Choose(2);
        }
    }
    else
    {
        var pick = endMenu.Update();
        if (pick == 0 || Input.Hit(ConsoleKey.R)) { Start(); state = Chat; }
        else if (pick == 1) { state = Title; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
    }

    fx.Update(dt);
    toasts.Update(dt);

    cv.Clear();
    DrawBackdrop();
    if (state == Title)
    {
        DrawTitle();
        fx.Draw(cv);
    }
    else if (state == Help)
    {
        DrawHelp();
        fx.Draw(cv);
    }
    else
    {
        DrawTopBar();
        DrawPhone();
        fx.Draw(cv);
        if (state == Ending) DrawEnding();
        if (state == Paused) DrawPause();
    }

    toasts.Draw(cv, 1);

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.35, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"situationship closed. {endingTitle} ({points} points, best {best}).");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x0a0712, 0x150c1e, u);
        for (var x = 0; x < W; x++)
        {
            var star = (x * 11 + y * 7 + (int)(fx.Time * 3)) % 149 == 0;
            cv.Set(x, y, star ? '·' : ' ', star ? 0x4a2a55 : c);
        }
    }
}

void DrawTopBar()
{
    cv.TextGrad(2, 0, "situationship", 0xff4fa3, 0xb9a7ff, (hue / 360) % 1);
    cv.TextRight($"round {Math.Min(round + 1, Content.Rounds.Length)}/{Content.Rounds.Length}", W - 3, 0, 0x8a7aa8);
    DrawStat(2, "peace", peace, 0x7ff5e3);
    DrawStat(19, "heart", heart, 0xff8fc8);
    DrawStat(36, "rizz", rizz, 0xb9a7ff);
}

void DrawStat(int x, string label, int val, int c)
{
    cv.Text(x, 1, label, 0x6a5a7a);
    Ui.Bar(cv, x + 6, 1, 7, val / 12.0, c, Col.Mix(c, 0xffffff, 0.35));
}

void DrawPhone()
{
    var pw = Math.Min(64, W - 4);
    var x0 = (W - pw) / 2;
    var x1 = x0 + pw - 1;
    var y0 = 2;
    var y1 = H - 2;
    cv.Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ' ', 0x0c0a14);
    cv.Frame(x0, y0, x1, y1, 0x4a3558);

    cv.Set(x0 + 2, y0 + 1, '●', 0xff8fc8);
    cv.Text(x0 + 4, y0 + 1, "them", 0xf0e8ff);
    cv.Text(x0 + 9, y0 + 1, "· online", 0x7ff5e3);
    cv.TextRight("2:14am", x1 - 2, y0 + 1, 0x5a4a6a);

    var chatTop = y0 + 2;
    var choiceTop = y1 - 7;
    var chatBottom = choiceTop - 3;
    DrawChat(x0 + 2, x1 - 2, chatTop, chatBottom);

    if (phase is 0 or 2)
    {
        var dots = new string('.', 1 + (int)(fx.Time * 3) % 3);
        cv.Text(x0 + 2, choiceTop - 1, "them is typing" + dots, 0x6a5a7a);
        cv.Text(x0 + 2, choiceTop + 1, "· · ·", 0x3a2a46);
    }
    else if (phase == 1)
    {
        cv.Text(x0 + 2, choiceTop, "reply", 0x6a5a7a);
        for (var i = 0; i < options.Length; i++)
        {
            var row = choiceTop + 1 + i;
            cv.Text(x0 + 2, row, $"{i + 1}", i == 0 ? 0xff8fc8 : i == 1 ? 0xb9a7ff : 0x7ff5e3);
            cv.Text(x0 + 5, row, Trunc(options[i].Text, pw - 10), 0xe8e0f4);
        }
    }

    cv.Text(x0 + 2, y1 - 1, "1-3 choose", 0x5a4a6a);
    cv.TextRight("P pause", x1 - 2, y1 - 1, 0x5a4a6a);
}

void DrawChat(int x0, int x1, int top, int bottom)
{
    var width = x1 - x0;
    var lines = new List<(string S, bool Mine)>();
    for (var i = 0; i < messages.Count; i++)
    {
        var m = messages[i];
        var text = m.Text;
        if (i == messages.Count - 1 && phase is 0 or 2 && !m.Mine)
            text = text[..Math.Clamp((int)typed, 0, text.Length)];
        if (text.Length == 0) continue;
        var wrapped = Ui.Wrap(text, m.Mine ? width - 4 : width);
        foreach (var line in wrapped) lines.Add((line, m.Mine));
        lines.Add(("", false));
    }

    var avail = bottom - top + 1;
    var start = Math.Max(0, lines.Count - avail);
    var y = top;
    if (start > 0)
    {
        cv.Text(x0, y, "· · ·", 0x3a2a46);
        y++;
        avail--;
    }
    for (var i = start; i < lines.Count && y <= top + avail - 1; i++, y++)
    {
        var (s, mine) = lines[i];
        if (s.Length == 0) continue;
        if (mine)
            cv.TextRight(s, x1, y, 0x9fd8ff);
        else
            cv.Text(x0, y, s, 0xe8e0f4);
    }
}

void DrawTitle()
{
    var t = fx.Time;
    var top = Math.Max(2, (H - 16) / 2);
    Ui.BigTitle(cv, top, "situationship", 0xff4fa3, 0x7ff5e3, t);
    Ui.TextShadowC(cv, top + 2, "a texting simulator with consequences", 0xe0d8f0);
    cv.TextC("you cannot unsend. you can, however, grow.", 0x8a7aa8, top + 3);

    titleMenu.Draw(cv, top + 5);

    cv.TextC("them  ·  are we hanging out this week", 0xb9a7ff, top + 10);
    cv.TextC("you   ·  define \"hanging out\"", 0x9fd8ff, top + 11);
    cv.TextC("them  ·  you're so weird", 0xb9a7ff, top + 12);

    if (best > 0) Ui.Badge(cv, (W - 20) / 2, top + 14, $"best {best} pts", 0xffd166, 0x241a08);

    Ui.Ticker(cv, H - 3, "read at 2:14am  ·  typing…  ·  no label no problem  ·  the group chat is invested",
        t, 0x8a7aa8, 0x5a4a6a, 6);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(60, W - 2), Math.Min(19, H - 2), "how to play", 0xff8fc8);
    var x = x0 + 3;
    var y = y0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "eleven texts, three replies each, one ending you have to live with. every reply nudges three stats.",
        x1 - x - 2, Ui.Ink, 3) + 1;
    cv.Text(x, y, "peace", 0x7ff5e3); cv.Text(x + 8, y, "boundaries. calm. logging off.", Ui.InkDim); y++;
    cv.Text(x, y, "heart", 0xff8fc8); cv.Text(x + 8, y, "warmth. honesty. actually saying it.", Ui.InkDim); y++;
    cv.Text(x, y, "rizz", 0xb9a7ff); cv.Text(x + 8, y, "chaos. charm. unbothered energy.", Ui.InkDim); y += 2;
    Ui.KeyHint(cv, x, y, "1-3", "choose a reply (or a-c)", 0xb9a7ff); y++;
    Ui.KeyHint(cv, x, y, "SPACE", "skip the typing animation", Ui.Accent2); y += 2;
    cv.Text(x, y, "ending is decided by whichever stat you fed.", Ui.InkDim);
    cv.TextC("enter / esc to go back", Ui.InkFaint, y1 - 1);
}

void DrawPause()
{
    cv.Dim(0.45);
    Settings.RefreshPauseLabels(pauseMenu);
    var (_, y0, _, y1) = Ui.CenterCard(cv, 38, 11, "paused", Ui.Accent);
    pauseMenu.Draw(cv, y0 + 2);
    cv.TextC("P / ESC to resume", Ui.InkFaint, y1 - 1);
}

void DrawEnding()
{
    cv.Dim(0.3);
    var w = Math.Min(62, W - 2);
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, w, Math.Min(18, H - 2), "ending", 0xff8fc8);
    cv.TextGrad(x0 + 3, y0 + 2, Trunc(endingTitle, w - 8), 0xff8fc8, 0x7ff5e3, (fx.Time * 0.1) % 1);
    var body = Ui.Wrap(endingBody, w - 6);
    for (var i = 0; i < body.Length && i < 3; i++)
        cv.Text(x0 + 3, y0 + 4 + i, body[i], 0xd8cfe8);

    var (rank, tier) = Ui.Rank(points, new[] { 200, 400, 700, 1100, 1600, 2200 });
    Ui.Divider(cv, x0 + 2, x1 - 2, y0 + 7);
    cv.Text(x0 + 3, y0 + 8, $"peace {peace}   heart {heart}   rizz {rizz}", 0xb9a7ff);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 9, "main character points", points.ToString(), 0xf0e8ff);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 10, "rank", rank, Ui.RankColor(tier));
    if (newRecord) Ui.NewBest(cv, (x0 + x1) / 2, y0 + 12, fx.Time);
    else Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 12, "best", best.ToString(), 0xffd166);

    endMenu.Draw(cv, y0 + 13);
    cv.TextC("R to text again", Ui.InkFaint, y1 - 1);
}

string Trunc(string s, int n) => s.Length <= n ? s : s[..Math.Max(0, n - 1)] + "…";

sealed class Msg
{
    public string Text;
    public bool Mine;
    public Msg(string text, bool mine) { Text = text; Mine = mine; }
}

sealed class Round
{
    public string Incoming;
    public Opt[] Options;
    public Round(string incoming, params Opt[] options) { Incoming = incoming; Options = options; }
}

sealed class Opt
{
    public string Text;
    public string Reply;
    public int Peace;
    public int Heart;
    public int Rizz;
    public Opt(string text, string reply, int peace, int heart, int rizz)
    {
        Text = text; Reply = reply; Peace = peace; Heart = heart; Rizz = rizz;
    }
}

static class Content
{
    public static readonly Round[] Rounds =
    {
        new("hey. you up?",
            new Opt("always for you.", "knew it. what are you wearing", 0, 2, 0),
            new Opt("it's 2am. go to sleep.", "damn ok mom", 2, -1, 1),
            new Opt("new phone. who's this?", "lol stop playing", 1, 0, 2)),

        new("sorry i disappeared for three weeks. been \"finding myself\"",
            new Opt("did you find anything?", "a little. mostly i found my ex", 1, 0, 1),
            new Opt("i didn't notice.", "ouch. ok.", 2, -2, 0),
            new Opt("i missed you so much.", "that's so sweet. anyway", -2, 2, 0)),

        new("we should hang out. like, soon.",
            new Opt("name a day and time.", "i'll let you know!!", 2, 0, 1),
            new Opt("i'm free whenever.", "perfect. i'll text you. maybe.", -2, 2, 0),
            new Opt("define \"hang out\".", "you're so funny. i mean like... idk.", 0, 0, 2)),

        new("my ex keeps texting me. it's so annoying lol",
            new Opt("sounds like a you problem.", "wow ok. cold.", 2, 0, 0),
            new Opt("want me to fight them?", "you're unhinged. i like it.", 0, 1, 2),
            new Opt("do you still have feelings for them?", "what? no. maybe. why.", 1, 1, 0)),

        new("what are we?",
            new Opt("two people who like each other. that's enough for now.", "that's actually really nice", 2, 1, 0),
            new Opt("you tell me. you started this.", "fair. i'm scared though.", 1, 0, 1),
            new Opt("your boyfriend should know.", "i don't have a boyfriend??", 2, 0, 3)),

        new("i'm bad at this. like, feelings.",
            new Opt("that's okay. be bad at it with me.", "you can't just say things like that", 1, 2, 0),
            new Opt("then go to therapy.", "i am literally in therapy", 3, -1, 0),
            new Opt("same. let's never talk about it.", "finally someone gets it", 0, 1, 2)),

        new("my friends think we're dating.",
            new Opt("are they wrong?", "...", 0, 2, 1),
            new Opt("i'd never date someone who takes 3 weeks to reply.", "brutal but fair", 3, -1, 0),
            new Opt("tell them i said hi.", "you're impossible", 0, 0, 2)),

        new("ok real talk. i like you. but i'm not ready for a relationship.",
            new Opt("then i'm not waiting around. take care.", "i understand. i'm sorry.", 3, 0, 0),
            new Opt("i can be patient.", "you deserve better than patient", -2, 3, 0),
            new Opt("same. let's just see what happens.", "see? this is why i like you", 0, 1, 1)),

        new("ok so. i might have told my mom about you.",
            new Opt("what exactly did you say?", "that you're funny. and that i'm cooked.", 1, 2, 0),
            new Opt("that's a lot for a tuesday.", "fair. i'll walk it back.", 2, -1, 1),
            new Opt("tell her i said hi, future mother-in-law.", "oh my god we are not there yet", 0, 0, 3)),

        new("are you free on saturday? like actually free.",
            new Opt("yes. i'm putting it in my calendar right now.", "wow. an actual plan. i'm swooning", 3, 1, 0),
            new Opt("probably. depends what we're doing.", "i'll plan something. promise.", 2, 0, 0),
            new Opt("i'm booked but i could be convinced.", "consider yourself convinced", 0, 0, 2)),

        new("i'm sorry i was weird last week. i do that.",
            new Opt("thank you for saying that. it helped.", "ok now i'm emotional at work", 2, 3, 0),
            new Opt("yeah you did. but we're good.", "noted. i'll do better.", 3, 1, 0),
            new Opt("weird is your whole thing. don't change.", "this is why i like texting you", 0, 1, 2))
    };
}
