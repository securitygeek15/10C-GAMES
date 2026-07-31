const int W = 50, H = 22;
var trail = new HashSet<(int X, int Y)>();
var p = (X: 10, Y: H / 2); var ai = (X: W - 10, Y: H / 2);
var pd = (X: 1, Y: 0); var ad = (X: -1, Y: 0);
var rng = new Random();
var result = "";
Console.CursorVisible = false;

while (result == "")
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) result = "Quit.";
        pd = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W when pd.Y != 1 => (0, -1),
            ConsoleKey.DownArrow or ConsoleKey.S when pd.Y != -1 => (0, 1),
            ConsoleKey.LeftArrow or ConsoleKey.A when pd.X != 1 => (-1, 0),
            ConsoleKey.RightArrow or ConsoleKey.D when pd.X != -1 => (1, 0),
            _ => pd
        };
    }
    if (rng.NextDouble() < 0.25) ad = BestAiDirection();
    trail.Add(p); trail.Add(ai);
    p = (p.X + pd.X, p.Y + pd.Y);
    ai = (ai.X + ad.X, ai.Y + ad.Y);
    var pCrash = Crash(p);
    var aCrash = Crash(ai);
    if (pCrash && aCrash) result = "Double crash.";
    else if (pCrash) result = "You crashed.";
    else if (aCrash) result = "AI crashed. You win!";
    Draw();
    Thread.Sleep(85);
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine(result);

(int X, int Y) BestAiDirection()
{
    var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
    return dirs.Where(d => d != (-ad.X, -ad.Y))
        .OrderByDescending(d => Free(ai.X + d.Item1, ai.Y + d.Item2))
        .First();
}

int Free(int x, int y)
{
    var count = 0;
    for (var dx = -1; dx <= 1; dx++)
    for (var dy = -1; dy <= 1; dy++)
        if (!Crash((x + dx, y + dy))) count++;
    return count;
}

bool Crash((int X, int Y) c) => c.X <= 0 || c.X >= W - 1 || c.Y <= 0 || c.Y >= H - 1 || trail.Contains(c);

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("LIGHT CYCLES  WASD/arrows turn  avoid trails");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            var border = x == 0 || x == W - 1 || y == 0 || y == H - 1;
            if (border) { Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write('#'); }
            else if ((x, y) == p) { Console.ForegroundColor = ConsoleColor.Cyan; Console.Write('P'); }
            else if ((x, y) == ai) { Console.ForegroundColor = ConsoleColor.Red; Console.Write('A'); }
            else if (trail.Contains((x, y))) { Console.ForegroundColor = ConsoleColor.Magenta; Console.Write('.'); }
            else Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ResetColor();
}
