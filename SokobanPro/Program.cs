using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(84, 30, 52, 22);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 52x22 and re-run.");
    return;
}

var W = size.W;
var H = size.H;
var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();

var levels = new[]
{
    new[]
    {
        "#####",
        "#   #",
        "# . #",
        "# $ #",
        "# @ #",
        "#####"
    },
    new[]
    {
        "########",
        "#..    #",
        "#$$    #",
        "#@     #",
        "#      #",
        "########"
    },
    new[]
    {
        "########",
        "#      #",
        "# .$ @ #",
        "#      #",
        "########"
    },
    new[]
    {
        "########",
        "#  .   #",
        "#  $   #",
        "#  @ $ #",
        "#   .  #",
        "########"
    },
    new[]
    {
        "#########",
        "# . . . #",
        "# $ $ $ #",
        "#   @   #",
        "#       #",
        "#########"
    },
    new[]
    {
        "#########",
        "# # # # #",
        "# . . . #",
        "# $ $ $ #",
        "#   @   #",
        "#########"
    },
    new[]
    {
        "#########",
        "# .   . #",
        "#       #",
        "# $$@   #",
        "#       #",
        "#########"
    },
    new[]
    {
        "#########",
        "# . . . #",
        "#       #",
        "# $ $ $ #",
        "#   @   #",
        "#########"
    },
    new[]
    {
        "##########",
        "# . .  . #",
        "#        #",
        "#  $$ @$ #",
        "#        #",
        "##########"
    }
};

var best = Store.Best("sokobanpro");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

var walls = new HashSet<(int X, int Y)>();
var goals = new HashSet<(int X, int Y)>();
var boxes = new HashSet<(int X, int Y)>();
var px = 0;
var py = 0;
var mapW = 0;
var mapH = 0;
var mapX = 0;
var mapY = 2;

var levelIdx = 0;
var moves = 0;
var totalMoves = 0;
var score = 0;
var history = new List<((int X, int Y) Player, List<(int X, int Y)> Boxes, int Moves)>();
var newRecord = false;
var stuckWarn = false;

var animFrom = (X: 0.0, Y: 0.0);
var animBoxFrom = (X: 0.0, Y: 0.0, Valid: false);
var animT = 1.0;
(int DX, int DY)? queued = null;

var transT = -1.0;
var pendingLevel = -1;
var clearT = -1.0;
var bannerT = 0.0;
var bannerText = "";

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 victory
var prevState = -1;
var stateT = 0.0;
var quit = false;

void LoadLevel(int idx)
{
    var src = levels[idx];
    walls.Clear();
    goals.Clear();
    boxes.Clear();
    history.Clear();
    mapH = src.Length;
    mapW = src[0].Length;
    for (var y = 0; y < mapH; y++)
        for (var x = 0; x < mapW; x++)
        {
            var c = src[y][x];
            if (c == '#') walls.Add((x, y));
            if (c is '.' or '+' or '*') goals.Add((x, y));
            if (c is '$' or '*') boxes.Add((x, y));
            if (c is '@' or '+') { px = x; py = y; }
        }
    mapX = (W - mapW) / 2;
    mapY = (H - mapH) / 2 + 1;
    moves = 0;
    animT = 1;
    animBoxFrom.Valid = false;
    queued = null;
    clearT = -1;
    stuckWarn = false;
    bannerT = 1.4;
    bannerText = $"LEVEL {idx + 1} / {levels.Length}";
}

void StartGame()
{
    levelIdx = 0;
    moves = 0;
    totalMoves = 0;
    score = 0;
    newRecord = false;
    transT = -1;
    pendingLevel = -1;
    LoadLevel(0);
}

bool WallAt(int x, int y) => walls.Contains((x, y));

bool HasDeadlock()
{
    foreach (var b in boxes)
    {
        if (goals.Contains(b)) continue;
        var hWall = WallAt(b.X - 1, b.Y) || WallAt(b.X + 1, b.Y);
        var vWall = WallAt(b.X, b.Y - 1) || WallAt(b.X, b.Y + 1);
        if (hWall && vWall) return true;
    }
    return false;
}

void TryMove(int dx, int dy)
{
    if (animT < 1 || transT >= 0 || clearT >= 0) { queued = (dx, dy); return; }
    var nx = px + dx;
    var ny = py + dy;
    if (WallAt(nx, ny)) { shake = Math.Max(shake, 1.2); return; }

    var pushed = false;
    if (boxes.Contains((nx, ny)))
    {
        var bx = nx + dx;
        var by = ny + dy;
        if (WallAt(bx, by) || boxes.Contains((bx, by))) { shake = Math.Max(shake, 1.6); return; }
        history.Add(((px, py), boxes.ToList(), moves));
        boxes.Remove((nx, ny));
        boxes.Add((bx, by));
        animBoxFrom = (nx, ny, true);
        pushed = true;
        fx.Burst(mapX + bx + 0.5, mapY + by + 0.5, 8, 0xffb84d, 0xffffff, 10, 0.4, 0, 4);
        if (goals.Contains((bx, by)))
        {
            score += 50;
            fx.Burst(mapX + bx + 0.5, mapY + by + 0.5, 22, 0x66ff9f, 0xffffff, 16, 0.8, 6, 2.5);
            fx.Ring(mapX + bx + 0.5, mapY + by + 0.5, 14, 20, 0.5, 0x66ff9f, 0xffffff, '·');
            pops.Add((mapX + bx - 1, mapY + by - 1, "+GOAL", 0x66ff9f, 1.0, 1.0));
            flash = Math.Max(flash, 0.18);
            flashCol = 0x66ff9f;
        }
    }
    else
    {
        history.Add(((px, py), boxes.ToList(), moves));
    }

    animFrom = (px, py);
    px = nx;
    py = ny;
    animT = 0;
    if (!pushed) animBoxFrom.Valid = false;
    moves++;
    totalMoves++;

    if (goals.All(boxes.Contains)) LevelClear();
    else
    {
        stuckWarn = HasDeadlock();
        if (stuckWarn)
        {
            pops.Add((mapX + px - 2, mapY + py - 2, "DEADLOCK? Z undo", 0xff5c7a, 1.4, 1.4));
            shake = Math.Max(shake, 3);
        }
    }
}

void Undo()
{
    if (history.Count == 0 || animT < 1 || transT >= 0) return;
    var last = history[^1];
    history.RemoveAt(history.Count - 1);
    px = last.Player.X;
    py = last.Player.Y;
    boxes.Clear();
    foreach (var b in last.Boxes) boxes.Add(b);
    moves = last.Moves;
    animT = 1;
    animBoxFrom.Valid = false;
    stuckWarn = HasDeadlock();
    fx.Burst(mapX + px + 0.5, mapY + py + 0.5, 6, 0x4ddfff, null, 8, 0.3, 0, 4);
}

void LevelClear()
{
    clearT = 1.5;
    var bonus = Math.Max(150, 1000 - moves * 6);
    score += bonus;
    flash = 0.45;
    flashCol = 0xffd700;
    shake = 6;
    for (var i = 0; i < 90; i++)
        fx.Spawn(rng.Next(W), -1, (rng.NextDouble() - 0.5) * 6, 4 + rng.NextDouble() * 8,
            2.2 + rng.NextDouble() * 1.6, Col.Hsl(rng.Next(360), 0.9, 0.6), Col.Hsl(rng.Next(360), 0.9, 0.55),
            rng.Next(3) == 0 ? '★' : '▒', 10, 0.4);
    pops.Add((W / 2 - 6, H / 2 + 1, $"CLEAR +{bonus}", 0xffd700, 1.4, 1.4));
}

void FinishLevelFlow()
{
    clearT = -1;
    if (levelIdx + 1 >= levels.Length)
    {
        state = 3;
        stateT = 0;
        if (score > best)
        {
            best = score;
            Store.Save("sokobanpro", score);
            newRecord = true;
        }
    }
    else
    {
        pendingLevel = levelIdx + 1;
        transT = 0;
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
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) TryMove(0, -1);
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) TryMove(0, 1);
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) TryMove(-1, 0);
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) TryMove(1, 0);
        if (Input.Hit(ConsoleKey.Z)) Undo();
        if (Input.Hit(ConsoleKey.R)) LoadLevel(levelIdx);
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
    if (bannerT > 0) bannerT -= dt;

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.5)
        fx.Spawn(rng.Next(W), -1, 0, 6 + rng.NextDouble() * 10, (H + 2.0) / (6 + rng.NextDouble() * 10),
            Col.Hsl(rng.Next(280, 360), 0.9, 0.6), Col.Hsl(rng.Next(30, 60), 1, 0.6), '·', 0, 0);

    if (state == 1)
    {
        if (animT < 1)
        {
            animT = Math.Min(1, animT + dt / 0.11);
            if (animT >= 1)
            {
                animBoxFrom.Valid = false;
                if (queued != null && clearT < 0 && transT < 0)
                {
                    var q = queued.Value;
                    queued = null;
                    TryMove(q.DX, q.DY);
                }
                else queued = null;
            }
        }
        else if (queued != null && clearT < 0 && transT < 0)
        {
            var q = queued.Value;
            queued = null;
            TryMove(q.DX, q.DY);
        }

        if (clearT > 0)
        {
            clearT -= dt;
            if (clearT <= 0) FinishLevelFlow();
        }
        if (transT >= 0)
        {
            transT += dt / 0.8;
            if (transT >= 0.5 && pendingLevel >= 0)
            {
                levelIdx = pendingLevel;
                pendingLevel = -1;
                LoadLevel(levelIdx);
            }
            if (transT >= 1) transT = -1;
        }
    }
    else if (state == 3)
    {
        // victory fireworks
        if (rng.NextDouble() < 0.25)
            fx.Burst(rng.Next(W), rng.Next(H / 2), 26, Col.Hsl(rng.Next(360), 0.95, 0.6),
                0xffffff, 26, 1.1, 8, 1.6);
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
    else if (state == 3)
    {
        fx.Draw(cv);
        DrawVictory();
    }
    else
    {
        DrawLevel();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (bannerT > 0 && state == 1) DrawBanner();
        if (state == 2) DrawPause();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.85, flash);
    }
    else if (state == 2)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = 0.55;
    }
    else if (transT >= 0)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = transT < 0.5 ? transT * 2 : (1 - transT) * 2;
    }
    else cv.OverlayK = 0;

    cv.Present();

    var sleepMs = (int)((STEP - (sw.Elapsed.TotalSeconds - now)) * 1000);
    if (sleepMs > 0) Thread.Sleep(sleepMs);
}

RestoreConsole();
Console.WriteLine($"Thanks for playing SOKOBAN PRO! Score {score}, best {best}.");

// ---------- rendering ----------
void DrawLevel()
{
    var t = fx.Time;

    // floor
    for (var y = 0; y < mapH; y++)
        for (var x = 0; x < mapW; x++)
        {
            if (walls.Contains((x, y))) continue;
            if ((x * 5 + y * 3) % 6 == 0)
                cv.Set(mapX + x, mapY + y, '·', 0x232a44);
        }

    // walls with diagonal gradient
    foreach (var w in walls)
    {
        var u = (w.X + w.Y) / (double)Math.Max(1, mapW + mapH);
        var basec = Col.Hsl(hue + u * 140, 0.55, 0.42);
        var exposed = !WallAt(w.X - 1, w.Y) || !WallAt(w.X + 1, w.Y) || !WallAt(w.X, w.Y - 1) || !WallAt(w.X, w.Y + 1);
        var c = exposed ? Col.Mix(basec, 0x9fb4e8, 0.25) : Col.Scale(basec, 0.7);
        cv.Set(mapX + w.X, mapY + w.Y, exposed ? '█' : '▓', c);
    }

    // goals
    foreach (var g in goals)
    {
        var pulse = 0.5 + 0.5 * Math.Sin(t * 6 + g.X + g.Y);
        var onIt = boxes.Contains(g);
        if (onIt) continue;
        cv.Set(mapX + g.X, mapY + g.Y, '◇', Col.Mix(Col.Hsl(300, 0.9, 0.5), Col.Hsl(330, 0.9, 0.7), pulse));
        var a = t * 4 + g.X;
        var ox = mapX + g.X + Math.Round(Math.Cos(a));
        var oy = mapY + g.Y + Math.Round(Math.Sin(a) * 0.9);
        cv.Set((int)ox, (int)oy, '·', Col.Scale(0xff4df0, 0.45 + pulse * 0.5));
    }

    // boxes
    foreach (var b in boxes)
    {
        if (animBoxFrom.Valid && animT < 1)
        {
            var destX = (int)Math.Round(animBoxFrom.X + (animBoxFrom.X - animFrom.X));
            var destY = (int)Math.Round(animBoxFrom.Y + (animBoxFrom.Y - animFrom.Y));
            if (b.X == destX && b.Y == destY) continue;
        }
        DrawBoxAt(mapX + b.X, mapY + b.Y, goals.Contains(b), t);
    }

    // animated sliding box
    if (animBoxFrom.Valid && animT < 1)
    {
        var k = animT * animT * (3 - 2 * animT);
        var destX = animBoxFrom.X + (animBoxFrom.X - animFrom.X);
        var destY = animBoxFrom.Y + (animBoxFrom.Y - animFrom.Y);
        var onGoal = goals.Contains(((int)Math.Round(destX), (int)Math.Round(destY)));
        var gx = animBoxFrom.X + (destX - animBoxFrom.X) * k;
        var gy = animBoxFrom.Y + (destY - animBoxFrom.Y) * k;
        cv.Set(mapX + (int)Math.Round(gx), mapY + (int)Math.Round(gy), onGoal ? '★' : '◈',
            onGoal ? Col.Mix(0x66ff9f, 0xffffff, 0.5 + 0.5 * Math.Sin(t * 7)) : Col.Mix(0xffb84d, 0xffffff, 0.35));
    }

    // player with easing
    var pk = animT >= 1 ? 1.0 : animT * animT * (3 - 2 * animT);
    var pvisX = animFrom.X + (px - animFrom.X) * pk;
    var pvisY = animFrom.Y + (py - animFrom.Y) * pk;
    var pg = 0.4 + 0.3 * Math.Sin(t * 9);
    cv.Set(mapX + (int)Math.Round(pvisX), mapY + (int)Math.Round(pvisY), '@', Col.Mix(0x4ddfff, 0xffffff, pg));
    cv.Set(mapX + (int)Math.Round(pvisX), mapY + (int)Math.Round(pvisY) - 1, '·', Col.Scale(0x4ddfff, 0.4));
}

void DrawBoxAt(int x, int y, bool onGoal, double t)
{
    if (onGoal)
    {
        var pulse = 0.5 + 0.5 * Math.Sin(t * 7 + x + y);
        cv.Set(x, y, '★', Col.Mix(0x66ff9f, 0xffffff, pulse));
        if (rng.NextDouble() < 0.06)
            fx.Spawn(x + 0.5, y, (rng.NextDouble() - 0.5) * 4, -2 - rng.NextDouble() * 3, 0.7, 0x66ff9f, 0xffffff, '·', -3, 0.6);
    }
    else
    {
        var pulse = 0.5 + 0.5 * Math.Sin(t * 7 + x + y);
        cv.Set(x, y, '◈', Col.Mix(0xffb84d, 0xffffff, 0.15 + pulse * 0.2));
    }
}

void DrawHud()
{
    cv.Text(2, 0, $"LEVEL {levelIdx + 1}/{levels.Length}", Col.Hsl(hue, 0.8, 0.62));
    cv.TextC($"MOVES {moves}", 0xf2f5ff, 0);
    var sc = $"SCORE {score:D6}";
    cv.Text(W - 2 - sc.Length, 0, sc, 0xf2f5ff);

    var b = $"BEST {best:D6}";
    cv.Text(2, 1, b, 0x8fa3c8);
    if (stuckWarn && state == 1)
        cv.TextC("BOX STUCK?  Z = UNDO   R = RESTART", Col.Scale(0xff5c7a, 0.6 + 0.4 * Math.Sin(fx.Time * 8)), 1);
    else
        cv.Text(W - 34, 1, "Z undo   R restart   P pause   Q quit", 0x607096);
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
    var k = Math.Min(1.0, bannerT > 0.9 ? (1.4 - bannerT) * 3 : bannerT * 1.6);
    cv.TextC(bannerText, Col.Scale(0xffffff, Math.Max(0.1, k)), 2);
}

void DrawPause()
{
    cv.TextC("PAUSED", 0xffffff, H / 2 - 1);
    cv.TextC("P / ESC resume    R restart    Q quit", 0xa8b8e0, H / 2 + 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "SOKOBAN PRO";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("N E O N   V A U L T", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("9 levels, smooth pushes, unlimited undo", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Push every crate onto a glowing goal.", 0x6f7fa8, 22);
}

void DrawVictory()
{
    if (stateT < 0.4) return;
    var half = Math.Min(27, W / 2 - 1);
    var x0 = W / 2 - half;
    var x1 = W / 2 + half;
    var y0 = 7;
    var y1 = 17;
    for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            cv.Set(x, y, '█', 0x140b24);
    for (var x = x0; x <= x1; x++)
    {
        cv.Set(x, y0, '─', 0x66ff9f);
        cv.Set(x, y1, '─', 0x66ff9f);
    }
    for (var y = y0; y <= y1; y++)
    {
        cv.Set(x0, y, '│', 0x66ff9f);
        cv.Set(x1, y, '│', 0x66ff9f);
    }
    var t = fx.Time;
    cv.TextGrad(W / 2 - 6, y0 + 2, "VAULT CLEARED", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xffd700, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"TOTAL MOVES {totalMoves}", 0x9fb0d8, y0 + 6);
    cv.TextC(newRecord ? "NEW RECORD!" : $"BEST {best:D6}", newRecord ? 0xffd700 : 0x8fa3c8, y0 + 8);
    if (t % 1.0 < 0.7) cv.TextC("SPACE play again     ESC title", 0xc9d4ff, y0 + 10);
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
