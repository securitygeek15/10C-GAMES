const int W = 42, H = 20;
var snake = new LinkedList<(int X, int Y)>();
var occupied = new HashSet<(int X, int Y)>();
var rng = new Random();
var dir = (X: 1, Y: 0);
var pending = dir;
var score = 0;
var delay = 105;
var alive = true;

snake.AddFirst((W / 2, H / 2));
occupied.Add(snake.First!.Value);
var food = NewFood();
Console.CursorVisible = false;

while (alive)
{
    var frameStart = Environment.TickCount64;
    while (Environment.TickCount64 - frameStart < delay)
    {
        if (!Console.KeyAvailable) continue;
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) alive = false;
        pending = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W when dir.Y != 1 => (0, -1),
            ConsoleKey.DownArrow or ConsoleKey.S when dir.Y != -1 => (0, 1),
            ConsoleKey.LeftArrow or ConsoleKey.A when dir.X != 1 => (-1, 0),
            ConsoleKey.RightArrow or ConsoleKey.D when dir.X != -1 => (1, 0),
            _ => pending
        };
    }
    if (!alive) break;

    dir = pending;
    var head = snake.First!.Value;
    var next = (X: head.X + dir.X, Y: head.Y + dir.Y);
    alive = next.X > 0 && next.X < W - 1 && next.Y > 0 && next.Y < H - 1 && !occupied.Contains(next);
    if (!alive) break;

    snake.AddFirst(next);
    occupied.Add(next);
    if (next == food)
    {
        score += 10;
        delay = Math.Max(45, delay - 3);
        food = NewFood();
    }
    else
    {
        occupied.Remove(snake.Last!.Value);
        snake.RemoveLast();
    }
    Draw();
}

Console.CursorVisible = true;
Console.ResetColor();
Console.SetCursorPosition(0, H + 3);
Console.WriteLine($"Game over. Score: {score}");

(int X, int Y) NewFood()
{
    (int X, int Y) p;
    do p = (rng.Next(1, W - 1), rng.Next(1, H - 1)); while (occupied.Contains(p));
    return p;
}

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("NEON SNAKE  WASD/arrows move  Q quit");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            var p = (x, y);
            if (x == 0 || x == W - 1 || y == 0 || y == H - 1) { Console.ForegroundColor = ConsoleColor.DarkCyan; Console.Write('#'); }
            else if (p == food) { Console.ForegroundColor = ConsoleColor.Red; Console.Write('@'); }
            else if (p == snake.First!.Value) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write('O'); }
            else if (occupied.Contains(p)) { Console.ForegroundColor = ConsoleColor.Green; Console.Write('o'); }
            else Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}   Speed {110 - delay}");
    Console.ResetColor();
}
