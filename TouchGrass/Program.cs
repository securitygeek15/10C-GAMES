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
var best = Store.Best("touchgrass");
var items = new List<Item>();
var pops = new List<Pop>();
var state = 0;
var score = 0;
var wellness = 100.0;
var elapsed = 0.0;
var level = 0;
var playerX = W / 2.0;
var spawnAcc = 0.0;
var shake = 0.0;
var flash = 0.0;
var newRecord = false;
var stateT = 0.0;
var moveTrail = 0.0;

var levelNames = new[] { "sunday reset", "hot girl walk", "picnic core", "touch grass", "main character" };
var footerLines = new[]
{
    "the sun is out. no thoughts, just petals.",
    "you are doing enough. genuinely.",
    "log off. the feed will survive without you.",
    "breathe in. breathe out. collect vibes.",
    "a little walk counts as a whole personality."
};

void Start()
{
    items.Clear();
    pops.Clear();
    score = 0;
    wellness = 100;
    elapsed = 0;
    level = 0;
    playerX = W / 2.0;
    spawnAcc = 0.6;
    shake = 0;
    flash = 0;
    newRecord = false;
    stateT = 0;
}

void Spawn()
{
    var px = playerX;
    var roll = rng.Next(100);
    var kind = roll < 42 ? 0 : roll < 52 ? 1 : roll < 60 ? 2 : roll < 70 ? 3 : roll < 88 ? 4 : 5;
    var item = new Item
    {
        Kind = kind,
        X = rng.Next(3, Math.Max(4, W - 2)),
        Y = 4,
        Phase = rng.NextDouble() * Math.PI * 2,
        Vy = kind switch
        {
            0 => 2.6 + rng.NextDouble() * 1.6,
            1 => 2.2 + rng.NextDouble() * 1.2,
            2 => 2.2 + rng.NextDouble() * 1.4,
            3 => 2.0 + rng.NextDouble() * 1.0,
            4 => 4.2 + level * 0.5 + rng.NextDouble() * 1.4,
            _ => 4.8 + level * 0.4 + rng.NextDouble() * 1.6
        }
    };
    if (kind == 4 && rng.Next(3) == 0)
        item.X = Math.Clamp(px + rng.Next(-8, 9), 2, W - 3);
    if (kind == 5 && rng.Next(2) == 0)
        item.X = Math.Clamp(px + rng.Next(-10, 11), 2, W - 3);
    items.Add(item);
}

void Caught(Item it)
{
    switch (it.Kind)
    {
        case 0:
            score += 12;
            wellness = Math.Min(100, wellness + 1.5);
            fx.Burst(it.X, it.Y, 10, 0xe58fa3, 0xf7c6d0, 9, 0.7, 5, 2, '❀');
            AddPop(it.X, it.Y, "+12 vibes", 0xd96f8a);
            break;
        case 1:
            score += 30;
            wellness = Math.Min(100, wellness + 6);
            fx.Burst(it.X, it.Y, 18, 0xf2c14e, 0xfff4c2, 12, 0.8, 2, 2, '✦');
            AddPop(it.X, it.Y, "+30 sunshine", 0xd39b18);
            flash = 0.1;
            break;
        case 2:
            wellness = Math.Min(100, wellness + 18);
            score += 15;
            fx.Burst(it.X, it.Y, 16, 0xe58fa3, 0xffe3ea, 11, 0.8, -2, 2, '♡');
            AddPop(it.X, it.Y, "+18 wellness", 0xd96f8a);
            break;
        case 3:
            score += 45;
            wellness = Math.Min(100, wellness + 3);
            fx.Burst(it.X, it.Y, 20, 0xb9a7ff, 0xf0eaff, 14, 0.9, 0, 2, '✧');
            AddPop(it.X, it.Y, "+45 whimsy", 0x8f7ee0);
            break;
        case 4:
            wellness -= 18;
            shake = 8;
            flash = 0.3;
            fx.Burst(it.X, it.Y, 22, 0x7c8a9a, 0x2e3a46, 15, 0.8, 4, 2, '☁');
            AddPop(it.X, it.Y, "doomscrolled :(", 0x5e6d7a);
            break;
        default:
            wellness -= 12;
            score = Math.Max(0, score - 15);
            shake = 6;
            fx.Burst(it.X, it.Y, 18, 0xff4d6d, 0x4a1020, 14, 0.8, 6, 2, '!');
            AddPop(it.X, it.Y, "notification anxiety", 0xd63b57);
            break;
    }
    wellness = Math.Max(0, wellness);
    if (wellness <= 0) Die();
}

void Die()
{
    state = 2;
    stateT = 0;
    if (score > best)
    {
        best = score;
        Store.Save("touchgrass", score);
        newRecord = true;
    }
    fx.Burst(W / 2.0, H / 2.0, 30, 0x8aa17a, 0xe58fa3, 16, 1.1, 2, 1.4, '❀');
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x - s.Length / 2.0, y, s, c, 1.0, 1.0));
}

GameLoop.Run((dt, t) =>
{
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.5);
    stateT += dt;

    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter)) { Start(); state = 1; }
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.12)
            fx.Spawn(rng.Next(W), 0, (rng.NextDouble() - 0.5) * 2, 1.2 + rng.NextDouble() * 2.0,
                7, 0xe58fa3, 0xf7c6d0, '❀', 0.2, 0.05);
    }
    else if (state == 1)
    {
        if (Input.Quit) return false;
        var move = 0.0;
        if (Input.HeldAny(ConsoleKey.A, ConsoleKey.LeftArrow)) move -= 1;
        if (Input.HeldAny(ConsoleKey.D, ConsoleKey.RightArrow)) move += 1;
        playerX = Math.Clamp(playerX + move * 26 * dt, 2, W - 3);
        if (move != 0)
        {
            moveTrail += dt;
            if (moveTrail > 0.05)
            {
                moveTrail = 0;
                fx.Spawn(playerX, H - 7, (rng.NextDouble() - 0.5) * 2, -0.5,
                    0.8, 0x8aa17a, 0xd8e2c8, '·', 0, 1);
            }
        }

        elapsed += dt;
        level = Math.Min(4, (int)(elapsed / 18));
        wellness -= (1.25 + level * 0.38) * dt;

        spawnAcc += dt;
        var interval = Math.Max(0.22, 0.68 - level * 0.07);
        while (spawnAcc >= interval)
        {
            spawnAcc -= interval;
            Spawn();
        }

        var py = H - 7;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            it.Y += it.Vy * dt;
            if (it.Kind == 3) it.X += Math.Sin(t * 3.2 + it.Phase) * 3.0 * dt;
            it.X = Math.Clamp(it.X, 1, W - 2);

            var dx = Math.Abs(it.X - playerX);
            if (it.Y >= py - 1.5 && it.Y <= py + 2 && dx <= 2.8)
            {
                items.RemoveAt(i);
                Caught(it);
                continue;
            }
            if (it.Y > H - 2) items.RemoveAt(i);
        }

        if (wellness <= 0) Die();
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
        p.Y -= dt * 3.2;
    }

    cv.Clear();
    cv.ShakeX = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    cv.ShakeY = shake > 0.3 ? rng.Next(-(int)shake, (int)shake + 1) : 0;
    DrawSky();
    DrawSun();
    DrawHills();
    if (state is 1 or 2)
    {
        foreach (var it in items) DrawItem(it);
        DrawPlayer();
    }
    fx.Draw(cv);
    DrawPops();
    if (state is 1 or 2) DrawHud();
    if (state == 0) DrawTitle();
    if (state == 2) DrawDead();

    if (flash > 0.01)
    {
        cv.OverlayColor = 0xe58fa3;
        cv.OverlayK = Math.Min(0.5, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"touch grass complete. vibes {score}, best {best}. go outside for real.");

void DrawSky()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x211a12, 0x141c14, u);
        for (var x = 0; x < W; x++)
        {
            var twinkle = (x * 7 + y * 11 + (int)(fx.Time * 5)) % 113 == 0;
            cv.Set(x, y, twinkle ? '·' : ' ', twinkle ? 0x5f7a52 : c);
        }
    }
}

void DrawSun()
{
    var sx = W - 11;
    var sy = 5;
    for (var dy = -4; dy <= 4; dy++)
        for (var dx = -4; dx <= 4; dx++)
        {
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 2.4) cv.Set(sx + dx, sy + dy, '█', Col.Mix(0xd9a441, 0xfff0b8, d / 2.4));
            else if (d < 3.5) cv.Set(sx + dx, sy + dy, '░', 0x6b5a2e);
        }
    for (var k = 0; k < 8; k++)
    {
        var a = fx.Time * 0.35 + k * Math.PI / 4;
        var rx = (int)Math.Round(sx + Math.Cos(a) * 6);
        var ry = (int)Math.Round(sy + Math.Sin(a) * 3.2);
        cv.Set(rx, ry, '·', 0xb08c3e);
    }
}

void DrawHills()
{
    for (var x = 0; x < W; x++)
    {
        var back = H - 6 - (int)(2.0 + 2.0 * Math.Sin(x * 0.09 + 2.1));
        for (var y = back; y < H; y++)
            cv.Set(x, y, ' ', Col.Hsl(96, 0.2, 0.24 + (y - back) * 0.012));
        cv.Set(x, back, '▁', Col.Hsl(96, 0.25, 0.36));
    }
    for (var x = 0; x < W; x++)
    {
        var front = H - 4 - (int)(1.0 + 1.0 * Math.Sin(x * 0.16 + 0.4));
        for (var y = front; y < H; y++)
            cv.Set(x, y, ' ', Col.Hsl(104, 0.26, 0.22 + (y - front) * 0.02));
        cv.Set(x, front, (x + (int)(fx.Time * 2)) % 7 == 0 ? '❀' : '▁',
            (x + (int)(fx.Time * 2)) % 7 == 0 ? 0xe58fa3 : Col.Hsl(104, 0.3, 0.4));
    }
}

void DrawItem(Item it)
{
    var (ch, c) = it.Kind switch
    {
        0 => ('❀', 0xf0a0b4),
        1 => ('☀', 0xf2c14e),
        2 => ('♡', 0xe58fa3),
        3 => ('✧', 0xb9a7ff),
        4 => ('☁', 0x8a93a0),
        _ => ('!', 0xff5c7a)
    };
    var x = (int)Math.Round(it.X);
    var y = (int)Math.Round(it.Y);
    var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 5 + it.Phase);
    cv.Set(x, y, ch, c);
    cv.Set(x - 1, y, '·', Col.Scale(c, 0.35 + pulse * 0.2));
    cv.Set(x + 1, y, '·', Col.Scale(c, 0.35 + pulse * 0.2));
}

void DrawPlayer()
{
    var x = (int)Math.Round(playerX);
    var y = H - 7;
    var bob = (int)Math.Round(Math.Sin(fx.Time * 4) * 0.6);
    y += bob;
    cv.Set(x - 1, y, '\\', 0xf6efe0);
    cv.Set(x, y, 'o', 0xf6efe0);
    cv.Set(x + 1, y, '/', 0xf6efe0);
    cv.Set(x - 1, y + 1, '/', 0x8aa17a);
    cv.Set(x + 1, y + 1, '\\', 0x8aa17a);
    cv.Set(x, y + 1, '▔', 0x6e8660);
    for (var k = 0; k < 3; k++)
    {
        var a = fx.Time * 1.6 + k * 2.1;
        cv.Set((int)Math.Round(x + Math.Cos(a) * 2.6), (int)Math.Round(y + Math.Sin(a) * 1.2),
            '·', 0x4f6b53);
    }
}

void DrawHud()
{
    cv.Text(2, 1, $"vibes {score:000000}", 0xcfe0b8);
    cv.TextRight($"best {best:000000}", W - 3, 1, 0x6f8a63);
    cv.TextC(levelNames[level], 0xa8c095, 0);
    cv.Text(2, 2, "wellness", 0x8aa17a);
    var wp = Math.Clamp(wellness / 100.0, 0, 1);
    var wc = wp > 0.55 ? 0x8fbf6f : wp > 0.25 ? 0xe0b84e : 0xe06a7a;
    Ui.Bar(cv, 11, 2, 20, wp, wc, Col.Mix(wc, 0xffffff, 0.4));
    cv.Text(33, 2, $"{(int)wellness}%", 0x9ab088);
    cv.TextC(footerLines[level], 0x5b6f52, H - 1);
    if (state == 1)
    {
        cv.Text(2, H - 2, "a / d  move", 0x63795a);
        cv.TextRight("q quit", W - 3, H - 2, 0x63795a);
    }
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
    var title = "t o u c h   g r a s s";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 8, title, 0xa8c895, 0xf0a0b4, (t * 0.1) % 1);
    cv.TextGrad(x, 9, "──────────────────────", 0xa8c895, 0xf0a0b4, (t * 0.1) % 1);
    cv.TextC("a cozy little game about logging off", 0xd8e2c8, 12);
    cv.TextC("catch the soft stuff. dodge the doom.", 0x8aa17a, 13);
    if (best > 0) cv.TextC($"best vibes {best:000000}", 0xe0c884, 16);
    if (t % 1.4 < 0.85) cv.TextC("PRESS SPACE TO GO OUTSIDE", 0xf6efe0, 19);
    cv.TextC("a / d  or  ← →  move", 0x8aa17a, 21);
    cv.TextC("q quits. grass is patient.", 0x5b6f52, 22);
}

void DrawDead()
{
    var panelW = Math.Min(50, W - 4);
    var x0 = (W - panelW) / 2;
    var x1 = x0 + panelW - 1;
    var y0 = H / 2 - 6;
    var y1 = y0 + 11;
    cv.Fill(x0, y0, x1, y1, ' ', 0x101a10);
    cv.Frame(x0, y0, x1, y1, 0x6f8a63);
    cv.TextC("you went outside.", 0xd8e2c8, y0 + 2);
    cv.TextC($"vibes {score:000000}", 0xf6efe0, y0 + 4);
    cv.TextC($"survived {elapsed:0.0}s", 0x8aa17a, y0 + 5);
    cv.TextC(newRecord ? "new soft-life record" : $"best vibes {best:000000}", newRecord ? 0xe0c884 : 0x6f8a63, y0 + 7);
    if (fx.Time % 1.2 < 0.8) cv.TextC("SPACE again    ESC title    Q quit", 0xcfe0b8, y0 + 9);
}

sealed class Item
{
    public int Kind;
    public double X;
    public double Y;
    public double Vy;
    public double Phase;
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
