using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

SetupConsole();
var size = Fit(96, 30, 60, 22);
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

var best = Store.Best("terminalracer");
var pops = new List<(double X, double Y, string S, int C, double Life, double Max)>();

// road constants
var segLen = 100.0;
var roadWidth = 1200.0;
var cameraDepth = 1.19;              // ~100 deg FOV
var cameraHeight = 900.0;
var playerZ = cameraHeight * cameraDepth;
var drawDistance = 110;
var maxSpeed = segLen * 60;
var accel = maxSpeed / 5;

var segs = new List<Seg>();
var trackLength = 0.0;
var position = 0.0;
var playerX = 0.0;
var speed = 0.0;
var playerWorldY = 0.0;

var cars = new List<Car>();
var pads = new List<(double Z, double Off)>();
var lives = 3;
var invuln = 0.0;
var boostT = 0.0;
var bonusScore = 0;
var score = 0;
var meters = 0;
var newRecord = false;
var skyPan = 0.0;

var flash = 0.0;
var flashCol = 0xffffff;
var shake = 0.0;
var hue = 0.0;
var state = 0;            // 0 title/attract, 1 play, 2 pause, 3 crash
var prevState = -1;
var stateT = 0.0;
var quit = false;

BuildTrack();
ResetTraffic();

double Ease(double t) => t * t * (3 - 2 * t);

void BuildTrack()
{
    segs.Clear();
    var y0 = 0.0;
    void Add(int n, double curve, double dy)
    {
        var startY = y0;
        for (var i = 0; i < n; i++)
        {
            var p = (double)i / n;
            var p2 = (double)(i + 1) / n;
            var ramp = p < 0.25 ? Ease(p / 0.25) : p > 0.75 ? Ease((1 - p) / 0.25) : 1.0;
            segs.Add(new Seg
            {
                Z = segs.Count * segLen,
                C = curve * ramp,
                Y1 = startY + dy * Ease(p),
                Y2 = startY + dy * Ease(p2),
            });
        }
        y0 = startY + dy;
    }

    Add(70, 0, 0);
    Add(60, 3, 0);
    Add(45, 0, 600);
    Add(60, -3, 0);
    Add(45, 0, -600);
    Add(50, -5, 0);
    Add(35, 0, 450);
    Add(50, 5, 0);
    Add(60, 0, -450);
    Add(45, 7, 0);
    Add(45, -7, 0);
    Add(70, 0, 0);
    Add(55, 4, 350);
    Add(55, -4, -350);
    Add(40, 0, 500);
    Add(70, 2, -500);
    Add(60, -6, 0);
    Add(60, 6, 0);
    Add(70, 0, 0);
    trackLength = segs.Count * segLen;
}

void ResetTraffic()
{
    cars.Clear();
    for (var i = 0; i < 16; i++)
    {
        var z = rng.NextDouble() * trackLength;
        cars.Add(new Car
        {
            Z = z,
            Off = -0.75 + rng.NextDouble() * 1.5,
            Speed = maxSpeed * (0.32 + rng.NextDouble() * 0.3),
            Color = new[] { 0xff5c7a, 0xffd700, 0x66ff9f, 0xb14aff, 0x4ddfff }[rng.Next(5)],
            Rel = 1,
        });
    }
    pads.Clear();
    for (var i = 0; i < 6; i++)
        pads.Add((rng.NextDouble() * trackLength, -0.7 + rng.NextDouble() * 1.4));
}

void StartGame()
{
    position = 0;
    playerX = 0;
    speed = maxSpeed * 0.5;
    lives = 3;
    invuln = 1.5;
    boostT = 0;
    bonusScore = 0;
    score = 0;
    meters = 0;
    newRecord = false;
    ResetTraffic();
}

void Crash()
{
    if (invuln > 0) return;
    lives--;
    invuln = 2.0;
    shake = 12;
    flash = 0.7;
    flashCol = 0xff3b5c;
    speed = maxSpeed * 0.22;
    fx.Burst(W / 2.0, H * 0.72, 40, 0xffb84d, 0xff3b5c, 26, 1.0, 0, 2);
    fx.Ring(W / 2.0, H * 0.72, 22, 34, 0.6, 0xffffff, 0xff9f1c, '·');
    pops.Add((W / 2 - 4, H * 0.72 - 3, "CRASH!", 0xff5c7a, 1.0, 1.0));
    if (lives <= 0)
    {
        state = 3;
        stateT = 0;
        if (score > best)
        {
            best = score;
            Store.Save("terminalracer", score);
            newRecord = true;
        }
    }
}

double WrapDelta(double d)
{
    if (d > trackLength / 2) d -= trackLength;
    if (d < -trackLength / 2) d += trackLength;
    return d;
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
    if (invuln > 0) invuln -= dt;
    if (boostT > 0) boostT -= dt;

    // ---------- update ----------
    var steer = 0.0;
    if (state == 1)
    {
        if (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A)) steer -= 1;
        if (Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D)) steer += 1;
    }
    else if (state == 0)
    {
        playerX = Math.Sin(position * 0.00022) * 0.7;
    }

    if (state is 0 or 1 or 3)
    {
        var targetSpeed = state == 3 ? 0 : state == 0 ? maxSpeed * 0.55 : maxSpeed * (boostT > 0 ? 1.28 : 1.0);
        if (state == 1 && Input.Hit(ConsoleKey.DownArrow) || state == 1 && Input.Hit(ConsoleKey.S))
            targetSpeed = maxSpeed * 0.35;
        if (speed < targetSpeed) speed = Math.Min(targetSpeed, speed + accel * 1.6 * dt);
        else speed = Math.Max(targetSpeed, speed - maxSpeed * 0.9 * dt);

        if (state == 1)
            playerX += steer * 2.1 * Math.Max(0.35, speed / maxSpeed) * dt;

        var onRoad = Math.Abs(playerX) <= 1.0;
        if (!onRoad && state == 1)
        {
            speed = Math.Max(maxSpeed * 0.28, speed - maxSpeed * 0.75 * dt);
            shake = Math.Max(shake, 2);
            if (rng.NextDouble() < 0.5)
                fx.Spawn(W / 2.0 + (rng.NextDouble() - 0.5) * 6, H * 0.74,
                    (rng.NextDouble() - 0.5) * 8, -6 - rng.NextDouble() * 6, 0.5, 0x8a7452, 0x3a2e1a, '·', 8, 2);
        }
        if (state == 1)
            playerX = Math.Clamp(playerX, -1.65, 1.65);

        position += speed * dt;
        if (position >= trackLength) position -= trackLength;

        // road under player
        var pIdx = (int)(position / segLen) % segs.Count;
        var pPct = (position % segLen) / segLen;
        playerWorldY = segs[pIdx].Y1 + (segs[pIdx].Y2 - segs[pIdx].Y1) * pPct;
        skyPan += segs[pIdx].C * (speed / maxSpeed) * dt * 0.55;

        // HUD numbers
        meters = (int)(position / 6);
        score = (int)(position / 6) + bonusScore;

        // traffic
        var playerWorld = position + playerZ;
        foreach (var c in cars)
        {
            c.Z += c.Speed * dt;
            if (c.Z >= trackLength) c.Z -= trackLength;
            var rel = WrapDelta(c.Z - playerWorld);
            if (c.Rel > 0 && rel <= 0 && state == 1)
            {
                if (Math.Abs(c.Off - playerX) < 0.75)
                {
                    bonusScore += 75;
                    pops.Add((W / 2 - 4, H * 0.66, "NEAR MISS +75", 0xffd700, 1.0, 1.0));
                    shake = Math.Max(shake, 3);
                    fx.Burst(W / 2.0, H * 0.7, 10, 0xffd700, 0xffffff, 14, 0.4, 0, 4);
                }
            }
            c.Rel = rel;
            if (state == 1 && invuln <= 0 && Math.Abs(rel) < 320 && Math.Abs(c.Off - playerX) < 0.40)
                Crash();
            if (rel < -2500)
            {
                c.Z = WrapDelta(playerWorld + 14000 + rng.NextDouble() * 26000);
                if (c.Z < 0) c.Z += trackLength;
                c.Off = -0.75 + rng.NextDouble() * 1.5;
                c.Speed = maxSpeed * (0.32 + rng.NextDouble() * 0.3);
                c.Rel = 1;
            }
        }

        // boost pads
        for (var i = 0; i < pads.Count; i++)
        {
            var p = pads[i];
            var rel = WrapDelta(p.Z - playerWorld);
            if (state == 1 && rel < 300 && rel > -300 && Math.Abs(p.Off - playerX) < 0.55 && boostT <= 0.4)
            {
                boostT = 1.6;
                bonusScore += 100;
                flash = 0.35;
                flashCol = 0x66ddff;
                shake = Math.Max(shake, 4);
                fx.Ring(W / 2.0, H * 0.72, 20, 30, 0.5, 0x66ddff, 0xffffff, '·');
                pops.Add((W / 2 - 3, H * 0.66, "BOOST +100", 0x66ddff, 1.0, 1.0));
            }
            if (rel < -1500)
                pads[i] = (WrapDelta(playerWorld + 9000 + rng.NextDouble() * 12000), -0.7 + rng.NextDouble() * 1.4);
        }

        // exhaust
        if (state != 3 && rng.NextDouble() < (boostT > 0 ? 0.95 : 0.5))
            fx.Spawn(W / 2.0 + (rng.NextDouble() - 0.5) * 4, H * 0.735,
                (rng.NextDouble() - 0.5) * 4, 8 + rng.NextDouble() * 8,
                0.35, boostT > 0 ? 0x66ddff : 0xff9f1c, 0x802000, '▄', 4, 3);
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

    DrawSky();
    DrawRoad();

    if (state == 0)
    {
        fx.Draw(cv);
        DrawTitle();
        DrawTitleOver();
    }
    else
    {
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
Console.WriteLine($"Thanks for playing TERMINAL RACER! {meters}m, score {score}, best {best}.");

// ---------- rendering ----------
void DrawSky()
{
    var horizon = (int)(H * 0.42);
    for (var y = 0; y <= horizon; y++)
    {
        var t = y / (double)horizon;
        var c = Col.Mix(0x120b2e, Col.Mix(0xff5e7a, 0xffb35c, t), t * t);
        for (var x = 0; x < W; x++)
            cv.Set(x, y, ' ', c);
    }

    // retro sun
    var sunX = W * 0.7;
    var sunY = horizon - 4;
    var r = Math.Max(5, H / 7);
    for (var dy = -r; dy <= r; dy++)
    {
        if (dy > 0 && dy % 2 == 0) continue;      // stripe gaps
        var half = Math.Sqrt(Math.Max(0, r * r - dy * dy));
        var wc = (int)(half * 2.1);
        var u = (dy + r) / (2.0 * r);
        var c = Col.Mix(0xffedb0, 0xff3b8f, u);
        for (var dx = -wc; dx <= wc; dx++)
            cv.Set((int)sunX + dx, sunY + dy, ' ', c);
    }

    // mountains
    for (var x = 0; x < W; x++)
    {
        var m = Math.Sin((x + skyPan * 14) * 0.09) * 0.5 + Math.Sin((x + skyPan * 26) * 0.031) * 0.5;
        var h = 2 + (int)((m + 1) * 2.4);
        for (var y = horizon - h; y <= horizon; y++)
            cv.Set(x, y, y == horizon - h ? '▲' : '█', Col.Mix(0x2a1b4e, 0x4a2f7a, 1 - (horizon - y) / (double)Math.Max(1, h)));
    }
}

void DrawRoad()
{
    var horizon = (int)(H * 0.42);
    var baseIdx = (int)(position / segLen) % segs.Count;
    var basePct = (position % segLen) / segLen;
    var camX = playerX * roadWidth;
    var camY = playerWorldY + cameraHeight;

    // segment buffers for sprites
    var bx = new double[drawDistance];
    var bdx = new double[drawDistance];
    var bY1 = new double[drawDistance];
    var bClip = new double[drawDistance];
    var bOk = new bool[drawDistance];

    double maxy = H;
    var x = 0.0;
    var dx = -segs[baseIdx].C * basePct;

    for (var n = 0; n < drawDistance; n++)
    {
        var idx = (baseIdx + n) % segs.Count;
        var seg = segs[idx];
        var looped = idx < baseIdx;
        var z1 = seg.Z + (looped ? trackLength : 0);
        var z2 = z1 + segLen;
        var zc1 = z1 - position;
        var zc2 = z2 - position;
        bx[n] = x;
        bdx[n] = dx;
        bY1[n] = horizon;
        bClip[n] = maxy;
        bOk[n] = false;
        if (zc1 <= cameraDepth) { x += dx; dx += seg.C; continue; }

        var s1 = cameraDepth / zc1;
        var s2 = cameraDepth / zc2;
        var sx1 = W / 2.0 + s1 * (x - camX) * W / 2.0;
        var sx2 = W / 2.0 + s2 * (x + dx - camX) * W / 2.0;
        var sw1 = s1 * roadWidth * W / 2.0;
        var sw2 = s2 * roadWidth * W / 2.0;
        var sy1 = horizon + s1 * (camY - seg.Y1) * H / 2.0;
        var sy2 = horizon + s2 * (camY - seg.Y2) * H / 2.0;

        x += dx;
        dx += seg.C;

        if (sy2 >= sy1 || sy2 >= maxy) continue;

        var band = (idx / 3) % 2 == 0;
        var grass = band ? 0x1f7a3e : 0x1a6b35;
        var roadC = band ? 0x3c3c4e : 0x363646;
        var rumbleC = band ? 0xe04040 : 0xe8e8e8;

        var yTop = (int)Math.Max(0, Math.Ceiling(sy2));
        var yBot = (int)Math.Min(H - 1, Math.Floor(Math.Min(sy1, maxy - 0.001)));
        for (var y = yTop; y <= yBot; y++)
        {
            var t = sy1 == sy2 ? 0 : (y - sy2) / (sy1 - sy2);
            var cx = sx1 + (sx2 - sx1) * t;
            var cw = sw1 + (sw2 - sw1) * t;
            var rumble = Math.Max(1, cw * 0.15);

            for (var px2 = 0; px2 < W; px2++)
                cv.Set(px2, y, ' ', grass);

            var rl = cx - cw;
            var rr = cx + cw;
            FillRange(y, rl, rl + rumble, rumbleC);
            FillRange(y, rr - rumble, rr, rumbleC);
            FillRange(y, rl + rumble, rr - rumble, roadC);
            if (band)
            {
                var laneW = Math.Max(1, cw * 0.02);
                var l1 = cx - cw / 3.0;
                var l2 = cx + cw / 3.0;
                FillRange(y, l1 - laneW / 2, l1 + laneW / 2, 0xf0f0f0);
                FillRange(y, l2 - laneW / 2, l2 + laneW / 2, 0xf0f0f0);
            }
        }

        bY1[n] = sy1;
        bClip[n] = maxy;
        bOk[n] = true;
        maxy = sy2;
    }

    // sprites: boost pads then cars, far to near
    for (var n = drawDistance - 1; n >= 0; n--)
    {
        if (!bOk[n]) continue;
        var idx = (baseIdx + n) % segs.Count;
        var seg = segs[idx];
        var looped = idx < baseIdx;

        foreach (var pd in pads)
        {
            var pIdx = (int)(pd.Z / segLen) % segs.Count;
            if (pIdx != idx) continue;
            var pct = (pd.Z % segLen) / segLen;
            var zc = seg.Z + pct * segLen + (looped ? trackLength : 0) - position;
            if (zc <= cameraDepth) continue;
            var sc = cameraDepth / zc;
            var wx = bx[n] + bdx[n] * pct + pd.Off * roadWidth;
            var sx = W / 2.0 + sc * (wx - camX) * W / 2.0;
            var sy = horizon + sc * (camY - (seg.Y1 + (seg.Y2 - seg.Y1) * pct)) * H / 2.0;
            if (sy >= bClip[n]) continue;
            var wc = Math.Max(3, (int)(sc * roadWidth * W / 2.0 * 0.5));
            var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 8 + pd.Z * 0.01);
            for (var i = -wc / 2; i <= wc / 2; i++)
                cv.Set((int)sx + i, (int)sy, '≡', Col.Mix(0x66ddff, 0xffffff, pulse));
        }

        foreach (var c in cars)
        {
            var cIdx = (int)(c.Z / segLen) % segs.Count;
            if (cIdx != idx) continue;
            var pct = (c.Z % segLen) / segLen;
            var zc = seg.Z + pct * segLen + (looped ? trackLength : 0) - position;
            if (zc <= cameraDepth) continue;
            var sc = cameraDepth / zc;
            var wx = bx[n] + bdx[n] * pct + c.Off * roadWidth;
            var sx = W / 2.0 + sc * (wx - camX) * W / 2.0;
            var sy = horizon + sc * (camY - (seg.Y1 + (seg.Y2 - seg.Y1) * pct)) * H / 2.0;
            if (sy >= bClip[n]) continue;
            var wc = Math.Max(2, (int)(sc * roadWidth * W / 2.0 * 0.34));
            var hr = Math.Max(1, wc / 3);
            var x0 = (int)sx - wc / 2;
            var yBase = (int)sy;
            for (var r2 = 0; r2 < hr; r2++)
            {
                var yy = yBase - hr + r2;
                for (var q = 0; q < wc; q++)
                {
                    var u = wc <= 1 ? 0.5 : (double)q / (wc - 1);
                    var cc = r2 == 0 ? Col.Mix(c.Color, 0xffffff, 0.35) : Col.Mix(c.Color, 0x000000, u * 0.4);
                    cv.Set(x0 + q, yy, '█', cc);
                }
            }
            cv.Set(x0, yBase, '·', 0x141420);
            cv.Set(x0 + wc - 1, yBase, '·', 0x141420);
        }
    }

    // player car
    {

        var pScale = cameraDepth / playerZ;
        var carY = horizon + pScale * (camY - playerWorldY) * H / 2.0;
        var lean = state == 1 ? (Input.Hit(ConsoleKey.LeftArrow) || Input.Hit(ConsoleKey.A) ? -1 :
            Input.Hit(ConsoleKey.RightArrow) || Input.Hit(ConsoleKey.D) ? 1 : 0) : 0;
        var body = invuln > 0 && Math.Sin(fx.Time * 30) > 0.4 ? 0x8fa3c8 : Col.Mix(0x4ddfff, 0xffffff, 0.35 + 0.2 * Math.Sin(fx.Time * 8));
        if (boostT > 0) body = Col.Mix(body, 0x66ddff, 0.6);
        var cx = W / 2;
        var yb = (int)carY;
        cv.Set(cx, yb - 3, '▲', body);
        cv.Set(cx - 1, yb - 2, '═', body);
        cv.Set(cx, yb - 2, '█', body);
        cv.Set(cx + 1, yb - 2, '═', body);
        for (var q = -2; q <= 2; q++)
            cv.Set(cx + q, yb - 1, '█', q < 0 ? Col.Mix(body, 0x000000, 0.35) : Col.Mix(body, 0x000000, Math.Abs(q) * 0.18));
        cv.Set(cx - 2, yb, '█', 0x141420);
        cv.Set(cx + 2, yb, '█', 0x141420);
        if (lean != 0)
            cv.Set(cx + lean * 3, yb - 1, lean < 0 ? '«' : '»', 0xffd700);
    }

    void FillRange(int y, double from, double to, int color)
    {
        var a = (int)Math.Ceiling(from);
        var b = (int)Math.Floor(to);
        if (b < 0 || a >= W) return;
        if (a < 0) a = 0;
        if (b >= W) b = W - 1;
        for (var xx = a; xx <= b; xx++)
            cv.Set(xx, y, ' ', color);
    }
}

void DrawHud()
{
    var kmh = (int)(speed / maxSpeed * 320);
    cv.Text(2, 0, $"SCORE {score:D6}", 0xf2f5ff);
    var bs = $"BEST {best:D6}";
    cv.Text(W - 2 - bs.Length, 0, bs, 0x8fa3c8);
    cv.TextC($"{kmh} km/h", boostT > 0 ? Col.Mix(0x66ddff, 0xffffff, 0.5 + 0.5 * Math.Sin(fx.Time * 12)) : Col.Hsl(hue, 0.8, 0.62), 0);
    cv.Text(2, 1, new string('♥', Math.Max(0, lives)), 0xff5c7a);
    cv.Text(W / 2 - 8, 1, $"{meters}m", 0x8fa3c8);
    cv.Text(W - 30, 1, "A/D steer  S brake  Q quit", 0x607096);

    // speed bar
    var sp = (int)Math.Round(24.0 * Math.Min(1, speed / (maxSpeed * 1.28)));
    cv.Text(2, H - 1, $"SPD {new string('█', Math.Min(24, sp))}{new string('░', 24 - Math.Min(24, sp))}",
        boostT > 0 ? 0x66ddff : 0x66ff9f);
    if (state == 1 && Math.Abs(playerX) > 1.0)
        cv.TextC("OFF ROAD!", Col.Scale(0xff9f1c, 0.6 + 0.4 * Math.Sin(fx.Time * 10)), H - 2);
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
    var title = "TERMINAL RACER";
    var x = (W - title.Length) / 2;
    cv.TextGrad(x, 4, title, Col.Hsl((t * 50) % 360, 0.9, 0.64), Col.Hsl((t * 50 + 150) % 360, 0.9, 0.64), 0);
    cv.TextGrad(x - 3, 6, new string('─', title.Length + 6),
        Col.Hsl((t * 50 + 40) % 360, 0.8, 0.5), Col.Hsl((t * 50 + 190) % 360, 0.8, 0.5), 0);
    cv.TextC("O U T R U N", Col.Hsl((t * 50 + 40) % 360, 0.6, 0.74), 8);
}

void DrawTitleOver()
{
    var t = fx.Time;
    if (best > 0) cv.TextC($"BEST {best:D6}", 0xffd166, 1);
    if (t % 1.2 < 0.75) cv.TextC("PRESS SPACE TO START", 0xffffff, H - 6);
    cv.TextC("Auto throttle. A/D steer, S brake. Hit cyan pads, dodge traffic.", 0x6f7fa8, H - 4);
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
    cv.TextGrad(W / 2 - 5, y0 + 2, "WRECKED", Col.Hsl((t * 60) % 360, 0.95, 0.6), 0xff9f1c, 0);
    cv.TextC($"SCORE {score:D6}", 0xffffff, y0 + 5);
    cv.TextC($"{meters}m DRIVEN", 0x9fb0d8, y0 + 6);
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

class Seg
{
    public double Z;
    public double C;
    public double Y1;
    public double Y2;
}

class Car
{
    public double Z;
    public double Off;
    public double Speed;
    public int Color;
    public double Rel;
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
