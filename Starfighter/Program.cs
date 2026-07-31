const int W = 46, H = 22;
var rng = new Random();
var px = W / 2;
var bullets = new List<(int X, int Y)>();
var enemies = new List<(int X, int Y)>();
var score = 0;
var lives = 3;
var tick = 0;
Console.CursorVisible = false;

while (lives > 0)
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) lives = 0;
        if (key is ConsoleKey.LeftArrow or ConsoleKey.A) px = Math.Max(1, px - 2);
        if (key is ConsoleKey.RightArrow or ConsoleKey.D) px = Math.Min(W - 2, px + 2);
        if (key is ConsoleKey.Spacebar) bullets.Add((px, H - 3));
    }

    if (rng.NextDouble() < 0.22) enemies.Add((rng.Next(1, W - 1), 1));
    bullets = bullets.Select(b => (X: b.X, Y: b.Y - 1)).Where(b => b.Y > 0).ToList();
    if (tick++ % 3 == 0) enemies = enemies.Select(e => (X: e.X + rng.Next(-1, 2), Y: e.Y + 1)).ToList();

    for (var i = enemies.Count - 1; i >= 0; i--)
    {
        var hit = bullets.FindIndex(b => Math.Abs(b.X - enemies[i].X) <= 1 && b.Y == enemies[i].Y);
        if (hit >= 0)
        {
            score += 25;
            bullets.RemoveAt(hit);
            enemies.RemoveAt(i);
        }
    }

    var landed = enemies.RemoveAll(e => e.Y >= H - 1);
    lives -= landed;
    Draw();
    Thread.Sleep(55);
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine($"Mission over. Score: {score}");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("STARFIGHTER  A/D move  Space fire  Q quit");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            if (y == H - 1 && x == px) { Console.ForegroundColor = ConsoleColor.Green; Console.Write('^'); continue; }
            if (bullets.Any(b => b.X == x && b.Y == y)) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write('|'); continue; }
            if (enemies.Any(e => e.X == x && e.Y == y)) { Console.ForegroundColor = ConsoleColor.Red; Console.Write('V'); continue; }
            Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}   Lives {lives}");
    Console.ResetColor();
}
