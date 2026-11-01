using GenZ;

Term.Setup();
var (W, H) = Term.Fit(100, 34, 62, 24);
if (W == 0)
{
    Term.Restore();
    Console.WriteLine("Terminal too small - need at least 62x24.");
    return;
}

var cv = new Canvas(W, H);
Settings.Apply(cv);
var fx = new Fx();
var rng = new Random();
var best = Store.Best("delulu");
var pops = new List<Pop>();
var options = new List<Option>();
const int Title = 0;
const int Playing = 1;
const int Dead = 2;
const int Paused = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("catch feelings", "how to play", "quit");
var deadMenu = new Menu("run it back", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var hintUsed = false;
var hidden = -1;
var score = 0;
var hearts = 3;
var round = 0;
var streak = 0;
var bestStreak = 0;
var duration = 9.0;
var timeLeft = 9.0;
var revealT = 0.0;
var correctIdx = 0;
var chosenIdx = -2;
var lastDelulu = -1;
var newRecord = false;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var hue = 0.0;
var verdict = "";

void Start()
{
    score = 0;
    hearts = 3;
    round = 0;
    streak = 0;
    bestStreak = 0;
    revealT = 0;
    chosenIdx = -2;
    lastDelulu = -1;
    newRecord = false;
    pops.Clear();
    stateT = 0;
    NextRound();
}

void NextRound()
{
    round++;
    duration = Math.Max(5.0, 9.5 - round * 0.22);
    timeLeft = duration;
    revealT = 0;
    chosenIdx = -2;
    verdict = "";
    hintUsed = false;
    hidden = -1;
    options.Clear();

    var d = rng.Next(Content.Delulu.Length);
    while (Content.Delulu.Length > 1 && d == lastDelulu) d = rng.Next(Content.Delulu.Length);
    lastDelulu = d;
    options.Add(new Option(Content.Delulu[d], true));

    var used = new HashSet<int>();
    while (used.Count < 3)
    {
        var s = rng.Next(Content.Sane.Length);
        if (!used.Add(s)) continue;
        options.Add(new Option(Content.Sane[s], false));
    }

    for (var i = options.Count - 1; i > 0; i--)
    {
        var j = rng.Next(i + 1);
        (options[i], options[j]) = (options[j], options[i]);
    }
    correctIdx = options.FindIndex(o => o.Delulu);
}

void UseHint()
{
    if (hintUsed || revealT > 0) return;
    var candidates = new List<int>();
    for (var i = 0; i < options.Count; i++)
        if (i != correctIdx) candidates.Add(i);
    if (candidates.Count == 0) return;
    hidden = candidates[rng.Next(candidates.Count)];
    hintUsed = true;
    score = Math.Max(0, score - 100);
    verdict = "the group chat weighed in.";
    toasts.Push("one cope crossed out, -100", 0xb9a7ff);
    fx.Spark(W / 2.0, 4, 0xb9a7ff, 14);
}

void Choose(int idx)
{
    if (revealT > 0 || idx == hidden) return;
    chosenIdx = idx;
    revealT = 1.35;
    if (idx == correctIdx)
    {
        streak++;
        bestStreak = Math.Max(bestStreak, streak);
        var gain = 100 + (streak - 1) * 40;
        score += gain;
        verdict = Verdicts.Correct[rng.Next(Verdicts.Correct.Length)];
        flash = 0.18;
        flashCol = 0x9be564;
        AddPop(W / 2.0 - 8, H / 2.0 - 2, $"+{gain} receipts", 0x9be564);
        fx.Burst(W / 2.0, H / 2.0, 26, 0x9be564, 0x7ff5e3, 16, 0.8, 8, 2, '✦');
    }
    else
    {
        hearts--;
        streak = 0;
        verdict = idx < 0 ? "the silence told you everything." : Verdicts.Wrong[rng.Next(Verdicts.Wrong.Length)];
        shake = 9;
        flash = 0.35;
        flashCol = 0xff2a4d;
    }
}

void Die()
{
    state = 2;
    stateT = 0;
    if (score > best)
    {
        best = score;
        Store.Save("delulu", score);
        newRecord = true;
    }
    shake = 12;
    fx.Burst(W / 2.0, H / 2.0, 40, 0xff4fa3, 0x4a0a20, 20, 1.1, 4, 2, '♡');
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x, y, s, c, 1.0, 1.0));
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 18) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 28);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.6);
    stateT += dt;

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Playing; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.18)
            fx.Spawn(rng.Next(W), H, (rng.NextDouble() - 0.5) * 1.5, -1.5 - rng.NextDouble() * 2.5,
                4.5, 0xff4fa3, 0x4a1a34, rng.Next(3) == 0 ? '♡' : '?', 0, 0.08);
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
            if (pick == 0) state = Playing;
            else if (pick == 1) { Start(); state = Playing; }
            else if (pick == 5) { state = Title; stateT = 0; }
            else Settings.PauseAction(pick);
        }
        if (Input.Pause) state = Playing;
        if (Input.Quit) return false;
        Settings.RefreshPauseLabels(pauseMenu);
    }
    else if (state == Playing)
    {
        if (Input.Quit) return false;
        if (Input.Pause) { state = Paused; pauseMenu.Index = 0; stateT = 0; }
        if (Input.Hit(ConsoleKey.H)) UseHint();
        if (revealT > 0)
        {
            revealT -= dt;
            if (revealT <= 0)
            {
                if (hearts <= 0) Die();
                else NextRound();
            }
        }
        else
        {
            if (Input.Hit(ConsoleKey.D1) || Input.Hit(ConsoleKey.NumPad1) || Input.Hit(ConsoleKey.A)) Choose(0);
            else if (Input.Hit(ConsoleKey.D2) || Input.Hit(ConsoleKey.NumPad2) || Input.Hit(ConsoleKey.B)) Choose(1);
            else if (Input.Hit(ConsoleKey.D3) || Input.Hit(ConsoleKey.NumPad3) || Input.Hit(ConsoleKey.C)) Choose(2);
            else if (Input.Hit(ConsoleKey.D4) || Input.Hit(ConsoleKey.NumPad4) || Input.Hit(ConsoleKey.D)) Choose(3);
            timeLeft -= dt;
            if (timeLeft <= 0 && revealT <= 0) Choose(-1);
        }
    }
    else
    {
        var pick = deadMenu.Update();
        if (pick == 0 || Input.Hit(ConsoleKey.R)) { Start(); state = Playing; }
        else if (pick == 1) { state = Title; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
    }

    fx.Update(dt);
    toasts.Update(dt);
    for (var i = pops.Count - 1; i >= 0; i--)
    {
        var p = pops[i];
        p.Life -= dt;
        if (p.Life <= 0) { pops.RemoveAt(i); continue; }
        p.Y -= dt * 10;
    }

    cv.Clear();
    cv.ShakeX = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
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
        DrawGame();
        fx.Draw(cv);
        DrawPops();
        if (state == Dead) DrawDead();
        if (state == Paused) DrawPause();
    }

    toasts.Draw(cv, 1);

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.5, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"delulu detector closed. score {score}, best {best}. get well soon.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x120c18, 0x1e1020, u);
        for (var x = 0; x < W; x++)
        {
            var dot = (x * 5 + y * 9 + (int)(fx.Time * 4)) % 131 == 0;
            cv.Set(x, y, dot ? '·' : ' ', dot ? 0x4a2a55 : c);
        }
    }
    for (var i = 0; i < 4; i++)
    {
        var x = (int)((fx.Time * 9 + i * 23) % W);
        var y = (int)((fx.Time * (4 + i) + i * 7) % (H - 2)) + 1;
        cv.Set(x, y, i % 2 == 0 ? '♡' : '✦', Col.Hsl(320 + i * 20, 0.5, 0.25));
    }
}

void DrawGame()
{
    cv.TextC("delulu detector", Col.Hsl(hue, 0.8, 0.68), 0);
    cv.Text(2, 1, $"round {round:00}   streak x{Math.Max(1, streak)}", 0x9a86aa);
    cv.TextRight($"{new string('♥', Math.Max(0, hearts))}{new string('♡', Math.Max(0, 3 - hearts))}   {score:00000}", W - 3, 1,
        hearts <= 1 ? 0xff5c7a : 0xff9fc4);

    cv.TextC("one of these is delulu. choose wisely.", 0xd8c8e2, 2);

    var timerW = Math.Min(40, W - 20);
    var frac = Math.Clamp(timeLeft / duration, 0, 1);
    var tc = frac > 0.55 ? 0x7ff5e3 : frac > 0.25 ? 0xffd166 : 0xff4d6d;
    Ui.Bar(cv, (W - timerW) / 2, 3, timerW, frac, tc, Col.Mix(tc, 0xffffff, 0.35));

    DrawCards();

    if (revealT > 0 && verdict.Length > 0)
    {
        var vy = H - 3;
        cv.TextC(verdict, chosenIdx == correctIdx ? 0x9be564 : 0xff6f8a, vy);
    }
    Ui.Hints(cv, H - 1, "1-4", "choose", "H", hintUsed ? "hint used" : "hint", "P", "pause");
}

void DrawCards()
{
    var gridTop = 5;
    var gridBottom = H - 4;
    var gap = 1;
    var ch = Math.Max(6, (gridBottom - gridTop - gap) / 2);
    var cw = Math.Max(20, (W - 5) / 2);
    var xa = 2;
    var xb = W - 2 - cw;
    for (var i = 0; i < 4 && i < options.Count; i++)
    {
        var col = i % 2;
        var row = i / 2;
        var x0 = col == 0 ? xa : xb;
        var y0 = gridTop + row * (ch + gap);
        var x1 = x0 + cw - 1;
        var y1 = Math.Min(gridBottom, y0 + ch - 1);
        DrawCard(i, x0, y0, x1, y1);
    }
}

void DrawCard(int i, int x0, int y0, int x1, int y1)
{
    var accent = i switch { 0 => 0xff4fa3, 1 => 0xb9a7ff, 2 => 0x7ff5e3, _ => 0xffd166 };
    var border = 0x3c2a48;
    var fill = 0x171020;
    var dim = false;
    var crossed = i == hidden;

    if (revealT > 0)
    {
        if (i == correctIdx)
        {
            border = 0x9be564;
            fill = 0x14200f;
        }
        else if (i == chosenIdx)
        {
            border = 0xff4d6d;
            fill = 0x240d12;
        }
        else
        {
            border = 0x2a2032;
            dim = true;
        }
    }

    if (crossed)
    {
        border = 0x2f2438;
        fill = 0x120c18;
        dim = true;
    }

    cv.Fill(x0, y0, x1, y1, ' ', fill);
    cv.Frame(x0, y0, x1, y1, border);

    var letter = ((char)('a' + i)).ToString();
    cv.Text(x0 + 2, y0 + 1, $"[{letter}]", dim ? 0x4a3a56 : accent);
    if (crossed) cv.TextRight("cope", x1 - 2, y0 + 1, 0x4a3a56);
    if (revealT > 0 && i == correctIdx)
        cv.TextRight("caught in 4k", x1 - 2, y0 + 1, 0x9be564);
    else if (revealT > 0 && i == chosenIdx)
        cv.TextRight("oh no", x1 - 2, y0 + 1, 0xff6f8a);

    var body = options[i].Text;
    var lines = Ui.Wrap(body, Math.Max(10, x1 - x0 - 3));
    var maxLines = Math.Max(1, y1 - y0 - 3);
    for (var k = 0; k < lines.Length && k < maxLines; k++)
        cv.Text(x0 + 2, y0 + 2 + k, lines[k], dim ? 0x6a5a78 : 0xf0e8f8);

    if (revealT > 0 && i == correctIdx)
    {
        cv.Set(x1 - 2, y1 - 1, '✦', 0x9be564);
        cv.Set(x0 + 2, y1 - 1, '✦', 0x9be564);
    }
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.5);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawTitle()
{
    var t = fx.Time;
    var top = Math.Max(2, (H - 14) / 2);
    Ui.BigTitle(cv, top, "delulu detector", 0xff4fa3, 0xb9a7ff, t);
    Ui.TextShadowC(cv, top + 2, "a quiz about reading the room", 0xd8c8e2);
    cv.TextC("(and the texts. mostly the texts.)", 0x8a7a9a, top + 3);

    titleMenu.Draw(cv, top + 5);

    if (best > 0) Ui.Badge(cv, (W - 13) / 2, top + 9, $"best {best:00000}", 0xffd166, 0x241a08);

    Ui.Ticker(cv, H - 3, "one of these is delulu  ·  no thoughts just receipts  ·  the group chat is typing",
        t, 0x8a7a9a, 0x5e4a6a, 6);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(58, W - 2), Math.Min(16, H - 2), "how to play", 0xff4fa3);
    var x = x0 + 3;
    var y = y0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "four statements, one delusion. pick the one that is genuinely unhinged before the timer runs out.",
        x1 - x - 2, Ui.Ink, 3) + 1;
    Ui.KeyHint(cv, x, y, "1-4", "choose a card (or a-d)", 0xb9a7ff); y++;
    Ui.KeyHint(cv, x, y, "H", "cross out one cope, costs 100", Ui.Accent2); y++;
    Ui.KeyHint(cv, x, y, "P", "pause + settings", Ui.Gold); y += 2;
    cv.Text(x, y, "a wrong pick or a timeout costs a brain cell.", Ui.InkDim);
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

void DrawDead()
{
    cv.Dim(0.4);
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(54, W - 2), Math.Min(18, H - 2), "delulu detected", 0xff4fa3);
    var cx = (x0 + x1) / 2;
    cv.TextC("the delusion was coming from inside the house.", 0x8a7a9a, y0 + 2);

    var (rank, tier) = Ui.Rank(score, new[] { 200, 500, 900, 1400, 2000, 2800 });
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 4, "receipts", $"{score:00000}", 0xf0e8f8);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 5, "rounds", round.ToString(), 0xb9a7ff);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 6, "best streak", $"x{bestStreak}", 0xff9fc4);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 7, "rank", rank, Ui.RankColor(tier));
    Ui.Divider(cv, x0 + 2, x1 - 2, y0 + 9);
    if (newRecord) Ui.NewBest(cv, cx, y0 + 10, fx.Time);
    else Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 10, "best", $"{best:00000}", 0xffd166);

    deadMenu.Draw(cv, y0 + 12);
    cv.TextC("R to try again", Ui.InkFaint, y1 - 1);
}

sealed class Option
{
    public string Text;
    public bool Delulu;
    public Option(string text, bool delulu) { Text = text; Delulu = delulu; }
}

sealed class Pop
{
    public double X;
    public double Y;
    public string S;
    public int C;
    public double Life;
    public double Max;
    public Pop(double x, double y, string s, int c, double life, double max)
    {
        X = x; Y = y; S = s; C = c; Life = life; Max = max;
    }
}

static class Verdicts
{
    public static readonly string[] Correct =
    {
        "caught in 4k.", "the receipts are receipts-ing.", "clocked it.", "not delulu. just perceptive.",
        "your third eye is open.", "mother knows.", "the group chat would be proud."
    };
    public static readonly string[] Wrong =
    {
        "delulu is contagious.", "you fell for it.", "the cope got you.", "girl. stand up.",
        "that was the delusion.", "we lost another one.", "log off and breathe."
    };
}

static class Content
{
    public static readonly string[] Delulu =
    {
        "he said \"we'll see.\" that's basically a yes.",
        "they viewed my story in 0.4 seconds. it's fate.",
        "if i manifest hard enough, he'll text back.",
        "we're in a situationship because he's scared of how much he likes me.",
        "the bartender remembered my order. we're soulmates.",
        "they didn't reply because their phone died for three weeks.",
        "our spotify blends are compatible. this is destiny.",
        "he follows 400 models but i'm different.",
        "we made eye contact at the gym. the wedding is in june.",
        "she said \"i'm busy this week\" which means she's playing hard to get.",
        "he only posts his ex because he's healing, and then it's me.",
        "we've been talking for eight months but haven't met. it's a slow burn.",
        "they liked my comment from 2019 at 2am. that's a confession.",
        "i'm not obsessed, i just have a very detailed spreadsheet.",
        "he named his cat after me. it's basically a proposal.",
        "the cashier said \"have a nice day\" and i heard \"i love you.\"",
        "we're not fighting, we're in our passionate era.",
        "he said he's not ready for a relationship, but with me he will be.",
        "the tarot reader said he's my twin flame. the cards don't lie.",
        "we matched on three apps. the algorithm is literally fate."
    };

    public static readonly string[] Sane =
    {
        "he hasn't replied in a week. it's not happening.",
        "i'm going to therapy and drinking water.",
        "we dated for two weeks. i'm allowed to be sad and move on.",
        "i left him on read because i was busy.",
        "i like them, but i'm okay if it doesn't work out.",
        "that was just a friendly hug.",
        "i'm focusing on my friends and my goals.",
        "i saw the red flags and left.",
        "a crush is not a commitment.",
        "they said no. i'm respecting that.",
        "i'm not for everyone and that's fine.",
        "i can miss someone and still not text them.",
        "it was one date. i'm keeping my options open.",
        "i'm allowed to be single and not in a waiting room.",
        "i deleted the app because it made me feel bad.",
        "he's in a relationship. i'm moving on.",
        "i said i'm not interested and i meant it.",
        "i don't need closure to close the tab.",
        "we can be friends without a secret timeline.",
        "i'm not competing for anyone's attention.",
        "their inconsistency is the answer.",
        "i'm romanticizing my own life instead."
    };
}
