using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(84, 30, 56, 22);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 56x22 and re-run.");
    return;
}

var W = size.W;
var H = size.H;
var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();

var best = Store.Best("lightcycles");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

// arena
var ax0 = 1;
var ay0 = 3;
var ax1 = W - 2;
var ay1 = H - 3;

var trail = new Dictionary<(int X, int Y), (int Who, double T)>();
var heads = new (int X, int Y, int DX, int DY, bool Alive)[3];
var botColors = new[] { 0x4ddfff, 0xff4df0, 0xff9f1c };

var score = 0;
var round = 1;
var roundsWon = 0;
var stepInt = 0.085;
var stepAcc = 0.0;
var pendingDir = (DX: 1, DY: 0);
var roundState = 0;        // 0 fighting, 1 round clear, 2 player dead
var roundT = 0.0;
var bannerT = 0.0;
var bannerText = "";
var newRecord = false;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 over
var prevState = -1;
var stateT = 0.0;
var quit = false;

void StartGame()
{
    score = 0;
    round = 1;
    roundsWon = 0;
    newRecord = false;
    BeginRound();
}

void BeginRound()
{
    trail.Clear();
    var midY = (ay0 + ay1) / 2;
    heads[0] = (ax0 + 5, midY, 1, 0, true);
    heads[1] = (ax1 - 5, midY - 3, -1, 0, true);
    heads[2] = (ax1 - 5, midY + 3, -1, 0, true);
    trail[(ax0 + 5, midY)] = (0, fx.Time);
    trail[(ax1 - 5, midY - 3)] = (1, fx.Time);
    trail[(ax1 - 5, midY + 3)] = (2, fx.Time);
    pendingDir = (1, 0);
    stepInt = Math.Max(0.055, 0.088 - (round - 1) * 0.004);
    stepAcc = 0;
    roundState = 0;
    roundT = 0;
    bannerT = 1.6;
    bannerText = round == 1 ? "ROUND 1  -  FIGHT!" : $"ROUND {round}";
}

void CrashAt(int x, int y, int who)
{
    var c = botColors[who];
    fx.Burst(x + 0.5, y + 0.5, 34, c, 0xffffff, 24, 1.0, 0, 2);
    fx.Ring(x + 0.5, y + 0.5, 20, 30, 0.6, c, 0xffffff, '·');
    shake = Math.Max(shake, who == 0 ? 11 : 5);
    flash = Math.Max(flash, who == 0 ? 0.6 : 0.25);
    flashCol = who == 0 ? 0xff3b5c : c;
}

(int DX, int DY)[] AllDirs() => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

bool Free(int x, int y) => x > ax0 && x < ax1 && y > ay0 && y < ay1 && !trail.ContainsKey((x, y));

(int DX, int DY) BestBotDir(int who)
{
    var h = heads[who];
    var cur = (DX: h.DX, DY: h.DY);
    var look = 7 + round;
    var bestScore = double.MinValue;
    var bestDir = cur;
    foreach (var d in AllDirs())
    {
        if (d.DX == -cur.DX && d.DY == -cur.DY) continue;
        if (d.DX == cur.DX && d.DY == cur.DY && rng.NextDouble() < 0.25) continue;
        var open = 0;
        var x = h.X + d.DX;
        var y = h.Y + d.DY;
        for (var i = 0; i < look && Free(x, y); i++)
        {
            open++;
            x += d.DX;
            y += d.DY;
        }
        // bonus for clearing nearby space on both sides
        var s = open + rng.NextDouble() * 2;
        if (d.DX == cur.DX && d.DY == cur.DY) s += 1.5;
        if (s > bestScore)
        {
            bestScore = s;
            bestDir = d;
        }
    }
    return bestDir;
}

void StepRound()
{
    var now = fx.Time;

    // player
    var p = heads[0];
    if (p.Alive)
    {
        if (!(pendingDir.DX == -p.DX && pendingDir.DY == -p.DY))
        {
            if (pendingDir != (p.DX, p.DY) && (pendingDir.DX != p.DX || pendingDir.DY != p.DY))
                fx.Burst(p.X + 0.5, p.Y + 0.5, 6, 0x4ddfff, 0xffffff, 8, 0.3, 0, 5);
            p.DX = pendingDir.DX;
            p.DY = pendingDir.DY;
        }
        var nx = p.X + p.DX;
        var ny = p.Y + p.DY;
        p.X = nx;
        p.Y = ny;
        if (!Free(nx, ny) || nx <= ax0 || nx >= ax1 || ny <= ay0 || ny >= ay1)
        {
            p.Alive = false;
            CrashAt(nx, ny, 0);
            roundState = 2;
            roundT = 0;
        }
        else trail[(nx, ny)] = (0, now);
        heads[0] = p;
    }

    // bots
    for (var who = 1; who < 3; who++)
    {
        var b = heads[who];
        if (!b.Alive) continue;
        var d = BestBotDir(who);
        b.DX = d.DX;
        b.DY = d.DY;
        var nx = b.X + b.DX;
        var ny = b.Y + b.DY;
        b.X = nx;
        b.Y = ny;
        var crash = !Free(nx, ny) || nx <= ax0 || nx >= ax1 || ny <= ay0 || ny >= ay1;
        if (crash)
        {
            b.Alive = false;
            CrashAt(nx, ny, who);
            score += 500;
            pops.Add((nx - 2, ny - 1, "BOT DOWN +500", botColors[who], 1.2, 1.2));
        }
        else
        {
            if (heads[0].Alive && nx == heads[0].X && ny == heads[0].Y)
            {
                b.Alive = false;
                heads[0].Alive = false;
                CrashAt(nx, ny, who);
                CrashAt(nx, ny, 0);
                roundState = 2;
                roundT = 0;
            }
            else trail[(nx, ny)] = (who, now);
        }
        heads[who] = b;
    }

    if (roundState == 0 && !heads[1].Alive && !heads[2].Alive)
    {
        roundState = 1;
        roundT = 0;
        roundsWon++;
        var bonus = 1000 * round;
        score += bonus;
        bannerT = 1.6;
        bannerText = $"ROUND CLEAR +{bonus}";
        flash = 0.4;
        flashCol = 0x66ff9f;
    }
}

void Die()
{
    state = 3;
    stateT = 0;
    if (score > best)
    {
        best = score;
        Store.Save("lightcycles", score);
        newRecord = true;
    }
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
        }
        if (Input.Quit) quit = true;
    }
    else if (state == 1)
    {
        if (Input.Hit(ConsoleKey.Escape) || Input.Hit(ConsoleKey.P)) state = 2;
        if (Input.Quit) quit = true;
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) pendingDir = (0, -1);
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) pendingDir = (0, 1);
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) pendingDir = (-1, 0);
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) pendingDir = (1, 0);
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

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.5)
        fx.Spawn(rng.Next(W), -1, 0, 6 + rng.NextDouble() * 12, (H + 2.0) / (6 + rng.NextDouble() * 12),
            Col.Hsl(rng.Next(170, 320), 0.9, 0.6), 0xffffff, '·', 0, 0);

    if (state == 1)
    {
        if (bannerT > 0) bannerT -= dt;

        if (roundState == 0)
        {
            stepAcc += dt;
            if (stepAcc >= stepInt)
            {
                stepAcc = 0;
                StepRound();
                score += Math.Max(1, (int)(stepInt * 14));
            }
        }
        else if (roundState == 1)
        {
            roundT += dt;
            if (roundT > 1.8)
            {
                round++;
                BeginRound();
            }
        }
        else if (roundState == 2)
        {
            roundT += dt;
            if (roundT > 1.4) Die();
        }
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

    if (state == 0)
    {
        DrawTitle();
        fx.Draw(cv);
        DrawTitleOver();
    }
    else
    {
        DrawArena();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (bannerT > 0 && state == 1) DrawBanner();
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
Console.WriteLine($"Thanks for playing LIGHT CYCLES! Score {score}, rounds won {roundsWon}, best {best}.");

// ---------- rendering ----------
void DrawArena()
{
    var t = fx.Time;

    // backdrop dots
    for (var y = ay0 + 1; y < ay1; y++)
        for (var x = ax0 + 1; x < ax1; x++)
            if ((x + y) % 4 == 0)
                cv.Set(x, y, '·', 0x191f36);

    // trails with fading glow
    foreach (var kv in trail)
    {
        var age = t - kv.Value.T;
        var k = Math.Exp(-age * 1.4);
        var basec = botColors[kv.Value.Who];
        var col = Col.Mix(Col.Scale(basec, 0.14), basec, k);
        if (k > 0.55) col = Col.Mix(col, 0xffffff, (k - 0.55) * 0.8);
        var ch = k > 0.7 ? '█' : k > 0.42 ? '▓' : k > 0.2 ? '▒' : '░';
        cv.Set(kv.Key.X, kv.Key.Y, ch, col);
    }

    // border
    for (var x = ax0; x <= ax1; x++)
    {
        cv.Set(x, ay0, '═', Col.Hsl(hue + x * 5, 0.8, 0.5));
        cv.Set(x, ay1, '═', Col.Hsl(hue + x * 5 + 180, 0.8, 0.5));
    }
    for (var y = ay0; y <= ay1; y++)
    {
        cv.Set(ax0, y, '║', Col.Hsl(hue + y * 6, 0.8, 0.5));
        cv.Set(ax1, y, '║', Col.Hsl(hue + y * 6 + 90, 0.8, 0.5));
    }
    cv.Set(ax0, ay0, '╔', Col.Hsl(hue, 0.9, 0.65));
    cv.Set(ax1, ay0, '╗', Col.Hsl(hue + 120, 0.9, 0.65));
    cv.Set(ax0, ay1, '╚', Col.Hsl(hue + 240, 0.9, 0.65));
    cv.Set(ax1, ay1, '╝', Col.Hsl(hue + 300, 0.9, 0.65));

    // heads
    for (var who = 0; who < 3; who++)
    {
        var h = heads[who];
        if (!h.Alive || state == 3) continue;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 12 + who);
        var col = Col.Mix(botColors[who], 0xffffff, 0.35 + pulse * 0.45);
        var ch = h.DX == 1 ? '▶' : h.DX == -1 ? '◀' : h.DY == 1 ? '▼' : '▲';
        cv.Set(h.X, h.Y, ch, col);
    }
}

void DrawHud()
{
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    cv.TextC($"ROUND {round}", Col.Hsl(hue, 0.8, 0.62), 0);

    cv.Text(2, 1, "YOU", 0x4ddfff);
    cv.Text(7, 1, "RIVALS", 0xff4df0);
    cv.TextC("WASD / ARROWS turn   P pause   Q quit", 0x607096, 1);
    cv.Text(2, H - 1, $"ROUNDS WON {roundsWon}", 0x66ff9f);
    cv.Text(W - 16, H - 1, $"SPEED {Math.Round(1 / stepInt, 1)}", 0xff9f1c);
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.6);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawBanner()
{
    var k = Math.Min(1.0, bannerT > 1.1 ? (1.6 - bannerT) * 3.5 : bannerT * 1.6);
    cv.TextC(bannerText, Col.Scale(0xffffff, Math.Max(0.15, k)), H / 2 - 3);
}

void DrawPause()
{
    cv.TextC("PAUSED", 0xffffff, H / 2 - 1);
    cv.TextC("P / ESC resume    R restart    Q quit", 0xa8b8e0, H / 2 + 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "LIGHT CYCLES";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("N E O N   D U E L", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("Outlast two rival cycles. Trails never fade to nothing.", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Clear a round = bonus. Each round the rivals get sharper.", 0x6f7fa8, 22);
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "WALL HIT", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"ROUND {round}   ROUNDS WON {roundsWon}", 0x9fb0d8, y0 + 6);
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
