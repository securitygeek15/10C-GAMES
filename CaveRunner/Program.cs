const int W = 54, H = 18;
var rng = new Random();
var playerY = H / 2;
var velocity = 0;
var cave = Enumerable.Range(0, W).Select(_ => (Top: 3, Bottom: H - 4)).ToList();
var score = 0;
var alive = true;
Console.CursorVisible = false;

while (alive)
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Spacebar or ConsoleKey.UpArrow or ConsoleKey.W) velocity = -2;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) alive = false;
    }
    velocity++;
    playerY += velocity > 0 ? 1 : -1;
    var last = cave[^1];
    var top = Math.Clamp(last.Top + rng.Next(-1, 2), 1, H - 8);
    var gap = 8;
    cave.RemoveAt(0);
    cave.Add((top, top + gap));
    score++;
    var wall = cave[8];
    alive &= playerY > wall.Top && playerY < wall.Bottom;
    Draw();
    Thread.Sleep(Math.Max(45, 95 - score / 35));
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine($"Crashed. Distance: {score}");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("CAVE RUNNER  Space/W flap upward  Q quit");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            var wall = y <= cave[x].Top || y >= cave[x].Bottom;
            if (x == 8 && y == playerY) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write('>'); }
            else if (wall) { Console.ForegroundColor = y <= cave[x].Top ? ConsoleColor.DarkGreen : ConsoleColor.Green; Console.Write('#'); }
            else Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Distance {score}");
    Console.ResetColor();
}
