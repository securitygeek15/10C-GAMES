const int H = 22;
var rng = new Random();
var lane = 1;
var obstacles = new List<(int Lane, int Y)>();
var score = 0;
var alive = true;
Console.CursorVisible = false;

while (alive)
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) alive = false;
        if (key is ConsoleKey.LeftArrow or ConsoleKey.A) lane = Math.Max(0, lane - 1);
        if (key is ConsoleKey.RightArrow or ConsoleKey.D) lane = Math.Min(2, lane + 1);
    }
    if (rng.NextDouble() < 0.28) obstacles.Add((rng.Next(3), 0));
    obstacles = obstacles.Select(o => (Lane: o.Lane, Y: o.Y + 1)).Where(o => o.Y < H).ToList();
    alive &= !obstacles.Any(o => o.Y == H - 2 && o.Lane == lane);
    score++;
    Draw();
    Thread.Sleep(Math.Max(45, 120 - score / 12));
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine($"Crash. Score: {score}");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("TERMINAL RACER  A/D or arrows change lanes");
    for (var y = 0; y < H; y++)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("|");
        for (var l = 0; l < 3; l++)
        {
            var car = y == H - 2 && l == lane;
            var block = obstacles.Any(o => o.Lane == l && o.Y == y);
            if (car) { Console.ForegroundColor = ConsoleColor.Cyan; Console.Write("  A  "); }
            else if (block) { Console.ForegroundColor = ConsoleColor.Red; Console.Write(" [ ] "); }
            else { Console.Write("     "); }
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("|");
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}");
    Console.ResetColor();
}
