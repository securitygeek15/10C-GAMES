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
var best = Store.Best("bratsummer");
var snake = new LinkedList<(int X, int Y)>();
var occ = new HashSet<(int X, int Y)>();
var obstacles = new HashSet<(int X, int Y)>();
var inputQ = new Queue<(int X, int Y)>();
var pops = new List<Pop>();
const int Title = 0;
const int Playing = 1;
const int Dead = 2;
const int Paused = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("party", "how to play", "quit");
var deadMenu = new Menu("party again", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var dir = (X: 1, Y: 0);
var food = (X: 0, Y: 0);
var foodParty = false;
var score = 0;
var foods = 0;
var moveInt = 0.14;
var moveAcc = 0.0;
var partyT = 0.0;
var hue = 0.0;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var newRecord = false;

var lime = 0x8ace00;
var phrases = new[] { "so julia", "it's giving brat", "365", "party girl", "lime green", "your fav reference's fav reference", "bumpin' that" };

var x0 = 1;
var y0 = 3;
var x1 = W - 2;
var y1 = H - 3;
var fx0 = x0 + 1;
var fy0 = y0 + 1;
var fw = x1 - x0 - 1;
var fh = y1 - y0 - 1;

void Start()
{
    snake.Clear();
    occ.Clear();
    obstacles.Clear();
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
    foods = 0;
    moveInt = 0.14;
    moveAcc = 0;
    partyT = 0;
    newRecord = false;
    SpawnFood();
}

void SpawnFood()
{
    (int X, int Y) p;
    var tries = 0;
    do
    {
        p = (rng.Next(fx0, fx0 + fw), rng.Next(fy0, fy0 + fh));
        tries++;
    } while (tries < 400 && (occ.Contains(p) || obstacles.Contains(p)));
    food = p;
    foodParty = foods > 0 && foods % 5 == 0;
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
    // party mode lets you run off one side and come back on the other
    if (partyT > 0)
    {
        if (nx < fx0) nx = fx0 + fw - 1;
        else if (nx >= fx0 + fw) nx = fx0;
        if (ny < fy0) ny = fy0 + fh - 1;
        else if (ny >= fy0 + fh) ny = fy0;
    }
    if (nx < fx0 || nx >= fx0 + fw || ny < fy0 || ny >= fy0 + fh || occ.Contains((nx, ny)) || obstacles.Contains((nx, ny)))
    {
        Die();
        return;
    }
    snake.AddFirst((nx, ny));
    occ.Add((nx, ny));
    if (nx == food.X && ny == food.Y)
    {
        Eat();
    }
    else
    {
        occ.Remove(snake.Last!.Value);
        snake.RemoveLast();
    }
}

void Eat()
{
    var party = foodParty;
    var mult = partyT > 0 ? 2 : 1;
    var gain = (party ? 60 : 10) * mult;
    score += gain;
    foods++;
    if (party)
    {
        partyT = 4.0;
        flash = 0.25;
        flashCol = lime;
        fx.Ring(food.X + 0.5, food.Y + 0.5, 20, 22, 0.6, lime, 0xffffff, '✦');
        toasts.Push("party mode: walls are optional", lime);
    }
    fx.Burst(food.X + 0.5, food.Y + 0.5, 20, lime, 0xffffff, 14, 0.65, 6, 2.5, '✦');
    AddPop(food.X, food.Y, $"+{gain}", lime);
    moveInt = Math.Max(0.065, 0.14 - foods * 0.0022);
    SpawnFood();
    if (foods % 4 == 0) AddObstacle();
}

void AddObstacle()
{
    if (obstacles.Count >= 18) return;
    for (var i = 0; i < 120; i++)
    {
        var p = (X: rng.Next(fx0, fx0 + fw), Y: rng.Next(fy0, fy0 + fh));
        if (occ.Contains(p) || obstacles.Contains(p) || p == food) continue;
        var head = snake.First!.Value;
        if (Math.Abs(p.X - head.X) + Math.Abs(p.Y - head.Y) < 6) continue;
        obstacles.Add(p);
        AddPop(p.X, p.Y, "responsibility", 0x8a8a7a);
        return;
    }
}

void Die()
{
    state = 2;
    stateT = 0;
    shake = 12;
    flash = 0.6;
    flashCol = 0x2a3a00;
    foreach (var seg in snake)
        fx.Burst(seg.X + 0.5, seg.Y + 0.5, 4, lime, 0x1a2a00, 12, 0.9, 5, 2);
    if (score > best)
    {
        best = score;
        Store.Save("bratsummer", score);
        newRecord = true;
    }
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x - s.Length / 2.0, y, s, c, 1.0, 1.0));
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 40) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 24);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.2);
    if (partyT > 0) partyT = Math.Max(0, partyT - dt);
    stateT += dt;

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Playing; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.3)
            fx.Spawn(rng.Next(W), H, (rng.NextDouble() - 0.5) * 2, -2 - rng.NextDouble() * 4,
                4.5, lime, 0x1a2a00, rng.Next(2) == 0 ? '✦' : '·', 0, 0.05);
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
        if (Input.Pause) { state = Paused; pauseMenu.Index = 0; stateT = 0; }
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) QueueDir(0, -1);
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) QueueDir(0, 1);
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) QueueDir(-1, 0);
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) QueueDir(1, 0);

        moveAcc += dt;
        var interval = partyT > 0 ? moveInt * 0.88 : moveInt;
        if (moveAcc >= interval)
        {
            moveAcc = 0;
            Step();
        }
    }
    else
    {
        var pick = deadMenu.Update();
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
        DrawField();
        DrawEntities();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (state == Dead) DrawDead();
        if (state == Paused) DrawPause();
    }

    toasts.Draw(cv, 1);

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.55, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"brat summer over. score {score}, best {best}. it was very brat.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            var dot = (x * 9 + y * 5 + (int)(fx.Time * 5)) % 151 == 0;
            cv.Set(x, y, dot ? '·' : ' ', dot ? 0x243000 : 0x080a04);
        }
    }
    var scan = (int)(fx.Time * 9) % Math.Max(1, H);
    for (var x = 0; x < W; x++) cv.Set(x, scan, '─', 0x141c04);
}

void DrawField()
{
    for (var y = fy0; y < fy0 + fh; y++)
        for (var x = fx0; x < fx0 + fw; x++)
            if ((x * 3 + y * 7 + (int)(fx.Time * 4)) % 19 == 0)
                cv.Set(x, y, '·', 0x1e2a08);

    for (var x = x0; x <= x1; x++)
    {
        cv.Set(x, y0, '─', lime);
        cv.Set(x, y1, '─', lime);
    }
    for (var y = y0; y <= y1; y++)
    {
        cv.Set(x0, y, '│', lime);
        cv.Set(x1, y, '│', lime);
    }
    cv.Set(x0, y0, '╭', 0xd8ff6a);
    cv.Set(x1, y0, '╮', 0xd8ff6a);
    cv.Set(x0, y1, '╰', 0x6a8a2a);
    cv.Set(x1, y1, '╯', 0x6a8a2a);
}

void DrawEntities()
{
    var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 7);
    if (foodParty)
    {
        cv.Set(food.X, food.Y, '★', 0xffffff);
        for (var k = 0; k < 4; k++)
        {
            var a = fx.Time * 3 + k * Math.PI / 2;
            cv.Set((int)Math.Round(food.X + Math.Cos(a) * 2), (int)Math.Round(food.Y + Math.Sin(a) * 1.2),
                '·', lime);
        }
    }
    else
    {
        cv.Set(food.X, food.Y, '✦', Col.Mix(lime, 0xffffff, pulse * 0.55));
        for (var k = 0; k < 3; k++)
        {
            var a = fx.Time * 4 + k * Math.PI * 2 / 3;
            cv.Set((int)Math.Round(food.X + Math.Cos(a) * 1.8), (int)Math.Round(food.Y + Math.Sin(a) * 1.1),
                '·', 0x4a6a1a);
        }
    }

    foreach (var o in obstacles)
    {
        var c = (o.X * 3 + o.Y) % 5 == 0 ? 0x8a8a7a : 0x4a4a42;
        cv.Set(o.X, o.Y, '▨', c);
    }

    if (state is Playing or Dead or Paused)
    {
        var i = 0;
        var n = snake.Count;
        foreach (var seg in snake)
        {
            var u = n <= 1 ? 0 : (double)i / (n - 1);
            int col;
            if (partyT > 0)
                col = Col.Hsl(hue + u * 300, 0.9, 0.55);
            else
                col = Col.Hsl(82 + u * 30, 0.8, 0.55 - u * 0.2);
            if (i == 0)
            {
                cv.Set(seg.X, seg.Y, '█', Col.Mix(col, 0xffffff, 0.55));
                cv.Set(seg.X, seg.Y - 1, '·', Col.Scale(col, 0.4));
            }
            else if (i == n - 1) cv.Set(seg.X, seg.Y, '░', col);
            else cv.Set(seg.X, seg.Y, i % 2 == 0 ? '▓' : '█', col);
            i++;
        }
    }
}

void DrawHud()
{
    var band = 0x8ace00;
    cv.Text(2, 0, "brat", band);
    cv.Text(7, 0, "summer", 0xd8ff9a);
    cv.TextC($"{score:000000}", 0xf4ffe0, 0);
    cv.TextRight($"best {best:000000}", W - 3, 0, 0x5a7a2a);
    cv.Text(2, 1, $"length {snake.Count}   lime {foods}", 0x6a8a3a);
    if (partyT > 0)
        cv.TextC($"PARTY MODE {partyT:0.0} · x2 · walls off", Col.Hsl(hue, 0.9, 0.6), 1);
    else
        cv.TextC("x1", 0x6a8a3a, 1);
    var phrase = phrases[(int)(fx.Time / 5) % phrases.Length];
    cv.TextC(phrase, 0x3e5020, H - 2);
    Ui.Hints(cv, H - 1, "WASD", "move", "P", "pause");
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.6);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawTitle()
{
    var t = fx.Time;
    var bandY = Math.Max(2, (H - 16) / 2);
    cv.Fill(3, bandY, W - 4, bandY + 5, ' ', lime);
    cv.TextC("B R A T   S U M M E R", 0x0a0a0a, bandY + 1);
    cv.TextC("365 party girl", 0x1a2a00, bandY + 3);
    cv.Set(5, bandY + 2, '✦', 0x0a0a0a);
    cv.Set(W - 6, bandY + 2, '✦', 0x0a0a0a);

    titleMenu.Draw(cv, bandY + 7);

    cv.TextC("eat the lime. dodge the responsibilities.", 0xd8ff9a, bandY + 11);
    if (best > 0) Ui.Badge(cv, (W - 13) / 2, bandY + 13, $"best {best:000000}", 0xffffff, 0x2a3a00);

    Ui.Ticker(cv, H - 3, "the party is 365 days  ·  it's giving snake  ·  hydrate  ·  so julia",
        t, 0x6a8a3a, 0x3e5020, 6);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (hx0, hy0, hx1, hy1) = Ui.CenterCard(cv, Math.Min(56, W - 2), Math.Min(17, H - 2), "how to play", lime);
    var x = hx0 + 3;
    var y = hy0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "classic snake, but lime green. eat the lime, grow, and do not touch a responsibility or yourself.",
        hx1 - x - 2, Ui.Ink, 3) + 1;
    Ui.KeyHint(cv, x, y, "WASD", "move", lime); y++;
    Ui.KeyHint(cv, x, y, "P", "pause + settings", Ui.Gold); y += 2;
    cv.Set(x, y, '✦', lime); cv.Text(x + 2, y, "lime: +10 points, grow one", Ui.InkDim); y++;
    cv.Set(x, y, '★', 0xffffff); cv.Text(x + 2, y, "every 5th lime: party mode, +60 and x2", Ui.InkDim); y++;
    cv.Text(x, y, "party mode also turns walls into doors.", 0xd8ff9a); y++;
    cv.Text(x, y, "responsibilities (▨) stack up as you eat.", Ui.InkDim);
    cv.TextC("enter / esc to go back", Ui.InkFaint, hy1 - 1);
}

void DrawPause()
{
    cv.Dim(0.45);
    Settings.RefreshPauseLabels(pauseMenu);
    var (_, py0, _, py1) = Ui.CenterCard(cv, 38, 11, "paused", Ui.Accent);
    pauseMenu.Draw(cv, py0 + 2);
    cv.TextC("P / ESC to resume", Ui.InkFaint, py1 - 1);
}

void DrawDead()
{
    cv.Dim(0.4);
    var (dx0, dy0, dx1, dy1) = Ui.CenterCard(cv, Math.Min(54, W - 2), Math.Min(18, H - 2), "the party's over", lime);
    var cx = (dx0 + dx1) / 2;
    cv.TextC("responsibilities: 1    you: 0", 0x8a8a7a, dy0 + 2);

    var (rank, tier) = Ui.Rank(score, new[] { 100, 250, 450, 700, 1000, 1400 });
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 4, "brat points", $"{score:000000}", 0xf4ffe0);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 5, "limes", foods.ToString(), lime);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 6, "length", snake.Count.ToString(), 0xd8ff9a);
    Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 7, "rank", rank, Ui.RankColor(tier));
    Ui.Divider(cv, dx0 + 2, dx1 - 2, dy0 + 9);
    if (newRecord) Ui.NewBest(cv, cx, dy0 + 10, fx.Time);
    else Ui.ScoreRow(cv, dx0 + 3, dx1 - 3, dy0 + 10, "best", $"{best:000000}", 0xffffff);

    deadMenu.Draw(cv, dy0 + 12);
    cv.TextC("R to party again", Ui.InkFaint, dy1 - 1);
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
