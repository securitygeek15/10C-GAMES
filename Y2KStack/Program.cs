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
var best = Store.Best("y2kstack");
var board = new int[10, 20];
var bag = new List<int>();
var pops = new List<Pop>();
var state = 0;          // 0 title, 1 play, 2 pause, 3 over
var score = 0;
var lines = 0;
var burned = 0;
var level = 1;
var curType = 0;
var nextType = 0;
var curShape = new int[4, 4];
var curX = 3;
var curY = 0;
var fallAcc = 0.0;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var hue = 0.0;
var newRecord = false;

var bx = Math.Max(2, (W - 44) / 2);
var by = Math.Max(2, (H - 20) / 2);
var panelX = bx + 24;

var colors = new[] { 0x7ff5e3, 0xffe066, 0xff8fc8, 0x9be564, 0xff6f8a, 0xb9a7ff, 0xffb86b };
var shapes = new int[7][,]
{
    new int[,] { {0,0,0,0}, {1,1,1,1}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {0,1,1,0}, {0,1,1,0}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {0,1,0,0}, {1,1,1,0}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {0,1,1,0}, {1,1,0,0}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {1,1,0,0}, {0,1,1,0}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {1,0,0,0}, {1,1,1,0}, {0,0,0,0}, {0,0,0,0} },
    new int[,] { {0,0,1,0}, {1,1,1,0}, {0,0,0,0}, {0,0,0,0} }
};

void Start()
{
    Array.Clear(board);
    bag.Clear();
    pops.Clear();
    score = 0;
    lines = 0;
    burned = 0;
    level = 1;
    fallAcc = 0;
    shake = 0;
    flash = 0;
    newRecord = false;
    nextType = NextType();
    Spawn();
    stateT = 0;
}

int NextType()
{
    if (bag.Count == 0)
    {
        for (var i = 0; i < 7; i++) bag.Add(i);
        for (var i = bag.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }
    }
    var t = bag[^1];
    bag.RemoveAt(bag.Count - 1);
    return t;
}

void Spawn()
{
    curType = nextType;
    nextType = NextType();
    curShape = (int[,])shapes[curType].Clone();
    curX = 3;
    curY = 0;
    fallAcc = 0;
    if (Collides(curShape, curX, curY)) Over();
}

bool Collides(int[,] shape, int sx, int sy)
{
    for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            if (shape[y, x] == 0) continue;
            var gx = sx + x;
            var gy = sy + y;
            if (gx < 0 || gx >= 10 || gy >= 20) return true;
            if (gy >= 0 && board[gx, gy] != 0) return true;
        }
    return false;
}

int[,] Rotate4(int[,] shape)
{
    var r = new int[4, 4];
    for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++) r[x, 3 - y] = shape[y, x];
    return r;
}

void TryRotate()
{
    if (curType == 1) return;
    var r = Rotate4(curShape);
    foreach (var k in new[] { 0, -1, 1, -2, 2 })
    {
        if (!Collides(r, curX + k, curY))
        {
            curShape = r;
            curX += k;
            return;
        }
    }
    if (!Collides(r, curX, curY - 1))
    {
        curShape = r;
        curY--;
    }
}

void TryMove(int dx, int dy)
{
    if (!Collides(curShape, curX + dx, curY + dy))
    {
        curX += dx;
        curY += dy;
        return;
    }
    if (dy > 0) Lock();
}

void StepDown()
{
    if (!Collides(curShape, curX, curY + 1))
    {
        curY++;
        if (Input.Held(ConsoleKey.DownArrow) || Input.Held(ConsoleKey.S)) score += 1;
    }
    else Lock();
}

void HardDrop()
{
    var dist = 0;
    while (!Collides(curShape, curX, curY + 1))
    {
        curY++;
        dist++;
    }
    score += dist * 2;
    if (dist > 0)
    {
        fx.Burst(bx + curX * 2 + 4, by + curY, 10, colors[curType], 0xffffff, 10, 0.4, 0, 2, '·');
    }
    Lock();
}

void Lock()
{
    var topOut = false;
    for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            if (curShape[y, x] == 0) continue;
            var gx = curX + x;
            var gy = curY + y;
            if (gy < 0) { topOut = true; continue; }
            board[gx, gy] = curType + 1;
        }

    var cleared = 0;
    for (var y = 19; y >= 0; y--)
    {
        var full = true;
        for (var x = 0; x < 10; x++) if (board[x, y] == 0) { full = false; break; }
        if (!full) continue;
        for (var yy = y; yy > 0; yy--)
            for (var x = 0; x < 10; x++) board[x, yy] = board[x, yy - 1];
        for (var x = 0; x < 10; x++) board[x, 0] = 0;
        cleared++;
        y++;
    }

    if (cleared > 0)
    {
        var gain = (cleared switch { 1 => 100, 2 => 300, 3 => 500, _ => 800 }) * level;
        score += gain;
        lines += cleared;
        burned += cleared;
        level = 1 + lines / 10;
        flash = 0.32;
        flashCol = colors[curType];
        shake = 4;
        AddPop(bx + 8, by + 9, cleared == 4 ? "burned a mixtape" : $"burned {cleared} cd", 0x7ff5e3);
        fx.Burst(bx + 10, by + 10, 24, 0xb9a7ff, 0x7ff5e3, 18, 0.8, 0, 2, '▓');
    }

    if (topOut)
    {
        Over();
        return;
    }
    Spawn();
    if (state == 1 && Collides(curShape, curX, curY)) Over();
}

void Over()
{
    if (state == 3) return;
    state = 3;
    stateT = 0;
    shake = 12;
    flash = 0.5;
    flashCol = 0x000000;
    if (score > best)
    {
        best = score;
        Store.Save("y2kstack", score);
        newRecord = true;
    }
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x - s.Length / 2.0, y, s, c, 1.0, 1.0));
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 25) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    stateT += dt;

    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter)) { Start(); state = 1; }
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.2)
            fx.Spawn(rng.Next(W), H, (rng.NextDouble() - 0.5) * 1.5, -1.5 - rng.NextDouble() * 2.5,
                4.5, 0x7ff5e3, 0xff8fc8, '✦', 0, 0.04);
    }
    else if (state == 1)
    {
        if (Input.Quit) return false;
        if (Input.Hit(ConsoleKey.P) || Input.Hit(ConsoleKey.Escape)) { state = 2; stateT = 0; }
        else
        {
            if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) TryMove(-1, 0);
            if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) TryMove(1, 0);
            if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) StepDown();
            if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W) || Input.Hit(ConsoleKey.X)) TryRotate();
            if (Input.Hit(ConsoleKey.Spacebar)) HardDrop();

            if (state == 1)
            {
                var interval = Math.Max(0.07, 0.68 - (level - 1) * 0.055);
                fallAcc += dt * (Input.Held(ConsoleKey.DownArrow) || Input.Held(ConsoleKey.S) ? 9 : 1);
                if (fallAcc >= interval)
                {
                    fallAcc = 0;
                    StepDown();
                }
            }
        }
    }
    else if (state == 2)
    {
        if (Input.Hit(ConsoleKey.P) || Input.Hit(ConsoleKey.Escape)) state = 1;
        if (Input.Hit(ConsoleKey.R)) { Start(); state = 1; }
        if (Input.Quit) return false;
    }
    else
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.R)) { Start(); state = 1; }
        if (Input.Hit(ConsoleKey.Escape)) { state = 0; stateT = 0; }
        if (Input.Quit) return false;
    }

    fx.Update(dt);
    for (var i = pops.Count - 1; i >= 0; i--)
    {
        var p = pops[i];
        p.Life -= dt;
        if (p.Life <= 0) { pops.RemoveAt(i); continue; }
        p.Y -= dt * 5;
    }

    cv.Clear();
    cv.ShakeX = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    DrawBackdrop();

    if (state == 0)
    {
        DrawTitle();
        fx.Draw(cv);
    }
    else
    {
        DrawBoard();
        DrawPanel();
        fx.Draw(cv);
        DrawPops();
        if (state == 2) DrawPause();
        if (state == 3) DrawOver();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.5, flash);
    }
    else if (state == 3)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = 0.35;
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"y2k stack closed. score {score}, best {best}. please insert disc 2.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x050912, 0x0c1626, u);
        for (var x = 0; x < W; x++)
        {
            var star = (x * 17 + y * 11 + (int)(fx.Time * 4)) % 179 == 0;
            cv.Set(x, y, star ? '·' : ' ', star ? 0x3a5a7a : c);
        }
    }
    var scan = (int)(fx.Time * 7) % Math.Max(1, H);
    for (var x = 0; x < W; x++) cv.Set(x, scan, '─', 0x101c2c);
}

void DrawBoard()
{
    cv.Fill(bx, by, bx + 19, by + 19, ' ', 0x070b14);
    for (var y = 0; y < 20; y++)
        for (var x = 0; x < 10; x++)
        {
            var wx = bx + x * 2;
            var wy = by + y;
            if (board[x, y] != 0)
            {
                var c = colors[board[x, y] - 1];
                cv.Set(wx, wy, '█', c);
                cv.Set(wx + 1, wy, '▓', Col.Mix(c, 0xffffff, 0.35));
            }
            else cv.Set(wx + 1, wy, '·', 0x18243a);
        }

    if (state is 1 or 2)
    {
        var gy = curY;
        while (!Collides(curShape, curX, gy + 1)) gy++;
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                if (curShape[y, x] == 0) continue;
                var wx = bx + (curX + x) * 2;
                var wy = by + gy + y;
                if (gy + y >= 0)
                {
                    cv.Set(wx, wy, '░', 0x37455a);
                    cv.Set(wx + 1, wy, '░', 0x37455a);
                }
            }

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                if (curShape[y, x] == 0) continue;
                var wx = bx + (curX + x) * 2;
                var wy = by + curY + y;
                if (wy < by) continue;
                var c = colors[curType];
                cv.Set(wx, wy, '█', c);
                cv.Set(wx + 1, wy, '▓', Col.Mix(c, 0xffffff, 0.4));
            }
    }

    for (var x = bx - 1; x <= bx + 20; x++)
    {
        cv.Set(x, by - 1, '▔', 0x24364e);
        cv.Set(x, by + 20, '▁', 0x24364e);
    }
    for (var y = by - 1; y <= by + 20; y++)
    {
        cv.Set(bx - 1, y, '▏', 0x24364e);
        cv.Set(bx + 20, y, '▕', 0x24364e);
    }
}

void DrawPanel()
{
    cv.Text(panelX, by, "next", 0x7fa0c0);
    var c = colors[nextType];
    for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
            if (shapes[nextType][y, x] != 0)
                cv.Set(panelX + 2 + x, by + 2 + y, '█', c);

    cv.Text(panelX, by + 7, $"score {score:000000}", 0xd8e8ff);
    cv.Text(panelX, by + 8, $"best  {best:000000}", 0x5a7a9a);
    cv.Text(panelX, by + 9, $"level {level}", 0x7ff5e3);
    cv.Text(panelX, by + 10, $"lines {lines}", 0xb9a7ff);
    cv.Text(panelX, by + 11, $"cds   {burned}", 0xff8fc8);

    cv.Text(panelX, by + 13, "left/right  move", 0x5a7a9a);
    cv.Text(panelX, by + 14, "up          rotate", 0x5a7a9a);
    cv.Text(panelX, by + 15, "down        soft", 0x5a7a9a);
    cv.Text(panelX, by + 16, "space       drop", 0x5a7a9a);
    cv.Text(panelX, by + 17, "p / esc     pause", 0x5a7a9a);
    cv.Text(panelX, by + 18, "q           quit", 0x3e5a72);
    cv.TextRight("2001", W - 3, 0, 0x3e5a72);

    cv.TextGrad(2, 0, "y2k stack", 0x7ff5e3, 0xff8fc8, (hue / 360) % 1);
    cv.Text(2, 1, $"score {score:000000}", 0x9ac0e0);
    cv.TextRight($"level {level}   lines {lines}", W - 3, 1, 0x5a7a9a);
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
    var boxW = Math.Min(34, W - 8);
    var px0 = (W - boxW) / 2;
    var y0 = by + 7;
    cv.Fill(px0, y0, px0 + boxW, y0 + 6, ' ', 0x0a0f1a);
    cv.Frame(px0, y0, px0 + boxW, y0 + 6, 0x7ff5e3);
    cv.TextC("PAUSED", 0xffffff, y0 + 2);
    cv.TextC("P / ESC resume    R restart", 0x8aa0c0, y0 + 4);
    cv.TextC("Q quit", 0x5a7a9a, y0 + 5);
}

void DrawTitle()
{
    var t = fx.Time;
    var bandY = Math.Max(4, H / 2 - 5);
    for (var y = bandY; y < bandY + 5; y++)
        for (var x = 3; x < W - 3; x++)
            cv.Set(x, y, ' ', Col.Hsl(190 + x * 1.6 + (y - bandY) * 8, 0.45, 0.3));

    cv.TextC("y 2 k   s t a c k", 0xf4ffff, bandY + 1);
    cv.TextC("the year is 2001. the blocks are forever.", 0xbfe8ff, bandY + 3);

    cv.TextC("stack the tech. burn the cds. survive the drop.", 0x8aa0c0, bandY + 7);
    if (best > 0) cv.TextC($"best {best:000000}", 0xffe066, bandY + 9);
    if (t % 1.25 < 0.8) cv.TextC("PRESS SPACE TO INSERT DISC", 0xffffff, bandY + 11);
    cv.TextC("arrows move/rotate     space drop     q quit", 0x5a7a9a, H - 2);
    cv.TextC("now loading: your childhood.", 0x3e5a72, H - 1);
}

void DrawOver()
{
    var panelW = Math.Min(58, W - 4);
    var x0 = (W - panelW) / 2;
    var x1 = x0 + panelW - 1;
    var y0 = Math.Max(3, H / 2 - 6);
    var y1 = Math.Min(H - 2, y0 + 10);
    cv.Fill(x0, y0, x1, y1, ' ', 0x080c14);
    cv.Frame(x0, y0, x1, y1, 0xff8fc8);
    cv.TextC("stack overflow.", 0xff8fc8, y0 + 2);
    cv.TextC("the y2k bug won.", 0x8aa0c0, y0 + 3);
    cv.TextC($"score {score:000000}    lines {lines}    level {level}", 0xd8e8ff, y0 + 5);
    cv.TextC(newRecord ? "new personal best" : $"best {best:000000}", newRecord ? 0xffe066 : 0x5a7a9a, y0 + 6);
    if (fx.Time % 1.15 < 0.78) cv.TextC("SPACE again    ESC title    Q quit", 0xd8e8ff, y0 + 9);
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
