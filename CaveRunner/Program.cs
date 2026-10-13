using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(90, 30, 56, 20);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 56x20 and re-run.");
    return;
}

var W = size.W;
var H = size.H;
var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();

var best = Store.Best("caverunner");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

const int TopLimit = 2;
const int BotLimit = -3;   // relative to H: bottom limit = H + BotLimit

var cols = new List<CaveCol>();
var scrollX = 0.0;
var nextIdx = 0;
var playerY = H / 2.0;
var playerVY = 0.0;
var speed = 13.0;
var crystals = 0;
var shield = 0;
var invuln = 0.0;
var flapT = 0.0;
var score = 0;
var meters = 0;
var newRecord = false;
var deadT = -1.0;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 dead
var prevState = -1;
var stateT = 0.0;
var quit = false;

int PlayerSX() => Math.Max(6, W / 5);

double Hash01(int i)
{
    unchecked
    {
        var h = i * 374761393 + 12345;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7fffffff) / (double)int.MaxValue;
    }
}

void GenerateAhead()
{
    var viewEnd = (int)(scrollX + W + 6);
    while (nextIdx <= viewEnd)
    {
        var i = nextIdx;
        if (i == 0)
        {
            cols.Add(new CaveCol { Top = TopLimit + 5, Bottom = H - 7 });
            nextIdx++;
            continue;
        }
        var prev = cols[^1];
        var gap = Math.Max(8, (int)(13 - i / 700.0));
        var mid = (prev.Top + prev.Bottom) / 2.0;
        mid += (rng.NextDouble() - 0.5) * 7;
        var top = (int)Math.Clamp(mid - gap / 2.0, TopLimit, H + BotLimit - gap);
        var bottom = top + gap;
        var col = new CaveCol { Top = top, Bottom = bottom };
        var roll = rng.NextDouble();
        if (roll < 0.22) col.Crystal = top + 1 + rng.Next(Math.Max(1, gap - 2));
        else if (roll < 0.27 && shield == 0) col.Shield = true;
        cols.Add(col);
        nextIdx++;
    }
    while (cols.Count > 0 && nextIdx - cols.Count < (int)scrollX - 3)
        cols.RemoveAt(0);
}

void StartGame()
{
    cols.Clear();
    nextIdx = 0;
    scrollX = 0;
    playerY = H / 2.0;
    playerVY = 0;
    speed = 13;
    crystals = 0;
    shield = 0;
    invuln = 1.0;
    score = 0;
    meters = 0;
    newRecord = false;
    deadT = -1;
    GenerateAhead();
}

void Die()
{
    if (invuln > 0) return;
    if (shield > 0)
    {
        shield = 0;
        invuln = 1.6;
        shake = 9;
        flash = 0.55;
        flashCol = 0x66ddff;
        fx.Burst(PlayerSX(), playerY, 34, 0x66ddff, 0xffffff, 22, 0.9, 0, 2);
        fx.Ring(PlayerSX(), playerY, 20, 30, 0.6, 0x66ddff, 0xffffff, '·');
        pops.Add((PlayerSX() - 3, playerY - 2, "SHIELD LOST", 0x66ddff, 1.2, 1.2));
        playerVY = -14;
        return;
    }
    state = 3;
    stateT = 0;
    deadT = 0;
    shake = 12;
    flash = 0.8;
    flashCol = 0xff5c3a;
    fx.Burst(PlayerSX(), playerY, 60, 0xffb84d, 0xff3b5c, 30, 1.3, 0, 2);
    fx.Ring(PlayerSX(), playerY, 26, 40, 0.7, 0xffffff, 0xff9f1c, '█');
    if (score > best)
    {
        best = score;
        Store.Save("caverunner", score);
        newRecord = true;
    }
}

void Flap()
{
    playerVY = -20;
    flapT = 0.18;
    fx.Burst(PlayerSX() - 2, playerY + 1, 8, 0x66ddff, 0xffffff, 10, 0.35, 0, 4);
    shake = Math.Max(shake, 1);
}

var sw = Stopwatch.StartNew();
var prevT = 0.0;
const double STEP = 1.0 / 60;

while (!quit)
{
    var now = sw.Elapsed.TotalSeconds;
    var dt = Math.Min(now - prevT, 0.1);
    prevT = now;
    Input.Poll();

    // ---------- input ----------
    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter))
        {
            StartGame();
            state = 1;
            Flap();
        }
        if (Input.Quit) quit = true;
    }
    else if (state == 1)
    {
        if (Input.Hit(ConsoleKey.Escape) || Input.Hit(ConsoleKey.P)) state = 2;
        if (Input.Quit) quit = true;
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) Flap();
    }
    else if (state == 2)
    {
        if (Input.Hit(ConsoleKey.Escape) || Input.Hit(ConsoleKey.P)) state = 1;
        if (Input.Hit(ConsoleKey.R))
        {
            StartGame();
            state = 1;
        }
        if (Input.Quit) quit = true;
    }
    else
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.R))
        {
            StartGame();
            state = 1;
            Flap();
        }
        if (Input.Hit(ConsoleKey.Escape)) state = 0;
        if (Input.Quit) quit = true;
    }

    if (state != prevState)
    {
        stateT = 0;
        prevState = state;
    }
    stateT += dt;
    hue = (hue + dt * 26) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 24);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    if (invuln > 0) invuln -= dt;
    if (flapT > 0) flapT -= dt;

    // ---------- update ----------
    if (state == 0)
    {
        scrollX += 5 * dt;
        if (rng.NextDouble() < 0.4)
            fx.Spawn(rng.Next(W), -1, 0, 5 + rng.NextDouble() * 8, (H + 2.0) / (5 + rng.NextDouble() * 8),
                Col.Hsl(rng.Next(260, 340), 0.8, 0.55), 0xffffff, '·', 0, 0);
        GenerateAhead();
    }
    else if (state == 1)
    {
        speed = 13 + Math.Min(13, scrollX / 550.0);
        scrollX += speed * dt;
        playerVY += 58 * dt;
        if (playerVY > 30) playerVY = 30;
        playerY += playerVY * dt;
        GenerateAhead();

        if (rng.NextDouble() < 0.5)
            fx.Spawn(PlayerSX() - 2, playerY + (rng.NextDouble() - 0.5) * 2, -speed * (0.6 + rng.NextDouble()),
                (rng.NextDouble() - 0.5) * 3, 0.4 + rng.NextDouble() * 0.3, 0x66ddff, 0x1a3a6a, '·', 0, 1.5);

        // speed lines in open space
        if (rng.NextDouble() < 0.3)
        {
            var li = (int)(scrollX + W) + rng.Next(3);
            var lc = FindCol(li);
            var yy = lc != null ? rng.Next(lc.Top + 1, Math.Max(lc.Top + 2, lc.Bottom)) : rng.Next(2, H - 2);
            fx.Spawn(W + 2, yy, -speed * 3, 0, 1.2, 0x5a7adf, 0x2a3a7a, '─', 0, 0);
        }

        // ceiling drips
        if (rng.NextDouble() < 0.25)
        {
            var ci = (int)(scrollX + W) + 1;
            var lc = FindCol(ci);
            if (lc != null)
                fx.Spawn(W + 1, lc.Top, -speed, 2 + rng.NextDouble() * 3, 1.0, 0x8a7452, 0x3a2e1a, '·', 8, 0.4);
        }

        // collision + pickups
        var sx = PlayerSX();
        var colIdxF = scrollX + sx;
        var c1 = FindCol((int)Math.Floor(colIdxF));
        var c2 = FindCol((int)Math.Floor(colIdxF + 1.5));
        foreach (var lc in new[] { c1, c2 })
        {
            if (lc == null) continue;
            if (playerY - 1 <= lc.Top + 0.4 || playerY + 1 >= lc.Bottom - 0.4)
            {
                Die();
                break;
            }
        }

        if (state == 1)
        {
            for (var ci2 = (int)(scrollX + sx) - 1; ci2 <= (int)(scrollX + sx) + 1; ci2++)
            {
                var lc = FindCol(ci2);
                if (lc == null) continue;
                var screenX = ci2 - scrollX;
                if (Math.Abs(screenX - sx) > 1.6) continue;
                if (lc.Crystal >= 0 && Math.Abs(playerY - lc.Crystal) < 1.8)
                {
                    var cy = lc.Crystal;
                    lc.Crystal = -1;
                    crystals++;
                    fx.Burst(screenX, cy, 16, 0x66ddff, 0xffffff, 14, 0.6, 0, 3);
                    pops.Add((screenX - 2, cy - 1, "+50", 0x66ddff, 0.9, 0.9));
                    flash = Math.Max(flash, 0.14);
                    flashCol = 0x66ddff;
                }
                if (lc.Shield && Math.Abs(playerY - (lc.Top + lc.Bottom) / 2.0) < 2.2)
                {
                    lc.Shield = false;
                    shield = 1;
                    fx.Burst(screenX, playerY, 20, 0xffd700, 0xffffff, 16, 0.7, 0, 3);
                    pops.Add((screenX - 3, playerY - 2, "+SHIELD", 0xffd700, 1.1, 1.1));
                }
            }
        }

        meters = (int)(scrollX * 2);
        score = (int)(scrollX * 3) + crystals * 50;

        // floor/ceiling safety
        if (playerY < 1) { playerY = 1; playerVY = 0; }
        if (playerY > H - 2) { playerY = H - 2; playerVY = 0; }
    }
    else if (state == 3 && deadT >= 0)
    {
        deadT += dt;
        playerVY += 40 * dt;
        playerY = Math.Min(H - 2, playerY + playerVY * dt);
        if (deadT > 1.2) deadT = -1;
    }

    fx.Update(dt);
    for (var i = pops.Count - 1; i >= 0; i--)
    {
        var p = pops[i];
        var l = p.Life - dt;
        if (l <= 0) { pops.RemoveAt(i); continue; }
        pops[i] = (p.X, p.Y - dt * 7, p.S, p.C, l, p.Max);
    }

    // ---------- render ----------
    cv.Clear();
    cv.ShakeX = shake > 0.2 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.2 ? rng.Next(-(int)shake, (int)shake + 1) : 0;

    DrawCave();

    if (state == 0)
    {
        fx.Draw(cv);
        DrawTitle();
        DrawTitleOver();
    }
    else
    {
        if (state != 3 || deadT >= 0)
        {
            var blink = invuln > 0 && Math.Sin(fx.Time * 30) > 0.4;
            if (!blink) DrawPlayer();
        }
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (state == 2) DrawPause();
        if (state == 3) DrawOver();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.85, flash);
    }
    else if (state == 3 && stateT < 0.6)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = Math.Min(0.6, stateT * 1.1);
    }
    else if (state == 2)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = 0.55;
    }
    else cv.OverlayK = 0;

    cv.Present();

    var sleepMs = (int)((STEP - (sw.Elapsed.TotalSeconds - now)) * 1000);
    if (sleepMs > 0) Thread.Sleep(sleepMs);
}

RestoreConsole();
Console.WriteLine($"Thanks for playing CAVE RUNNER! {meters}m, score {score}, best {best}.");

// ---------- rendering ----------
CaveCol? FindCol(int worldIdx)
{
    var i = worldIdx - nextIdx + cols.Count;
    if (i < 0 || i >= cols.Count) return null;
    return cols[i];
}

void DrawCave()
{
    var t = fx.Time;

    // far layer (parallax 0.35)
    for (var x = 0; x < W; x++)
    {
        var ci = (int)Math.Floor((x + scrollX * 0.35) / 3.0);
        var hT = 3 + (int)(Hash01(ci) * 5 + Math.Sin(ci * 0.7) * 2);
        var hB = H - 4 - (int)(Hash01(ci + 777) * 5 + Math.Sin(ci * 0.5 + 2) * 2);
        for (var y = TopLimit; y <= hT; y++)
            cv.Set(x, y, '▒', Col.Scale(0x232a52, 1 - (y - TopLimit) * 0.1));
        for (var y = hB; y <= H + BotLimit; y++)
            cv.Set(x, y, '▒', Col.Scale(0x232a52, 1 - (H + BotLimit - y) * 0.1));
    }

    // mid layer (parallax 0.65)
    for (var x = 0; x < W; x++)
    {
        var ci = (int)Math.Floor((x + scrollX * 0.65) / 2.0);
        var hT = 2 + (int)(Hash01(ci + 4242) * 6);
        var hB = H - 3 - (int)(Hash01(ci + 9999) * 6);
        for (var y = TopLimit; y <= hT; y++)
            cv.Set(x, y, y == hT ? '▓' : '▒', Col.Scale(0x323a6b, 1 - (y - TopLimit) * 0.08));
        for (var y = hB; y <= H + BotLimit; y++)
            cv.Set(x, y, y == hB ? '▓' : '▒', Col.Scale(0x323a6b, 1 - (H + BotLimit - y) * 0.08));
    }

    // foreground cave (collision layer)
    for (var x = 0; x < W; x++)
    {
        var ci = (int)Math.Floor(x + scrollX);
        var lc = FindCol(ci);
        if (lc == null) continue;
        for (var y = TopLimit; y <= lc.Top; y++)
        {
            var strata = Col.Hsl(262 + y * 4 + Math.Sin(ci * 0.3) * 10, 0.35, 0.34);
            if (y == lc.Top)
            {
                var glow = 0.5 + 0.5 * Math.Sin(t * 5 + ci * 0.6);
                cv.Set(x, y, '▄', Col.Mix(strata, 0x9f6bff, 0.25 + glow * 0.2));
            }
            else cv.Set(x, y, (ci + y) % 5 == 0 ? '▓' : '█', Col.Scale(strata, y == TopLimit ? 1.1 : 1.0));
        }
        for (var y = lc.Bottom; y <= H + BotLimit; y++)
        {
            var strata = Col.Hsl(20 + (H + BotLimit - y) * 4, 0.4, 0.36);
            if (y == lc.Bottom)
            {
                var glow = 0.5 + 0.5 * Math.Sin(t * 5 + ci * 0.6 + 2);
                cv.Set(x, y, '▀', Col.Mix(strata, 0xff9f5c, 0.25 + glow * 0.2));
            }
            else cv.Set(x, y, (ci + y) % 5 == 0 ? '▓' : '█', Col.Scale(strata, 1.0));
        }

        // crystals & shields
        if (lc.Crystal >= 0)
        {
            var sx2 = ci - scrollX;
            var pulse = 0.5 + 0.5 * Math.Sin(t * 8 + ci);
            cv.Set((int)Math.Round(sx2), lc.Crystal, '♦', Col.Mix(0x66ddff, 0xffffff, pulse));
            if (rng.NextDouble() < 0.1)
                fx.Spawn(sx2, lc.Crystal, 0, -2, 0.6, 0x66ddff, 0xffffff, '·', -2, 0.5);
        }
        if (lc.Shield)
        {
            var sx2 = ci - scrollX;
            var cy = (lc.Top + lc.Bottom) / 2;
            var pulse = 0.5 + 0.5 * Math.Sin(t * 6 + ci);
            cv.Set((int)Math.Round(sx2), cy, '◉', Col.Mix(0xffd700, 0xffffff, pulse));
            cv.Set((int)Math.Round(sx2), cy - 1, '·', Col.Scale(0xffd700, 0.6));
        }
    }
}

void DrawPlayer()
{
    var t = fx.Time;
    var sx = PlayerSX();
    var y = (int)Math.Round(playerY);
    var body = Col.Mix(0x66ddff, 0xffffff, 0.4 + 0.3 * Math.Sin(t * 10) + (flapT > 0 ? 0.3 : 0));
    if (invuln > 0) body = Col.Mix(body, 0xffd700, 0.5);
    cv.Set(sx - 2, y, flapT > 0 ? '«' : '·', Col.Mix(body, 0xff9f1c, 0.4));
    cv.Set(sx - 1, y, '◄', body);
    cv.Set(sx, y, '◉', body);
    cv.Set(sx + 1, y, '►', body);
    cv.Set(sx + 2, y, flapT > 0 ? '»' : '·', Col.Mix(body, 0xff9f1c, 0.4));
    cv.Set(sx, y - 1, '·', Col.Scale(body, 0.5));
    cv.Set(sx, y + 1, '·', Col.Scale(body, 0.5));
    if (shield > 0)
    {
        var a = t * 7;
        cv.Set((int)(sx + Math.Cos(a) * 3), (int)(playerY + Math.Sin(a) * 2), '·', 0xffd700);
        cv.Set((int)(sx + Math.Cos(a + Math.PI) * 3), (int)(playerY + Math.Sin(a + Math.PI) * 2), '·', 0xffd700);
    }
}

void DrawHud()
{
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    cv.TextC($"{meters}m", Col.Hsl(hue, 0.8, 0.62), 0);

    if (shield > 0) cv.Text(2, H - 1, "SHIELD ◉", 0xffd700);
    cv.Text(W - 36, H - 1, "SPACE/W flap   P pause   Q quit", 0x607096);
    if (state == 1) cv.Text(W / 2 - 6, H - 1, $"CRYSTALS {crystals}", 0x66ddff);
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.6);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawPause()
{
    cv.TextC("PAUSED", 0xffffff, H / 2 - 1);
    cv.TextC("P / ESC resume    R restart    Q quit", 0xa8b8e0, H / 2 + 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "CAVE RUNNER";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("D E P T H S", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("3 parallax layers, crystals, one shield", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("SPACE / W to flap. Grab diamonds, dodge the rock.", 0x6f7fa8, 22);
}

void DrawOver()
{
    if (stateT < 0.6) return;
    var half = Math.Min(26, W / 2 - 1);
    var x0 = W / 2 - half;
    var x1 = W / 2 + half;
    var y0 = 7;
    var y1 = 17;
    for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            cv.Set(x, y, '█', 0x140b24);
    for (var x = x0; x <= x1; x++)
    {
        cv.Set(x, y0, '─', 0xff4d6d);
        cv.Set(x, y1, '─', 0xff4d6d);
    }
    for (var y = y0; y <= y1; y++)
    {
        cv.Set(x0, y, '│', 0xff4d6d);
        cv.Set(x1, y, '│', 0xff4d6d);
    }
    var t = fx.Time;
    cv.TextGrad(W / 2 - 5, y0 + 2, "CRASHED", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"{meters}m   CRYSTALS {crystals}", 0x9fb0d8, y0 + 6);
    cv.TextC(newRecord ? "NEW RECORD!" : $"BEST {best:D6}", newRecord ? 0xffd700 : 0x8fa3c8, y0 + 8);
    if (t % 1.0 < 0.7) cv.TextC("SPACE retry     ESC title", 0xc9d4ff, y0 + 10);
}

// ---------- engine ----------
void SetupConsole()
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            var h = Win.GetStdHandle(-11);
            if (Win.GetConsoleMode(h, out var m)) Win.SetConsoleMode(h, m | 0x4);
        }
    }
    catch { }
    try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
    try { Console.CursorVisible = false; } catch { }
    Console.Write("\x1b[2J\x1b[?25l\x1b[?7l");
}

void RestoreConsole()
{
    Console.Write("\x1b[0m\x1b[?25h\x1b[?7h");
    try { Console.CursorVisible = true; } catch { }
    try { Console.SetCursorPosition(0, 0); Console.Clear(); } catch { }
}

(int W, int H) Fit(int maxW, int maxH, int minW, int minH)
{
    var w = 100;
    var h = 30;
    try { w = Console.WindowWidth; h = Console.WindowHeight; } catch { }
    w = Math.Min(w, maxW);
    h = Math.Min(h, maxH);
    return w >= minW && h >= minH ? (w, h) : (0, 0);
}

class CaveCol
{
    public int Top;
    public int Bottom;
    public int Crystal = -1;    // y of crystal, -1 none
    public bool Shield;
}

static class Win
{
    [DllImport("kernel32")]
    public static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32")]
    public static extern bool GetConsoleMode(IntPtr h, out uint m);
    [DllImport("kernel32")]
    public static extern bool SetConsoleMode(IntPtr h, uint m);
}

sealed class Canvas
{
    public readonly int W;
    public readonly int H;
    readonly char[] _ch;
    readonly int[] _fg;
    readonly StringBuilder _sb;
    public int ShakeX;
    public int ShakeY;
    public int OverlayColor = 0x000000;
    public double OverlayK;

    public Canvas(int w, int h)
    {
        W = w;
        H = h;
        _ch = new char[w * h];
        _fg = new int[w * h];
        _sb = new StringBuilder(w * h + 64);
        Clear();
    }

    public void Clear()
    {
        Array.Fill(_ch, ' ');
        Array.Fill(_fg, 0);
    }

    public void Set(int x, int y, char c, int color)
    {
        x += ShakeX;
        y += ShakeY;
        if ((uint)x < (uint)W && (uint)y < (uint)H)
        {
            var i = y * W + x;
            _ch[i] = c;
            _fg[i] = color;
        }
    }

    public void Text(int x, int y, string s, int color)
    {
        for (var i = 0; i < s.Length; i++)
            if (s[i] != ' ') Set(x + i, y, s[i], color);
    }

    public void TextC(string s, int color, int y) => Text((W - s.Length) / 2, y, s, color);

    public void TextGrad(int x, int y, string s, int c1, int c2, double phase = -1)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var u = s.Length <= 1 ? 0 : (double)i / (s.Length - 1);
            if (phase >= 0) u = (u + phase) % 1.0;
            if (s[i] != ' ') Set(x + i, y, s[i], Col.Mix(c1, c2, u));
        }
    }

    public void Present()
    {
        _sb.Clear();
        _sb.Append("\x1b[H");
        var last = -1;
        var k = OverlayK;
        var oc = OverlayColor;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var i = y * W + x;
                var c = _ch[i];
                var fg = _fg[i];
                if (c == ' ' && fg != 0) c = '█';
                if (k > 0.004)
                {
                    if (c == ' ')
                    {
                        c = '█';
                        fg = Col.Scale(oc, k);
                    }
                    else fg = Col.Mix(fg, oc, k);
                }
                if (c != ' ')
                {
                    if (fg != last)
                    {
                        AppendColor(fg);
                        last = fg;
                    }
                    _sb.Append(c);
                }
                else _sb.Append(' ');
            }
            if (y < H - 1) _sb.Append("\r\n");
        }
        _sb.Append("\x1b[0m");
        Console.Write(_sb);
    }

    void AppendColor(int c)
    {
        _sb.Append("\x1b[38;2;");
        AppendNum((c >> 16) & 255);
        _sb.Append(';');
        AppendNum((c >> 8) & 255);
        _sb.Append(';');
        AppendNum(c & 255);
        _sb.Append('m');
    }

    void AppendNum(int v)
    {
        if (v >= 100) _sb.Append((char)('0' + v / 100));
        if (v >= 10) _sb.Append((char)('0' + v / 10 % 10));
        _sb.Append((char)('0' + v % 10));
    }
}

sealed class Fx
{
    struct P
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;
        public double Max;
        public double Grav;
        public double Drag;
        public int C1;
        public int C2;
        public char Ch;
    }

    readonly List<P> _parts = new();
    public readonly Random Rng = new();
    public double Time;

    public void Spawn(double x, double y, double vx, double vy, double life, int c1, int c2,
        char ch = '\0', double grav = 0, double drag = 0.5)
    {
        _parts.Add(new P { X = x, Y = y, Vx = vx, Vy = vy, Life = life, Max = life, Grav = grav, Drag = drag, C1 = c1, C2 = c2, Ch = ch });
        while (_parts.Count > 900) _parts.RemoveAt(0);
    }

    public void Burst(double x, double y, int n, int c1, int? c2 = null, double speed = 14,
        double life = 0.6, double grav = 0, double drag = 2.5, char ch = '\0')
    {
        var cc2 = c2 ?? c1;
        for (var i = 0; i < n; i++)
        {
            var a = Rng.NextDouble() * Math.PI * 2;
            var sp = speed * (0.25 + Rng.NextDouble() * 0.95);
            Spawn(x, y, Math.Cos(a) * sp, Math.Sin(a) * sp, life * (0.5 + Rng.NextDouble() * 0.7),
                c1, cc2, ch, grav, drag);
        }
    }

    public void Ring(double x, double y, int n, double speed, double life, int c1, int? c2 = null, char ch = '·')
    {
        var cc2 = c2 ?? c1;
        for (var i = 0; i < n; i++)
        {
            var a = i * Math.PI * 2 / n;
            Spawn(x, y, Math.Cos(a) * speed, Math.Sin(a) * speed, life, c1, cc2, ch, 0, 1.2);
        }
    }

    public void Update(double dt)
    {
        Time += dt;
        for (var i = _parts.Count - 1; i >= 0; i--)
        {
            var p = _parts[i];
            p.Life -= dt;
            if (p.Life <= 0)
            {
                _parts.RemoveAt(i);
                continue;
            }
            p.Vy += p.Grav * dt;
            var d = 1.0 / (1.0 + p.Drag * dt);
            p.Vx *= d;
            p.Vy *= d;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            _parts[i] = p;
        }
    }

    public void Draw(Canvas cv)
    {
        foreach (var p in _parts)
        {
            var t = Math.Max(0, Math.Min(1, p.Life / p.Max));
            var c = p.Ch != '\0' ? p.Ch : t > 0.72 ? '█' : t > 0.45 ? '▓' : t > 0.22 ? '▒' : '░';
            cv.Set((int)Math.Round(p.X), (int)Math.Round(p.Y), c, Col.Mix(p.C2, p.C1, t));
        }
    }
}

static class Input
{
    static readonly HashSet<ConsoleKey> _hit = new();
    public static void Poll()
    {
        _hit.Clear();
        try
        {
            while (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true);
                _hit.Add(k.Key);
            }
        }
        catch { }
    }
    public static bool Hit(ConsoleKey k) => _hit.Contains(k);
    public static bool Quit => Hit(ConsoleKey.Q);
}

static class Store
{
    static string Dir()
    {
        var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "term-games");
        Directory.CreateDirectory(d);
        return d;
    }

    public static int Best(string id)
    {
        try
        {
            var p = Path.Combine(Dir(), id + ".score");
            return File.Exists(p) && int.TryParse(File.ReadAllText(p), out var v) ? v : 0;
        }
        catch { return 0; }
    }

    public static void Save(string id, int v)
    {
        try { File.WriteAllText(Path.Combine(Dir(), id + ".score"), v.ToString()); } catch { }
    }
}

static class Col
{
    public static int Rgb(int r, int g, int b) => (Clamp(r) << 16) | (Clamp(g) << 8) | Clamp(b);
    static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    public static int Mix(int a, int b, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return Rgb(
            (int)(((a >> 16) & 255) + (((b >> 16) & 255) - ((a >> 16) & 255)) * t),
            (int)(((a >> 8) & 255) + (((b >> 8) & 255) - ((a >> 8) & 255)) * t),
            (int)((a & 255) + ((b & 255) - (a & 255)) * t));
    }

    public static int Scale(int a, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return Rgb((int)(((a >> 16) & 255) * t), (int)(((a >> 8) & 255) * t), (int)((a & 255) * t));
    }

    public static int Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        if (s < 0) s = 0;
        if (s > 1) s = 1;
        if (l < 0) l = 0;
        if (l > 1) l = 1;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Rgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}
