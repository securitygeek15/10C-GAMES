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
var best = Store.Best("brainrot");
var bullets = new List<Bullet>();
var enemies = new List<Enemy>();
var cells = new List<Cell>();
var pops = new List<Pop>();
var state = 0;
var score = 0;
var lives = 3;
var wave = 1;
var weapon = 1;
var cellCount = 0;
var kills = 0;
var elapsed = 0.0;
var playerX = W / 2.0;
var fireCd = 0.0;
var spawnAcc = 1.0;
var invuln = 0.0;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var waveT = 0.0;
var waveText = "";
var newRecord = false;
var gone = 0;

void Start()
{
    bullets.Clear();
    enemies.Clear();
    cells.Clear();
    pops.Clear();
    score = 0;
    lives = 3;
    wave = 1;
    weapon = 1;
    cellCount = 0;
    kills = 0;
    elapsed = 0;
    playerX = W / 2.0;
    fireCd = 0;
    spawnAcc = 1.1;
    invuln = 1.0;
    shake = 0;
    flash = 0;
    waveT = 1.8;
    waveText = "wave 1: the feed awakens";
    newRecord = false;
}

void SpawnEnemy()
{
    var r = rng.Next(100);
    var type = r < 55 ? 0 : r < 72 ? 1 : r < 88 ? 2 : 3;
    var e = new Enemy
    {
        Type = type,
        X = rng.Next(3, Math.Max(4, W - 3)),
        Y = 3,
        Hp = type == 1 ? 2 : 1,
        Phase = rng.NextDouble() * Math.PI * 2,
        Speed = type switch
        {
            0 => 2.6 + wave * 0.34,
            1 => 1.9 + wave * 0.24,
            2 => 3.3 + wave * 0.38,
            _ => 3.8 + wave * 0.42
        }
    };
    enemies.Add(e);
}

void Fire()
{
    if (fireCd > 0) return;
    fireCd = weapon >= 3 ? 0.11 : 0.14;
    var px = (int)Math.Round(playerX);
    bullets.Add(new Bullet { X = px, Y = H - 6 });
    if (weapon >= 2)
    {
        bullets.Add(new Bullet { X = px - 1, Y = H - 6 });
        bullets.Add(new Bullet { X = px + 1, Y = H - 6 });
    }
    if (weapon >= 3)
    {
        bullets.Add(new Bullet { X = px - 2, Y = H - 6 });
        bullets.Add(new Bullet { X = px + 2, Y = H - 6 });
    }
    fx.Spawn(px, H - 7, 0, -12, 0.12, 0x9be564, 0x2a4a1a, '·', 0, 2);
}

void KillEnemy(Enemy e)
{
    kills++;
    var val = (e.Type switch { 0 => 50, 1 => 140, 2 => 90, _ => 70 }) * Math.Min(4, 1 + wave / 3);
    score += val;
    AddPop(e.X, e.Y, $"+{val}", e.Type == 2 ? 0xffd166 : 0xff9fc4);
    fx.Burst(e.X, e.Y, e.Type == 1 ? 26 : 16, 0xff4fa3, 0x3a1020, 16, 0.65, 3, 2.4, '✖');
    enemies.Remove(e);
    if (rng.NextDouble() < 0.18)
        cells.Add(new Cell { X = e.X, Y = e.Y });
}

void Hurt(Enemy? e)
{
    if (invuln > 0) return;
    lives--;
    invuln = 1.8;
    shake = 10;
    flash = 0.35;
    flashCol = 0xff2a4d;
    if (e is not null)
    {
        fx.Burst(e.X, e.Y, 22, 0xff2a4d, 0x2a0a14, 18, 0.8, 3, 2, '✖');
        enemies.Remove(e);
    }
    if (lives <= 0) Die();
}

void Die()
{
    state = 2;
    stateT = 0;
    gone = (int)elapsed;
    if (score > best)
    {
        best = score;
        Store.Save("brainrot", score);
        newRecord = true;
    }
    shake = 15;
    flash = 0.7;
    flashCol = 0x000000;
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x - s.Length / 2.0, y, s, c, 0.9, 0.9));
}

GameLoop.Run((dt, t) =>
{
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.2);
    if (invuln > 0) invuln = Math.Max(0, invuln - dt);
    if (waveT > 0) waveT = Math.Max(0, waveT - dt);
    stateT += dt;

    if (state == 0)
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.Enter)) { Start(); state = 1; }
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.2)
            fx.Spawn(rng.Next(W), 3, (rng.NextDouble() - 0.5) * 2, 2 + rng.NextDouble() * 3,
                6, 0x9be564, 0x1a2a10, rng.Next(3) == 0 ? '✖' : '·', 0, 0.05);
    }
    else if (state == 1)
    {
        if (Input.Quit) return false;
        elapsed += dt;
        fireCd -= dt;

        var move = 0.0;
        if (Input.HeldAny(ConsoleKey.A, ConsoleKey.LeftArrow)) move -= 1;
        if (Input.HeldAny(ConsoleKey.D, ConsoleKey.RightArrow)) move += 1;
        playerX = Math.Clamp(playerX + move * 30 * dt, 3, W - 4);
        if (Input.Held(ConsoleKey.Spacebar) || Input.Held(ConsoleKey.J)) Fire();

        var newWave = 1 + (int)(elapsed / 18);
        if (newWave != wave)
        {
            wave = newWave;
            waveT = 2.2;
            waveText = $"wave {wave}: more brainrot";
            flash = 0.15;
            flashCol = 0x9be564;
        }

        bullets.RemoveAll(b => b.Y < 2);
        foreach (var b in bullets) b.Y -= 48 * dt;

        for (var i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            e.Y += e.Speed * dt;
            if (e.Type == 2) e.X += Math.Sin(t * 4.5 + e.Phase) * 4.5 * dt;
            e.X = Math.Clamp(e.X, 2, W - 3);

            var near = Math.Abs(e.X - playerX) < 1.8 && Math.Abs(e.Y - (H - 5)) < 1.6;
            if (near) { Hurt(e); continue; }
            if (e.Y > H - 2)
            {
                enemies.RemoveAt(i);
                Hurt(null);
                AddPop(e.X, H - 3, "brainrot spread", 0xff5c7a);
            }
        }

        for (var bi = bullets.Count - 1; bi >= 0; bi--)
        {
            var b = bullets[bi];
            var hit = false;
            for (var ei = enemies.Count - 1; ei >= 0; ei--)
            {
                var e = enemies[ei];
                if (Math.Abs(b.X - e.X) > 1.2 || Math.Abs(b.Y - e.Y) > 1.2) continue;
                b.Y = -10;
                hit = true;
                e.Hp--;
                if (e.Hp <= 0) KillEnemy(e);
                else fx.Burst(e.X, e.Y, 5, 0xffd166, 0x4a3a1a, 8, 0.3, 0, 2, '·');
                break;
            }
            if (hit) bullets.RemoveAt(bi);
        }
        bullets.RemoveAll(b => b.Y < 2);

        for (var i = cells.Count - 1; i >= 0; i--)
        {
            var c = cells[i];
            c.Y += 3.5 * dt;
            if (Math.Abs(c.X - playerX) < 2 && Math.Abs(c.Y - (H - 5)) < 2)
            {
                cells.RemoveAt(i);
                cellCount++;
                score += 40;
                AddPop(c.X, c.Y, "+brain cell", 0x7ff5e3);
                if (cellCount % 6 == 0 && weapon < 3)
                {
                    weapon++;
                    waveT = 1.6;
                    waveText = weapon == 2 ? "double blaster unlocked" : "triple blaster unlocked";
                    flash = 0.2;
                    flashCol = 0x7ff5e3;
                }
            }
            else if (c.Y > H - 2) cells.RemoveAt(i);
        }

        spawnAcc += dt;
        var interval = Math.Max(0.32, 1.05 - wave * 0.07);
        while (spawnAcc >= interval)
        {
            spawnAcc -= interval;
            SpawnEnemy();
        }

        for (var i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            p.Life -= dt;
            if (p.Life <= 0) { pops.RemoveAt(i); continue; }
            p.Y -= dt * 6;
        }
    }
    else
    {
        if (Input.Hit(ConsoleKey.Spacebar) || Input.Hit(ConsoleKey.R)) { Start(); state = 1; }
        if (Input.Hit(ConsoleKey.Escape)) { state = 0; stateT = 0; }
        if (Input.Quit) return false;
    }

    fx.Update(dt);

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
        DrawField();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (state == 2) DrawDead();
    }

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.55, flash);
    }
    else if (state == 2)
    {
        cv.OverlayColor = 0x000000;
        cv.OverlayK = 0.35;
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"brainrot blaster closed. score {score}, best {best}. go read a book.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x050b07, 0x0a140c, u);
        for (var x = 0; x < W; x++)
        {
            var line = (x + y * 2 + (int)(fx.Time * 10)) % 23 == 0;
            cv.Set(x, y, line ? '·' : ' ', line ? 0x2a402a : c);
        }
    }
    for (var y = 2; y < H - 1; y += 5)
        for (var x = 0; x < W; x++) cv.Set(x, y, '─', 0x101c10);
}

void DrawField()
{
    foreach (var b in bullets)
    {
        var x = (int)Math.Round(b.X);
        var y = (int)Math.Round(b.Y);
        cv.Set(x, y, '│', 0xb6ff6a);
        cv.Set(x, y + 1, '·', 0x4a7a2a);
    }

    foreach (var c in cells)
    {
        var x = (int)Math.Round(c.X);
        var y = (int)Math.Round(c.Y);
        cv.Set(x, y, '✧', 0x7ff5e3);
        cv.Set(x - 1, y, '·', 0x2a6a5a);
    }

    foreach (var e in enemies)
    {
        var x = (int)Math.Round(e.X);
        var y = (int)Math.Round(e.Y);
        var (ch, c) = e.Type switch
        {
            0 => ('✖', 0xff4fa3),
            1 => ('☁', 0x9a8ab8),
            2 => ('$', 0xffd166),
            _ => ('!', 0xff5c7a)
        };
        cv.Set(x, y, ch, c);
        cv.Set(x - 1, y, '·', Col.Scale(c, 0.3));
        cv.Set(x + 1, y, '·', Col.Scale(c, 0.3));
    }

    var blink = invuln > 0 && (int)(fx.Time * 20) % 2 == 0;
    if (!blink)
    {
        var px = (int)Math.Round(playerX);
        cv.Set(px, H - 5, '▲', 0x9be564);
        cv.Set(px - 1, H - 5, '╱', 0x5a8a3a);
        cv.Set(px + 1, H - 5, '╲', 0x5a8a3a);
        cv.Set(px, H - 4, '╨', 0x3a5a2a);
    }

    if (waveT > 0 && state == 1)
    {
        var k = Math.Min(1.0, waveT / 0.6);
        cv.TextC(waveText, Col.Scale(0x9be564, k), H / 2 - 1);
    }
}

void DrawHud()
{
    cv.Text(2, 0, "brainrot blaster", 0xff4fa3);
    cv.TextC($"{score:000000}", 0xf4ffe8, 0);
    cv.TextRight($"{new string('♥', Math.Max(0, lives))}{new string('♡', Math.Max(0, 3 - lives))}", W - 3, 0,
        lives <= 1 ? 0xff5c7a : 0xff9fc4);
    cv.Text(2, 1, $"wave {wave}   blaster lv {weapon}   cells {cellCount}", 0x6a8a5a);
    cv.TextRight($"best {best:000000}", W - 3, 1, 0x3e5a38);

    var ticker = "  ✖ skibidi  ·  sigma  ·  rizz  ·  ohio  ·  gyatt  ·  fanum tax  ·  mewing  ·  looksmaxxing  ·  delulu  ·  brainrot  ";
    var offset = (int)(fx.Time * 8) % ticker.Length;
    var view = ticker.Substring(offset) + ticker.Substring(0, offset);
    cv.TextC(view.Substring(0, Math.Min(W - 2, view.Length)), 0x2f4a2a, H - 2);
    cv.TextC("a / d  move     hold space  shoot     q quit", 0x4a6a42, H - 1);
}

void DrawPops()
{
    foreach (var p in pops)
    {
        var k = Math.Min(1.0, p.Life / p.Max * 1.7);
        cv.Text((int)Math.Round(p.X), (int)Math.Round(p.Y), p.S, Col.Scale(p.C, k));
    }
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "b r a i n r o t   b l a s t e r";
    var x = (W - title.Length) / 2;
    var glitch = (int)(t * 6) % 7 == 0 ? 1 : 0;
    cv.TextGrad(x, 7, title, 0x9be564, 0xff4fa3, (t * 0.16) % 1);
    if (glitch == 1)
    {
        cv.TextGrad(x + 1, 8, title, 0xff4fa3, 0x9be564, (t * 0.16 + 0.3) % 1);
        cv.TextC("do not adjust your screen", 0xff5c7a, 9);
    }
    cv.TextC("shoot the brainrot before it shoots you", 0xd8ffe8, 11);
    cv.TextC("collect brain cells. upgrade the blaster. log off.", 0x6a8a6a, 12);
    if (best > 0) cv.TextC($"best {best:000000}", 0xffd166, 15);
    if (t % 1.15 < 0.75) cv.TextC("PRESS SPACE TO LOCK IN", 0xffffff, 18);
    cv.TextC("a / d  move      hold space  shoot      q  quit", 0x6a8a6a, 21);
    cv.TextC("your attention span is the final boss.", 0x3e5a38, 23);
}

void DrawDead()
{
    var panelW = Math.Min(58, W - 4);
    var x0 = (W - panelW) / 2;
    var x1 = x0 + panelW - 1;
    var y0 = H / 2 - 6;
    var y1 = y0 + 11;
    cv.Fill(x0, y0, x1, y1, ' ', 0x08110a);
    cv.Frame(x0, y0, x1, y1, 0x9be564);
    cv.TextC("brain fully rotted.", 0xff5c7a, y0 + 2);
    cv.TextC("you became the content.", 0x6a8a6a, y0 + 3);
    cv.TextC($"score {score:000000}    wave {wave}    cells {cellCount}", 0xf4ffe8, y0 + 5);
    cv.TextC($"survived {gone}s    best {best:000000}", newRecord ? 0xffd166 : 0x6a8a6a, y0 + 6);
    cv.TextC(newRecord ? "new personal best (concerning)" : "the feed always wins", 0x9be564, y0 + 8);
    if (fx.Time % 1.1 < 0.75) cv.TextC("SPACE again    ESC title    Q quit", 0xd8ffe8, y0 + 10);
}

sealed class Bullet
{
    public double X;
    public double Y;
}

sealed class Enemy
{
    public int Type;
    public double X;
    public double Y;
    public double Speed;
    public double Phase;
    public int Hp;
}

sealed class Cell
{
    public double X;
    public double Y;
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
