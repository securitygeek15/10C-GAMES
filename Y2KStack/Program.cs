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
Settings.Apply(cv);
var fx = new Fx();
var rng = new Random();
var best = Store.Best("y2kstack");
var board = new int[10, 20];
var bag = new List<int>();
var pops = new List<Pop>();
const int Title = 0;
const int Playing = 1;
const int Paused = 2;
const int OverState = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("insert disc", "how to play", "quit");
var overMenu = new Menu("burn another", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var heldType = -1;
var holdUsed = false;
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
    heldType = -1;
    holdUsed = false;
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
    holdUsed = false;
    if (Collides(curShape, curX, curY)) Over();
}

void HoldPiece()
{
    if (holdUsed) return;
    holdUsed = true;
    var swap = heldType;
    heldType = curType;
    curType = swap < 0 ? nextType : swap;
    if (swap < 0) nextType = NextType();
    curShape = (int[,])shapes[curType].Clone();
    curX = 3;
    curY = 0;
    fallAcc = 0;
    fx.Spark(bx + 10, by + 10, colors[curType], 10);
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

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Playing; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.2)
            fx.Spawn(rng.Next(W), H, (rng.NextDouble() - 0.5) * 1.5, -1.5 - rng.NextDouble() * 2.5,
                4.5, 0x7ff5e3, 0xff8fc8, '✦', 0, 0.04);
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
        if (Input.Hit(ConsoleKey.P) || Input.Hit(ConsoleKey.Escape)) { state = Paused; pauseMenu.Index = 0; stateT = 0; }
        else
        {
            if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) TryMove(-1, 0);
            if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) TryMove(1, 0);
            if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) StepDown();
            if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W) || Input.Hit(ConsoleKey.X)) TryRotate();
            if (Input.Hit(ConsoleKey.C)) HoldPiece();
            if (Input.Hit(ConsoleKey.Spacebar)) HardDrop();

            if (state == Playing)
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
    else
    {
        var pick = overMenu.Update();
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
        p.Y -= dt * 5;
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
        DrawBoard();
        DrawPanel();
        fx.Draw(cv);
        DrawPops();
        if (state == Paused) DrawPause();
        if (state == OverState) DrawOver();
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
    cv.Text(panelX + 9, by, "hold", 0x7fa0c0);
    var c = colors[nextType];
    for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
            if (shapes[nextType][y, x] != 0)
                cv.Set(panelX + 2 + x, by + 2 + y, '█', c);
    if (heldType >= 0)
    {
        var hc = holdUsed ? Col.Scale(colors[heldType], 0.4) : colors[heldType];
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                if (shapes[heldType][y, x] != 0)
                    cv.Set(panelX + 10 + x, by + 2 + y, '▓', hc);
    }

    cv.Text(panelX, by + 7, $"score {score:000000}", 0xd8e8ff);
    cv.Text(panelX, by + 8, $"best  {best:000000}", 0x5a7a9a);
    cv.Text(panelX, by + 9, $"level {level}", 0x7ff5e3);
    cv.Text(panelX, by + 10, $"lines {lines}", 0xb9a7ff);
    cv.Text(panelX, by + 11, $"cds   {burned}", 0xff8fc8);

    cv.Text(panelX, by + 13, "left/right  move", 0x5a7a9a);
    cv.Text(panelX, by + 14, "up          rotate", 0x5a7a9a);
    cv.Text(panelX, by + 15, "down        soft", 0x5a7a9a);
    cv.Text(panelX, by + 16, "space       drop", 0x5a7a9a);
    cv.Text(panelX, by + 17, "c           hold", 0x7fa0c0);
    cv.Text(panelX, by + 18, "p / esc     pause", 0x5a7a9a);
    cv.Text(panelX, by + 19, "q           quit", 0x3e5a72);
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
    cv.Dim(0.45);
    Settings.RefreshPauseLabels(pauseMenu);
    var (_, y0, _, y1) = Ui.CenterCard(cv, Math.Min(40, W - 4), Math.Min(12, H - 2), "paused", 0x7ff5e3);
    pauseMenu.Draw(cv, y0 + 2);
    cv.TextC("P / ESC to resume", Ui.InkFaint, y1 - 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var bandY = Math.Max(2, (H - 16) / 2);
    for (var y = bandY; y < bandY + 5; y++)
        for (var x = 3; x < W - 3; x++)
            cv.Set(x, y, ' ', Col.Hsl(190 + x * 1.6 + (y - bandY) * 8, 0.45, 0.3));

    cv.TextC("Y 2 K   S T A C K", 0xf4ffff, bandY + 1);
    cv.TextC("the year is 2001. the blocks are forever.", 0xbfe8ff, bandY + 3);

    titleMenu.Draw(cv, bandY + 7);

    cv.TextC("stack the tech. burn the cds. survive the drop.", 0x8aa0c0, bandY + 11);
    if (best > 0) Ui.Badge(cv, (W - 13) / 2, bandY + 13, $"best {best:000000}", 0xffe066, 0x242008);

    Ui.Ticker(cv, H - 3, "now loading: your childhood  ·  please insert disc 2  ·  chrome blocks forever",
        t, 0x5a7a9a, 0x3e5a72, 6);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (hx0, hy0, hx1, hy1) = Ui.CenterCard(cv, Math.Min(58, W - 2), Math.Min(18, H - 2), "how to play", 0x7ff5e3);
    var x = hx0 + 3;
    var y = hy0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "stack the cd cases. fill a row to burn it. the higher the level, the faster the drop.",
        hx1 - x - 2, Ui.Ink, 3) + 1;
    Ui.KeyHint(cv, x, y, "←/→", "move", 0x7ff5e3); y++;
    Ui.KeyHint(cv, x, y, "↑/X", "rotate", 0x9be564); y++;
    Ui.KeyHint(cv, x, y, "SPACE", "hard drop, scores per row", Ui.Gold); y++;
    Ui.KeyHint(cv, x, y, "C", "hold a piece (once per drop)", 0xff8fc8); y++;
    Ui.KeyHint(cv, x, y, "P", "pause + settings", Ui.Accent); y += 2;
    cv.Text(x, y, "four rows at once is a burned mixtape.", Ui.InkDim);
    cv.TextC("enter / esc to go back", Ui.InkFaint, hy1 - 1);
}

void DrawOver()
{
    cv.Dim(0.4);
    var (dx0, dy0, dx1, dy1) = Ui.CenterCard(cv, Math.Min(56, W - 2), Math.Min(18, H - 2), "stack overflow", 0xff8fc8);
    var cx = (dx0 + dx1) / 2;
    cv.TextC("the y2k bug won.", 0x8aa0c0, dy0 + 2);

    var (rank, tier) = Ui.Rank(score, new[] { 500, 1500, 3000, 6000, 10000, 16000 });
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 4, "score", $"{score:000000}", 0xd8e8ff);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 5, "lines", lines.ToString(), 0xb9a7ff);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 6, "level / cds", $"{level} / {burned}", 0x7ff5e3);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 7, "rank", rank, Ui.RankColor(tier));
    Ui.Divider(cv, dx0 + 2, dx1 - 2, dy0 + 9);
    if (newRecord) Ui.NewBest(cv, cx, dy0 + 10, fx.Time);
    else Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 10, "best", $"{best:000000}", 0xffe066);

    overMenu.Draw(cv, dy0 + 12);
    cv.TextC("R to burn another", Ui.InkFaint, dy1 - 1);
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
