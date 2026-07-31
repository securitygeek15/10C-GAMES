const int W = 10, H = 20;
var board = new int[W, H];
var shapes = new (int X, int Y)[][]
{
    [(0,0), (1,0), (-1,0), (2,0)],
    [(0,0), (1,0), (0,1), (1,1)],
    [(0,0), (-1,0), (1,0), (1,1)],
    [(0,0), (-1,0), (0,1), (1,1)],
    [(0,0), (1,0), (0,1), (-1,1)]
};
var rng = new Random();
var piece = Spawn();
var score = 0;
var alive = true;
Console.CursorVisible = false;

while (alive)
{
    var start = Environment.TickCount64;
    while (Environment.TickCount64 - start < Math.Max(120, 420 - score * 8))
    {
        if (!Console.KeyAvailable) continue;
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) alive = false;
        if (key is ConsoleKey.LeftArrow or ConsoleKey.A && Can(piece.X - 1, piece.Y, piece.Cells)) piece.X--;
        if (key is ConsoleKey.RightArrow or ConsoleKey.D && Can(piece.X + 1, piece.Y, piece.Cells)) piece.X++;
        if (key is ConsoleKey.DownArrow or ConsoleKey.S && Can(piece.X, piece.Y + 1, piece.Cells)) piece.Y++;
        if (key is ConsoleKey.UpArrow or ConsoleKey.W or ConsoleKey.Spacebar)
        {
            var rotated = piece.Cells.Select(c => (X: -c.Y, Y: c.X)).ToArray();
            if (Can(piece.X, piece.Y, rotated)) piece.Cells = rotated;
        }
        Draw();
    }
    if (!alive) break;
    if (Can(piece.X, piece.Y + 1, piece.Cells)) piece.Y++;
    else
    {
        foreach (var c in piece.Cells) board[piece.X + c.X, piece.Y + c.Y] = 1;
        ClearLines();
        piece = Spawn();
        alive = Can(piece.X, piece.Y, piece.Cells);
    }
    Draw();
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, H + 5);
Console.WriteLine($"Game over. Score: {score}");

(int X, int Y, (int X, int Y)[] Cells) Spawn() => (W / 2, 1, shapes[rng.Next(shapes.Length)].ToArray());

bool Can(int px, int py, (int X, int Y)[] cells)
{
    foreach (var c in cells)
    {
        var x = px + c.X;
        var y = py + c.Y;
        if (x < 0 || x >= W || y < 0 || y >= H || board[x, y] != 0) return false;
    }
    return true;
}

void ClearLines()
{
    for (var y = H - 1; y >= 0; y--)
    {
        var full = true;
        for (var x = 0; x < W; x++) full &= board[x, y] != 0;
        if (!full) continue;
        score += 100;
        for (var yy = y; yy > 0; yy--)
        for (var x = 0; x < W; x++)
            board[x, yy] = board[x, yy - 1];
        for (var x = 0; x < W; x++) board[x, 0] = 0;
        y++;
    }
}

void Draw()
{
    var active = piece.Cells.Select(c => (X: piece.X + c.X, Y: piece.Y + c.Y)).ToHashSet();
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("FALLING BLOCKS  arrows/WASD move  up/space rotate  Q quit");
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("+" + new string('-', W * 2) + "+");
    for (var y = 0; y < H; y++)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("|");
        for (var x = 0; x < W; x++)
        {
            if (active.Contains((x, y))) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write("[]"); }
            else if (board[x, y] != 0) { Console.ForegroundColor = ConsoleColor.Blue; Console.Write("##"); }
            else Console.Write("  ");
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("|");
    }
    Console.WriteLine("+" + new string('-', W * 2) + "+");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Score {score}");
    Console.ResetColor();
}
