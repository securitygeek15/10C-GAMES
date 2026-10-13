using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(96, 30, 64, 24);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 64x24 and re-run.");
    return;
}

var W = size.W;
var H = size.H;
var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();

var best = Store.Best("starfighter");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

// starfield: 3 parallax layers
var stars = new (double X, double Y, int Layer, int Color)[90];
for (var i = 0; i < stars.Length; i++)
    stars[i] = (rng.Next(W), rng.Next(1, H - 3), i % 3,
        i % 3 == 0 ? 0x3a4a7a : i % 3 == 1 ? 0x9ab4e8 : 0xffffff);

// drifting nebula blobs
var blobs = new (double X, double Y, double R, int Hue)[10];
for (var i = 0; i < blobs.Length; i++)
    blobs[i] = (rng.Next(W), rng.Next(2, H - 4), 3 + rng.NextDouble() * 5, rng.Next(360));

var shots = new List<(double X, double Y, double VX, double VY, int Dmg)>();
var eshots = new List<(double X, double Y, double VX, double VY)>();
var enemies = new List<Enemy>();
var pups = new List<(double X, double Y, int Kind)>();

var px = W / 2.0;
var py = H - 5.0;
var lives = 3;
var bombs = 3;
var wlevel = 1;
var invuln = 0.0;
var fireT = 0.0;

var score = 0;
var wave = 0;
var waveGapT = 0.0;
var bannerT = 0.0;
var bannerText = "";
var kills = 0;
var newRecord = false;

var hue = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 over
var prevState = -1;
var stateT = 0.0;
var quit = false;

void StartGame()
{
    shots.Clear();
    eshots.Clear();
    enemies.Clear();
    pups.Clear();
    px = W / 2.0;
    py = H - 5.0;
    lives = 3;
    bombs = 3;
    wlevel = 1;
    invuln = 1.5;
    fireT = 0;
    score = 0;
    wave = 0;
    kills = 0;
    newRecord = false;
    bannerT = 0;
    waveGapT = 0.4;
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 12;
    flash = 0.8;
    flashCol = 0xff3b5c;
    fx.Burst(px, py, 70, 0xffd700, 0xff3b5c, 30, 1.3, 0, 2);
    fx.Ring(px, py, 30, 40, 0.8, 0xffffff, 0xff9f1c, '█');
    if (score > best)
    {
        best = score;
        Store.Save("starfighter", score);
        newRecord = true;
    }
}

void SpawnWave(int n)
{
    bannerT = 1.7;
    bannerText = n % 5 == 0 ? $"WAVE {n}  //  ELITE" : $"WAVE {n}";
    if (n % 5 == 0)
    {
        enemies.Add(new Enemy { X = W / 2.0, Y = -3, BX = W / 2.0, Type = 3, Hp = 30, HoldY = 5, Dir = 1 });
        for (var i = 0; i < 5; i++)
            enemies.Add(new Enemy { X = W / 2 + (i - 2) * 6, Y = -2 - Math.Abs(i - 2), BX = W / 2 + (i - 2) * 6, Type = 0, Hp = 1 });
        return;
    }
    var cols = 5 + n % 4;
    var rows = Math.Min(4, 1 + n / 2);
    var mL = 4;
    var step = cols > 1 ? (W - 2.0 * mL) / (cols - 1) : 0;
    for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            var x = mL + c * step;
            var type = r == 0 && n >= 3 ? 2 : (c + r) % 2 == 0 && n >= 2 ? 1 : 0;
            var hp = type == 2 ? 3 : type == 1 ? 2 : 1;
            enemies.Add(new Enemy { X = x, Y = -2 - r * 2.0, BX = x, Type = type, Hp = hp, HoldY = 4 + r * 2, Dir = c % 2 == 0 ? 1 : -1 });
        }
}

void Fire()
{
    var speed = -72;
    if (wlevel == 1) shots.Add((px, py - 2, 0, speed, 1));
    else if (wlevel == 2)
    {
        shots.Add((px - 1.5, py - 1, 0, speed, 1));
        shots.Add((px + 1.5, py - 1, 0, speed, 1));
    }
    else if (wlevel == 3)
    {
        shots.Add((px - 2, py - 1, -4, speed, 1));
        shots.Add((px + 2, py - 1, 4, speed, 1));
        shots.Add((px, py - 2, 0, speed, 1));
    }
    else
    {
        shots.Add((px - 2.5, py - 1, -10, speed, 1));
        shots.Add((px - 1, py - 2, -3, speed, 1));
        shots.Add((px + 1, py - 2, 3, speed, 1));
        shots.Add((px + 2.5, py - 1, 10, speed, 1));
    }
    fireT = 0.17 - wlevel * 0.015;
    fx.Spawn(px, py - 2, 0, -6, 0.14, 0xffffff, 0x66e0ff, '█', 0, 6);
}

void UseBomb()
{
    if (bombs <= 0 || state != 1) return;
    bombs--;
    flash = 0.95;
    flashCol = 0xffffff;
    shake = 13;
    eshots.Clear();
    fx.Ring(px, py, 44, 70, 0.75, 0xffffff, 0x66ddff, '█');
    fx.Burst(px, py, 60, 0x66ddff, 0xffffff, 40, 1.0, 0, 1.5);
    for (var i = enemies.Count - 1; i >= 0; i--)
    {
        enemies[i].Hp -= 8;
        if (enemies[i].Hp <= 0) KillEnemy(i);
    }
}

void KillEnemy(int i)
{
    var e = enemies[i];
    var colors = new[] { 0xff5c7a, 0xffb84d, 0xb14aff, 0xffd700 };
    var values = new[] { 25, 40, 60, 600 };
    var gain = values[e.Type] * (1 + wave / 5);
    score += gain;
    kills++;
    var col = colors[e.Type];
    fx.Burst(e.X, e.Y, e.Type == 3 ? 60 : 18, col, 0xffffff, e.Type == 3 ? 34 : 18, 0.8, 0, 2.5);
    fx.Ring(e.X, e.Y, e.Type == 3 ? 34 : 14, e.Type == 3 ? 44 : 24, 0.5, col, 0xffffff, '·');
    shake = Math.Max(shake, e.Type == 3 ? 9 : 2.5);
    pops.Add((e.X - 2, e.Y, $"+{gain}", 0xffe08a, 0.9, 0.9));
    var roll = rng.NextDouble();
    if (roll < 0.10) pups.Add((e.X, e.Y, 0));
    else if (roll < 0.16) pups.Add((e.X, e.Y, 1));
    else if (roll < 0.185 && lives < 5) pups.Add((e.X, e.Y, 2));
    enemies.RemoveAt(i);
}

void PlayerHit()
{
    if (invuln > 0) return;
    lives--;
    invuln = 2.5;
    shake = 11;
    flash = 0.65;
    flashCol = 0xff3b5c;
    fx.Burst(px, py, 40, 0xff5c7a, 0xffffff, 26, 1.0, 0, 2);
    fx.Ring(px, py, 24, 34, 0.6, 0xff5c7a, 0xffffff, '·');
    eshots.RemoveAll(s => Math.Abs(s.X - px) < 8 && Math.Abs(s.Y - py) < 6);
    wlevel = Math.Max(1, wlevel - 1);
    if (lives <= 0) Die();
    else pops.Add((px - 3, py - 3, "HIT!", 0xff5c7a, 1.0, 1.0));
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
        if (Input.Hit(ConsoleKey.Spacebar)) UseBomb();
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
    hue = (hue + dt * 22) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.2);

    // starfield always drifts
    for (var i = 0; i < stars.Length; i++)
    {
        var s = stars[i];
        s.Y += (4 + s.Layer * 6) * dt;
        if (s.Y >= H - 2)
        {
            s.Y = 1;
            s.X = rng.Next(W);
        }
        stars[i] = s;
    }
    for (var i = 0; i < blobs.Length; i++)
    {
        var b = blobs[i];
        b.Y += 1.5 * dt;
        if (b.Y - b.R >= H - 3) { b.Y = -b.R; b.X = rng.Next(W); }
        blobs[i] = b;
    }

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.4)
        fx.Spawn(rng.Next(W), -1, 0, 6 + rng.NextDouble() * 10, (H + 2.0) / (6 + rng.NextDouble() * 10),
            Col.Hsl(rng.Next(180, 320), 0.9, 0.6), 0xffffff, '·', 0, 0);

    if (state == 1)
    {
        // player movement
        var mx = 0;
        var my = 0;
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) mx -= 1;
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) mx += 1;
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) my -= 1;
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) my += 1;
        px = Math.Clamp(px + mx * 34 * dt, 2, W - 3);
        py = Math.Clamp(py + my * 26 * dt, H - 9, H - 5);

        // engine exhaust
        if (rng.NextDouble() < 0.8)
            fx.Spawn(px + (rng.NextDouble() - 0.5) * 2, py + 2, (rng.NextDouble() - 0.5) * 3, 6 + rng.NextDouble() * 6,
                0.3 + rng.NextDouble() * 0.2, 0x66ddff, 0xff9f1c, '▄', 4, 3);

        if (invuln > 0) invuln -= dt;
        fireT -= dt;
        if (fireT <= 0) Fire();

        // shots
        for (var i = shots.Count - 1; i >= 0; i--)
        {
            var s = shots[i];
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            shots[i] = s;
            if (s.Y < 0 || s.X < 0 || s.X >= W) shots.RemoveAt(i);
        }
        for (var i = eshots.Count - 1; i >= 0; i--)
        {
            var s = eshots[i];
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            eshots[i] = s;
            if (s.Y > H - 2 || s.Y < 0 || s.X < 0 || s.X >= W) eshots.RemoveAt(i);
        }

        // enemies
        for (var i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            e.T += dt;
            var sp = 1 + wave * 0.04;
            if (e.Type == 3)
            {
                if (e.Y < e.HoldY) e.Y += 9 * dt * sp;
                else
                {
                    e.X += e.Dir * 15 * dt;
                    if (e.X < 7 || e.X > W - 8) e.Dir = -e.Dir;
                }
                e.FireT -= dt;
                if (e.FireT <= 0 && e.Y >= e.HoldY - 1)
                {
                    e.FireT = Math.Max(0.5, 1.2 - wave * 0.04);
                    for (var k = -1; k <= 1; k++)
                    {
                        var a = Math.Atan2(py - e.Y, px - e.X) + k * 0.32;
                        eshots.Add((e.X, e.Y + 1, Math.Cos(a) * 22, Math.Sin(a) * 22));
                    }
                }
            }
            else if (e.Type == 2)
            {
                if (e.Y < e.HoldY) e.Y += 11 * dt * sp;
                else
                {
                    e.X = e.BX + Math.Sin(e.T * 1.1 + e.BX) * 5;
                    e.FireT -= dt;
                    if (e.FireT <= 0)
                    {
                        e.FireT = Math.Max(0.7, 1.7 - wave * 0.08);
                        var a = Math.Atan2(py - e.Y, px - e.X);
                        eshots.Add((e.X, e.Y + 1, Math.Cos(a) * 24, Math.Sin(a) * 24));
                    }
                }
            }
            else
            {
                e.Y += (e.Type == 1 ? 9 : 12) * dt * sp;
                e.X = e.BX + Math.Sin(e.T * (e.Type == 1 ? 3 : 1.6) + e.BX) * (e.Type == 1 ? 5 : 1.3);
            }

            if (e.Y > H - 3) { enemies.RemoveAt(i); continue; }

            // body vs player
            if (invuln <= 0 && Math.Abs(e.X - px) < 2.2 && Math.Abs(e.Y - py) < 1.8)
            {
                KillEnemy(i);
                PlayerHit();
                continue;
            }
        }

        // shot vs enemy
        for (var si = shots.Count - 1; si >= 0; si--)
        {
            var s = shots[si];
            var hit = false;
            for (var ei = enemies.Count - 1; ei >= 0; ei--)
            {
                var e = enemies[ei];
                var w = e.Type == 3 ? 5 : e.Type == 2 ? 3 : 2;
                if (Math.Abs(s.X - e.X) < w && Math.Abs(s.Y - e.Y) < 1.6)
                {
                    e.Hp -= s.Dmg;
                    fx.Burst(s.X, s.Y, 4, 0xffffff, 0x66ddff, 10, 0.3, 0, 4);
                    hit = true;
                    if (e.Hp <= 0) KillEnemy(ei);
                    break;
                }
            }
            if (hit) shots.RemoveAt(si);
        }

        // enemy shot vs player
        if (invuln <= 0)
            for (var i = eshots.Count - 1; i >= 0; i--)
            {
                var s = eshots[i];
                if (Math.Abs(s.X - px) < 1.4 && Math.Abs(s.Y - py) < 1.5)
                {
                    eshots.RemoveAt(i);
                    PlayerHit();
                    break;
                }
            }

        // powerups
        for (var i = pups.Count - 1; i >= 0; i--)
        {
            var p = pups[i];
            p.Y += 7 * dt;
            pups[i] = p;
            if (p.Y > H - 3) { pups.RemoveAt(i); continue; }
            if (Math.Abs(p.X - px) < 2.4 && Math.Abs(p.Y - py) < 2.2)
            {
                if (p.Kind == 0)
                {
                    if (wlevel < 4) { wlevel++; pops.Add((px - 3, py - 3, "POWER UP", 0x66ff9f, 1.1, 1.1)); }
                    else { score += 200; pops.Add((px - 3, py - 3, "+200", 0xffe08a, 1.0, 1.0)); }
                }
                else if (p.Kind == 1) { bombs = Math.Min(5, bombs + 1); pops.Add((px - 3, py - 3, "+BOMB", 0x66ddff, 1.1, 1.1)); }
                else { lives = Math.Min(5, lives + 1); pops.Add((px - 3, py - 3, "+LIFE", 0xff5c7a, 1.1, 1.1)); }
                fx.Burst(p.X, p.Y, 14, p.Kind == 0 ? 0x66ff9f : p.Kind == 1 ? 0x66ddff : 0xff5c7a, 0xffffff, 14, 0.5, 0, 3);
                pups.RemoveAt(i);
            }
        }

        // wave flow
        if (enemies.Count == 0 && waveGapT <= 0 && bannerT <= 0)
        {
            if (wave > 0)
            {
                var bonus = 50 * wave;
                score += bonus;
                pops.Add((W / 2 - 4, H / 2, $"WAVE CLEAR +{bonus}", 0x66ff9f, 1.4, 1.4));
                if (rng.NextDouble() < 0.5) pups.Add((W / 2.0, 2, rng.NextDouble() < 0.75 ? 0 : 1));
            }
            wave++;
            SpawnWave(wave);
            bannerT = 1.7;
            waveGapT = 1.0;
        }
        if (waveGapT > 0) waveGapT -= dt;
        if (bannerT > 0) bannerT -= dt;
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

    DrawSpace();

    if (state == 0)
    {
        fx.Draw(cv);
        DrawTitle();
        DrawTitleOver();
    }
    else
    {
        DrawPups();
        DrawShots();
        DrawEnemies();
        if (state != 3 && !(invuln > 0 && Math.Sin(fx.Time * 30) > 0)) DrawShip();
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
Console.WriteLine($"Thanks for playing STARFIGHTER! Score {score}, wave {wave}, best {best}.");

// ---------- rendering ----------
void DrawSpace()
{
    // nebula haze
    foreach (var b in blobs)
    {
        var r = (int)b.R;
        for (var dy = -r; dy <= r; dy++)
            for (var dx = -r * 2; dx <= r * 2; dx++)
            {
                var x = (int)b.X + dx;
                var y = (int)b.Y + dy;
                if (y < 1 || y >= H - 2 || x < 0 || x >= W) continue;
                var d = Math.Sqrt(dx * dx * 0.25 + dy * dy);
                if (d > b.R) continue;
                if ((x * 7 + y * 13) % 6 != 0) continue;
                var a = 1 - d / b.R;
                cv.Set(x, y, '·', Col.Hsl(b.Hue, 0.75, 0.12 + a * 0.16));
            }
    }

    foreach (var s in stars)
    {
        var c = s.Layer == 2 && (int)s.Y % 7 == 0 ? '*' : s.Layer == 1 ? '·' : '.';
        cv.Set((int)s.X, (int)s.Y, c, s.Color);
    }
}

void DrawSprite(double x, double y, string[] rows, int color, double topHighlight = 0)
{
    var cx = (int)Math.Round(x);
    var cy = (int)Math.Round(y);
    for (var r = 0; r < rows.Length; r++)
    {
        var row = rows[r];
        var left = cx - row.Length / 2;
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] == ' ') continue;
            var col = r == 0 && topHighlight > 0 ? Col.Mix(color, 0xffffff, topHighlight) : color;
            cv.Set(left + i, cy + r, row[i], col);
        }
    }
}

void DrawShip()
{
    var hull = Col.Mix(0x66ddff, 0xffffff, 0.3 + 0.15 * Math.Sin(fx.Time * 8));
    DrawSprite(px, py, new[] { "  ▲  ", " ═█═ " }, hull, 0.4);
    cv.Set((int)Math.Round(px) - 2, (int)Math.Round(py) + 1, '·', 0xff9f1c);
    cv.Set((int)Math.Round(px) + 2, (int)Math.Round(py) + 1, '·', 0xff9f1c);
}

void DrawEnemies()
{
    foreach (var e in enemies)
    {
        if (e.Type == 0) DrawSprite(e.X, e.Y, new[] { "███", " ▼ " }, Col.Mix(0xff5c7a, 0xff9f1c, 0.5 + 0.4 * Math.Sin(e.T * 6)));
        else if (e.Type == 1) DrawSprite(e.X, e.Y, new[] { "◄██►", " ██ " }, Col.Mix(0xffb84d, 0xffe08a, 0.5 + 0.4 * Math.Sin(e.T * 5)));
        else if (e.Type == 2) DrawSprite(e.X, e.Y, new[] { "█████", " ◉◉ " }, Col.Mix(0xb14aff, 0xff7bd5, 0.5 + 0.4 * Math.Sin(e.T * 4)));
        else
        {
            var hurt = 1.0 - (double)e.Hp / 30;
            DrawSprite(e.X, e.Y, new[] { "█████████", " ▐█████▌ ", "  █████  " },
                Col.Mix(Col.Mix(0xffd700, 0xff3b5c, hurt), 0xffffff, 0.2 + 0.2 * Math.Sin(e.T * 8)));
            cv.Text((int)e.X - 6, (int)e.Y - 1, $"ELITE {e.Hp}", 0xffe08a);
        }
    }
}

void DrawShots()
{
    foreach (var s in shots)
    {
        cv.Set((int)Math.Round(s.X), (int)Math.Round(s.Y), '█', 0xffffff);
        cv.Set((int)Math.Round(s.X), (int)Math.Round(s.Y) + 1, '░', 0x66ddff);
    }
    foreach (var s in eshots)
    {
        cv.Set((int)Math.Round(s.X), (int)Math.Round(s.Y), '●', 0xff5c7a);
        cv.Set((int)Math.Round(s.X), (int)Math.Round(s.Y) - 1, '·', 0xffb84d);
    }
}

void DrawPups()
{
    foreach (var p in pups)
    {
        var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 8 + p.X);
        var c = p.Kind == 0 ? Col.Mix(0x66ff9f, 0xffffff, pulse) : p.Kind == 1 ? Col.Mix(0x66ddff, 0xffffff, pulse) : Col.Mix(0xff5c7a, 0xffffff, pulse);
        var ch = p.Kind == 0 ? '▲' : p.Kind == 1 ? '✦' : '♥';
        cv.Set((int)Math.Round(p.X), (int)Math.Round(p.Y), ch, c);
    }
}

void DrawHud()
{
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    cv.TextC($"WAVE {wave}", Col.Hsl(hue, 0.8, 0.62), 0);

    var lifeStr = new string('♥', lives);
    cv.Text(2, H - 1, lifeStr, 0xff5c7a);
    cv.Text(4 + lives, H - 1, new string('✦', bombs), 0x66ddff);
    cv.Text(6 + lives + bombs, H - 1, $"WEAPON {new string('█', wlevel)}{new string('░', 4 - wlevel)}", 0x66ff9f);
    cv.Text(W - 34, H - 1, "ARROWS/WASD move  SPACE bomb  P pause  Q quit", 0x607096);
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
    var k = Math.Min(1.0, bannerT > 1.2 ? (1.7 - bannerT) * 4 : bannerT * 1.4);
    var col = Col.Scale(Col.Hsl(hue, 0.9, 0.62), k);
    cv.TextC(bannerText, col, H / 2 - 3);
}

void DrawPause()
{
    cv.TextC("PAUSED", 0xffffff, size.H / 2 - 1);
    cv.TextC("P / ESC resume    R restart    Q quit", 0xa8b8e0, size.H / 2 + 1);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "STARFIGHTER";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 8, title, Col.Hsl((t * 50) % 360, 0.9, 0.66), Col.Hsl((t * 50 + 160) % 360, 0.9, 0.66), 0);
    cv.TextGrad(x - 3, 10, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 200) % 360, 0.8, 0.5), 0);
    cv.TextC("N O V A   W A R", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.74), 12);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 16);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Auto-fire. SPACE = bomb. Elite ships every 5th wave.", 0x6f7fa8, 22);
}

void DrawOver()
{
    if (stateT < 0.6) return;
    var half = Math.Min(27, W / 2 - 1);
    var x0 = W / 2 - half;
    var x1 = W / 2 + half;
    var y0 = 7;
    var y1 = 18;
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "SHIP LOST", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"WAVE {wave}   KILLS {kills}", 0x9fb0d8, y0 + 6);
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

class Enemy
{
    public double X;
    public double Y;
    public double BX;
    public double T;
    public double FireT;
    public double HoldY;
    public int Type;    // 0 drone, 1 weaver, 2 shooter, 3 elite
    public int Hp;
    public int Dir = 1;
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
