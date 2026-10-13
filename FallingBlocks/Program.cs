using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(90, 30, 64, 24);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 64x24 and re-run.");
    return;
}

var cv = new Canvas(size.W, size.H);
var fx = new Fx();
var rng = new Random();

const int BW = 10;
const int FH = 18;
var wx = (size.W - 22) / 2;      // well border column
var fy = 3;                       // first field screen row

var best = Store.Best("fallingblocks");
var board = new int[BW, FH];
var bag = new Queue<int>();
var preview = new Queue<int>();
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

// palette: I O T S Z J L
var shapeColors = new[] { 0x2ee6ff, 0xffd60a, 0xb14aff, 0x2eff8a, 0xff3b5c, 0x4a6cff, 0xff9f1c };
var shapes = new (int X, int Y)[][]
{
    new[] { (0,0), (1,0), (2,0), (3,0) },
    new[] { (0,0), (1,0), (0,1), (1,1) },
    new[] { (0,0), (1,0), (2,0), (1,1) },
    new[] { (1,0), (2,0), (0,1), (1,1) },
    new[] { (0,0), (1,0), (1,1), (2,1) },
    new[] { (0,0), (0,1), (1,1), (2,1) },
    new[] { (2,0), (0,1), (1,1), (2,1) },
};

var pieceId = 0;
var cells = shapes[0];
var pcx = 3;
var pcy = 0.0;
var pvisY = 0.0;
var holdId = -1;
var holdUsed = false;
var lockT = 0.0;
var gravAcc = 0.0;

var score = 0;
var lines = 0;
var combo = 0;
var bestCombo = 0;
var newRecord = false;
var clearRows = new List<int>();
var clearT = 0.0;
var hue = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 over
var prevState = -1;
var stateT = 0.0;
var quit = false;

int Level() => 1 + lines / 10;
double GravInt() => Math.Max(0.05, 0.85 - (Level() - 1) * 0.075);

int NextId()
{
    if (bag.Count == 0)
        foreach (var id in Enumerable.Range(0, 7).OrderBy(_ => rng.Next()))
            bag.Enqueue(id);
    return bag.Dequeue();
}

void RefillPreview()
{
    while (preview.Count < 4) preview.Enqueue(NextId());
}

bool CanPlace((int X, int Y)[] cs, int x, double y)
{
    var iy = (int)Math.Round(y);
    foreach (var c in cs)
    {
        var bx = x + c.X;
        var by = iy + c.Y;
        if (bx < 0 || bx >= BW || by >= FH) return false;
        if (by >= 0 && board[bx, by] != 0) return false;
    }
    return true;
}

void Spawn(int id)
{
    pieceId = id;
    cells = shapes[id].ToArray();
    pcx = 3;
    pcy = 0;
    pvisY = 0;
    lockT = 0;
    gravAcc = 0;
    holdUsed = false;
    if (!CanPlace(cells, pcx, 0)) Die();
}

void StartGame()
{
    Array.Clear(board);
    bag.Clear();
    preview.Clear();
    clearRows.Clear();
    clearT = 0;
    score = 0;
    lines = 0;
    combo = 0;
    bestCombo = 0;
    newRecord = false;
    holdId = -1;
    holdUsed = false;
    RefillPreview();
    Spawn(preview.Dequeue());
    RefillPreview();
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 11;
    flash = 0.75;
    flashCol = 0xff3b5c;
    for (var y = 0; y < FH; y++)
        for (var x = 0; x < BW; x++)
            if (board[x, y] != 0)
                fx.Burst(wx + 1 + x * 2 + 1, fy + y, 3, board[x, y], 0x12081f, 14, 1.1, 4, 2.5);
    if (score > best)
    {
        best = score;
        Store.Save("fallingblocks", score);
        newRecord = true;
    }
}

void LockPiece()
{
    var iy = (int)Math.Round(pcy);
    var topOut = false;
    foreach (var c in cells)
    {
        var bx = pcx + c.X;
        var by = iy + c.Y;
        if (by < 0) { topOut = true; continue; }
        board[bx, by] = shapeColors[pieceId];
    }
    if (topOut) { Die(); return; }

    clearRows.Clear();
    for (var y = 0; y < FH; y++)
    {
        var full = true;
        for (var x = 0; x < BW; x++) full &= board[x, y] != 0;
        if (full) clearRows.Add(y);
    }

    if (clearRows.Count > 0)
    {
        combo++;
        bestCombo = Math.Max(bestCombo, combo);
        var baseScore = new[] { 0, 100, 300, 500, 800 }[clearRows.Count];
        var gain = baseScore * Level() + 50 * (combo - 1) * Level();
        score += gain;
        lines += clearRows.Count;
        clearT = 0.5;
        shake = 3 + clearRows.Count * 2.5;
        flash = clearRows.Count >= 4 ? 0.55 : 0.3;
        flashCol = clearRows.Count >= 4 ? 0xffd700 : 0xffffff;
        foreach (var y in clearRows)
            for (var x = 0; x < BW; x += 2)
                fx.Spawn(wx + 1 + x * 2 + 1, fy + y, (rng.NextDouble() - 0.5) * 40, -(6 + rng.NextDouble() * 26),
                    0.8 + rng.NextDouble() * 0.5, 0xffffff, shapeColors[(x + y) % 7], '█', 28, 2.2);
        pops.Add((wx + 1, fy + clearRows[0] - 1,
            clearRows.Count >= 4 ? $"TETRIS +{gain}" : $"+{gain}",
            clearRows.Count >= 4 ? 0xffd700 : 0xffffff, 1.3, 1.3));
        if (combo > 1)
            pops.Add((wx + 1, fy + clearRows[0] + 1, $"COMBO x{combo}", 0xff7bd5, 1.3, 1.3));
    }
    else
    {
        combo = 0;
        shake = Math.Max(shake, 1.4);
        for (var i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            fx.Burst(wx + 1 + (pcx + c.X) * 2 + 1, fy + iy + c.Y, 3, shapeColors[pieceId], null, 8, 0.4, 0, 4);
        }
        Spawn(preview.Dequeue());
        RefillPreview();
    }
}

void Collapse()
{
    var kept = new List<int[]>();
    for (var y = FH - 1; y >= 0; y--)
    {
        if (clearRows.Contains(y)) continue;
        var row = new int[BW];
        for (var x = 0; x < BW; x++) row[x] = board[x, y];
        kept.Add(row);
    }
    Array.Clear(board);
    var ny = FH - 1;
    foreach (var row in kept)
    {
        for (var x = 0; x < BW; x++) board[x, ny] = row[x];
        ny--;
    }
    clearRows.Clear();
    Spawn(preview.Dequeue());
    RefillPreview();
}

void Hold()
{
    if (holdUsed) return;
    var cur = pieceId;
    if (holdId >= 0) Spawn(holdId);
    else Spawn(preview.Dequeue());
    if (state != 3) RefillPreview();
    holdId = cur;
    holdUsed = true;
}

void TryRotate()
{
    var rot = cells.Select(c => (X: -c.Y, Y: c.X)).ToArray();
    foreach (var (kx, ky) in new[] { (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1), (0, 1) })
    {
        if (CanPlace(rot, pcx + kx, pcy + ky))
        {
            cells = rot;
            pcx += kx;
            pcy += ky;
            lockT = 0;
            return;
        }
    }
}

void HardDrop()
{
    var dist = 0;
    while (CanPlace(cells, pcx, pcy + 1)) { pcy += 1; dist++; }
    pvisY = pcy;
    score += dist * 2;
    shake = Math.Max(shake, 3);
    foreach (var c in cells)
        fx.Burst(wx + 1 + (pcx + c.X) * 2 + 1, fy + (int)Math.Round(pcy) + c.Y, 4, shapeColors[pieceId], null, 10, 0.45, 14, 3);
    lockT = 999;
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
        if (clearT <= 0)
        {
            if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A))
                if (CanPlace(cells, pcx - 1, pcy)) { pcx--; lockT = 0; }
            if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D))
                if (CanPlace(cells, pcx + 1, pcy)) { pcx++; lockT = 0; }
            if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S))
                if (CanPlace(cells, pcx, pcy + 1)) { pcy += 1; score += 1; gravAcc = 0; }
            if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W) || Input.Hit(ConsoleKey.X)) TryRotate();
            if (Input.Hit(ConsoleKey.Spacebar)) HardDrop();
            if (Input.Hit(ConsoleKey.C)) Hold();
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
    if (shake > 0) shake = Math.Max(0, shake - dt * 24);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.6)
        fx.Spawn(rng.Next(size.W), -1, 0, 5 + rng.NextDouble() * 12,
            (size.H + 2) / (5 + rng.NextDouble() * 12), Col.Hsl(rng.Next(360), 0.85, 0.55), Col.Hsl((rng.Next(360) + 120) % 360, 0.85, 0.5), '▄', 0, 0);

    if (state == 1)
    {
        if (clearT > 0)
        {
            clearT -= dt;
            if (clearT <= 0) Collapse();
        }
        else
        {
            pvisY += (pcy - pvisY) * Math.Min(1, dt * 24);
            if (Math.Abs(pvisY - pcy) < 0.02) pvisY = pcy;

            gravAcc += dt;
            if (gravAcc >= GravInt())
            {
                gravAcc = 0;
                if (CanPlace(cells, pcx, pcy + 1)) pcy += 1;
            }

            if (CanPlace(cells, pcx, pcy + 1))
            {
                lockT = 0;
            }
            else
            {
                lockT += dt;
                if (lockT >= 0.5 && state == 1) LockPiece();
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
        DrawWell();
        DrawPanels();
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
Console.WriteLine($"Thanks for playing FALLING BLOCKS! Score {score}, lines {lines}, best {best}.");

// ---------- rendering ----------
void DrawWell()
{
    // interior ambience
    for (var y = 0; y < FH; y++)
        for (var x = 0; x < BW; x++)
            if (board[x, y] == 0 && (x * 5 + y * 3 + (int)(fx.Time * 5)) % 19 == 0)
                cv.Set(wx + 1 + x * 2, fy + y, '·', Col.Hsl(hue + y * 8, 0.6, 0.16));

    // locked cells
    for (var y = 0; y < FH; y++)
    {
        var clearing = clearT > 0 && clearRows.Contains(y);
        for (var x = 0; x < BW; x++)
        {
            var c = board[x, y];
            if (c == 0) continue;
            if (clearing)
            {
                var blink = (int)(clearT * 18) % 2 == 0;
                var bcol = blink ? 0xffffff : Col.Mix(c, 0xffffff, 0.6);
                cv.Set(wx + 1 + x * 2, fy + y, '█', bcol);
                cv.Set(wx + 2 + x * 2, fy + y, '█', bcol);
            }
            else
            {
                var shade = Col.Mix(c, 0x000000, y * 0.012);
                cv.Set(wx + 1 + x * 2, fy + y, '█', shade);
                cv.Set(wx + 2 + x * 2, fy + y, '█', Col.Mix(shade, 0x000000, 0.25));
            }
        }
    }

    // well frame
    for (var x = 0; x <= 21; x++)
    {
        cv.Set(wx + x, fy - 1, '─', Col.Hsl(hue + x * 8, 0.8, 0.42));
        cv.Set(wx + x, fy + FH, '─', Col.Hsl(hue + x * 8, 0.8, 0.42));
    }
    for (var y = fy - 1; y <= fy + FH; y++)
    {
        cv.Set(wx, y, '│', Col.Hsl(hue + y * 7, 0.8, 0.42));
        cv.Set(wx + 21, y, '│', Col.Hsl(hue + y * 7, 0.8, 0.42));
    }
    cv.Set(wx, fy - 1, '┌', Col.Hsl(hue, 0.9, 0.6));
    cv.Set(wx + 21, fy - 1, '┐', Col.Hsl(hue + 120, 0.9, 0.6));
    cv.Set(wx, fy + FH, '└', Col.Hsl(hue + 240, 0.9, 0.6));
    cv.Set(wx + 21, fy + FH, '┘', Col.Hsl(hue + 300, 0.9, 0.6));

    if (state == 3 || clearT > 0) return;

    // ghost
    var gY = pcy;
    while (CanPlace(cells, pcx, gY + 1)) gY += 1;
    if (gY > pcy)
    {
        var gc = Col.Mix(shapeColors[pieceId], 0x000000, 0.55);
        foreach (var c in cells)
        {
            var gy = (int)Math.Round(gY) + c.Y;
            if (gy < 0) continue;
            cv.Set(wx + 1 + (pcx + c.X) * 2, fy + gy, '░', gc);
            cv.Set(wx + 2 + (pcx + c.X) * 2, fy + gy, '░', gc);
        }
    }

    // falling piece with eased visual position
    var col = shapeColors[pieceId];
    var minY = cells.Min(c => c.Y);
    foreach (var c in cells)
    {
        var sy = (int)Math.Round(c.Y + pvisY);
        if (sy < 0 || sy >= FH) continue;
        var shade = Col.Mix(col, 0x000000, (c.Y - minY) * 0.10);
        if (c.Y == minY) shade = Col.Mix(shade, 0xffffff, 0.25 + 0.1 * Math.Sin(fx.Time * 8));
        cv.Set(wx + 1 + (pcx + c.X) * 2, fy + sy, '█', shade);
        cv.Set(wx + 2 + (pcx + c.X) * 2, fy + sy, '█', Col.Mix(shade, 0x000000, 0.22));
    }
}

void DrawMini(int cx, int cy, (int X, int Y)[] cs, int color)
{
    if (cs.Length == 0) return;
    var minx = cs.Min(c => c.X);
    var miny = cs.Min(c => c.Y);
    var maxx = cs.Max(c => c.X);
    var maxy = cs.Max(c => c.Y);
    var ox = cx - (maxx - minx + 1);
    foreach (var c in cs)
    {
        var x = ox + (c.X - minx) * 2;
        var y = cy + (c.Y - miny);
        cv.Set(x, y, '█', color);
        cv.Set(x + 1, y, '█', Col.Mix(color, 0x000000, 0.25));
    }
}

void DrawPanels()
{
    var lx = 2;
    var rx = wx + 23;
    var t = fx.Time;

    // left: stats
    cv.Text(lx, 1, "SCORE", 0x8fa3c8);
    cv.Text(lx, 2, score.ToString("D6"), 0xffffff);
    cv.Text(lx, 4, "BEST", 0x8fa3c8);
    cv.Text(lx, 5, best.ToString("D6"), 0xffd166);
    cv.Text(lx, 7, "LEVEL", 0x8fa3c8);
    cv.Text(lx, 8, Level().ToString(), Col.Hsl((t * 60) % 360, 0.9, 0.62));
    cv.Text(lx, 10, "LINES", 0x8fa3c8);
    cv.Text(lx, 11, lines.ToString(), 0x5be0c0);
    if (combo > 1)
    {
        cv.Text(lx, 13, $"COMBO", 0xff7bd5);
        cv.Text(lx, 14, $"x{combo}", 0xff7bd5);
    }
    for (var i = 0; i < FH; i += 2)
        cv.Set(lx, fy + i, '·', 0x2a3350);

    // right: hold + next
    cv.Text(rx, 1, "HOLD", holdUsed ? 0x5a6480 : 0xff7bd5);
    if (holdId >= 0) DrawMini(rx + 4, 3, shapes[holdId], holdUsed ? 0x5a6480 : shapeColors[holdId]);
    for (var i = 0; i < FH; i += 2)
        cv.Set(rx + 13, fy + i, '·', 0x2a3350);

    cv.Text(rx, 7, "NEXT", 0x8fa3c8);
    var q = preview.ToArray();
    for (var i = 0; i < Math.Min(3, q.Length); i++)
        DrawMini(rx + 4, 9 + i * 4, shapes[q[i]], i == 0 ? shapeColors[q[i]] : Col.Mix(shapeColors[q[i]], 0x000000, 0.3));
}

void DrawHud()
{
    cv.TextC("FALLING BLOCKS // NEON DROP", Col.Hsl(hue, 0.7, 0.62), 0);
    cv.Text(2, size.H - 2, "A/D move  W rotate  S soft  SPACE hard  C hold  P pause  Q quit", 0x607096);
    if (newRecord && state == 3) cv.Text(size.W - 18, size.H - 2, "NEW RECORD!", 0xffd700);
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
    var title = "FALLING BLOCKS";
    var x = (size.W - title.Length) / 2;
    cv.TextGrad(x, 8, title, Col.Hsl(t * 50 % 360, 0.9, 0.64), Col.Hsl((t * 50 + 160) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 10, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 200) % 360, 0.8, 0.5), 0);
    cv.TextC("N E O N   D R O P", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 12);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 16);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("7 pieces, ghost, hold, hard drop, combos", 0x6f7fa8, 22);
}

void DrawOver()
{
    if (stateT < 0.6) return;
    var half = Math.Min(26, size.W / 2 - 1);
    var x0 = size.W / 2 - half;
    var x1 = size.W / 2 + half;
    var y0 = 6;
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
    cv.TextGrad(size.W / 2 - 5, y0 + 2, "GAME OVER", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"LINES {lines}   LEVEL {Level()}   BEST COMBO x{bestCombo}", 0x9fb0d8, y0 + 6);
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
