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
var best = Store.Best("aurafarm");
var orbs = new List<Orb>();
var enemies = new List<Enemy>();
var pops = new List<Pop>();
const int Title = 0;
const int Playing = 1;
const int Dead = 2;
const int Paused = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("farm aura", "how to play", "quit");
var deadMenu = new Menu("run it back", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var aura = 0;
var hearts = 3;
var combo = 0;
var comboT = 0.0;
var elapsed = 0.0;
var playerX = W / 2.0;
var playerY = H / 2.0;
var lastDx = 1.0;
var lastDy = 0.0;
var invuln = 0.0;
var dashT = 0.0;
var dashCd = 0.0;
var dashVx = 0.0;
var dashVy = 0.0;
var spawnAcc = 0.0;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var bannerT = 0.0;
var banner = "";
var bannerCol = 0xffffff;
var nextMilestone = 2500;
var newRecord = false;
var gone = 0;
var bonusAcc = 8.0;
var milestones = new[]
{
    "main character era", "it's giving protagonist", "silent confidence", "aura farming",
    "no thoughts, just aura", "the plot revolves around you"
};

void Start()
{
    aura = 0;
    hearts = 3;
    combo = 0;
    comboT = 0;
    elapsed = 0;
    playerX = W / 2.0;
    playerY = H / 2.0;
    lastDx = 1;
    lastDy = 0;
    invuln = 1.0;
    dashT = 0;
    dashCd = 0;
    spawnAcc = 1.3;
    shake = 0;
    flash = 0;
    bannerT = 0;
    nextMilestone = 2500;
    newRecord = false;
    bonusAcc = 8;
    orbs.Clear();
    enemies.Clear();
    pops.Clear();
    toasts.Push("go be the main character", 0xb9a7ff);
    for (var i = 0; i < 8; i++) AddOrb();
}

void AddOrb(int kind = 0)
{
    double x, y;
    var tries = 0;
    do
    {
        x = rng.Next(3, Math.Max(4, W - 3));
        y = rng.Next(4, Math.Max(5, H - 2));
        tries++;
    } while (tries < 30 && Math.Abs(x - playerX) + Math.Abs(y - playerY) < 8);
    orbs.Add(new Orb { X = x, Y = y, Phase = rng.NextDouble() * Math.PI * 2, Kind = kind });
}

void SpawnEnemy()
{
    var m = 2;
    var side = rng.Next(4);
    double x, y;
    if (side == 0) { x = rng.Next(m, Math.Max(m + 1, W - m)); y = 3; }
    else if (side == 1) { x = rng.Next(m, Math.Max(m + 1, W - m)); y = H - 2; }
    else if (side == 2) { x = 1; y = rng.Next(4, Math.Max(5, H - 2)); }
    else { x = W - 2; y = rng.Next(4, Math.Max(5, H - 2)); }
    enemies.Add(new Enemy
    {
        X = x,
        Y = y,
        Speed = 1.7 + rng.NextDouble() * 1.2 + Math.Min(4.0, elapsed * 0.05),
        Phase = rng.NextDouble() * Math.PI * 2
    });
}

void Collect(Orb orb)
{
    if (orb.Kind == 2)
    {
        orbs.Remove(orb);
        AddOrb();
        if (hearts < 3)
        {
            hearts++;
            AddPop(orb.X, orb.Y, "+1 heart", 0xff9fc4);
            toasts.Push("self care acquired", 0xff9fc4);
        }
        else
        {
            aura += 250;
            AddPop(orb.X, orb.Y, "+250 full hearts", 0xff9fc4);
        }
        fx.Burst(orb.X, orb.Y, 16, 0xff9fc4, 0x4a1a2a, 12, 0.7, 0, 2.5, '♥');
        return;
    }
    if (orb.Kind == 1)
    {
        orbs.Remove(orb);
        AddOrb();
        aura += 1500;
        combo = Math.Min(10, combo + 2);
        comboT = 2.8;
        invuln = Math.Max(invuln, 1.4);
        banner = "hyper aura";
        bannerCol = 0xffd166;
        bannerT = 2.0;
        shake = 6;
        AddPop(orb.X, orb.Y, "+1500 HYPER", 0xffd166);
        fx.Ring(orb.X, orb.Y, 24, 22, 0.65, 0xffd166, 0xffffff, '✦');
        toasts.Push("hyper aura +1500", 0xffd166);
        return;
    }
    var mult = Math.Min(10, 1 + combo);
    var gain = 100 * mult;
    aura += gain;
    combo++;
    comboT = 2.8;
    orbs.Remove(orb);
    AddOrb();
    AddPop(orb.X, orb.Y, $"+{gain} aura", 0xffd166);
    fx.Burst(orb.X, orb.Y, 20, 0xffd166, 0xb9a7ff, 14, 0.7, 0, 2.2, '✦');
    if (aura >= nextMilestone)
    {
        banner = milestones[rng.Next(milestones.Length)];
        bannerCol = 0xb9a7ff;
        bannerT = 2.4;
        nextMilestone += 2500;
        flash = 0.16;
        flashCol = 0xb9a7ff;
    }
}

void Hurt()
{
    if (invuln > 0 || dashT > 0) return;
    hearts--;
    combo = 0;
    invuln = 1.8;
    shake = 12;
    flash = 0.4;
    flashCol = 0xff2a4d;
    AddPop(playerX, playerY - 1, "npc drained your aura", 0xff5c7a);
    fx.Burst(playerX, playerY, 30, 0xff2a4d, 0x2a0a16, 18, 0.9, 4, 2, '✖');
    foreach (var e in enemies)
    {
        var dx = e.X - playerX;
        var dy = e.Y - playerY;
        var d = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        e.X += dx / d * 6;
        e.Y += dy / d * 4;
    }
    if (hearts <= 0) Die();
}

void Die()
{
    state = 2;
    stateT = 0;
    gone = (int)elapsed;
    if (aura > best)
    {
        best = aura;
        Store.Save("aurafarm", aura);
        newRecord = true;
    }
    shake = 16;
    flash = 0.7;
    flashCol = 0x000000;
}

void AddPop(double x, double y, string s, int c)
{
    pops.Add(new Pop(x - s.Length / 2.0, y, s, c, 1.0, 1.0));
}

GameLoop.Run((dt, t) =>
{
    if (shake > 0) shake = Math.Max(0, shake - dt * 26);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2.2);
    if (bannerT > 0) bannerT = Math.Max(0, bannerT - dt);
    if (state != Paused)
    {
        if (invuln > 0) invuln = Math.Max(0, invuln - dt);
        if (dashCd > 0) dashCd = Math.Max(0, dashCd - dt);
    }
    stateT += dt;

    var ix = 0.0;
    var iy = 0.0;
    if (Input.HeldAny(ConsoleKey.A, ConsoleKey.LeftArrow)) ix -= 1;
    if (Input.HeldAny(ConsoleKey.D, ConsoleKey.RightArrow)) ix += 1;
    if (Input.HeldAny(ConsoleKey.W, ConsoleKey.UpArrow)) iy -= 1;
    if (Input.HeldAny(ConsoleKey.S, ConsoleKey.DownArrow)) iy += 1;

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Playing; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
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
        elapsed += dt;
        comboT -= dt;
        if (comboT <= 0) combo = 0;

        if (ix != 0 || iy != 0)
        {
            var len = Math.Sqrt(ix * ix + iy * iy);
            lastDx = ix / len;
            lastDy = iy / len;
        }
        if (Input.Hit(ConsoleKey.Spacebar) && dashCd <= 0)
        {
            dashT = 0.17;
            dashCd = 1.25;
            dashVx = lastDx * 72;
            dashVy = lastDy * 42;
            fx.Burst(playerX, playerY, 18, 0xb9a7ff, 0x5a4a8a, 10, 0.5, 0, 2, '·');
        }

        if (dashT > 0)
        {
            dashT -= dt;
            playerX += dashVx * dt;
            playerY += dashVy * dt;
            if (rng.NextDouble() < 0.8)
                fx.Spawn(playerX, playerY, -dashVx * 0.15, -dashVy * 0.15, 0.35,
                    Col.Hsl(280 + rng.Next(60), 0.9, 0.7), 0x2a1a44, '·', 0, 2);
        }
        else
        {
            playerX += ix * 24 * dt;
            playerY += iy * 13 * dt;
        }
        playerX = Math.Clamp(playerX, 2, W - 3);
        playerY = Math.Clamp(playerY, 3, H - 2);

        // orbs
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var o = orbs[i];
            var dx = o.X - playerX;
            var dy = (o.Y - playerY) * 1.8;
            if (Math.Sqrt(dx * dx + dy * dy) < 2.2) Collect(o);
        }

        // enemies
        for (var i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            var dx = playerX - e.X;
            var dy = playerY - e.Y;
            var d = Math.Max(0.01, Math.Sqrt(dx * dx + dy * dy * 4));
            e.X += dx / d * e.Speed * dt;
            e.Y += dy / d * e.Speed * 0.56 * dt;
            var near = Math.Sqrt((e.X - playerX) * (e.X - playerX) + (e.Y - playerY) * (e.Y - playerY) * 4);
            if (near < 1.9)
            {
                if (dashT > 0)
                {
                    enemies.RemoveAt(i);
                    aura += 50;
                    AddPop(e.X, e.Y, "+50 dodge", 0x7ff5e3);
                    fx.Burst(e.X, e.Y, 18, 0x7ff5e3, 0x1a4a44, 16, 0.7, 2, 2, '✦');
                }
                else Hurt();
            }
        }

        var cap = Math.Min(13, 2 + (int)(elapsed / 9));
        spawnAcc += dt;
        var interval = Math.Max(0.5, 2.0 - elapsed * 0.05);
        while (spawnAcc >= interval)
        {
            spawnAcc -= interval;
            if (enemies.Count < cap) SpawnEnemy();
        }

        for (var i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            p.Life -= dt;
            if (p.Life <= 0) { pops.RemoveAt(i); continue; }
            p.Y -= dt * 7;
        }

        // every so often something better than plain aura shows up
        bonusAcc -= dt;
        if (bonusAcc <= 0 && orbs.Count < 10)
        {
            bonusAcc = 9 + rng.NextDouble() * 7;
            AddOrb(rng.NextDouble() < 0.55 ? 1 : 2);
            AddPop(playerX, playerY - 2, "something nice dropped", 0x9be7ff);
            fx.Spark(playerX, playerY, 0x9be7ff, 10);
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
        DrawArena();
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
        cv.OverlayK = Math.Min(0.6, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"aura farm closed. aura {aura}, best {best}. stay mysterious.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x0b0616, 0x140a22, u);
        for (var x = 0; x < W; x++)
        {
            var grid = x % 4 == 0 && y % 2 == 0;
            cv.Set(x, y, grid ? '·' : ' ', grid ? Col.Hsl(270 + x, 0.4, 0.14) : c);
        }
    }
    var scan = (int)(fx.Time * 8) % Math.Max(1, H);
    for (var x = 0; x < W; x++) cv.Set(x, scan, '─', 0x1d1230);
}

void DrawArena()
{
    foreach (var o in orbs)
    {
        var pulse = 0.5 + 0.5 * Math.Sin(fx.Time * 5 + o.Phase);
        var x = (int)Math.Round(o.X);
        var y = (int)Math.Round(o.Y);
        if (o.Kind == 2)
        {
            cv.Set(x, y, '♥', Col.Mix(0xff9fc4, 0xffffff, pulse));
            cv.Set(x - 1, y, '·', 0x6a2a3a);
            cv.Set(x + 1, y, '·', 0x6a2a3a);
            continue;
        }
        if (o.Kind == 1)
        {
            cv.Set(x, y, '◈', Col.Mix(0xffd166, 0xffffff, pulse));
            for (var k = 0; k < 4; k++)
            {
                var a = fx.Time * 2.6 + k * Math.PI / 2;
                cv.Set((int)Math.Round(x + Math.Cos(a) * 2.6), (int)Math.Round(y + Math.Sin(a) * 1.4),
                    '✦', 0x8a6a2a);
            }
            continue;
        }
        cv.Set(x, y, '✦', Col.Hsl(46 + pulse * 10, 1, 0.55 + pulse * 0.15));
        for (var k = 0; k < 2; k++)
        {
            var a = fx.Time * 2.4 + k * Math.PI + o.Phase;
            cv.Set((int)Math.Round(x + Math.Cos(a) * 1.6), (int)Math.Round(y + Math.Sin(a) * 1.0),
                '·', 0x8a6a2a);
        }
    }

    for (var i = 0; i < enemies.Count; i++)
    {
        var e = enemies[i];
        var x = (int)Math.Round(e.X);
        var y = (int)Math.Round(e.Y);
        var c = Col.Hsl(340 + i * 9, 0.8, 0.55);
        cv.Set(x, y, '✖', c);
        cv.Set(x - 1, y, '·', Col.Scale(c, 0.35));
        cv.Set(x + 1, y, '·', Col.Scale(c, 0.35));
        if ((int)(fx.Time * 2) % 3 == 0 && i % 2 == 0)
            cv.Text(x - 1, y - 1, "npc", 0x5a3a4a);
    }

    // aura ring
    for (var k = 0; k < 12; k++)
    {
        var a = fx.Time * 1.1 + k * Math.PI / 6;
        var rx = (int)Math.Round(playerX + Math.Cos(a) * 3.2);
        var ry = (int)Math.Round(playerY + Math.Sin(a) * 1.9);
        cv.Set(rx, ry, '·', Col.Hsl(280 + k * 8, 0.7, 0.4));
    }

    var blink = invuln > 0 && (int)(fx.Time * 20) % 2 == 0;
    if (!blink)
    {
        var px = (int)Math.Round(playerX);
        var py = (int)Math.Round(playerY);
        cv.Set(px, py, '◆', 0xffffff);
        cv.Set(px - 1, py, '✦', 0xb9a7ff);
        cv.Set(px + 1, py, '✦', 0xb9a7ff);
    }

    if (bannerT > 0 && state == Playing)
    {
        var k = Math.Min(1.0, bannerT / 0.5);
        Ui.Badge(cv, (W - banner.Length - 2) / 2, H / 2 - 1, banner, Col.Scale(bannerCol, k), 0x120c1e);
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

void DrawHud()
{
    cv.Text(2, 0, $"aura {aura:000000}", Col.Hsl(48, 1, 0.66));
    cv.TextC($"streak x{combo}", 0xb9a7ff, 0);
    cv.TextRight($"{new string('♥', Math.Max(0, hearts))}{new string('♡', Math.Max(0, 3 - hearts))}", W - 3, 0,
        hearts <= 1 ? 0xff5c7a : 0xff9fc4);
    cv.Text(2, 1, $"best {best:000000}", 0x5a4a72);
    cv.TextC($"{elapsed:0.0}s   npcs {enemies.Count}", 0x6a5a88, 1);
    Ui.Bar(cv, 2, H - 2, 10, 1 - Math.Clamp(dashCd / 1.25, 0, 1), 0xb9a7ff, 0x7ff5e3);
    cv.Text(14, H - 2, dashCd <= 0 ? "dash ready" : "dash charging", dashCd <= 0 ? 0x7ff5e3 : 0x5a4a72);
    Ui.Hints(cv, H - 1, "WASD", "move", "SPACE", "dash", "P", "pause");
}

void DrawTitle()
{
    var t = fx.Time;
    var top = Math.Max(2, (H - 13) / 2);
    Ui.BigTitle(cv, top, "aura farm", 0xb9a7ff, 0x7ff5e3, t);
    Ui.TextShadowC(cv, top + 2, "you are the main character. act accordingly.", 0xe0d8f0);
    cv.TextC("collect aura  ·  dodge npcs  ·  stay mysterious", 0x8a7aa8, top + 3);

    titleMenu.Draw(cv, top + 5);

    var px = W / 2;
    var py = top + 9;
    for (var k = 0; k < 12; k++)
    {
        var a = t * 0.9 + k * Math.PI / 6;
        cv.Set((int)Math.Round(px + Math.Cos(a) * 6), (int)Math.Round(py + Math.Sin(a) * 2.4),
            '·', Col.Hsl(270 + k * 10, 0.7, 0.4));
    }
    cv.Set(px, py, '◆', 0xffffff);
    cv.Set(px - 2, py, '✦', 0xffd166);
    cv.Set(px + 2, py, '✦', 0xffd166);

    if (best > 0) Ui.Badge(cv, (W - 13) / 2, top + 11, $"best {best:000000}", 0xffd166, 0x241a08);

    Ui.Ticker(cv, H - 3, "no thoughts just aura  ·  main character syndrome  ·  npc energy detected  ·  log off eventually",
        t, 0x8a7aa8, 0x5a4a72, 6);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(58, W - 2), Math.Min(17, H - 2), "how to play", 0xb9a7ff);
    var x = x0 + 3;
    var y = y0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "you are the main character. orbs are aura. npcs follow you around and drain it on contact, which is deeply rude.",
        x1 - x - 2, Ui.Ink, 3) + 1;
    Ui.KeyHint(cv, x, y, "WASD", "move", 0x7ff5e3); y++;
    Ui.KeyHint(cv, x, y, "SPACE", "dash, and dodge straight through npcs", 0xb9a7ff); y++;
    Ui.KeyHint(cv, x, y, "P", "pause + settings", Ui.Gold); y += 2;
    cv.Set(x, y, '✦', Ui.Gold); cv.Text(x + 2, y, "aura - builds your streak multiplier", Ui.InkDim); y++;
    cv.Set(x, y, '◈', 0xffd166); cv.Text(x + 2, y, "hyper aura - 1500 and a moment of invincibility", Ui.InkDim); y++;
    cv.Set(x, y, '♥', 0xff9fc4); cv.Text(x + 2, y, "self care - restores a heart", Ui.InkDim);
    cv.TextC("enter / esc to go back", Ui.InkFaint, y1 - 1);
}

void DrawPause()
{
    cv.Dim(0.45);
    Settings.RefreshPauseLabels(pauseMenu);
    var (_, y0, _, y1) = Ui.CenterCard(cv, 38, 11, "paused", Ui.Accent);
    pauseMenu.Draw(cv, y0 + 2);
    cv.TextC("P / ESC to resume", Ui.InkFaint, y1 - 1);
}

void DrawDead()
{
    cv.Dim(0.35);
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(54, W - 2), Math.Min(18, H - 2), "run over", 0xff5c7a);
    var cx = (x0 + x1) / 2;
    cv.TextC("aura -1,000,000", 0xff5c7a, y0 + 2);
    cv.TextC("the npcs got to you.", 0x8a7aa8, y0 + 3);

    var (rank, tier) = Ui.Rank(aura, new[] { 500, 1500, 3000, 6000, 10000, 18000 });
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 5, "aura", $"{aura:000000}", 0xf0e8ff);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 6, "survived", $"{gone}s", 0xf0e8ff);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 7, "rank", rank, Ui.RankColor(tier));
    Ui.Divider(cv, x0 + 2, x1 - 2, y0 + 9);
    if (newRecord) Ui.NewBest(cv, cx, y0 + 10, fx.Time);
    else Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 10, "best aura", $"{best:000000}", 0xffd166);

    deadMenu.Draw(cv, y0 + 12);
    cv.TextC("R to run it back", Ui.InkFaint, y1 - 1);
}

sealed class Orb
{
    public double X;
    public double Y;
    public double Phase;
    public int Kind;   // 0 aura, 1 hyper, 2 self care
}

sealed class Enemy
{
    public double X;
    public double Y;
    public double Speed;
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
