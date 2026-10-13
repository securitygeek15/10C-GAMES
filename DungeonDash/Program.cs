using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(80, 30, 52, 22);
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

var MW = Math.Min(W - 6, 70);
var MH = Math.Min(H - 6, 26);
var mapX = (W - MW) / 2;
var mapY = 2;

var best = Store.Best("dungeondash");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

var map = new char[MW, MH];
var seen = new bool[MW, MH];
var rooms = new List<(int X, int Y, int W, int H)>();
var mobs = new List<Mob>();
var gems = new List<(int X, int Y)>();
var hearts = new List<(int X, int Y)>();

var px = 1;
var py = 1;
var rx = 1.0;
var ry = 1.0;
var hp = 5;
var maxHp = 5;
var invuln = 0.0;
var moveCd = 0.0;
var floorNum = 1;
var gemsGot = 0;
var gemsTotal = 0;
var kills = 0;
var score = 0;
var exitX = 1;
var exitY = 1;
var newRecord = false;
var transT = -1.0;
var pendingFloor = 0;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title, 1 play, 2 pause, 3 dead
var prevState = -1;
var stateT = 0.0;
var quit = false;

bool Wall(int x, int y) => x < 0 || y < 0 || x >= MW || y >= MH || map[x, y] == '#';

void GenerateFloor()
{
    for (var x = 0; x < MW; x++)
        for (var y = 0; y < MH; y++)
        {
            map[x, y] = '#';
            seen[x, y] = false;
        }
    rooms.Clear();
    mobs.Clear();
    gems.Clear();
    hearts.Clear();
    gemsGot = 0;
    gemsTotal = Math.Min(8, 3 + floorNum / 2);

    var attempts = 0;
    while (rooms.Count < 9 && attempts++ < 240)
    {
        var rw = rng.Next(5, 12);
        var rh = rng.Next(4, 8);
        var rx0 = rng.Next(1, MW - rw - 1);
        var ry0 = rng.Next(1, MH - rh - 1);
        var overlaps = rooms.Any(r => rx0 <= r.X + r.W && rx0 + rw + 1 >= r.X && ry0 <= r.Y + r.H && ry0 + rh + 1 >= r.Y);
        if (overlaps) continue;
        if (rooms.Count > 0)
        {
            var prev = rooms[^1];
            var ax = prev.X + prev.W / 2;
            var ay = prev.Y + prev.H / 2;
            var bx = rx0 + rw / 2;
            var by = ry0 + rh / 2;
            if (rng.Next(2) == 0)
            {
                for (var x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++) { map[x, ay] = '.'; if (ay + 1 < MH) map[x, ay + 1] = '.'; }
                for (var y = Math.Min(ay, by); y <= Math.Max(ay, by); y++) { map[bx, y] = '.'; if (bx + 1 < MW) map[bx + 1, y] = '.'; }
            }
            else
            {
                for (var y = Math.Min(ay, by); y <= Math.Max(ay, by); y++) { map[bx, y] = '.'; if (bx + 1 < MW) map[bx + 1, y] = '.'; }
                for (var x = Math.Min(ax, bx); x <= Math.Max(ax, bx); x++) { map[x, ay] = '.'; if (ay + 1 < MH) map[x, ay + 1] = '.'; }
            }
        }
        for (var y = ry0; y < ry0 + rh; y++)
            for (var x = rx0; x < rx0 + rw; x++)
                map[x, y] = '.';
        rooms.Add((rx0, ry0, rw, rh));
    }

    var first = rooms[0];
    px = first.X + first.W / 2;
    py = first.Y + first.H / 2;
    rx = px;
    ry = py;
    var last = rooms[^1];
    exitX = last.X + last.W / 2;
    exitY = last.Y + last.H / 2;

    // gems in distinct rooms (not first)
    var pool = rooms.Skip(1).OrderBy(_ => rng.Next()).ToList();
    for (var i = 0; i < gemsTotal && i < pool.Count; i++)
    {
        var r = pool[i];
        gems.Add((r.X + rng.Next(r.W), r.Y + rng.Next(r.H)));
    }
    gemsTotal = gems.Count;

    if (rng.NextDouble() < 0.6 && pool.Count > 1)
    {
        var r = pool[^1];
        hearts.Add((r.X + rng.Next(r.W), r.Y + rng.Next(r.H)));
    }

    var mobCount = Math.Min(14, 3 + floorNum * 2);
    for (var i = 0; i < mobCount; i++)
    {
        var r = rooms[rng.Next(1, rooms.Count)];
        var mx = r.X + rng.Next(r.W);
        var my = r.Y + rng.Next(r.H);
        if ((mx == px && my == py) || gems.Contains((mx, my))) continue;
        var kind = rng.Next(3);
        if (floorNum >= 4 && rng.Next(4) == 0) kind = 2;
        mobs.Add(new Mob { X = mx, Y = my, RX = mx, RY = my, Kind = kind, Hp = kind == 0 ? 1 : kind == 1 ? 2 : 3 });
    }
}

void StartGame()
{
    floorNum = 1;
    hp = maxHp;
    score = 0;
    kills = 0;
    newRecord = false;
    invuln = 1.0;
    transT = -1;
    GenerateFloor();
    fadeSeen();
}

void fadeSeen()
{
    for (var x = 0; x < MW; x++)
        for (var y = 0; y < MH; y++)
            if (Math.Abs(x - px) <= 9 && Math.Abs(y - py) <= 9) seen[x, y] = true;
}

void Die()
{
    state = 3;
    stateT = 0;
    shake = 12;
    flash = 0.8;
    flashCol = 0xff3b5c;
    fx.Burst(mapX + rx, mapY + ry, 60, 0xff5c7a, 0xffffff, 26, 1.2, 0, 2);
    fx.Ring(mapX + rx, mapY + ry, 28, 36, 0.7, 0xff5c7a, 0xffffff, '·');
    if (score > best)
    {
        best = score;
        Store.Save("dungeondash", score);
        newRecord = true;
    }
}

bool Los(int x0, int y0, int x1, int y1)
{
    var dx = Math.Abs(x1 - x0);
    var dy = Math.Abs(y1 - y0);
    var sx = x0 < x1 ? 1 : -1;
    var sy = y0 < y1 ? 1 : -1;
    var err = dx - dy;
    var x = x0;
    var y = y0;
    while (true)
    {
        if (x == x1 && y == y1) return true;
        if (!(x == x0 && y == y0) && map[x, y] == '#') return false;
        var e2 = 2 * err;
        if (e2 > -dy) { err -= dy; x += sx; }
        if (e2 < dx) { err += dx; y += sy; }
        if (x < 0 || y < 0 || x >= MW || y >= MH) return false;
    }
}

void TryMove(int dx, int dy)
{
    if (transT >= 0 || moveCd > 0) return;
    moveCd = 0.085;
    var nx = px + dx;
    var ny = py + dy;
    if (Wall(nx, ny)) { shake = Math.Max(shake, 1.5); return; }

    var mob = mobs.FirstOrDefault(m => m.X == nx && m.Y == ny);
    if (mob != null)
    {
        mob.Hp -= 1;
        mob.FlashT = 0.18;
        shake = Math.Max(shake, 2.5);
        fx.Burst(mapX + nx + 0.5, mapY + ny + 0.5, 8, 0xffe08a, 0xff5c7a, 12, 0.4, 0, 4);
        if (mob.Hp <= 0)
        {
            kills++;
            score += 25 * (mob.Kind + 1);
            fx.Burst(mapX + nx + 0.5, mapY + ny + 0.5, 20, mob.Color(), 0xffffff, 16, 0.7, 4, 2.5);
            fx.Ring(mapX + nx + 0.5, mapY + ny + 0.5, 12, 20, 0.5, mob.Color(), 0xffffff, '·');
            pops.Add((mapX + nx - 1, mapY + ny, $"+{25 * (mob.Kind + 1)}", 0xffe08a, 0.9, 0.9));
            mobs.Remove(mob);
            if (hp < maxHp && rng.NextDouble() < 0.18) hearts.Add((nx, ny));
        }
        EndTurn();
        return;
    }

    px = nx;
    py = ny;

    var gi = gems.FindIndex(g => g.X == nx && g.Y == ny);
    if (gi >= 0)
    {
        gems.RemoveAt(gi);
        gemsGot++;
        score += 30 * floorNum;
        fx.Burst(mapX + nx + 0.5, mapY + ny + 0.5, 22, 0x66ddff, 0xffffff, 16, 0.7, 0, 3);
        fx.Ring(mapX + nx + 0.5, mapY + ny + 0.5, 14, 22, 0.5, 0x66ddff, 0xffffff, '·');
        pops.Add((mapX + nx - 1, mapY + ny - 1, $"+{30 * floorNum}", 0x66ddff, 1.0, 1.0));
        flash = 0.18;
        flashCol = 0x66ddff;
        if (gemsGot >= gemsTotal)
        {
            pops.Add((mapX + exitX - 3, mapY + exitY - 1, "EXIT OPEN!", 0x66ff9f, 1.6, 1.6));
            fx.Burst(mapX + exitX + 0.5, mapY + exitY + 0.5, 30, 0x66ff9f, 0xffffff, 18, 0.9, 0, 3);
        }
    }

    var hi = hearts.FindIndex(h => h.X == nx && h.Y == ny);
    if (hi >= 0)
    {
        hearts.RemoveAt(hi);
        hp = Math.Min(maxHp, hp + 1);
        pops.Add((mapX + nx - 1, mapY + ny, "+HP", 0xff5c7a, 1.0, 1.0));
        fx.Burst(mapX + nx + 0.5, mapY + ny + 0.5, 14, 0xff5c7a, 0xffffff, 12, 0.5, 0, 3);
    }

    if (nx == exitX && ny == exitY)
    {
        if (gemsGot >= gemsTotal)
        {
            score += 100 * floorNum;
            pendingFloor = floorNum + 1;
            transT = 0;
            return;
        }
        pops.Add((mapX + exitX - 4, mapY + exitY - 1, $"NEED {gemsTotal - gemsGot} MORE", 0xff9f1c, 1.2, 1.2));
    }

    EndTurn();
}

void EndTurn()
{
    if (invuln > 0) return;
    foreach (var m in mobs.ToArray())
    {
        var dx = px - m.X;
        var dy = py - m.Y;
        var dist = Math.Abs(dx) + Math.Abs(dy);
        int nx;
        int ny;
        if (dist <= 9 && Los(m.X, m.Y, px, py))
        {
            var chase = Math.Abs(dx) > Math.Abs(dy) ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
            if (Wall(m.X + chase.Item1, m.Y + chase.Item2) || mobs.Any(o => o != m && o.X == m.X + chase.Item1 && o.Y == m.Y + chase.Item2))
                chase = Math.Abs(dx) > Math.Abs(dy) ? (0, Math.Sign(dy)) : (Math.Sign(dx), 0);
            nx = m.X + chase.Item1;
            ny = m.Y + chase.Item2;
        }
        else if (rng.NextDouble() < 0.35)
        {
            var w = rng.Next(4);
            nx = m.X + (w == 0 ? 1 : w == 1 ? -1 : 0);
            ny = m.Y + (w == 2 ? 1 : w == 3 ? -1 : 0);
        }
        else continue;

        if (nx == px && ny == py)
        {
            if (invuln <= 0)
            {
                hp--;
                invuln = 0.7;
                shake = 8;
                flash = 0.55;
                flashCol = 0xff3b5c;
                fx.Burst(mapX + rx + 0.5, mapY + ry + 0.5, 18, 0xff5c7a, 0xffffff, 16, 0.6, 0, 3);
                pops.Add((mapX + px - 1, mapY + py - 1, "-1 HP", 0xff5c7a, 1.0, 1.0));
                if (hp <= 0) Die();
            }
            continue;
        }
        if (!Wall(nx, ny) && !mobs.Any(o => o != m && o.X == nx && o.Y == ny) && !(nx == exitX && ny == exitY))
        {
            m.X = nx;
            m.Y = ny;
        }
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
        if (transT < 0)
        {
            if (Input.Hit(ConsoleKey.UpArrow) || Input.Hit(ConsoleKey.W)) TryMove(0, -1);
            if (Input.Hit(ConsoleKey.DownArrow) || Input.Hit(ConsoleKey.S)) TryMove(0, 1);
            if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) TryMove(-1, 0);
            if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) TryMove(1, 0);
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
    hue = (hue + dt * 24) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 24);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.4);
    if (moveCd > 0) moveCd -= dt;
    if (invuln > 0) invuln -= dt;

    // ---------- update ----------
    if (state == 1)
    {
        // smooth interpolation
        rx += (px - rx) * Math.Min(1, dt * 16);
        ry += (py - ry) * Math.Min(1, dt * 16);
        if (Math.Abs(rx - px) < 0.02) rx = px;
        if (Math.Abs(ry - py) < 0.02) ry = py;
        foreach (var m in mobs)
        {
            m.RX += (m.X - m.RX) * Math.Min(1, dt * 14);
            m.RY += (m.Y - m.RY) * Math.Min(1, dt * 14);
            if (m.FlashT > 0) m.FlashT -= dt;
            m.T += dt;
        }

        // torch embers
        if (rng.NextDouble() < 0.4)
            fx.Spawn(mapX + rx + 0.5 + (rng.NextDouble() - 0.5), mapY + ry,
                (rng.NextDouble() - 0.5) * 2, -(2 + rng.NextDouble() * 4),
                0.5 + rng.NextDouble() * 0.5, 0xffc873, 0x7a2e00, '·', -1.5, 0.6);

        // floor transition
        if (transT >= 0)
        {
            transT += dt / 0.9;
            if (transT >= 0.5 && pendingFloor > 0)
            {
                floorNum = pendingFloor;
                pendingFloor = 0;
                hp = Math.Min(maxHp, hp + 1);
                GenerateFloor();
                fadeSeen();
            }
            if (transT >= 1) transT = -1;
        }

        // reveal map
        var sightR = 8.5;
        for (var x = Math.Max(0, px - 10); x < Math.Min(MW, px + 11); x++)
            for (var y = Math.Max(0, py - 10); y < Math.Min(MH, py + 11); y++)
            {
                var d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d <= sightR && Los(px, py, x, y)) seen[x, y] = true;
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
        DrawDungeon();
        fx.Draw(cv);
        DrawPops();
        DrawHud();
        if (state == 2) DrawPause();
        if (state == 3) DrawDead();
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
Console.WriteLine($"Thanks for playing DUNGEON DASH! Floor {floorNum}, score {score}, best {best}.");

// ---------- rendering ----------
void DrawDungeon()
{
    var t = fx.Time;
    var flick = 0.9 + 0.1 * Math.Sin(t * 13) + 0.06 * Math.Sin(t * 31);
    var sightR = 8.5;

    for (var y = 0; y < MH; y++)
        for (var x = 0; x < MW; x++)
        {
            if (!seen[x, y]) continue;
            var d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            var lit = d <= sightR && Los(px, py, x, y);
            var k = lit ? (1 - d / sightR * 0.72) * flick : 0.18;
            var wall = map[x, y] == '#';
            int col;
            char ch;
            if (wall)
            {
                var exposed = !Wall(x - 1, y) || !Wall(x + 1, y) || !Wall(x, y - 1) || !Wall(x, y + 1);
                col = Col.Scale(exposed ? 0x8a7452 : 0x5f4e37, 0.16 + 0.84 * k);
                ch = exposed ? '█' : '▓';
            }
            else
            {
                var basec = (x * 7 + y * 11) % 9 == 0 ? 0x3d4666 : 0x2c3350;
                col = Col.Scale(basec, 0.3 + 0.7 * k);
                ch = (x * 5 + y * 3) % 7 == 0 ? '·' : ' ';
            }
            cv.Set(mapX + x, mapY + y, ch, col);
        }

    // exit
    if (seen[exitX, exitY])
    {
        var d = Math.Sqrt((exitX - px) * (exitX - px) + (exitY - py) * (exitY - py));
        var lit = d <= sightR && Los(px, py, exitX, exitY);
        var open = gemsGot >= gemsTotal;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 6);
        var ec = open ? Col.Mix(0x66ff9f, 0xffffff, pulse) : Col.Mix(0x8a5c2e, 0xff5c3a, pulse * 0.5);
        if (!lit) ec = Col.Scale(ec, 0.35);
        cv.Set(mapX + exitX, mapY + exitY, open ? '▼' : '≡', ec);
        if (open) cv.Set(mapX + exitX, mapY + exitY - 1, '·', Col.Scale(0x66ff9f, 0.4 + pulse * 0.4));
    }

    // gems
    foreach (var g in gems)
    {
        var d = Math.Sqrt((g.X - px) * (g.X - px) + (g.Y - py) * (g.Y - py));
        if (d > sightR || !Los(px, py, g.X, g.Y)) continue;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 7 + g.X + g.Y);
        var k2 = (1 - d / sightR * 0.6) * flick;
        cv.Set(mapX + g.X, mapY + g.Y, '♦', Col.Mix(Col.Scale(0x66ddff, k2), 0xffffff, pulse));
    }

    // hearts
    foreach (var h in hearts)
    {
        var d = Math.Sqrt((h.X - px) * (h.X - px) + (h.Y - py) * (h.Y - py));
        if (d > sightR || !Los(px, py, h.X, h.Y)) continue;
        var pulse = 0.5 + 0.5 * Math.Sin(t * 6 + h.X);
        cv.Set(mapX + h.X, mapY + h.Y, '♥', Col.Mix(Col.Scale(0xff5c7a, 0.7), 0xffffff, pulse));
    }

    // mobs
    foreach (var m in mobs)
    {
        var d = Math.Sqrt((m.X - px) * (m.X - px) + (m.Y - py) * (m.Y - py));
        if (d > sightR + 1 || !Los(px, py, m.X, m.Y)) continue;
        var c = m.FlashT > 0 ? 0xffffff : m.Color();
        if (m.Kind == 2) c = Col.Mix(c, 0xffffff, 0.2 * Math.Sin(m.T * 6));
        cv.Set(mapX + (int)Math.Round(m.RX), mapY + (int)Math.Round(m.RY), m.Kind == 0 ? 'b' : m.Kind == 1 ? 's' : 'S', c);
    }

    // player
    if (state != 3 && !(invuln > 0 && Math.Sin(t * 30) > 0.4))
    {
        var glow = 0.4 + 0.3 * Math.Sin(t * 9);
        cv.Set(mapX + (int)Math.Round(rx), mapY + (int)Math.Round(ry), '@', Col.Mix(0xffe08a, 0xffffff, glow));
        cv.Set(mapX + (int)Math.Round(rx), mapY + (int)Math.Round(ry) - 1, '·', Col.Scale(0xffc873, 0.35));
    }

    // frame
    for (var x = -1; x <= MW; x++)
    {
        cv.Set(mapX + x, mapY - 1, '─', 0x3a4468);
        cv.Set(mapX + x, mapY + MH, '─', 0x3a4468);
    }
    for (var y = -1; y <= MH; y++)
    {
        cv.Set(mapX - 1, mapY + y, '│', 0x3a4468);
        cv.Set(mapX + MW, mapY + y, '│', 0x3a4468);
    }
}

void DrawHud()
{
    cv.Text(2, 0, new string('♥', hp) + new string('♡', Math.Max(0, maxHp - hp)), 0xff5c7a);
    cv.TextC($"FLOOR {floorNum}", Col.Hsl(hue, 0.8, 0.62), 0);
    var sc = $"SCORE {score:D6}";
    cv.Text(W - 2 - sc.Length, 0, sc, 0xf2f5ff);

    var g = $"GEMS {gemsGot}/{gemsTotal}";
    cv.Text(2, 1, g, gemsGot >= gemsTotal ? 0x66ff9f : 0x66ddff);
    var b = $"BEST {best:D6}";
    cv.Text(W - 2 - b.Length, 1, b, 0x8fa3c8);

    cv.Text(2, H - 2, "WASD/ARROWS move & attack   P pause   Q quit", 0x607096);
    if (gemsGot >= gemsTotal && state == 1)
        cv.TextC("ALL GEMS - REACH THE EXIT ▼", Col.Scale(0x66ff9f, 0.7 + 0.3 * Math.Sin(fx.Time * 6)), H - 3);
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
    var title = "DUNGEON DASH";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 7, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 9, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("T H E   D E S C E N T", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.72), 11);
    cv.TextC("Procedural floors - torchlight - greedy monsters", 0x6f7fa8, 14);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 17);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, 19);
    cv.TextC("Grab every gem, then reach the exit. Bump to attack.", 0x6f7fa8, 22);
}

void DrawDead()
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "YOU DIED", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"FLOOR {floorNum}   KILLS {kills}", 0x9fb0d8, y0 + 6);
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
    public int X;
    public int Y;
    public double RX;
    public double RY;
    public int Hp;
    public int Kind;    // 0 bat, 1 slime, 2 skeleton
    public double FlashT;
    public double T;
    public int Color() => Kind == 0 ? 0xb14aff : Kind == 1 ? 0x2eff8a : 0xff5c7a;
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
