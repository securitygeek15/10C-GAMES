const int W = 44, H = 20;
var rng = new Random();
var player = (X: W / 2, Y: H / 2);
var enemies = new List<(int X, int Y)> { (2, 2), (W - 3, 2), (2, H - 3) };
var score = 0;
var alive = true;
Console.CursorVisible = false;

while (alive)
{
    var moved = false;
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) alive = false;
        var (dx, dy) = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W => (0, -1),
            ConsoleKey.DownArrow or ConsoleKey.S => (0, 1),
            ConsoleKey.LeftArrow or ConsoleKey.A => (-1, 0),
            ConsoleKey.RightArrow or ConsoleKey.D => (1, 0),
            _ => (0, 0)
        };
        if (dx != 0 || dy != 0)
        {
            player = (Math.Clamp(player.X + dx, 1, W - 2), Math.Clamp(player.Y + dy, 1, H - 2));
            moved = true;
        }
    }
    if (!moved && rng.NextDouble() < 0.15) enemies.Add((rng.Next(1, W - 1), rng.Next(1, H - 1)));
    enemies = enemies.Select(e => (e.X + Math.Sign(player.X - e.X), e.Y + Math.Sign(player.Y - e.Y))).ToList();
    alive &= !enemies.Any(e => e == player);
    score++;
    Draw();
    Thread.Sleep(115);
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine($"Overrun. Score: {score}");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("ARENA SURVIVOR  WASD/arrows dodge chasing enemies");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            if (x == 0 || y == 0 || x == W - 1 || y == H - 1) { Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write('#'); }
            else if ((x, y) == player) { Console.ForegroundColor = ConsoleColor.Cyan; Console.Write('@'); }
            else if (enemies.Any(e => e == (x, y))) { Console.ForegroundColor = ConsoleColor.Red; Console.Write('x'); }
            else Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}  Enemies {enemies.Count}");
    Console.ResetColor();
}
