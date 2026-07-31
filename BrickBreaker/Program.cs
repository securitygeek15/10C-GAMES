const int W = 44, H = 22;
var bricks = new bool[W, 6];
for (var y = 1; y < 6; y++)
for (var x = 2; x < W - 2; x++)
    bricks[x, y] = x % 2 == 0;
var paddle = W / 2;
var ball = (X: W / 2, Y: H - 5);
var vel = (X: 1, Y: -1);
var score = 0;
var lives = 3;
Console.CursorVisible = false;

while (lives > 0 && bricks.Cast<bool>().Any(b => b))
{
    while (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) lives = 0;
        if (key is ConsoleKey.LeftArrow or ConsoleKey.A) paddle = Math.Max(4, paddle - 2);
        if (key is ConsoleKey.RightArrow or ConsoleKey.D) paddle = Math.Min(W - 5, paddle + 2);
    }

    var nx = ball.X + vel.X; var ny = ball.Y + vel.Y;
    if (nx <= 0 || nx >= W - 1) vel.X *= -1;
    if (ny <= 0) vel.Y *= -1;
    if (ny >= H - 2 && Math.Abs(nx - paddle) <= 4) vel.Y = -1;
    if (ny >= H)
    {
        lives--; ball = (W / 2, H - 5); vel = (1, -1);
    }
    else if (ny >= 0 && ny < 6 && nx >= 0 && nx < W && bricks[nx, ny])
    {
        bricks[nx, ny] = false;
        score += 10;
        vel.Y *= -1;
    }
    else ball = (ball.X + vel.X, ball.Y + vel.Y);
    Draw();
    Thread.Sleep(45);
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 3);
Console.WriteLine(!bricks.Cast<bool>().Any(b => b) ? "Board cleared!" : $"Game over. Score {score}");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("BRICK BREAKER  A/D move paddle  Q quit");
    for (var y = 0; y < H; y++)
    {
        for (var x = 0; x < W; x++)
        {
            if (y == H - 1) { Console.ForegroundColor = ConsoleColor.DarkCyan; Console.Write('='); }
            else if (y == H - 2 && Math.Abs(x - paddle) <= 4) { Console.ForegroundColor = ConsoleColor.Green; Console.Write('_'); }
            else if (x == ball.X && y == ball.Y) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write('O'); }
            else if (y < 6 && bricks[x, y]) { Console.ForegroundColor = (ConsoleColor)(9 + y % 5); Console.Write('#'); }
            else Console.Write(' ');
        }
        Console.WriteLine();
    }
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}   Lives {lives}");
    Console.ResetColor();
}
