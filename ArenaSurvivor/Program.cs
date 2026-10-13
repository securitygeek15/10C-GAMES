using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(92, 30, 60, 22);
if (size.W == 0)
{
    Console.WriteLine("Terminal too small - resize to at least 60x22 and re-run.");
    return;
}

var W = size.W;
var H = size.H;
var cv = new Canvas(W, H);
var fx = new Fx();
var rng = new Random();

var best = Store.Best("arenasurvivor");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

var ax0 = 1;
var ay0 = 2;
var ax1 = W - 2;
var ay1 = H - 4;

var mobs = new List<Mob>();
var shots = new List<(double X, double Y, double VX, double VY, int Dmg)>();
var gems = new List<(double X, double Y, int Val)>();

var px = W / 2.0;
var py = H / 2.0;
var pvx = 0.0;
var pvy = 0.0;
var moveDir = (DX: 0.0, DY: 0.0);
var moveDirT = 0.0;
var maxSpeed = 24.0;
var hp = 10;
var maxHp = 10;
var invuln = 0.0;

var level = 1;
var xp = 0;
var xpNeed = 4;
var dmg = 1;
var fireInt = 0.38;
var fireT = 0.0;
var volley = 1;
var magnetR = 3.5;

var elapsed = 0.0;
var kills = 0;
var score = 0;
var spawnT = 0.0;
var newRecord = false;

var upgrades = new List<Upgrade>();
var pendingLevels = 0;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 dead, 4 levelup
var prevState = -1;
var stateT = 0.0;
var quit = false;

void RollUpgrades()
{
    var pool = new List<Upgrade>
    {
        new() { Name = "POWER", Desc = "+1 shot damage", Key = ConsoleKey.D1, Apply = () => dmg++ },
        new() { Name = "HASTE", Desc = "18% faster fire", Key = ConsoleKey.D2, Apply = () => fireInt = Math.Max(0.08, fireInt * 0.82) },
        new() { Name = "SWIFT", Desc = "+4 move speed", Key = ConsoleKey.D3, Apply = () => maxSpeed += 4 },
        new() { Name = "VITALITY", Desc = "+3 max HP, heal 3", Key = ConsoleKey.D4, Apply = () => { maxHp += 3; hp = Math.Min(maxHp, hp + 3); } },
        new() { Name = "VOLLEY", Desc = "+1 projectile", Key = ConsoleKey.D5, Apply = () => volley = Math.Min(5, volley + 1) },
        new() { Name = "MAGNET", Desc = "+2 pickup range", Key = ConsoleKey.D6, Apply = () => magnetR += 2 },
    };
    upgrades = pool.OrderBy(_ => rng.Next()).Take(3).ToList();
}

void StartGame()
{
    mobs.Clear();
    shots.Clear();
    gems.Clear();
    px = W / 2.0;
    py = H / 2.0;
    pvx = pvy = 0;
    moveDirT = 0;
    maxSpeed = 24;
    hp = maxHp = 10;
    invuln = 1.5;
    level = 1;
    xp = 0;
    xpNeed = 4;
    dmg = 1;
    fireInt = 0.38;
    volley = 1;
    magnetR = 3.5;
    elapsed = 0;
    kills = 0;
    score = 0;
    spawnT = 1.5;
    newRecord = false;
    pendingLevels = 0;
    RollUpgrades();
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 12;
    flash = 0.8;
    flashCol = 0xff3b5c;
    fx.Burst(px, py, 60, 0x4ddfff, 0xffffff, 28, 1.2, 0, 2);
    fx.Ring(px, py, 28, 38, 0.7, 0xff5c7a, 0xffffff, '·');
    if (score > best)
    {
        best = score;
        Store.Save("arenasurvivor", score);
        newRecord = true;
    }
}

void SpawnMob()
{
    var edge = rng.Next(4);
    var x = edge == 0 ? ax0 + 0.5 : edge == 1 ? ax1 - 0.5 : ax0 + rng.NextDouble() * (ax1 - ax0);
    var y = edge == 2 ? ay0 + 0.5 : edge == 3 ? ay1 - 0.5 : ay0 + rng.NextDouble() * (ay1 - ay0);
    var r = rng.NextDouble();
    int kind;
    if (elapsed > 100 && r < 0.22) kind = 2;
    else if (elapsed > 45 && r < 0.45) kind = 1;
    else kind = 0;
    var tier = 1 + elapsed / 50.0;
    var hpv = kind == 2 ? (int)(1 * tier) : kind == 1 ? (int)(3 * tier) : (int)(1 * tier);
    mobs.Add(new Mob
    {
        X = x, Y = y, RX = x, RY = y, Kind = kind, Hp = Math.Max(1, hpv),
        Speed = kind == 2 ? 17 : kind == 1 ? 6.5 : 10 + Math.Min(6, elapsed / 40.0),
        T = rng.NextDouble() * 6
    });
}

void Fire()
{
    Mob? target = null;
    var bestD = double.MaxValue;
    foreach (var m in mobs)
    {
        var d = (m.X - px) * (m.X - px) + (m.Y - py) * (m.Y - py);
        if (d < bestD) { bestD = d; target = m; }
    }
    if (target == null || bestD > 30 * 30) return;
    var ang = Math.Atan2(target.Y - py, target.X - px);
    for (var i = 0; i < volley; i++)
    {
        var a = ang + (i - (volley - 1) / 2.0) * 0.16;
        shots.Add((px, py, Math.Cos(a) * 62, Math.Sin(a) * 62, dmg));
    }
    fireT = fireInt;
    fx.Spawn(px + Math.Cos(ang) * 1.5, py + Math.Sin(ang) * 1.5, Math.Cos(ang) * 8, Math.Sin(ang) * 8,
        0.15, 0xffffff, 0x66ddff, '█', 0, 6);
}

void KillMob(int i)
{
    var m = mobs[i];
    kills++;
    var col = m.Kind == 0 ? 0xff5c7a : m.Kind == 1 ? 0xff9f1c : 0xff4df0;
    fx.Burst(m.X, m.Y, m.Kind == 1 ? 22 : 12, col, 0xffffff, 16, 0.6, 0, 3);
    fx.Ring(m.X, m.Y, 10, 18, 0.4, col, 0xffffff, '·');
    shake = Math.Max(shake, m.Kind == 1 ? 3 : 1.2);
    var drops = m.Kind == 1 ? 3 : 1;
    for (var d = 0; d < drops; d++)
        gems.Add((m.X + (rng.NextDouble() - 0.5), m.Y + (rng.NextDouble() - 0.5), m.Kind == 1 ? 2 : 1));
    mobs.RemoveAt(i);
}

void GainXp(int v)
{
    xp += v;
    while (xp >= xpNeed)
    {
        xp -= xpNeed;
        level++;
        xpNeed = 4 + level * 2;
        pendingLevels++;
    }
    if (pendingLevels > 0 && state == 1)
    {
        state = 4;
        RollUpgrades();
        flash = 0.5;
        flashCol = 0xffd700;
        fx.Ring(px, py, 26, 30, 0.7, 0xffd700, 0xffffff, '·');
        pops.Add((px - 3, py - 3, $"LEVEL {level}!", 0xffd700, 1.4, 1.4));
    }
}

void TakeUpgrade(int idx)
{
    if (idx < 0 || idx >= upgrades.Count) return;
    upgrades[idx].Apply();
    pendingLevels--;
    fx.Burst(px, py, 26, 0x66ff9f, 0xffffff, 20, 0.7, 0, 3);
    if (pendingLevels > 0) RollUpgrades();
    else state = 1;
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
        if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) { moveDir = (0, -1); moveDirT = 0.3; }
        if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) { moveDir = (0, 1); moveDirT = 0.3; }
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) { moveDir = (-1, 0); moveDirT = 0.3; }
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) { moveDir = (1, 0); moveDirT = 0.3; }
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
    else if (state == 4)
    {
        if (Input.Hit(ConsoleKey.D1)) TakeUpgrade(0);
        if (Input.Hit(ConsoleKey.D2)) TakeUpgrade(1);
        if (Input.Hit(ConsoleKey.D3)) TakeUpgrade(2);
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
    if (invuln > 0) invuln -= dt;

    // ---------- update ----------
    if (state == 0 && rng.NextDouble() < 0.5)
        fx.Spawn(rng.Next(W), -1, 0, 6 + rng.NextDouble() * 10, (H + 2.0) / (6 + rng.NextDouble() * 10),
            Col.Hsl(rng.Next(300, 360), 0.9, 0.6), 0xffffff, '·', 0, 0);

    if (state == 1)
    {
        elapsed += dt;
        score = kills * 10 + (int)elapsed * 5 + (level - 1) * 100;

        // movement
        if (moveDirT > 0) moveDirT -= dt;
        var tgtX = moveDirT > 0 ? moveDir.DX * maxSpeed : 0;
        var tgtY = moveDirT > 0 ? moveDir.DY * maxSpeed : 0;
        var k = Math.Min(1, dt * 11);
        pvx += (tgtX - pvx) * k;
        pvy += (tgtY - pvy) * k;
        px = Math.Clamp(px + pvx * dt, ax0 + 1, ax1 - 1);
        py = Math.Clamp(py + pvy * dt, ay0 + 1, ay1 - 1);

        // feet dust
        if (Math.Abs(pvx) + Math.Abs(pvy) > 6 && rng.NextDouble() < 0.5)
            fx.Spawn(px, py + 1, (rng.NextDouble() - 0.5) * 3, (rng.NextDouble() - 0.5) * 2, 0.4, 0x3a4468, 0x1a2038, '·', 0, 3);

        // firing
        fireT -= dt;
        if (fireT <= 0) Fire();

        // spawn
        spawnT -= dt;
        if (spawnT <= 0)
        {
            SpawnMob();
            if (elapsed > 30 && rng.NextDouble() < 0.4) SpawnMob();
            spawnT = Math.Max(0.22, 1.1 - elapsed * 0.008);
        }

        // shots
        for (var i = shots.Count - 1; i >= 0; i--)
        {
            var s = shots[i];
            s.X += s.VX * dt;
            s.Y += s.VY * dt;
            shots[i] = s;
            if (s.X < ax0 || s.X > ax1 || s.Y < ay0 || s.Y > ay1) { shots.RemoveAt(i); continue; }
            for (var mi = mobs.Count - 1; mi >= 0; mi--)
            {
                var m = mobs[mi];
                if (Math.Abs(s.X - m.X) < 1.4 && Math.Abs(s.Y - m.Y) < 1.3)
                {
                    m.Hp -= s.Dmg;
                    m.FlashT = 0.12;
                    fx.Burst(s.X, s.Y, 4, 0xffffff, 0x66ddff, 8, 0.25, 0, 5);
                    if (m.Hp <= 0) KillMob(mi);
                    shots.RemoveAt(i);
                    break;
                }
            }
        }

        // mobs
        for (var i = mobs.Count - 1; i >= 0; i--)
        {
            var m = mobs[i];
            m.T += dt;
            if (m.FlashT > 0) m.FlashT -= dt;
            var dx = px - m.X;
            var dy = py - m.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 0.01)
            {
                var wob = m.Kind == 0 ? Math.Sin(m.T * 6) * 0.35 : 0;
                var ux = dx / d;
                var uy = dy / d;
                m.X += (ux - uy * wob) * m.Speed * dt;
                m.Y += (uy + ux * wob) * m.Speed * dt;
            }
            // separation
            for (var j = 0; j < mobs.Count; j++)
            {
                if (j == i) continue;
                var o = mobs[j];
                var sx2 = m.X - o.X;
                var sy2 = m.Y - o.Y;
                var sd = sx2 * sx2 + sy2 * sy2;
                if (sd < 1.2 && sd > 0.0001)
                {
                    var inv = 0.6 * dt / Math.Sqrt(sd);
                    m.X += sx2 * inv;
                    m.Y += sy2 * inv;
                }
            }
            m.RX += (m.X - m.RX) * Math.Min(1, dt * 14);
            m.RY += (m.Y - m.RY) * Math.Min(1, dt * 14);

            if (invuln <= 0 && Math.Abs(m.X - px) < 1.7 && Math.Abs(m.Y - py) < 1.5)
            {
                hp--;
                invuln = 0.8;
                shake = 8;
                flash = 0.5;
                flashCol = 0xff3b5c;
                fx.Burst(px, py, 18, 0xff5c7a, 0xffffff, 16, 0.6, 0, 3);
                pops.Add((px - 2, py - 2, "-HP", 0xff5c7a, 0.9, 0.9));
                if (hp <= 0) Die();
            }
        }

        // gems
        for (var i = gems.Count - 1; i >= 0; i--)
        {
            var g = gems[i];
            var dx = px - g.X;
            var dy = py - g.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < magnetR && d > 0.01)
            {
                g.X += dx / d * 30 * dt;
                g.Y += dy / d * 30 * dt;
            }
            gems[i] = g;
            if (d < 1.5)
            {
                gems.RemoveAt(i);
                fx.Burst(g.X, g.Y, 6, 0x66ff9f, 0xffffff, 8, 0.3, 0, 4);
                GainXp(g.Val);
                if (state != 1) break;
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
        DrawArena();
        foreach (var g in gems)
        {
            var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 8 + g.X);
            cv.Set((int)Math.Round(g.X), (int)Math.Round(g.Y), '♦', Col.Mix(0x66ff9f, 0xffffff, pulse));
        }
        foreach (var m in mobs)
        {
            var col = m.Kind == 0 ? 0xff5c7a : m.Kind == 1 ? 0xff9f1c : 0xff4df0;
            if (m.FlashT > 0) col = 0xffffff;
            else col = Col.Mix(col, 0xffffff, 0.15 + 0.15 * Math.Sin(m.T * 6));
            var ch = m.Kind == 0 ? 'x' : m.Kind == 1 ? 'B' : 's';
            cv.Set((int)Math.Round(m.RX), (int)Math.Round(m.RY), ch, col);
            if (m.Kind == 1) cv.Set((int)Math.Round(m.RX), (int)Math.Round(m.RY) - 1, '▓', Col.Scale(0xff9f1c, 0.7));
        }
        foreach (var s in shots)
        {
            cv.Set((int)Math.Round(s.X), (int)Math.Round(s.Y), '█', 0xffffff);
            cv.Set((int)Math.Round(s.X) - Math.Sign(s.VX), (int)Math.Round(s.Y) - Math.Sign(s.VY), '·', 0x66ddff);
        }
        if (state != 3 && !(invuln > 0 && Math.Sin(fx.Time * 30) > 0.4)) DrawPlayer();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (state == 2) DrawPause();
        if (state == 4) DrawUpgrades();
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
Console.WriteLine($"Thanks for playing ARENA SURVIVOR! {elapsed:0}s, level {level}, score {score}, best {best}.");

// ---------- rendering ----------
void DrawArena()
{
    var t = fx.Time;
    for (var y = ay0 + 1; y < ay1; y++)
        for (var x = ax0 + 1; x < ax1; x++)
            if ((x + y) % 5 == 0)
                cv.Set(x, y, '·', 0x191f36);

    for (var x = ax0; x <= ax1; x++)
    {
        cv.Set(x, ay0, '═', Col.Hsl(hue + x * 5, 0.8, 0.5));
        cv.Set(x, ay1, '═', Col.Hsl(hue + x * 5 + 180, 0.8, 0.5));
    }
    for (var y = ay0; y <= ay1; y++)
    {
        cv.Set(ax0, y, '║', Col.Hsl(hue + y * 6, 0.8, 0.5));
        cv.Set(ax1, y, '║', Col.Hsl(hue + y * 6 + 90, 0.8, 0.5));
    }
    cv.Set(ax0, ay0, '╔', Col.Hsl(hue, 0.9, 0.65));
    cv.Set(ax1, ay0, '╗', Col.Hsl(hue + 120, 0.9, 0.65));
    cv.Set(ax0, ay1, '╚', Col.Hsl(hue + 240, 0.9, 0.65));
    cv.Set(ax1, ay1, '╝', Col.Hsl(hue + 300, 0.9, 0.65));
}

void DrawPlayer()
{
    var t = fx.Time;
    var glow = 0.35 + 0.3 * Math.Sin(t * 10);
    var body = Col.Mix(0x4ddfff, 0xffffff, glow);
    cv.Set((int)Math.Round(px), (int)Math.Round(py), '@', body);
    cv.Set((int)Math.Round(px) - 1, (int)Math.Round(py), '·', Col.Scale(0x4ddfff, 0.6));
    cv.Set((int)Math.Round(px) + 1, (int)Math.Round(py), '·', Col.Scale(0x4ddfff, 0.6));
    cv.Set((int)Math.Round(px), (int)Math.Round(py) - 1, '·', Col.Scale(0x4ddfff, 0.6));
    cv.Set((int)Math.Round(px), (int)Math.Round(py) + 1, '·', Col.Scale(0x4ddfff, 0.6));
}

void DrawHud()
{
    var t = fx.Time;
    cv.Text(2, 0, $"HP {new string('█', (int)Math.Round(10.0 * hp / maxHp))}{new string('░', 10 - (int)Math.Round(10.0 * hp / maxHp))} {hp}/{maxHp}",
        hp <= 3 ? Col.Scale(0xff5c7a, 0.7 + 0.3 * Math.Sin(t * 10)) : 0xff5c7a);
    cv.TextC($"TIME {elapsed:00.0}s   LEVEL {level}", Col.Hsl(hue, 0.8, 0.62), 0);
    var sc = $"SCORE {score:D6}";
    cv.Text(W - 2 - sc.Length, 0, sc, 0xf2f5ff);

    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 1, bs, 0x8fa3c8);
    cv.Text(2, 1, $"KILLS {kills}", 0xffe08a);

    var xpK = (int)Math.Round(20.0 * xp / xpNeed);
    cv.Text(2, H - 2, $"LVL {level} [{new string('█', xpK)}{new string('░', 20 - xpK)}] {xp}/{xpNeed}", 0x66ff9f);
    cv.Text(W - 32, H - 1, "WASD/ARROWS move   P pause   Q quit", 0x607096);
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

void DrawUpgrades()
{
    var t = fx.Time;
    var pw = 26;
    var gap = 3;
    var total = upgrades.Count * pw + (upgrades.Count - 1) * gap;
    var x0 = (W - total) / 2;
    var y0 = H / 2 - 4;
    var y1 = H / 2 + 4;
    for (var u = 0; u < upgrades.Count; u++)
    {
        var ux = x0 + u * (pw + gap);
        for (var y = y0; y <= y1; y++)
            for (var x = ux; x < ux + pw; x++)
                cv.Set(x, y, '█', Col.Mix(0x140b24, 0x241540, 0.5 + 0.5 * Math.Sin(t * 4 + u)));
        var bc = Col.Hsl((t * 60 + u * 120) % 360, 0.9, 0.6);
        for (var x = ux; x < ux + pw; x++)
        {
            cv.Set(x, y0, '─', bc);
            cv.Set(x, y1, '─', bc);
        }
        for (var y = y0; y <= y1; y++)
        {
            cv.Set(ux, y, '│', bc);
            cv.Set(ux + pw - 1, y, '│', bc);
        }
        var up = upgrades[u];
        cv.Text(ux + 2, y0 + 1, $"[{u + 1}]", 0xffd166);
        cv.TextGrad(ux + 6, y0 + 1, up.Name, bc, Col.Mix(bc, 0xffffff, 0.6), 0);
        cv.Text(ux + 2, y0 + 3, up.Desc, 0xc9d4ff);
        cv.Text(ux + 2, y0 + 5, "press number", 0x607096);
    }
    cv.TextC("LEVEL UP!  CHOOSE AN UPGRADE", Col.Mix(0xffd700, 0xffffff, 0.5 + 0.5 * Math.Sin(t * 8)), y0 - 2);
}

void DrawTitle()
{
    var t = fx.Time;
    var title = "ARENA SURVIVOR";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("T H E   H O A R D", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("Auto-fire, XP levels, 6 upgrades, escalating hoard", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Move to dodge. Your weapon fires itself. Grab green XP.", 0x6f7fa8, 22);
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "OVERRUN", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"SURVIVED {elapsed:0.0}s   LEVEL {level}   KILLS {kills}", 0x9fb0d8, y0 + 6);
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

class Mob
{
    public double X;
    public double Y;
    public double RX;
    public double RY;
    public int Hp;
    public int Kind;    // 0 crawler, 1 brute, 2 sprinter
    public double Speed;
    public double T;
    public double FlashT;
}

class Upgrade
{
    public string Name = "";
    public string Desc = "";
    public ConsoleKey Key;
    public Action Apply = () => { };
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
