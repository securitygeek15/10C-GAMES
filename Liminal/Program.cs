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
var best = Store.Best("liminal");
var grid = new char[1, 1];
var explored = new HashSet<(int X, int Y)>();
const int Title = 0;
const int Playing = 1;
const int Result = 2;
const int Paused = 3;
const int Help = 4;
var state = Title;
var titleMenu = new Menu("noclip in", "how to play", "quit");
var resultMenu = new Menu("go back in", "title screen", "quit");
var pauseMenu = Settings.PauseMenu();
var toasts = new Toasts();
var flashLightT = 0.0;
var won = false;
var mw = 0;
var mh = 0;
var ox = 0;
var oy = 0;
var px = 1;
var py = 1;
var exitX = 1;
var exitY = 1;
var tapes = 0;
var sanity = 100.0;
var elapsed = 0.0;
var steps = 0;
var moveCd = 0.0;
var eventText = "";
var eventT = 0.0;
var shake = 0.0;
var flash = 0.0;
var flashCol = 0xffffff;
var stateT = 0.0;
var score = 0;
var newRecord = false;
var itActive = false;
var itX = 1;
var itY = 1;
var itTimer = 0.0;
var hue = 0.0;

var weird = new[]
{
    "the hum gets louder.", "you've been here before.", "someone moved the wall.",
    "smells like old carpet.", "the lights buzz in b flat.", "you hear a party upstairs.",
    "your phone has no signal. obviously.", "the carpet is slightly damp.",
    "a door closes somewhere.", "you are not being followed. probably.",
    "the wallpaper is breathing. politely.", "there is no level 1."
};

void Start()
{
    BuildMaze();
    explored.Clear();
    tapes = 0;
    sanity = 100;
    elapsed = 0;
    steps = 0;
    moveCd = 0;
    eventText = "you noclipped out of the group chat.";
    eventT = 4.5;
    shake = 0;
    flash = 0;
    itActive = false;
    won = false;
    newRecord = false;
    stateT = 0;
    score = 0;
    flashLightT = 0;
    for (var y = -2; y <= 2; y++)
        for (var x = -2; x <= 2; x++) explored.Add((px + x, py + y));
}

void BuildMaze()
{
    mw = Math.Min(31, Math.Max(11, W - 6));
    if (mw % 2 == 0) mw--;
    mh = Math.Min(15, Math.Max(9, H - 8));
    if (mh % 2 == 0) mh--;
    ox = (W - mw) / 2;
    oy = 2 + (H - 4 - mh) / 2;
    ox = Math.Max(1, ox);
    oy = Math.Max(3, oy);

    grid = new char[mw, mh];
    for (var y = 0; y < mh; y++)
        for (var x = 0; x < mw; x++) grid[x, y] = '#';

    Carve(1, 1);

    var dist = Bfs(1, 1);
    var far = 0;
    exitX = 1;
    exitY = 1;
    for (var y = 1; y < mh - 1; y++)
        for (var x = 1; x < mw - 1; x++)
            if (grid[x, y] == '.' && dist[x, y] > far)
            {
                far = dist[x, y];
                exitX = x;
                exitY = y;
            }
    grid[exitX, exitY] = 'E';

    var floors = new List<(int X, int Y)>();
    for (var y = 1; y < mh - 1; y++)
        for (var x = 1; x < mw - 1; x++)
            if (grid[x, y] == '.' && (x != 1 || y != 1) && (x != exitX || y != exitY) && dist[x, y] > 4)
                floors.Add((x, y));
    if (floors.Count < 3)
        for (var y = 1; y < mh - 1; y++)
            for (var x = 1; x < mw - 1; x++)
                if (grid[x, y] == '.' && (x != 1 || y != 1) && (x != exitX || y != exitY))
                    floors.Add((x, y));
    Shuffle(floors);
    for (var i = 0; i < 3 && i < floors.Count; i++)
        grid[floors[i].X, floors[i].Y] = 'T';

    var waters = 0;
    for (var i = floors.Count - 1; i >= 0 && waters < 6; i--)
    {
        var (x, y) = floors[i];
        if (grid[x, y] == '.')
        {
            grid[x, y] = 'W';
            waters++;
        }
    }

    px = 1;
    py = 1;
    explored.Clear();
}

void Carve(int x, int y)
{
    grid[x, y] = '.';
    var dirs = new (int Dx, int Dy)[] { (0, -2), (2, 0), (0, 2), (-2, 0) };
    ShuffleArr(dirs);
    foreach (var (dx, dy) in dirs)
    {
        var nx = x + dx;
        var ny = y + dy;
        if (nx <= 0 || ny <= 0 || nx >= mw - 1 || ny >= mh - 1) continue;
        if (grid[nx, ny] == '#')
        {
            grid[x + dx / 2, y + dy / 2] = '.';
            Carve(nx, ny);
        }
    }
}

int[,] Bfs(int sx, int sy)
{
    var dist = new int[mw, mh];
    for (var y = 0; y < mh; y++)
        for (var x = 0; x < mw; x++) dist[x, y] = -1;
    var q = new Queue<(int X, int Y)>();
    dist[sx, sy] = 0;
    q.Enqueue((sx, sy));
    while (q.Count > 0)
    {
        var (x, y) = q.Dequeue();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) continue;
            if (grid[nx, ny] == '#' || dist[nx, ny] >= 0) continue;
            dist[nx, ny] = dist[x, y] + 1;
            q.Enqueue((nx, ny));
        }
    }
    return dist;
}

void Shuffle<T>(List<T> list)
{
    for (var i = list.Count - 1; i > 0; i--)
    {
        var j = rng.Next(i + 1);
        (list[i], list[j]) = (list[j], list[i]);
    }
}

void ShuffleArr<T>(T[] list)
{
    for (var i = list.Length - 1; i > 0; i--)
    {
        var j = rng.Next(i + 1);
        (list[i], list[j]) = (list[j], list[i]);
    }
}

void Step(int dx, int dy)
{
    var nx = px + dx;
    var ny = py + dy;
    if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) return;
    if (grid[nx, ny] == '#') return;
    px = nx;
    py = ny;
    steps++;

    var wx = ox + px;
    var wy = oy + py;
    switch (grid[nx, ny])
    {
        case 'T':
            grid[nx, ny] = '.';
            tapes++;
            sanity = Math.Min(100, sanity + 10);
            eventText = "vhs tape acquired. it's labeled \"us\".";
            eventT = 3.5;
            fx.Burst(wx, wy, 20, 0x7ff5e3, 0xffd166, 14, 0.8, 0, 2, '▣');
            flash = 0.15;
            flashCol = 0x7ff5e3;
            if (tapes == 2 && !itActive) ActivateIt();
            break;
        case 'W':
            grid[nx, ny] = '.';
            sanity = Math.Min(100, sanity + 28);
            eventText = "almond water. tastes like a memory.";
            eventT = 3;
            fx.Burst(wx, wy, 14, 0x7ff5e3, 0x2a5a6a, 11, 0.7, 0, 2, '○');
            break;
        case 'E':
            if (tapes >= 3) Win();
            else
            {
                eventText = "the door needs three tapes. obviously.";
                eventT = 2.8;
                shake = 3;
            }
            break;
        default:
            if (steps % 13 == 0)
            {
                eventText = weird[rng.Next(weird.Length)];
                eventT = 3;
            }
            break;
    }

    if (itActive && itX == px && itY == py) ItHit();
}

void ActivateIt()
{
    itActive = true;
    itTimer = 0.8;
    var found = false;
    for (var i = 0; i < 200; i++)
    {
        var x = rng.Next(1, mw - 1);
        var y = rng.Next(1, mh - 1);
        if (grid[x, y] == '#') continue;
        if (Math.Abs(x - px) + Math.Abs(y - py) < 9) continue;
        itX = x;
        itY = y;
        found = true;
        break;
    }
    if (!found)
        for (var y = 1; y < mh - 1 && !found; y++)
            for (var x = 1; x < mw - 1; x++)
            {
                if (grid[x, y] == '#') continue;
                if (Math.Abs(x - px) + Math.Abs(y - py) < 5) continue;
                itX = x;
                itY = y;
                found = true;
                break;
            }
    eventText = "something started following you.";
    eventT = 3.5;
}

void MoveIt()
{
    var options = new List<(int X, int Y)>();
    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
    {
        var nx = itX + dx;
        var ny = itY + dy;
        if (nx < 0 || ny < 0 || nx >= mw || ny >= mh) continue;
        if (grid[nx, ny] != '#') options.Add((nx, ny));
    }
    if (options.Count == 0) return;
    (int X, int Y) pick;
    if (rng.NextDouble() < 0.65)
    {
        options.Sort((a, b) => (Math.Abs(a.X - px) + Math.Abs(a.Y - py)).CompareTo(Math.Abs(b.X - px) + Math.Abs(b.Y - py)));
        pick = options[0];
    }
    else pick = options[rng.Next(options.Count)];
    itX = pick.X;
    itY = pick.Y;
    if (itX == px && itY == py) ItHit();
}

void ItHit()
{
    sanity -= 22;
    shake = 12;
    flash = 0.45;
    flashCol = 0x8a2a2a;
    eventText = "something smiled.";
    eventT = 3;
    for (var i = 0; i < 200; i++)
    {
        var x = rng.Next(1, mw - 1);
        var y = rng.Next(1, mh - 1);
        if (grid[x, y] == '#') continue;
        if (Math.Abs(x - px) + Math.Abs(y - py) < 10) continue;
        itX = x;
        itY = y;
        break;
    }
    if (sanity <= 0) Die();
}

void Win()
{
    state = 2;
    won = true;
    stateT = 0;
    score = (int)(elapsed * 10) + tapes * 100 + 500;
    SaveBest();
    fx.Burst(ox + px, oy + py, 40, 0xffd166, 0x7ff5e3, 20, 1.2, 0, 2, '✦');
}

void Die()
{
    state = 2;
    won = false;
    stateT = 0;
    score = (int)(elapsed * 10) + tapes * 100;
    SaveBest();
    shake = 18;
    flash = 0.8;
    flashCol = 0x000000;
}

void SaveBest()
{
    if (score > best)
    {
        best = score;
        Store.Save("liminal", score);
        newRecord = true;
    }
}

GameLoop.Run((dt, t) =>
{
    hue = (hue + dt * 8) % 360;
    if (shake > 0) shake = Math.Max(0, shake - dt * 25);
    if (flash > 0) flash = Math.Max(0, flash - dt * 2);
    if (eventT > 0) eventT = Math.Max(0, eventT - dt);
    stateT += dt;
    if (moveCd > 0) moveCd -= dt;

    if (state == Title)
    {
        var pick = titleMenu.Update();
        if (pick == 0) { Start(); state = Playing; }
        else if (pick == 1) { state = Help; stateT = 0; }
        else if (pick == 2) return false;
        if (Input.Quit) return false;
        if (rng.NextDouble() < 0.15)
            fx.Spawn(rng.Next(W), 2 + rng.Next(Math.Max(1, H - 4)), (rng.NextDouble() - 0.5) * 2, 3 + rng.NextDouble() * 4,
                6, 0x8a7a3a, 0x1a160a, '▓', 0, 0.02);
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
        sanity -= (0.55 + tapes * 0.08) * dt;
        if (sanity <= 0) { sanity = 0; Die(); }
        if (flashLightT > 0) flashLightT = Math.Max(0, flashLightT - dt);
        else if (Input.Hit(ConsoleKey.F) && sanity > 12)
        {
            flashLightT = 3.2;
            sanity -= 8;
            eventText = "the flash reveals more than you wanted.";
            eventT = 3;
            fx.Ring(ox + px, oy + py, 24, 18, 0.6, 0xfff3c4, 0xffd166, '✦');
            toasts.Push("camera flash: -8 sanity", 0xfff3c4);
        }

        if (moveCd <= 0)
        {
            var dx = 0;
            var dy = 0;
            if (Input.HeldAny(ConsoleKey.W, ConsoleKey.UpArrow)) dy = -1;
            else if (Input.HeldAny(ConsoleKey.S, ConsoleKey.DownArrow)) dy = 1;
            else if (Input.HeldAny(ConsoleKey.A, ConsoleKey.LeftArrow)) dx = -1;
            else if (Input.HeldAny(ConsoleKey.D, ConsoleKey.RightArrow)) dx = 1;
            if (dx != 0 || dy != 0)
            {
                moveCd = 0.13;
                Step(dx, dy);
            }
        }

        if (itActive)
        {
            itTimer -= dt;
            if (itTimer <= 0)
            {
                itTimer = Math.Max(0.55, 1.15 - elapsed * 0.002);
                MoveIt();
            }
        }
    }
    else
    {
        var pick = resultMenu.Update();
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
        DrawMaze(t);
        fx.Draw(cv);
        DrawHud();
        if (state == Result) DrawResult();
        if (state == Paused) DrawPause();
    }

    toasts.Draw(cv, 1);

    if (flash > 0.01)
    {
        cv.OverlayColor = flashCol;
        cv.OverlayK = Math.Min(0.8, flash);
    }
    else cv.OverlayK = 0;

    cv.Present();
    return true;
});

Term.Restore();
Console.WriteLine($"liminal closed. {(won ? "you escaped" : "you are still here")}. score {score}, best {best}.");

void DrawBackdrop()
{
    for (var y = 0; y < H; y++)
    {
        var u = (double)y / Math.Max(1, H - 1);
        var c = Col.Mix(0x0a0905, 0x14120a, u);
        for (var x = 0; x < W; x++)
        {
            var noise = (x * 13 + y * 7 + (int)(fx.Time * 9)) % 173 == 0;
            cv.Set(x, y, noise ? '·' : ' ', noise ? 0x3a3420 : c);
        }
    }
}

void DrawMaze(double time)
{
    var radius = 4.4 + 0.5 * Math.Sin(time * 2.7);
    if ((int)(time * 7) % 19 == 0) radius = 3.0;
    if (flashLightT > 0) radius = 9.0;
    var warm = 0.5 + 0.5 * Math.Sin(time * 1.3);

    for (var y = 0; y < mh; y++)
    {
        for (var x = 0; x < mw; x++)
        {
            var dx = x - px;
            var dy = y - py;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var visible = d <= radius;
            if (visible) explored.Add((x, y));
            if (!visible && !explored.Contains((x, y))) continue;

            var ch = grid[x, y];
            char glyph;
            var bright = visible ? Math.Max(0.25, 1 - d / (radius + 1.2)) : 0.26;
            int color;
            switch (ch)
            {
                case '#':
                    glyph = '▓';
                    color = Col.Scale(Col.Hsl(45 + warm * 6, 0.34, 0.35), bright);
                    break;
                case 'T':
                    glyph = '▣';
                    color = visible ? 0x7ff5e3 : 0x2a5a52;
                    break;
                case 'W':
                    glyph = '○';
                    color = visible ? 0x6fb8c8 : 0x24444a;
                    break;
                case 'E':
                    glyph = '▯';
                    color = !visible ? 0x3a5228 : tapes >= 3 ? 0x9be564 : 0x8a5a2a;
                    break;
                default:
                    glyph = '·';
                    color = Col.Scale(Col.Hsl(48, 0.24, 0.32), bright * (0.8 + 0.2 * warm));
                    break;
            }
            cv.Set(ox + x, oy + y, glyph, color);
        }
    }

    var itDist = Math.Sqrt((itX - px) * (itX - px) + (itY - py) * (itY - py));
    if (itActive && itDist <= radius + 1.5)
    {
        cv.Set(ox + itX, oy + itY, '?', 0xff5c7a);
    }

    var pxs = ox + px;
    var pys = oy + py;
    cv.Set(pxs, pys, '@', 0xfff3c4);
    for (var k = 0; k < 8; k++)
    {
        var a = time * 1.5 + k * Math.PI / 4;
        cv.Set((int)Math.Round(pxs + Math.Cos(a) * 2.6), (int)Math.Round(pys + Math.Sin(a) * 1.5),
            '·', 0x6a5a2a);
    }
}

void DrawHud()
{
    var rec = (int)(fx.Time) % 2 == 0 ? '●' : '○';
    cv.Set(2, 0, rec, 0xff5c7a);
    cv.Text(4, 0, $"REC 00:{elapsed / 60:00}:{elapsed % 60:00}", 0x8a7a3a);
    cv.TextC("LEVEL 0", 0xd8c884, 0);

    cv.TextRight("sanity", W - 14, 0, 0x8a7a4a);
    Ui.Bar(cv, W - 7, 0, 5, sanity / 100.0, 0x9be564, 0xffd166);
    cv.Text(2, 1, $"tapes {new string('▣', tapes)}{new string('▢', Math.Max(0, 3 - tapes))}", 0x7ff5e3);
    cv.TextRight($"steps {steps}", W - 3, 1, 0x5a5030);

    if (eventT > 0)
        cv.TextC(eventText, Col.Scale(0xd8c884, Math.Min(1, eventT)), H - 2);
    else if (flashLightT > 0)
        cv.TextC("the flash is still burning.", 0xfff3c4, H - 2);
    else
        cv.TextC("the hum continues.", 0x4a4228, H - 2);
    Ui.Hints(cv, H - 1, "WASD", "move", "F", flashLightT > 0 ? "flash on" : "flash", "P", "pause");

    if (ox > 8)
    {
        var side = "you are still here";
        for (var i = 0; i < side.Length && oy + 2 + i < H - 2; i++)
            cv.Set(2, oy + 2 + i, side[i], 0x3a3420);
    }
}

void DrawTitle()
{
    var time = fx.Time;
    var top = Math.Max(2, (H - 15) / 2);
    Ui.BigTitle(cv, top, "liminal", 0xd8c884, 0x7ff5e3, time);
    Ui.TextShadowC(cv, top + 2, "level 0: the group chat has no exit", 0xe0d8b0);
    cv.TextC("find 3 vhs tapes. find the door. do not get comfortable.", 0x8a7a4a, top + 3);

    titleMenu.Draw(cv, top + 5);

    if (best > 0) Ui.Badge(cv, (W - 11) / 2, top + 9, $"best {best}", 0xffd166, 0x241a08);

    Ui.Ticker(cv, H - 3, "the carpet is damp  ·  the lights buzz in b flat  ·  you are fine. probably.",
        time, 0x8a7a4a, 0x4a4228, 5);
    Ui.Hints(cv, H - 1, "W/S", "select", "ENTER", "pick", "Q", "quit");
}

void DrawHelp()
{
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, Math.Min(58, W - 2), Math.Min(18, H - 2), "how to play", 0xd8c884);
    var x = x0 + 3;
    var y = y0 + 2;
    y += Ui.WrapDraw(cv, x, y,
        "a maze in the dark. find three vhs tapes, then the door. something starts following you after the second tape.",
        x1 - x - 2, Ui.Ink, 3) + 1;
    Ui.KeyHint(cv, x, y, "WASD", "move", 0xd8c884); y++;
    Ui.KeyHint(cv, x, y, "F", "camera flash: sees further, costs 8 sanity", 0xfff3c4); y++;
    Ui.KeyHint(cv, x, y, "P", "pause + settings", Ui.Gold); y += 2;
    cv.Set(x, y, '▣', 0x7ff5e3); cv.Text(x + 2, y, "vhs tape: +10 sanity", Ui.InkDim); y++;
    cv.Set(x, y, '○', 0x6fb8c8); cv.Text(x + 2, y, "almond water: +28 sanity", Ui.InkDim); y++;
    cv.Set(x, y, '?', 0xff5c7a); cv.Text(x + 2, y, "that: -22 sanity if it catches you", Ui.InkDim); y++;
    cv.Text(x, y, "sanity also drains on its own. faster with tapes.", Ui.InkFaint);
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

void DrawResult()
{
    cv.Dim(0.3);
    var w = Math.Min(58, W - 2);
    var (x0, y0, x1, y1) = Ui.CenterCard(cv, w, Math.Min(18, H - 2), won ? "escaped" : "still here", won ? 0x9be564 : 0x8a5a2a);
    var cx = (x0 + x1) / 2;
    cv.TextC(won ? "you noclipped out." : "the hum got you.", won ? 0x9be564 : 0xff5c7a, y0 + 2);
    var body = Ui.Wrap(won
        ? "the door opened into a parking garage. your group chat has 47 unread messages. you do not open them."
        : "you are now part of level 0. someone will find your playlist. eventually.", w - 6);
    for (var i = 0; i < body.Length && i < 3; i++)
        cv.Text(x0 + 3, y0 + 4 + i, body[i], won ? 0xd8d0a8 : 0xa89870);

    var (rank, tier) = Ui.Rank(score, new[] { 200, 400, 700, 1000, 1400, 1900 });
    Ui.Divider(cv, x0 + 2, x1 - 2, y0 + 8);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 9, "tapes / time", $"{tapes}/3   {elapsed:0.0}s", 0xd8c884);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 10, "score", score.ToString(), 0xf4ffe8);
    Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 11, "rank", rank, Ui.RankColor(tier));
    if (newRecord) Ui.NewBest(cv, cx, y0 + 12, fx.Time);
    else Ui.ScoreRow(cv, x0 + 3, x1 - 3, y0 + 12, "best", best.ToString(), 0xffd166);

    resultMenu.Draw(cv, y0 + 13);
    cv.TextC("R to go back in", Ui.InkFaint, y1 - 1);
}
