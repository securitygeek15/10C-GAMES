var levels = new[]
{
    new[] { "########", "#..G...#", "#..B...#", "#..@...#", "########" },
    new[] { "##########", "#..G.....#", "#..##B...#", "#....@...#", "##########" },
    new[] { "###########", "#..G..G...#", "#..B..B...#", "#....@....#", "###########" }
};
var level = 0;
var moves = 0;
Console.CursorVisible = false;

while (level < levels.Length)
{
    var walls = new HashSet<(int X, int Y)>();
    var goals = new HashSet<(int X, int Y)>();
    var boxes = new HashSet<(int X, int Y)>();
    var player = (X: 0, Y: 0);
    var source = levels[level];
    for (var y = 0; y < source.Length; y++)
    for (var x = 0; x < source[y].Length; x++)
    {
        if (source[y][x] == '#') walls.Add((x, y));
        if (source[y][x] == 'G') goals.Add((x, y));
        if (source[y][x] == 'B') boxes.Add((x, y));
        if (source[y][x] == '@') player = (x, y);
    }

    while (!goals.All(boxes.Contains))
    {
        Draw(source[0].Length, source.Length, walls, goals, boxes, player);
        var key = Console.ReadKey(true).Key;
        if (key is ConsoleKey.Q or ConsoleKey.Escape) { Console.CursorVisible = true; return; }
        var (dx, dy) = key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.W => (0, -1),
            ConsoleKey.DownArrow or ConsoleKey.S => (0, 1),
            ConsoleKey.LeftArrow or ConsoleKey.A => (-1, 0),
            ConsoleKey.RightArrow or ConsoleKey.D => (1, 0),
            _ => (0, 0)
        };
        Move(walls, boxes, ref player, dx, dy);
    }
    level++;
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, 8);
Console.WriteLine($"All warehouse rooms solved in {moves} moves.");

void Move(HashSet<(int X, int Y)> walls, HashSet<(int X, int Y)> boxes, ref (int X, int Y) player, int dx, int dy)
{
    if (dx == 0 && dy == 0) return;
    var next = (player.X + dx, player.Y + dy);
    var beyond = (player.X + dx * 2, player.Y + dy * 2);
    if (walls.Contains(next)) return;
    if (boxes.Contains(next))
    {
        if (walls.Contains(beyond) || boxes.Contains(beyond)) return;
        boxes.Remove(next);
        boxes.Add(beyond);
    }
    player = next;
    moves++;
}

void Draw(int width, int height, HashSet<(int X, int Y)> walls, HashSet<(int X, int Y)> goals, HashSet<(int X, int Y)> boxes, (int X, int Y) player)
{
    Console.SetCursorPosition(0, 0);
    Console.WriteLine($"SOKOBAN PRO  Level {level + 1}/{levels.Length}  Moves {moves}");
    Console.WriteLine("Push every box onto a goal. WASD/arrows, Q quit");
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var p = (x, y);
            if (walls.Contains(p)) { Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write('#'); }
            else if (player == p) { Console.ForegroundColor = ConsoleColor.Cyan; Console.Write('@'); }
            else if (boxes.Contains(p) && goals.Contains(p)) { Console.ForegroundColor = ConsoleColor.Green; Console.Write('*'); }
            else if (boxes.Contains(p)) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write('B'); }
            else if (goals.Contains(p)) { Console.ForegroundColor = ConsoleColor.Magenta; Console.Write('G'); }
            else { Console.ForegroundColor = ConsoleColor.DarkBlue; Console.Write('.'); }
        }
        Console.WriteLine();
    }
    Console.ResetColor();
}
