using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(100, 30, 58, 20);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 58x20 and re-run.");
    return;
}

var cv = new Canvas(size.W, size.H);
var fx = new Fx();
var rng = new Random();

// Playfield geometry: outer border, inner field
var bx0 = 1;
var by0 = 3;
var bx1 = size.W - 2;
var by1 = size.H - 3;
var fx0 = bx0 + 1;
var fy0 = by0 + 1;
var fw = bx1 - bx0 - 1;
var fh = by1 - by0 - 1;

var best = Store.Best("neonsnake");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

var snake = new LinkedList<(int X, int Y)>();
var occ = new HashSet<(int X, int Y)>();
var inputQ = new Queue<(int X, int Y)>();
var dir = (X: 1, Y: 0);
var food = (X: 0, Y: 0);
var score = 0;
var eaten = 0;
var combo = 1;
var comboTimer = 0.0;
var moveInt = 0.14;
var moveAcc = 0.0;
var hue = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var newRecord = false;
var state = 0;            // 0 title, 1 play, 2 pause, 3 dead
var prevState = -1;
var stateT = 0.0;

void StartGame()
{
    snake.Clear();
    occ.Clear();
    inputQ.Clear();
    var sx = fx0 + fw / 2;
    var sy = fy0 + fh / 2;
    for (var i = 0; i < 4; i++)
    {
        var p = (X: sx - i, Y: sy);
        snake.AddLast(p);
        occ.Add(p);
    }
    dir = (1, 0);
    score = 0;
    eaten = 0;
    combo = 1;
    comboTimer = 0;
    moveInt = 0.14;
    moveAcc = 0;
    newRecord = false;
    SpawnFood();
}

void SpawnFood()
{
    (int X, int Y) p;
    do p = (rng.Next(fx0, fx0 + fw), rng.Next(fy0, fy0 + fh));
    while (occ.Contains(p));
    food = p;
}

void QueueDir(int dx, int dy)
{
    var last = inputQ.Count > 0 ? inputQ.Last() : dir;
    if (dx == -last.X && dy == -last.Y) return;
    if (dx == last.X && dy == last.Y) return;
    if (inputQ.Count < 3) inputQ.Enqueue((dx, dy));
}

void Step()
{
    if (inputQ.Count > 0)
    {
        var d = inputQ.Dequeue();
        if (!(d.X == -dir.X && d.Y == -dir.Y)) dir = d;
    }
    var head = snake.First!.Value;
    var nx = head.X + dir.X;
    var ny = head.Y + dir.Y;
    if (nx < fx0 || nx >= fx0 + fw || ny < fy0 || ny >= fy0 + fh || occ.Contains((nx, ny)))
    {
        Die();
        return;
    }
    snake.AddFirst((nx, ny));
    occ.Add((nx, ny));
    if (nx == food.X && ny == food.Y) Eat();
    else
    {
        occ.Remove(snake.Last!.Value);
        snake.RemoveLast();
    }
}

void Eat()
{
    combo = comboTimer > 0 ? Math.Min(9, combo + 1) : 1;
    comboTimer = 3.2;
    var gain = 10 * combo;
    score += gain;
    eaten++;
    moveInt = Math.Max(0.055, 0.14 - eaten * 0.0035);
    fx.Burst(food.X + 0.5, food.Y + 0.5, 30, Col.Hsl(45, 1, 0.62), Col.Hsl(340, 1, 0.6), 17, 0.75, 7, 3);
    fx.Ring(food.X + 0.5, food.Y + 0.5, 16, 24, 0.55, 0xfff3b0, 0xff8fb0);
    pops.Add((food.X - 1, food.Y, $"+{gain}", Col.Hsl(48, 1, 0.62), 1.1, 1.1));
    flash = 0.22;
    flashCol = 0xfff2b8;
    SpawnFood();
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 9;
    flash = 0.7;
    flashCol = 0xff3b6b;
    foreach (var seg in snake)
        fx.Burst(seg.X + 0.5, seg.Y + 0.5, 5, Col.Hsl(hue + seg.Y * 8, 0.9, 0.6), 0x181030, 13, 1.0, 5, 2.5);
    if (score > best)
    {
        best = score;
        Store.Save("neonsnake", score);
        newRecord = true;
    }
}

var sw = Stopwatch.StartNew();
var prev = 0.0;
const double STEP = 1.0 / 60;
var quit = false;

while (!quit)
{
    var now = sw.Elapsed.TotalSeconds;
    var dt = Math.Min(now - prev, 0.1);
    prev = now;
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
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) QueueDir(0, -1);
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) QueueDir(0, 1);
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) QueueDir(-1, 0);
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) QueueDir(1, 0);
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
    hue = (hue + dt * 30) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 22);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.2);

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.5)
        fx.Spawn(rng.Next(size.W), size.H, (rng.NextDouble() - 0.5) * 3, -(2 + rng.NextDouble() * 7),
            2.5 + rng.NextDouble() * 2.5, Col.Hsl(rng.Next(170, 320), 0.9, 0.55), Col.Hsl(rng.Next(28, 55), 1, 0.6),
            '\0', 0, 0.2);

    if (state == 1)
    {
        comboTimer -= dt;
        if (comboTimer <= 0) combo = 1;
        moveAcc += dt;
        if (moveAcc >= moveInt)
        {
            moveAcc = 0;
            Step();
        }
    }

    fx.Update(dt);
    for (var i = pops.Count - 1; i >= 0; i--)
    {
        var p = pops[i];
        var l = p.Life - dt;
        if (l <= 0)
        {
            pops.RemoveAt(i);
            continue;
        }
        pops[i] = (p.X, p.Y - dt * 7, p.S, p.C, l, p.Max);
    }

    // ---------- render ----------
    cv.Clear();
    cv.ShakeX = shake > 0.2 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.2 ? rng.Next(-(int)shake, (int)shake + 1) : 0;

    if (state == 0)
        DrawTitle();
    else
    {
        DrawField();
        if (state is 1 or 2)
        {
            fx.Draw(cv);
            DrawPops();
            DrawHud();
            if (state == 2) DrawPause();
        }
        else
        {
            fx.Draw(cv);
            DrawPops();
            DrawHud();
            DrawDead();
        }
    }
    if (state == 0)
    {
        fx.Draw(cv);
        DrawTitleOver();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.85, flash);
    }
    else if (state == 3 && stateT < 0.55)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = Math.Min(0.6, stateT * 1.2);
    }
    else cv.OverlayK = 0;

    cv.Present();

    var sleepMs = (int)((STEP - (sw.Elapsed.TotalSeconds - now)) * 1000);
    if (sleepMs > 0) Thread.Sleep(sleepMs);
}

RestoreConsole();
Console.WriteLine($"Thanks for playing NEON SNAKE! Score {score}, best {best}.");

// ---------- rendering ----------
void DrawField()
{
    for (var y = fy0; y < fy0 + fh; y++)
        for (var x = fx0; x < fx0 + fw; x++)
            if ((x * 3 + y * 7 + (int)(fx.Time * 4)) % 17 == 0)
                cv.Set(x, y, '·', Col.Hsl(hue + x * 3, 0.7, 0.17));

    for (var x = bx0; x <= bx1; x++)
    {
        cv.Set(x, by0, '─', Col.Hsl(hue + x * 6, 0.85, 0.45));
        cv.Set(x, by1, '─', Col.Hsl(hue + x * 6, 0.85, 0.45));
    }
    for (var y = by0; y <= by1; y++)
    {
        cv.Set(bx0, y, '│', Col.Hsl(hue + y * 6, 0.85, 0.45));
        cv.Set(bx1, y, '│', Col.Hsl(hue + y * 6, 0.85, 0.45));
    }
    cv.Set(bx0, by0, '┌', Col.Hsl(hue, 0.9, 0.62));
    cv.Set(bx1, by0, '┐', Col.Hsl(hue + 90, 0.9, 0.62));
    cv.Set(bx0, by1, '└', Col.Hsl(hue + 180, 0.9, 0.62));
    cv.Set(bx1, by1, '┘', Col.Hsl(hue + 270, 0.9, 0.62));

    // food with pulsing core and orbiting sparks
    var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 9);
    var fcol = Col.Hsl(350 + 25 * pulse, 0.95, 0.45 + 0.2 * pulse);
    cv.Set(food.X, food.Y, '◆', fcol);
    for (var k = 0; k < 3; k++)
    {
        var a = fx.Time * 5 + k * (Math.PI * 2 / 3);
        cv.Set((int)Math.Round(food.X + Math.Cos(a) * 1.7), (int)Math.Round(food.Y + Math.Sin(a) * 1.7),
            '·', Col.Hsl(30 + 70 * pulse, 1, 0.62));
    }

    if (state is 1 or 2)
    {
        var i = 0;
        var n = snake.Count;
        foreach (var seg in snake)
        {
            var t = n <= 1 ? 0 : (double)i / (n - 1);
            var light = 0.62 - t * 0.26;
            var col = Col.Hsl(hue + t * 150, 0.85, light);
            if (i == 0)
            {
                var glow = 0.35 + 0.25 * Math.Sin(fx.Time * 10);
                col = Col.Mix(col, 0xffffff, glow);
                cv.Set(seg.X, seg.Y, '█', col);
            }
            else if (i == n - 1) cv.Set(seg.X, seg.Y, '░', col);
            else cv.Set(seg.X, seg.Y, i % 2 == 0 ? '▓' : '█', col);
            i++;
        }
    }
}

void DrawHud()
{
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(size.W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    if (combo > 1 && comboTimer > 0)
        cv.TextC($"COMBO x{combo}", Col.Hsl(45 - combo * 4, 1, 0.6), 2);

    var lvl = Math.Min(10, 1 + eaten / 5);
    cv.Text(2, 1, $"SPEED {new string('█', lvl)}{new string('░', 10 - lvl)}", 0x5be0c0);
    cv.Text(size.W - 30, 1, "P pause   Q quit", 0x607096);

    cv.TextC($"NEON SNAKE ULTRA  -  eat, combo, survive", 0x4a5578, size.H - 2);
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
    cv.TextC("PAUSED", 0xffffff, size.H / 2 - 1);
    cv.TextC("P / ESC resume    R restart    Q quit", 0xa8b8e0, size.H / 2 + 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "NEON SNAKE";
    var x = (size.W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl(t * 50 % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("U L T R A   E D I T I O N", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 14);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 17);
    cv.TextC("WASD / ARROWS move      ESC or Q quit", 0x6f7fa8, 20);
}

void DrawDead()
{
    if (stateT < 0.55) return;
    var half = Math.Min(26, size.W / 2 - 1);
    var x0 = size.W / 2 - half;
    var x1 = size.W / 2 + half;
    var y0 = 7;
    var y1 = 18;
    if (y1 >= size.H) y1 = size.H - 1;
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
    cv.TextGrad(size.W / 2 - 5, y0 + 2, "GAME OVER", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"ORBS {eaten}", 0x9fb0d8, y0 + 6);
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
