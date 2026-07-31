var map = new[]
{
    "############################",
    "#@....#.........#......G...#",
    "#.##..#.#####.#.#.####.##..#",
    "#..#....#...#.#...#....#...#",
    "##.######.#.#.#####.####.#.#",
    "#.....G...#.#.......#....#.#",
    "#.#########.#######.#.####.#",
    "#.#.........#.....#.#......#",
    "#.#.#######.#.###.#.######.#",
    "#...#.....#...#...#....G...#",
    "###.#.###.#####.##########.#",
    "#.....#.................E..#",
    "############################"
}.Select(r => r.ToCharArray()).ToArray();
var px = 1; var py = 1; var gold = 0; var steps = 0; var won = false;
Console.CursorVisible = false;

while (!won)
{
    Draw();
    var key = Console.ReadKey(true).Key;
    if (key is ConsoleKey.Q or ConsoleKey.Escape) break;
    var (dx, dy) = key switch
    {
        ConsoleKey.UpArrow or ConsoleKey.W => (0, -1),
        ConsoleKey.DownArrow or ConsoleKey.S => (0, 1),
        ConsoleKey.LeftArrow or ConsoleKey.A => (-1, 0),
        ConsoleKey.RightArrow or ConsoleKey.D => (1, 0),
        _ => (0, 0)
    };
    var nx = px + dx; var ny = py + dy;
    if (map[ny][nx] == '#') continue;
    map[py][px] = '.';
    px = nx; py = ny; steps++;
    if (map[py][px] == 'G') gold++;
    if (map[py][px] == 'E' && gold >= 3) won = true;
    map[py][px] = '@';
}

Console.CursorVisible = true;
Console.SetCursorPosition(0, map.Length + 3);
Console.WriteLine(won ? $"Escaped with all gold in {steps} steps." : "Dungeon run ended.");

void Draw()
{
    Console.SetCursorPosition(0, 0);
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("DUNGEON DASH  WASD/arrows move  collect 3 gold then exit");
    for (var y = 0; y < map.Length; y++)
    {
        for (var x = 0; x < map[y].Length; x++)
        {
            Console.ForegroundColor = map[y][x] switch
            {
                '#' => ConsoleColor.DarkGray,
                'G' => ConsoleColor.Yellow,
                'E' => ConsoleColor.Green,
                '@' => ConsoleColor.Cyan,
                '.' => ConsoleColor.DarkBlue,
                _ => ConsoleColor.White
            };
            Console.Write(map[y][x]);
        }
        Console.WriteLine();
    }
    Console.ResetColor();
    Console.WriteLine($"Gold {gold}/3   Steps {steps}");
}
