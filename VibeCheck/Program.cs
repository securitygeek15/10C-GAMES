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
var fx = new Fx();
var rng = new Random();
var best = Store.Best("vibecheck");
var notes = new List<Note>();
var state = 0;          // 0 title, 1 play, 2 results
var clock = 0.0;
var score = 0;
var combo = 0;
var maxCombo = 0;
var judgeT = 0.0;
var judgeText = "";
var judgeCol = 0xffffff;
var judgeLane = 0;
var newRecord = false;
var stateT = 0.0;
var songEnd = 40.0;
var lastBeatPulse = -1;
var flash = 0.0;
var flashCol = 0xffffff;
var hue = 0.0;

const double BPM = 118;
var beatDur = 60.0 / BPM;
var laneW = 7;
var totalW = 4 * laneW + 3;
var lane0 = (W - totalW) / 2;
var hitY = H - 5;
var laneNames = new[] { "D", "F", "J", "K" };
var laneColors = new[] { 0x9be564, 0x7ff5e3, 0xff8fc8, 0xb9a7ff };
var approach = 1.45;

void BuildChart()
{
    notes.Clear();
    var totalBeats = 84;
    for (var i = 0; i < totalBeats; i++)
    {
        var time = 2.4 + i * beatDur;
        if (i % 8 == 0)
        {
            var a = rng.Next(4);
            var b = rng.Next(4);
            while (b == a) b = rng.Next(4);
            notes.Add(new Note { Lane = a, Time = time });
            notes.Add(new Note { Lane = b, Time = time });
        }
        else if (i % 4 == 0)
        {
            notes.Add(new Note { Lane = rng.Next(4), Time = time });
            if (rng.NextDouble() < 0.55) notes.Add(new Note { Lane = rng.Next(4), Time = time });
        }
        else if (rng.NextDouble() < 0.44)
        {
            notes.Add(new Note { Lane = rng.Next(4), Time = time });
        }

        if (i % 8 == 6 && rng.NextDouble() < 0.5)
            notes.Add(new Note { Lane = rng.Next(4), Time = time + beatDur / 2 });
        if (i % 8 == 4 && rng.NextDouble() < 0.35)
            notes.Add(new Note { Lane = rng.Next(4), Time = time + beatDur / 2 });
    }
    notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    songEnd = notes[^1].Time + 2.2;
}

void Start()
{
    BuildChart();
    clock = 0;
    score = 0;
    combo = 0;
    maxCombo = 0;
    judgeT = 0;
    judgeText = "";
    newRecord = false;
    stateT = 0;
    lastBeatPulse = -1;
}

void TryHit(int lane)
{
    Note? found = null;
    var bestDelta = 0.16;
    foreach (var n in notes)
    {
        if (n.Lane != lane || n.State != 0) continue;
        var d = Math.Abs(n.Time - clock);
        if (d <= bestDelta)
        {
            bestDelta = d;
            found = n;
        }
    }
    if (found is null) return;

    found.State = 1;
    var delta = Math.Abs(found.Time - clock);
    string label;
    int baseScore;
    if (delta <= 0.055) { label = "perfect"; baseScore = 1000; found.Weight = 1.0; }
    else if (delta <= 0.1) { label = "great"; baseScore = 700; found.Weight = 0.7; }
    else { label = "good"; baseScore = 400; found.Weight = 0.4; }

    combo++;
    maxCombo = Math.Max(maxCombo, combo);
    var mult = 1 + Math.Min(4, combo / 10);
    score += baseScore * mult;
    judgeText = label;
    judgeCol = delta <= 0.055 ? 0x9be564 : delta <= 0.1 ? 0x7ff5e3 : 0xffd166;
    judgeT = 0.55;
    judgeLane = lane;

    var lx = lane0 + lane * (laneW + 1);
    fx.Burst(lx + 3, hitY, 14, laneColors[lane], 0xffffff, 12, 0.55, 2, 2.4, '✦');
    flash = 0.08;
    flashCol = laneColors[lane];
}

void Finish()
{
    state = 2;
    stateT = 0;
    if (score > best)
    {
        best = score;
        Store.Save("vibecheck", score);
        newRecord = true;
    }
}

int LaneX(int lane) => lane0 + lane * (laneW + 1);

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 30) % 360;
    stateT += dt;
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.5);
    if (judgeT > 0) judgeT = Math.Max(0, judgeT - dt);

    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter)) { Start(); state = 1; }
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.35)
        {
            var lane = rng.Next(4);
            fx.Spawn(LaneX(lane) + 3, hitY, 0, -6, 1.2, laneColors[lane], 0x2a1a30, '▄', 0, 0);
        }
    }
    else if (state == 1)
    {
        if (Input.Quit) return false;
        clock += dt;

        if (Input.Hit(ConsoleKey.D)) TryHit(0);
        if (Input.Hit(ConsoleKey.F)) TryHit(1);
        if (Input.Hit(ConsoleKey.J)) TryHit(2);
        if (Input.Hit(ConsoleKey.K)) TryHit(3);

        foreach (var n in notes)
        {
            if (n.State != 0 || n.Time > clock - 0.16) continue;
            n.State = 2;
            n.Weight = 0;
            combo = 0;
            judgeText = "miss";
            judgeCol = 0xff5c7a;
            judgeT = 0.4;
            judgeLane = n.Lane;
        }

        var beatIndex = (int)(clock / beatDur);
        if (beatIndex != lastBeatPulse)
        {
            lastBeatPulse = beatIndex;
            if (clock > 1) flash = Math.Max(flash, 0.03);
        }

        if (clock >= songEnd) Finish();
    }
    else
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.R)) { Start(); state = 1; }
        if (Input.Hit(ConsoleKey.Escape)) { state = 0; stateT = 0; }
        if (Input.Quit) return false;
    }

    fx.Update(dt);

    cv.Clear();
    DrawBackdrop();
    if (state == 0)
    {
        DrawTitle();
        fx.Draw(cv);
    }
    else
    {
        DrawPlayfield();
        fx.Draw(cv);
        DrawHud();
        if (state == 2) DrawResults();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.3, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"vibe check complete. score {score}, best {best}. your vibe is intact.");

ConsoleKey KeyForLane(int lane) => lane switch
{
    0 => ConsoleKey.D,
    1 => ConsoleKey.F,
    2 => ConsoleKey.J,
    _ => ConsoleKey.K
};

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x080a06, 0x14101a, u);
        for (var x = 0; x < W; x++)
        {
            var dot = (x * 7 + y * 5 + (int)(fx.Time * 6)) % 107 == 0;
            cv.Set(x, y, dot ? '·' : ' ', dot ? 0x33402a : c);
        }
    }
    DrawEqualizer();
}

void DrawEqualizer()
{
    var left = lane0 - 2;
    var right = lane0 + totalW + 1;
    var maxH = Math.Min(H / 2, 9);
    if (left > 2)
    {
        for (var k = 0; k < Math.Min(8, left - 1); k++)
        {
            var h = 1 + (int)(Math.Abs(Math.Sin(fx.Time * (1.2 + k * 0.17) + k)) * maxH);
            var x = left - 1 - k;
            for (var i = 0; i < h; i++)
                cv.Set(x, hitY - i, '█', Col.Hsl(90 + k * 18, 0.7, 0.25 + i * 0.04));
        }
    }
    if (W - right > 2)
    {
        for (var k = 0; k < Math.Min(8, W - right - 1); k++)
        {
            var h = 1 + (int)(Math.Abs(Math.Cos(fx.Time * (1.1 + k * 0.2) + k)) * maxH);
            var x = right + 1 + k;
            for (var i = 0; i < h; i++)
                cv.Set(x, hitY - i, '█', Col.Hsl(300 + k * 14, 0.7, 0.25 + i * 0.04));
        }
    }
}

void DrawPlayfield()
{
    for (var lane = 0; lane < 4; lane++)
    {
        var lx = LaneX(lane);
        for (var y = 3; y <= hitY; y++)
        {
            for (var x = lx; x < lx + laneW; x++)
            {
                var pulse = 0.5 + 0.5 * Math.Sin(clock * 4 + lane);
                var c = Col.Mix(0x0d0d12, Col.Scale(laneColors[lane], 0.1 + pulse * 0.04), 1);
                cv.Set(x, y, ' ', c);
            }
            if (y % 3 == 0) cv.Set(lx + 3, y, '·', Col.Scale(laneColors[lane], 0.3));
        }
        for (var y = 3; y <= hitY; y++) cv.Set(lx - 1, y, '│', 0x2a2632);
    }
    cv.Set(lane0 + totalW, hitY, '│', 0x2a2632);
    for (var y = 3; y <= hitY; y++) cv.Set(lane0 + totalW, y, '│', 0x2a2632);

    foreach (var n in notes)
    {
        if (n.State != 0) continue;
        var delta = n.Time - clock;
        if (delta > approach || delta < -0.16) continue;
        var y = (int)Math.Round(hitY - delta * ((hitY - 4) / approach));
        if (y < 3 || y > hitY) continue;
        var lx = LaneX(n.Lane);
        var c = laneColors[n.Lane];
        for (var x = 0; x < 5; x++)
        {
            cv.Set(lx + 1 + x, y, '█', c);
            cv.Set(lx + 1 + x, y - 1, '▀', Col.Mix(c, 0xffffff, 0.35));
        }
        cv.Set(lx + 3, y + 1, '▄', Col.Scale(c, 0.5));
    }

    // hit line
    var linePulse = 0.55 + 0.45 * Math.Sin(clock * 6);
    for (var lane = 0; lane < 4; lane++)
    {
        var lx = LaneX(lane);
        for (var x = lx; x < lx + laneW; x++)
            cv.Set(x, hitY, '═', Col.Mix(laneColors[lane], 0xffffff, linePulse * 0.35));
    }

    // key labels
    for (var lane = 0; lane < 4; lane++)
    {
        var lx = LaneX(lane);
        var active = judgeT > 0 && judgeLane == lane;
        cv.Text(lx + 3, H - 3, laneNames[lane], active ? 0xffffff : laneColors[lane]);
    }

    if (judgeT > 0)
    {
        var k = Math.Min(1.0, judgeT / 0.3);
        cv.TextC(judgeText, Col.Scale(judgeCol, k), hitY - 3);
    }

    if (clock < 2.4 && state == 1)
    {
        var n = Math.Max(1, (int)Math.Ceiling((2.4 - clock) / 0.6));
        cv.TextC(n.ToString(), 0xffffff, hitY - 6);
        cv.TextC("get ready", 0x8a7aa8, hitY - 4);
    }

    for (var lane = 0; lane < 4; lane++)
    {
        if (!Input.Held(KeyForLane(lane))) continue;
        var lx = LaneX(lane);
        cv.Set(lx + 3, hitY - 1, '▀', laneColors[lane]);
    }
}

void DrawHud()
{
    cv.TextGrad(2, 0, "vibe check", 0x9be564, 0xff8fc8, (hue / 360) % 1);
    cv.TextRight($"{score:0000000}", W - 3, 0, 0xf4ffe8);
    cv.Text(2, 1, $"combo x{combo}", combo >= 10 ? 0x9be564 : 0x6a8a5a);
    var accuracy = Accuracy();
    cv.TextC($"{accuracy:0.0}%    max combo x{maxCombo}", 0x8a7aa8, 1);
    cv.TextRight("BPM 118", W - 3, 1, 0x5a6a4a);
    var frac = Math.Clamp(clock / songEnd, 0, 1);
    Ui.Bar(cv, 2, 2, W - 5, frac, 0x9be564, 0xff8fc8);
    cv.TextC("d  f  j  k", 0x5a6a4a, H - 2);
    cv.TextC("d f j k  hit     q quit", 0x3e4a38, H - 1);
}

double Accuracy()
{
    if (notes.Count == 0) return 100;
    var sum = 0.0;
    foreach (var n in notes) sum += n.Weight;
    return sum / notes.Count * 100;
}

string Grade()
{
    var acc = Accuracy();
    return acc >= 95 ? "S" : acc >= 88 ? "A" : acc >= 78 ? "B" : acc >= 65 ? "C" : "D";
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "v i b e   c h e c k";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 6, title, 0x9be564, 0xff8fc8, (t * 0.14) % 1);
    cv.TextGrad(x, 7, "──────────────────", 0x7ff5e3, 0xb9a7ff, (t * 0.14) % 1);
    cv.TextC("a rhythm game for people who say \"i'm fine\"", 0xe0f0d8, 9);
    cv.TextC("d  f  j  k  —  four lanes, one vibe", 0x8a9a78, 10);

    for (var lane = 0; lane < 4; lane++)
    {
        var lx = (W - 31) / 2 + lane * 8;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 3 + lane);
        for (var k = 0; k < 5; k++)
            cv.Set(lx + k, 14 + (int)(Math.Sin(t * 2 + lane) * 2), '▄', laneColors[lane]);
        cv.Set(lx + 2, 16, (char)('0' + lane + 1), Col.Scale(laneColors[lane], 0.4 + pulse * 0.6));
    }

    if (best > 0) cv.TextC($"best {best:0000000}", 0xffd166, 18);
    if (t % 1.2 < 0.78) cv.TextC("PRESS SPACE TO LOCK IN", 0xffffff, 20);
    cv.TextC("d f j k  to hit      q quit", 0x6a7a62, 21);
}

void DrawResults()
{
    var panelW = Math.Min(58, W - 4);
    var x0 = (W - panelW) / 2;
    var x1 = x0 + panelW - 1;
    var y0 = Math.Max(3, H / 2 - 7);
    var y1 = Math.Min(H - 2, y0 + 12);
    cv.Fill(x0, y0, x1, y1, ' ', 0x0a0d08);
    cv.Frame(x0, y0, x1, y1, 0x9be564);

    var grade = Grade();
    cv.TextGrad(x0 + 3, y0 + 2, $"vibe check: {grade}", 0x9be564, 0xff8fc8, (fx.Time * 0.1) % 1);
    cv.TextC($"score {score:0000000}", 0xf4ffe8, y0 + 4);
    cv.TextC($"accuracy {Accuracy():0.0}%    max combo x{maxCombo}", 0xb9a7ff, y0 + 5);
    cv.TextC(newRecord ? "new personal best" : $"best {best:0000000}", newRecord ? 0xffd166 : 0x6a7a62, y0 + 7);
    cv.TextC(grade switch
    {
        "S" => "main character energy.",
        "A" => "your vibe is intact.",
        "B" => "the vibe is vibing.",
        "C" => "the vibe is buffering.",
        _ => "the vibe left the chat."
    }, 0x8a9a78, y0 + 9);
    if (fx.Time % 1.1 < 0.76) cv.TextC("SPACE again    ESC title    Q quit", 0xd8e8d0, y0 + 11);
}

sealed class Note
{
    public int Lane;
    public double Time;
    public int State;   // 0 pending, 1 hit, 2 missed
    public double Weight;
}
