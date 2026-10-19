using GenZ;

Term.Setup();
var (W, H) = Term.Fit(96, 34, 58, 24);
if (W == 0)
{
    Term.Restore();
    Console.WriteLine("Terminal too small - need at least 58x24.");
    return;
}

var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();
var best = Store.Best("doomscroll");
var pops = new List<Pop>();
var post = new Post();
var state = 0;
var battery = 4;
var score = 0;
var combo = 0;
var rounds = 0;
var bestCombo = 0;
var duration = 2.6;
var timeLeft = 2.6;
var pauseT = 0.0;
var dying = false;
var verdict = "";
var verdictCol = 0xffffff;
var newRecord = false;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var hue = 0.0;

void Start()
{
    battery = 4;
    score = 0;
    combo = 0;
    rounds = 0;
    bestCombo = 0;
    pauseT = 0;
    dying = false;
    newRecord = false;
    verdict = "";
    pops.Clear();
    fx.Burst(W / 2.0, 10, 24, 0xff4fa3, 0xb9a7ff, 16, 0.8, 22, 3, '♡');
    NextPost();
}

void NextPost()
{
    rounds++;
    duration = Math.Max(0.95, 2.7 - rounds * 0.075);
    timeLeft = duration;
    post = MakePost();
}

void Answer(int choice)
{
    if (pauseT > 0) return;
    var want = post.Kind == 0 ? 1 : post.Kind == 1 ? 0 : 2;
    var ok = choice == want;
    lastAnswer(ok);
}

void lastAnswer(bool ok)
{
    if (ok)
    {
        combo++;
        bestCombo = Math.Max(bestCombo, combo);
        var gain = 10 * combo;
        score += gain;
        verdict = Verdicts.Good[rng.Next(Verdicts.Good.Length)];
        verdictCol = 0x7ff5e3;
        pauseT = 0.4;
        AddPop($" +{gain} followers", 0xff8fc8, 0.95);
        fx.Burst(W / 2.0, H / 2.0, 26, 0xff4fa3, 0xb9a7ff, 15, 0.7, 10, 3, '♡');
        flash = 0.12;
        flashCol = 0xffc2df;
    }
    else
    {
        battery--;
        combo = 0;
        verdict = Verdicts.Bad[rng.Next(Verdicts.Bad.Length)];
        verdictCol = 0xff5c7a;
        pauseT = 0.55;
        shake = 8;
        flash = 0.4;
        flashCol = 0xff2a4d;
        AddPop(" -1 battery", 0xff5c7a, 0.95);
        if (battery <= 0) dying = true;
    }
}

void AddPop(string s, int c, double life)
{
    pops.Add(new Pop(W / 2.0 - s.Length / 2.0, H / 2.0 - 2, s, c, life, life));
}

void Die()
{
    state = 2;
    stateT = 0;
    shake = 12;
    flash = 0.65;
    flashCol = 0x000000;
    if (score > best)
    {
        best = score;
        Store.Save("doomscroll", score);
        newRecord = true;
    }
    fx.Burst(W / 2.0, H / 2.0, 40, 0xff2a4d, 0x2a0a14, 22, 1.0, 6, 2);
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 24) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 30);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    stateT += dt;

    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter))
        {
            Start();
            state = 1;
            stateT = 0;
        }
        if (Input.Quit) return false;
    }
    else if (state == 1)
    {
        if (Input.Quit) return false;
        if (pauseT > 0)
        {
            pauseT -= dt;
            if (pauseT <= 0)
            {
                if (dying) Die();
                else NextPost();
            }
        }
        else
        {
            if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) Answer(0);
            else if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) Answer(1);
            else if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) Answer(2);
            timeLeft -= dt;
            if (timeLeft <= 0 && pauseT <= 0)
            {
                timeout();
            }
        }
    }
    else
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.R)) { Start(); state = 1; stateT = 0; }
        if (Input.Hit(ConsoleKey.Escape)) { state = 0; stateT = 0; }
        if (Input.Quit) return false;
    }

    void timeout()
    {
        battery--;
        combo = 0;
        verdict = "you froze. relatable.";
        verdictCol = 0xffb020;
        pauseT = 0.5;
        shake = 5;
        if (battery <= 0) dying = true;
    }

    // ambient hearts
    if (rng.NextDouble() < 0.25)
    {
        var phoneW = Math.Min(52, W - 4);
        var px0 = (W - phoneW) / 2;
        var px1 = px0 + phoneW - 1;
        var left = rng.Next(0, Math.Max(1, px0 - 1));
        var right = rng.Next(Math.Min(W - 1, px1 + 2), W);
        var x = rng.Next(2) == 0 ? left : right;
        if (x > 0 && x < W - 1)
            fx.Spawn(x, H - 1, (rng.NextDouble() - 0.5) * 1.2, -1.4 - rng.NextDouble() * 2.2,
                4.5, 0xff4fa3, 0x6a2a4a, rng.Next(3) == 0 ? '♡' : '·', 0, 0.05);
    }

    fx.Update(dt);
    for (var i = pops.Count - 1; i >= 0; i--)
    {
        var p = pops[i];
        var life = p.Life - dt;
        if (life <= 0) { pops.RemoveAt(i); continue; }
        p.Y -= dt * 8;
        p.Life = life;
    }

    cv.Clear();
    cv.ShakeX = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    DrawBackdrop();

    if (state == 0)
    {
        DrawTitle();
        fx.Draw(cv);
        DrawPops();
    }
    else
    {
        DrawPhone();
        fx.Draw(cv);
        DrawPops();
        if (state == 2) DrawDead();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.8, flash);
    }
    else if (state == 2)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = 0.28;
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"doomscroll closed. followers {score}, best {best}.");

// ---------- scenes ----------

void DrawBackdrop()
{
    var dark = 0x0a0a12;
    for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
            if ((x * 7 + y * 13 + (int)(fx.Time * 6)) % 97 == 0)
                cv.Set(x, y, '·', 0x22202e);
    for (var y = 0; y < H; y++)
        if (y % 2 == 0) cv.Set(0, y, ' ', dark);
}

void PhoneGeom(out int x0, out int y0, out int x1, out int y1)
{
    var pw = Math.Min(52, W - 4);
    x0 = (W - pw) / 2;
    x1 = x0 + pw - 1;
    y0 = 1;
    y1 = H - 2;
}

void DrawPhone()
{
    PhoneGeom(out var x0, out var y0, out var x1, out var y1);
    cv.Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ' ', 0x08080f);

    for (var x = x0 + 1; x < x1; x++)
    {
        cv.Set(x, y0, '─', Col.Hsl(280 + x, 0.35, 0.55));
        cv.Set(x, y1, '─', Col.Hsl(280 + x, 0.35, 0.42));
    }
    for (var y = y0 + 1; y < y1; y++)
    {
        cv.Set(x0, y, '│', Col.Hsl(285 + y * 8, 0.3, 0.35));
        cv.Set(x1, y, '│', Col.Hsl(285 + y * 8, 0.3, 0.35));
    }
    cv.Set(x0, y0, '╭', 0xcfc4e8);
    cv.Set(x1, y0, '╮', 0xcfc4e8);
    cv.Set(x0, y1, '╰', 0x8d84a6);
    cv.Set(x1, y1, '╯', 0x8d84a6);

    // status bar
    cv.Text(x0 + 3, y0 + 1, "3:47", 0xdedaf0);
    var bat = new string('▰', Math.Max(0, battery)) + new string('▱', Math.Max(0, 4 - battery));
    var batCol = battery <= 1 ? 0xff4d6d : 0xa9a4c2;
    cv.TextRight($"{bat} 12%", x1 - 3, y0 + 1, batCol);

    // tabs
    var tabs = "following    for you    live";
    var tx = (W - tabs.Length) / 2;
    cv.Text(tx, y0 + 2, "following", 0x6e6888);
    cv.Text(tx + 15, y0 + 2, "for you", 0xffffff);
    cv.Text(tx + 28, y0 + 2, "live", 0x6e6888);
    cv.Set(tx + 19, y0 + 3, '━', 0xff4fa3);
    cv.Set(tx + 20, y0 + 3, '━', 0xff4fa3);

    if (state == 2) return;

    var cx0 = x0 + 2;
    var cx1 = x1 - 2;
    var headerY = y0 + 4;
    var photoY0 = headerY + 1;
    var buttonsY = y1 - 5;
    var photoY1 = Math.Max(photoY0 + 3, Math.Min(photoY0 + 11, buttonsY - 9));
    var captionY = photoY1 + 2;
    var innerW = cx1 - cx0 + 1;

    cv.Fill(cx0 - 1, headerY, cx1 + 1, buttonsY - 2, ' ', 0x101018);

    // post header
    cv.Set(cx0, headerY, '●', Col.Hsl(post.Hue, 0.9, 0.62));
    cv.Text(cx0 + 2, headerY, post.Handle, 0xf4f0ff);
    cv.Text(cx0 + 2 + post.Handle.Length + 1, headerY, post.Tag, Col.Hsl(post.Hue, 0.6, 0.6));
    cv.TextRight("•••", cx1, headerY, 0x6e6888);
    cv.TextRight("2h", cx1 - 4, headerY, 0x6e6888);

    // photo / gradient art
    for (var yy = 0; yy <= photoY1 - photoY0; yy++)
    {
        for (var xx = 0; xx < innerW; xx++)
        {
            var u = (xx * 0.055 + yy * 0.27 + fx.Time * 0.04 + post.Hue * 0.01) % 1.0;
            if (u < 0) u += 1;
            var ch = u < 0.22 ? '░' : u < 0.52 ? '▒' : u < 0.8 ? '▓' : '█';
            var l = 0.16 + 0.42 * Math.Abs(Math.Sin(xx * 0.3 + yy * 0.55 + post.Hue));
            cv.Set(cx0 + xx, photoY0 + yy, ch, Col.Hsl(post.Hue + xx * 0.9, 0.8, l));
        }
    }
    var gx = cx0 + innerW / 2;
    var gy = photoY0 + (photoY1 - photoY0) / 2;
    cv.Set(gx, gy, post.Glyph[0], 0xfff6ff);
    fx.Spawn(gx, gy, 0, 0, 0.24, 0xffffff, 0xffffff, '·', 0, 0);

    // tag + caption
    cv.Text(cx0, captionY, post.Tag, Col.Hsl(post.Hue, 0.7, 0.62));
    var lines = Ui.Wrap(post.Body, innerW);
    for (var i = 0; i < lines.Length && i < 2; i++)
        cv.Text(cx0, captionY + 1 + i, lines[i], 0xeeeaf8);

    // engagement
    var likes = Fmt(post.Likes);
    var eng = $"♡ {likes}    ◌ {post.Comments}    ↗ {post.Shares}";
    cv.Text(cx0, buttonsY - 2, eng, 0x8a84a2);
    cv.Set(cx0, buttonsY - 2, '♡', 0xff6fb1);

    // decision buttons
    var bw = innerW / 3;
    var labels = new[] { "← skip", "like →", "↓ block" };
    var colors = new[] { 0xb9a7ff, 0xff4fa3, 0xffd166 };
    for (var i = 0; i < 3; i++)
    {
        var bx = cx0 + i * bw;
        cv.Fill(bx + 1, buttonsY, bx + bw - 2, buttonsY, ' ', 0x1b1a28);
        var lx = bx + (bw - labels[i].Length) / 2;
        cv.Text(lx, buttonsY, labels[i], colors[i]);
    }

    // timer
    var timerW = Math.Max(8, innerW - 18);
    var timerX = cx0 + (innerW - timerW) / 2;
    var frac = duration <= 0 ? 0 : Math.Clamp(timeLeft / duration, 0, 1);
    var tc = frac > 0.55 ? 0x7ff5e3 : frac > 0.28 ? 0xffd166 : 0xff4d6d;
    Ui.Bar(cv, timerX, y1 - 3, timerW, frac, tc, Col.Mix(tc, 0xffffff, 0.3));
    cv.TextC($"screen time {rounds / 4 + 3}h {(rounds * 7) % 60:00}m", 0x4e4a62, y1 - 2);
    cv.TextC("▬", 0x5c5870, y1 - 1);

    if (pauseT > 0)
    {
        var vx = cx0 + 3;
        var vy = photoY0 + 1;
        cv.Fill(vx, vy, cx1 - 3, vy + 2, ' ', 0x0c0c14);
        cv.TextC(verdict, verdictCol, vy + 1);
        if (!dying) cv.TextC(combo > 1 ? $"streak x{combo}" : "", 0xff8fc8, vy + 2);
    }
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.7);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "d o o m s c r o l l";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 8, title, 0xff4fa3, 0xb9a7ff, (t * 0.15) % 1);
    cv.TextGrad(x, 9, "────────────────────", 0xff4fa3, 0x7ff5e3, (t * 0.15) % 1);
    cv.TextC("a terminal game about touching grass", 0xd8cfe8, 11);
    cv.TextC("(eventually)", 0x6e6888, 12);
    if (best > 0) cv.TextC($"best: {Fmt(best)} followers", 0xffd166, 14);
    if (t % 1.3 < 0.8) cv.TextC("PRESS SPACE TO SCROLL", 0xffffff, 17);
    cv.TextC("← skip      → like      ↓ block", 0x8a84a2, 20);
    cv.TextC("q quits. the feed does not.", 0x4e4a62, 22);
}

void DrawDead()
{
    PhoneGeom(out var x0, out var y0, out var x1, out var y1);
    cv.Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ' ', 0x07070c);
    var cy = y0 + 6;
    cv.TextC("SCREEN TIME REPORT", 0xff4fa3, cy);
    cv.TextC("your battery died at 3:47am", 0x8a84a2, cy + 2);
    cv.TextC($"{Fmt(score)} followers", 0xffffff, cy + 5);
    cv.TextC($"{rounds} posts scrolled    best streak x{bestCombo}", 0xb9a7ff, cy + 6);
    cv.TextC(newRecord ? "new personal best (concerning)" : $"best: {Fmt(best)}", newRecord ? 0xffd166 : 0x6e6888, cy + 8);
    if (fx.Time % 1.1 < 0.75) cv.TextC("SPACE rot again    ESC title    Q quit", 0xd8cfe8, cy + 11);
    cv.TextC("consider touching grass.", 0x5c8a5c, cy + 13);
    cv.TextC("▱▱▱▱  0%", 0xff4d6d, y1 - 2);
}

string Fmt(int n) => n >= 1000 ? $"{n / 1000.0:0.#}k" : n.ToString();

Post MakePost()
{
    var kindRoll = rng.Next(10);
    var kind = kindRoll < 5 ? 0 : kindRoll < 8 ? 1 : 2;
    var pool = kind == 0 ? Content.Slay : kind == 1 ? Content.Cringe : Content.Ads;
    var item = pool[rng.Next(pool.Length)];
    return new Post
    {
        Kind = kind,
        Tag = item.Tag,
        Body = item.Body,
        Handle = item.Handle,
        Glyph = kind == 0 ? (rng.Next(2) == 0 ? "♡" : "✦") : kind == 1 ? "☁" : "$",
        Hue = kind == 0 ? 315 + rng.Next(45) : kind == 1 ? 20 + rng.Next(30) : 48 + rng.Next(18),
        Likes = kind == 0 ? rng.Next(1200, 98000) : rng.Next(40, 4200),
        Comments = rng.Next(12, 990),
        Shares = rng.Next(3, 420)
    };
}

sealed class Pop
{
    public double X;
    public double Y;
    public string S = "";
    public int C;
    public double Life;
    public double Max;
    public Pop(double x, double y, string s, int c, double life, double max)
    {
        X = x; Y = y; S = s; C = c; Life = life; Max = max;
    }
}

sealed class Post
{
    public int Kind;
    public string Tag = "";
    public string Body = "";
    public string Handle = "";
    public string Glyph = "♡";
    public int Hue;
    public int Likes;
    public int Comments;
    public int Shares;
}

static class Content
{
    public static readonly (string Tag, string Body, string Handle)[] Slay =
    {
        ("#relatable", "my 3am playlist is not a phase, it's a personality", "@softlaunch"),
        ("#healing", "the barista spelled my name right. we're getting married.", "@icedlatte"),
        ("#maincharacter", "walked past a dog in a tiny raincoat and felt something again", "@sundayscaries"),
        ("#poetry", "my roman empire is a text i sent in 2022 and never got a reply to", "@overtinking"),
        ("#girlmath", "saved $40 by spending $120. the math is mathing.", "@financediary"),
        ("#delulu", "he liked a post from 2019. the investigation is ongoing.", "@fbistalker"),
        ("#grwm", "getting ready to sit in my room and romanticize my life", "@vlogcore"),
        ("#wellness", "i drank water AND went outside. i am basically a new person", "@touchgrass"),
        ("#situationship", "we are not dating, we just text every single day at 3am", "@nocommitment"),
        ("#corecore", "the sun came through the window and i forgot to be sad for 4 seconds", "@cinematic"),
        ("#softlife", "romanticizing the walk to the bin like it's a runway", "@slowmornings"),
        ("#unhinged", "i miss the version of me who could nap without a podcast", "@sleepdebt")
    };

    public static readonly (string Tag, string Body, string Handle)[] Cringe =
    {
        ("#fyp", "REPLY 'AMEN' TO CLAIM YOUR BLESSING", "@blessedbot"),
        ("#fyp", "my crypto course: 7 spots left and they're going fast", "@gurucore"),
        ("#fyp", "a 47-slide story about someone's engagement", "@ringwatch"),
        ("#fyp", "a stranger explaining the cloud conspiracy", "@skylore"),
        ("#fyp", "repost if you love your mother (99% won't)", "@guiltposting"),
        ("#fyp", "give me 5 minutes to change your entire life", "@hustlehaus"),
        ("#fyp", "we bought a house at 22 and here's how you can too", "@landlordbaby"),
        ("#fyp", "a 6-minute video of a guy chewing", "@asmrzone"),
        ("#fyp", "rate my ex's new girlfriend with me", "@exfiles"),
        ("#fyp", "this is your sign to buy a $90 candle", "@scentedlie"),
        ("#fyp", "a slideshow of someone's gym routine. 38 slides.", "@proteinshake"),
        ("#fyp", "why your star sign is the reason you're single", "@cosmiccope")
    };

    public static readonly (string Tag, string Body, string Handle)[] Ads =
    {
        ("SPONSORED", "detox tea that will simply not work", "@teatox"),
        ("SPONSORED", "90% off a haul you will forget by tuesday", "@fastfashion"),
        ("SPONSORED", "an AI app that texts like your ex", "@exbot"),
        ("SPONSORED", "a mattress with a monthly subscription", "@sleepsub"),
        ("SPONSORED", "crypto for people who love losing money", "@coinflip"),
        ("SPONSORED", "skinny syrup: now with more consequences", "@syrupco"),
        ("SPONSORED", "a course on how to get rich while sleeping", "@dreamrich"),
        ("SPONSORED", "rent-a-friend premium: unlimited small talk", "@friendpass")
    };
}

static class Verdicts
{
    public static readonly string[] Good = { "slayed.", "ate.", "mother.", "real.", "no notes.", "clocked it.", "big brain.", "locked in." };
    public static readonly string[] Bad = { "cringe.", "yikes.", "not the vibe.", "delete.", "oof.", "missed the lore.", "logged off." };
}
