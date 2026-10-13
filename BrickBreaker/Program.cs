using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(88, 30, 56, 22);
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

var best = Store.Best("brickbreaker");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

var cols = Math.Clamp((W - 4) / 4, 10, 20);
var rows = 7;
var bx0 = (W - cols * 4 + 1) / 2;
var by0 = 2;
var steel = new bool[cols, rows];
var bhp = new int[cols, rows];
var bmax = new int[cols, rows];

var paddleX = W / 2.0;
var paddleW = 9.0;
var paddleDir = 0;
var paddleDirT = 0.0;
var paddleTrail = new List<double>();

var balls = new List<Ball>();
var pups = new List<(double X, double Y, int Kind, double VX)>();

var score = 0;
var lives = 3;
var level = 1;
var combo = 0;
var wideT = 0.0;
var newRecord = false;
var stuck = true;
var clearT = -1.0;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 over
var prevState = -1;
var stateT = 0.0;
var quit = false;

bool AnyBricks()
{
    for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            if (bhp[c, r] > 0 && !steel[c, r]) return true;
    return false;
}

void BuildLevel(int n)
{
    Array.Clear(steel);
    Array.Clear(bhp);
    Array.Clear(bmax);
    var pattern = (n - 1) % 5;
    var bonusHp = (n - 1) / 5;
    var cc = (cols - 1) / 2.0;
    for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            var hp = 0;
            switch (pattern)
            {
                case 0: hp = r < 2 ? 2 : 1; break;
                case 1: hp = (r + c) % 2 == 0 && r < 6 ? 2 : (r + c) % 2 == 0 ? 1 : 0; break;
                case 2:
                    var d = Math.Abs(c - cc) + Math.Abs(r - 2.5);
                    hp = d < 2.5 ? 3 : d < 4.5 ? 2 : d < 6.5 ? 1 : 0;
                    if (d >= 6.5) hp = 0;
                    break;
                case 3: hp = c % 3 == 1 ? (r < 4 ? 3 : 1) : 1; if (r >= 5 && c % 3 == 1) hp = 0; break;
                case 4: hp = c >= r && c < cols - r ? (r == 0 ? 2 : 1) : 0; break;
            }
            if (hp > 0)
            {
                hp = Math.Min(4, hp + bonusHp);
                bhp[c, r] = hp;
                bmax[c, r] = hp;
            }
        }
    if (n >= 3)
        for (var c = 0; c < cols; c++)
            if (bhp[c, 0] > 0 && c % 4 == 0)
            {
                steel[c, 0] = true;
                bhp[c, 0] = 99;
            }
}

int BrickColor(int c, int r)
{
    return Col.Hsl((r * 42 + c * 9 + level * 35) % 360, 0.85, 0.55);
}

void SpawnBall(bool stuckBall)
{
    var a = -Math.PI / 2 + (rng.NextDouble() - 0.5) * 0.8;
    var sp = 34 + level * 2;
    balls.Add(new Ball { X = paddleX, Y = H - 5, VX = Math.Cos(a) * sp, VY = Math.Sin(a) * sp });
    stuck = stuckBall;
}

void StartGame()
{
    score = 0;
    lives = 3;
    level = 1;
    combo = 0;
    wideT = 0;
    newRecord = false;
    clearT = -1;
    balls.Clear();
    pups.Clear();
    paddleX = W / 2.0;
    BuildLevel(1);
    SpawnBall(true);
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 12;
    flash = 0.8;
    flashCol = 0xff3b5c;
    fx.Burst(paddleX, H - 4, 60, 0xff5c7a, 0xffffff, 28, 1.2, 0, 2);
    fx.Ring(paddleX, H - 4, 28, 38, 0.7, 0xff5c7a, 0xffffff, '·');
    if (score > best)
    {
        best = score;
        Store.Save("brickbreaker", score);
        newRecord = true;
    }
}

void HitBrick(int c, int r, double hx, double hy)
{
    var colr = BrickColor(c, r);
    if (steel[c, r])
    {
        fx.Burst(hx, hy, 6, 0xc8d0e0, 0xffffff, 12, 0.35, 0, 5);
        shake = Math.Max(shake, 1.5);
        return;
    }
    bhp[c, r]--;
    combo++;
    var gain = 10 * level * Math.Max(1, combo / 3);
    if (bhp[c, r] <= 0) gain += 15 * level;
    score += gain;
    shake = Math.Max(shake, 2);
    fx.Burst(hx, hy, bhp[c, r] <= 0 ? 16 : 6, colr, 0xffffff, bhp[c, r] <= 0 ? 18 : 10,
        0.6, 12, 2.5);
    if (bhp[c, r] <= 0)
    {
        fx.Ring(hx, hy, 12, 22, 0.45, colr, 0xffffff, '·');
        if (rng.NextDouble() < 0.12)
            pups.Add((bx0 + c * 4 + 1.5, by0 + r * 2 + 0.5, rng.Next(4), (rng.NextDouble() - 0.5) * 6));
        if (!AnyBricks()) LevelClear();
    }
    else if (rng.NextDouble() < 0.4)
        pops.Add((hx - 1, hy - 1, $"+{gain}", 0xffe08a, 0.7, 0.7));
}

void LevelClear()
{
    clearT = 1.8;
    var bonus = 500 * level;
    score += bonus;
    shake = 8;
    flash = 0.5;
    flashCol = 0xffd700;
    for (var c = 0; c < cols; c++)
        fx.Burst(bx0 + c * 4 + 1.5, by0 + rows, 6, BrickColor(c, 0), 0xffffff, 24, 0.9, 20, 2);
    pops.Add((W / 2 - 6, H / 2, $"LEVEL CLEAR +{bonus}", 0xffd700, 1.6, 1.6));
}

void NextLevel()
{
    clearT = -1;
    level++;
    balls.Clear();
    pups.Clear();
    BuildLevel(level);
    SpawnBall(true);
}

void LoseBall()
{
    shake = 7;
    flash = 0.35;
    flashCol = 0xff3b5c;
    fx.Burst(paddleX, H - 3, 24, 0xff5c7a, 0xffffff, 18, 0.7, 8, 2.5);
    combo = 0;
    lives--;
    if (lives <= 0) { Die(); return; }
    balls.Clear();
    SpawnBall(true);
}

void ApplyPowerup(int kind, double x)
{
    var names = new[] { "WIDE!", "MULTIBALL!", "SLOW!", "+LIFE" };
    var cols2 = new[] { 0x66ff9f, 0x66ddff, 0xffe08a, 0xff5c7a };
    pops.Add((x - 3, H - 6, names[kind], cols2[kind], 1.1, 1.1));
    fx.Burst(x, H - 4, 16, cols2[kind], 0xffffff, 14, 0.5, 0, 3);
    if (kind == 0) wideT = 12;
    else if (kind == 1)
    {
        var add = new List<Ball>();
        foreach (var b in balls)
        {
            if (add.Count + balls.Count >= 6) break;
            var a1 = Math.Atan2(b.VY, b.VX) + 0.45;
            var sp = Math.Sqrt(b.VX * b.VX + b.VY * b.VY);
            add.Add(new Ball { X = b.X, Y = b.Y, VX = Math.Cos(a1) * sp, VY = Math.Sin(a1) * sp });
        }
        balls.AddRange(add);
        if (balls.Count == 0) SpawnBall(false);
    }
    else if (kind == 2)
    {
        foreach (var b in balls) { b.VX *= 0.72; b.VY *= 0.72; }
    }
    else lives = Math.Min(6, lives + 1);
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
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) { paddleDir = -1; paddleDirT = 0.3; }
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) { paddleDir = 1; paddleDirT = 0.3; }
        if (Input.Hit(ConsoleKey.Spacebar))
        {
            if (stuck && balls.Count > 0 && clearT < 0)
            {
                stuck = false;
                var b = balls[0];
                var a = -Math.PI / 2 + (rng.NextDouble() - 0.5) * 0.7;
                var sp = Math.Sqrt(b.VX * b.VX + b.VY * b.VY);
                b.VX = Math.Cos(a) * sp;
                b.VY = Math.Sin(a) * sp;
                fx.Burst(b.X, b.Y, 8, 0x66ddff, 0xffffff, 10, 0.3, 0, 4);
            }
        }
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
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    if (wideT > 0)
    {
        wideT -= dt;
        if (wideT <= 0) paddleW = 9;
    }

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.5)
        fx.Spawn(rng.Next(W), -1, 0, 6 + rng.NextDouble() * 10, (H + 2.0) / (6 + rng.NextDouble() * 10),
            Col.Hsl(rng.Next(360), 0.85, 0.55), 0xffffff, '▄', 0, 0);

    if (state == 1)
    {
        // paddle
        if (paddleDirT > 0)
        {
            paddleDirT -= dt;
            paddleX += paddleDir * 52 * dt;
        }
        var half = paddleW / 2;
        paddleX = Math.Clamp(paddleX, 1 + half, W - 2 - half);
        paddleTrail.Add(paddleX);
        if (paddleTrail.Count > 7) paddleTrail.RemoveAt(0);

        if (clearT > 0)
        {
            clearT -= dt;
            if (clearT <= 0) NextLevel();
        }
        else
        {
            // balls
            for (var bi = balls.Count - 1; bi >= 0; bi--)
            {
                var b = balls[bi];
                if (stuck)
                {
                    b.X = paddleX;
                    b.Y = H - 5;
                    balls[bi] = b;
                    continue;
                }

                b.Trail.Add((b.X, b.Y));
                if (b.Trail.Count > 9) b.Trail.RemoveAt(0);

                var sp = Math.Sqrt(b.VX * b.VX + b.VY * b.VY);
                var steps = Math.Max(1, (int)Math.Ceiling(sp * dt / 0.4));
                var sdt = dt / steps;
                for (var s = 0; s < steps; s++)
                {
                    // X axis
                    var nx = b.X + b.VX * sdt;
                    if (nx < 0.6) { nx = 0.6; b.VX = Math.Abs(b.VX); fx.Burst(nx, b.Y, 4, 0x66ddff, null, 8, 0.25, 0, 5); }
                    else if (nx > W - 1.6) { nx = W - 1.6; b.VX = -Math.Abs(b.VX); fx.Burst(nx, b.Y, 4, 0x66ddff, null, 8, 0.25, 0, 5); }
                    var cx = (nx - bx0) / 4;
                    var ci = (int)cx;
                    var ry = (int)((b.Y - by0) / 2);
                    if (ci >= 0 && ci < cols && ry >= 0 && ry < rows && bhp[ci, ry] > 0 && cx - ci is > 0.15 and < 0.9)
                    {
                        b.VX = -b.VX;
                        nx = b.X;
                        HitBrick(ci, ry, bx0 + ci * 4 + 1.5, by0 + ry * 2 + 0.5);
                    }
                    b.X = nx;

                    // Y axis
                    var ny = b.Y + b.VY * sdt;
                    if (ny < 1.6) { ny = 1.6; b.VY = Math.Abs(b.VY); fx.Burst(b.X, ny, 4, 0x66ddff, null, 8, 0.25, 0, 5); }
                    ci = (int)((b.X - bx0) / 4);
                    var cxf = (b.X - bx0) / 4 - ci;
                    ry = (int)((ny - by0) / 2);
                    if (ci >= 0 && ci < cols && ry >= 0 && ry < rows && bhp[ci, ry] > 0 && cxf is > 0.1 and < 0.9)
                    {
                        b.VY = -b.VY;
                        ny = b.Y;
                        HitBrick(ci, ry, bx0 + ci * 4 + 1.5, by0 + ry * 2 + 0.5);
                    }

                    // paddle
                    var py2 = H - 4;
                    if (b.VY > 0 && ny >= py2 && b.Y < py2 && Math.Abs(b.X - paddleX) <= paddleW / 2 + 0.5)
                    {
                        var off = (b.X - paddleX) / (paddleW / 2);
                        var spd = Math.Min(72, Math.Sqrt(b.VX * b.VX + b.VY * b.VY) * 1.045);
                        var ang = -Math.PI / 2 + off * 1.05;
                        b.VX = Math.Cos(ang) * spd;
                        b.VY = Math.Sin(ang) * spd;
                        ny = py2 - 0.6;
                        combo = 0;
                        fx.Burst(b.X, py2, 8, 0x66ff9f, 0xffffff, 12, 0.3, 0, 5);
                    }
                    b.Y = ny;
                }

                if (b.Y > H - 2)
                {
                    balls.RemoveAt(bi);
                    continue;
                }
                balls[bi] = b;
            }
            if (balls.Count == 0 && state == 1) LoseBall();

            // powerups
            for (var i = pups.Count - 1; i >= 0; i--)
            {
                var p = pups[i];
                p.Y += 13 * dt;
                p.X += p.VX * dt;
                if (p.X < 1 || p.X > W - 2) p.VX = -p.VX;
                pups[i] = p;
                if (p.Y > H - 2) { pups.RemoveAt(i); continue; }
                if (p.Y >= H - 5 && Math.Abs(p.X - paddleX) <= paddleW / 2 + 1)
                {
                    ApplyPowerup(p.Kind, p.X);
                    pups.RemoveAt(i);
                }
            }
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
        DrawField();
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
Console.WriteLine($"Thanks for playing BRICK BREAKER! Score {score}, level {level}, best {best}.");

// ---------- rendering ----------
void DrawField()
{
    var t = fx.Time;

    // side rails
    for (var y = 1; y < H - 2; y++)
    {
        cv.Set(0, y, '│', Col.Scale(Col.Hsl(hue + y * 6, 0.8, 0.5), 0.55));
        cv.Set(W - 1, y, '│', Col.Scale(Col.Hsl(hue + y * 6 + 180, 0.8, 0.5), 0.55));
    }
    for (var x = 0; x < W; x++)
        cv.Set(x, 1, '─', Col.Scale(Col.Hsl(hue + x * 5, 0.8, 0.5), 0.4));

    // bricks
    for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (bhp[c, r] <= 0) continue;
            var x = bx0 + c * 4;
            var y = by0 + r * 2;
            int col;
            char ch;
            if (steel[c, r])
            {
                col = Col.Mix(0x8f9ab0, 0xffffff, 0.2 + 0.2 * Math.Sin(t * 5 + c));
                ch = '▓';
            }
            else
            {
                var basec = BrickColor(c, r);
                var tough = (bmax[c, r] - Math.Min(bhp[c, r], bmax[c, r])) * 0.0;
                var hpK = (bhp[c, r] - 1) * 0.18;
                col = Col.Mix(basec, 0xffffff, hpK + tough);
                if (bhp[c, r] < bmax[c, r]) col = Col.Mix(col, 0x000000, 0.35);
                ch = '█';
            }
            cv.Set(x, y, ch, Col.Mix(col, 0xffffff, 0.18));
            cv.Set(x + 1, y, ch, col);
            cv.Set(x + 2, y, ch, Col.Mix(col, 0x000000, 0.3));
        }

    // powerups
    foreach (var p in pups)
    {
        var pc = p.Kind == 0 ? 0x66ff9f : p.Kind == 1 ? 0x66ddff : p.Kind == 2 ? 0xffe08a : 0xff5c7a;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 8 + p.X);
        cv.Set((int)p.X, (int)p.Y, "WMSL"[p.Kind], Col.Mix(pc, 0xffffff, pulse));
        cv.Set((int)p.X, (int)p.Y - 1, '·', Col.Scale(pc, 0.5));
    }

    // ball trails + balls
    foreach (var b in balls)
    {
        for (var i = 0; i < b.Trail.Count; i++)
        {
            var k = (i + 1.0) / b.Trail.Count;
            cv.Set((int)Math.Round(b.Trail[i].X), (int)Math.Round(b.Trail[i].Y), '·',
                Col.Scale(0x66ddff, k * 0.5));
        }
        cv.Set((int)Math.Round(b.X), (int)Math.Round(b.Y), '●', 0xffffff);
        cv.Set((int)Math.Round(b.X), (int)Math.Round(b.Y) - 1, '·', 0x66ddff);
    }

    // paddle with motion smear
    var pw = (int)paddleW;
    var pxi = (int)Math.Round(paddleX);
    for (var i = 0; i < paddleTrail.Count - 1; i++)
    {
        var k = (i + 1.0) / paddleTrail.Count;
        var tx = (int)paddleTrail[i];
        cv.Set(tx - pw / 2, H - 4, '█', Col.Scale(0x66ff9f, 0.12 * k));
        cv.Set(tx + pw / 2, H - 4, '█', Col.Scale(0x66ff9f, 0.12 * k));
    }
    for (var i = -pw / 2; i <= pw / 2; i++)
    {
        var u = pw == 0 ? 0 : Math.Abs(i) / (pw / 2.0);
        var pc = Col.Mix(Col.Mix(0x66ff9f, 0xffffff, 0.35 + 0.2 * Math.Sin(t * 8)), 0x0a2e1f, u * u * 0.85);
        cv.Set(pxi + i, H - 4, '█', pc);
    }
    cv.Set(pxi - pw / 2, H - 3, '·', Col.Scale(0x66ff9f, 0.5));
    cv.Set(pxi + pw / 2, H - 3, '·', Col.Scale(0x66ff9f, 0.5));

    if (stuck && balls.Count > 0 && clearT < 0 && state == 1 && t % 1.0 < 0.7)
        cv.TextC("PRESS SPACE TO LAUNCH", 0xa8e8ff, H - 7);
    if (clearT > 0)
        cv.TextC($"LEVEL {level} CLEAR!", Col.Mix(0xffd700, 0xffffff, 0.5 + 0.5 * Math.Sin(t * 10)), H / 2 - 2);
}

void DrawHud()
{
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    cv.TextC($"LEVEL {level}", Col.Hsl(hue, 0.8, 0.62), 0);

    cv.Text(2, H - 1, new string('♥', lives), 0xff5c7a);
    if (combo >= 3)
        cv.TextC($"COMBO x{combo}", Col.Hsl(45 - Math.Min(30, combo), 1, 0.6), H - 1);
    if (wideT > 0)
        cv.Text(W - 20, H - 1, $"WIDE {wideT:0.0}s", 0x66ff9f);
    cv.Text(W / 2 - 24, H - 1, "A/D move  SPACE launch  P pause  Q quit", 0x607096);
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
    var title = "BRICK BREAKER";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("P R I S M", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("5 patterns, multiball, powerups, combos", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Catch powerups: W wide, M multiball, S slow, L life", 0x6f7fa8, 22);
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "GAME OVER", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"LEVEL {level}", 0x9fb0d8, y0 + 6);
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

class Ball
{
    public double X;
    public double Y;
    public double VX;
    public double VY;
    public List<(double X, double Y)> Trail = new();
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
