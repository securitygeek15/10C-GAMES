using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GenZ;

public static class Term
{
    public static void Setup()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var h = Win.GetStdHandle(-11);
                if (Win.GetConsoleMode(h, out var m)) Win.SetConsoleMode(h, m | 0x4);
            }
        }
        catch { }
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        try { Console.CursorVisible = false; } catch { }
        Console.Write("\x1b[2J\x1b[?25l\x1b[?7l");
    }

    public static void Restore()
    {
        Console.Write("\x1b[0m\x1b[?25h\x1b[?7h");
        try { Console.CursorVisible = true; } catch { }
        try { Console.SetCursorPosition(0, 0); Console.Clear(); } catch { }
    }

    public static (int W, int H) Fit(int maxW, int maxH, int minW, int minH)
    {
        var w = maxW;
        var h = maxH;
        try
        {
            var cw = Console.WindowWidth;
            var ch = Console.WindowHeight;
            if (cw > 0) w = Math.Min(cw, maxW);
            if (ch > 0) h = Math.Min(ch, maxH);
        }
        catch { }
        return w >= minW && h >= minH ? (w, h) : (0, 0);
    }
}

static class Win
{
    [DllImport("kernel32")]
    public static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32")]
    public static extern bool GetConsoleMode(IntPtr h, out uint m);
    [DllImport("kernel32")]
    public static extern bool SetConsoleMode(IntPtr h, uint m);
}

public static class GameLoop
{
    public static void Run(Func<double, double, bool> frame, int fps = 60)
    {
        var sw = Stopwatch.StartNew();
        var prev = 0.0;
        var step = 1.0 / fps;
        while (true)
        {
            var now = sw.Elapsed.TotalSeconds;
            var dt = Math.Min(now - prev, 0.1);
            prev = now;
            Input.Poll();
            if (!frame(dt, now)) break;
            var ms = (int)((step - (sw.Elapsed.TotalSeconds - now)) * 1000);
            if (ms > 0) Thread.Sleep(ms);
        }
    }
}

public sealed class Canvas
{
    public readonly int W;
    public readonly int H;
    readonly char[] _ch;
    readonly int[] _fg;
    readonly StringBuilder _sb;
    public int ShakeX;
    public int ShakeY;
    public int OverlayColor = 0x000000;
    public double OverlayK;

    public Canvas(int w, int h)
    {
        W = w;
        H = h;
        _ch = new char[w * h];
        _fg = new int[w * h];
        _sb = new StringBuilder(w * h + 64);
        Clear();
    }

    public void Clear()
    {
        Array.Fill(_ch, ' ');
        Array.Fill(_fg, 0);
    }

    public void Set(int x, int y, char c, int color)
    {
        x += ShakeX;
        y += ShakeY;
        if ((uint)x < (uint)W && (uint)y < (uint)H)
        {
            var i = y * W + x;
            _ch[i] = c;
            _fg[i] = color;
        }
    }

    public void Text(int x, int y, string s, int color)
    {
        for (var i = 0; i < s.Length; i++)
            if (s[i] != ' ') Set(x + i, y, s[i], color);
    }

    public void TextRaw(int x, int y, string s, int color)
    {
        for (var i = 0; i < s.Length; i++) Set(x + i, y, s[i], color);
    }

    public void TextC(string s, int color, int y) => Text((W - s.Length) / 2, y, s, color);

    public void TextRight(string s, int x1, int y, int color) => Text(x1 - s.Length, y, s, color);

    public void TextGrad(int x, int y, string s, int c1, int c2, double phase = -1)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var u = s.Length <= 1 ? 0 : (double)i / (s.Length - 1);
            if (phase >= 0) u = (u + phase) % 1.0;
            if (s[i] != ' ') Set(x + i, y, s[i], Col.Mix(c1, c2, u));
        }
    }

    public void Fill(int x0, int y0, int x1, int y1, char c, int color)
    {
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++) Set(x, y, c, color);
    }

    public void Frame(int x0, int y0, int x1, int y1, int color, char fill = ' ')
    {
        for (var x = x0 + 1; x < x1; x++)
        {
            Set(x, y0, '─', color);
            Set(x, y1, '─', color);
        }
        for (var y = y0 + 1; y < y1; y++)
        {
            Set(x0, y, '│', color);
            Set(x1, y, '│', color);
        }
        Set(x0, y0, '╭', color);
        Set(x1, y0, '╮', color);
        Set(x0, y1, '╰', color);
        Set(x1, y1, '╯', color);
        if (fill != ' ') Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, fill, 0);
    }

    public void Present()
    {
        _sb.Clear();
        _sb.Append("\x1b[H");
        var last = -1;
        var k = OverlayK;
        var oc = OverlayColor;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var i = y * W + x;
                var c = _ch[i];
                var fg = _fg[i];
                if (c == ' ' && fg != 0) c = '█';
                if (k > 0.004)
                {
                    if (c == ' ')
                    {
                        c = '█';
                        fg = Col.Scale(oc, k);
                    }
                    else fg = Col.Mix(fg, oc, k);
                }
                if (c != ' ')
                {
                    if (fg != last)
                    {
                        AppendColor(fg);
                        last = fg;
                    }
                    _sb.Append(c);
                }
                else _sb.Append(' ');
            }
            if (y < H - 1) _sb.Append("\r\n");
        }
        _sb.Append("\x1b[0m");
        Console.Write(_sb);
    }

    void AppendColor(int c)
    {
        _sb.Append("\x1b[38;2;");
        AppendNum((c >> 16) & 255);
        _sb.Append(';');
        AppendNum((c >> 8) & 255);
        _sb.Append(';');
        AppendNum(c & 255);
        _sb.Append('m');
    }

    void AppendNum(int v)
    {
        if (v >= 100) _sb.Append((char)('0' + v / 100));
        if (v >= 10) _sb.Append((char)('0' + v / 10 % 10));
        _sb.Append((char)('0' + v % 10));
    }
}

public sealed class Fx
{
    struct P
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;
        public double Max;
        public double Grav;
        public double Drag;
        public int C1;
        public int C2;
        public char Ch;
    }

    readonly List<P> _parts = new();
    public readonly Random Rng = new();
    public double Time;

    public void Spawn(double x, double y, double vx, double vy, double life, int c1, int c2,
        char ch = '\0', double grav = 0, double drag = 0.5)
    {
        _parts.Add(new P { X = x, Y = y, Vx = vx, Vy = vy, Life = life, Max = life, Grav = grav, Drag = drag, C1 = c1, C2 = c2, Ch = ch });
        while (_parts.Count > 900) _parts.RemoveAt(0);
    }

    public void Burst(double x, double y, int n, int c1, int? c2 = null, double speed = 14,
        double life = 0.6, double grav = 0, double drag = 2.5, char ch = '\0')
    {
        var cc2 = c2 ?? c1;
        for (var i = 0; i < n; i++)
        {
            var a = Rng.NextDouble() * Math.PI * 2;
            var sp = speed * (0.25 + Rng.NextDouble() * 0.95);
            Spawn(x, y, Math.Cos(a) * sp, Math.Sin(a) * sp, life * (0.5 + Rng.NextDouble() * 0.7),
                c1, cc2, ch, grav, drag);
        }
    }

    public void Ring(double x, double y, int n, double speed, double life, int c1, int? c2 = null, char ch = '·')
    {
        var cc2 = c2 ?? c1;
        for (var i = 0; i < n; i++)
        {
            var a = i * Math.PI * 2 / n;
            Spawn(x, y, Math.Cos(a) * speed, Math.Sin(a) * speed, life, c1, cc2, ch, 0, 1.2);
        }
    }

    public void Update(double dt)
    {
        Time += dt;
        for (var i = _parts.Count - 1; i >= 0; i--)
        {
            var p = _parts[i];
            p.Life -= dt;
            if (p.Life <= 0)
            {
                _parts.RemoveAt(i);
                continue;
            }
            p.Vy += p.Grav * dt;
            var d = 1.0 / (1.0 + p.Drag * dt);
            p.Vx *= d;
            p.Vy *= d;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            _parts[i] = p;
        }
    }

    public void Draw(Canvas cv)
    {
        foreach (var p in _parts)
        {
            var t = Math.Max(0, Math.Min(1, p.Life / p.Max));
            var c = p.Ch != '\0' ? p.Ch : t > 0.72 ? '█' : t > 0.45 ? '▓' : t > 0.22 ? '▒' : '░';
            cv.Set((int)Math.Round(p.X), (int)Math.Round(p.Y), c, Col.Mix(p.C2, p.C1, t));
        }
    }
}

public static class Input
{
    static readonly HashSet<ConsoleKey> _hit = new();

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    public static void Poll()
    {
        _hit.Clear();
        try
        {
            while (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true);
                _hit.Add(k.Key);
            }
        }
        catch { }
    }

    public static bool Hit(ConsoleKey k) => _hit.Contains(k);
    public static bool AnyHit => _hit.Count > 0;
    public static bool Quit => Hit(ConsoleKey.Q);

    public static bool Held(ConsoleKey k)
    {
        if (!OperatingSystem.IsWindows()) return _hit.Contains(k);
        try { return (GetAsyncKeyState((int)k) & 0x8000) != 0; }
        catch { return _hit.Contains(k); }
    }

    public static bool HeldAny(params ConsoleKey[] keys)
    {
        foreach (var k in keys) if (Held(k)) return true;
        return false;
    }
}

public static class Store
{
    static string Dir()
    {
        var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "term-games");
        Directory.CreateDirectory(d);
        return d;
    }

    public static int Best(string id)
    {
        try
        {
            var p = Path.Combine(Dir(), id + ".score");
            return File.Exists(p) && int.TryParse(File.ReadAllText(p), out var v) ? v : 0;
        }
        catch { return 0; }
    }

    public static void Save(string id, int v)
    {
        try { File.WriteAllText(Path.Combine(Dir(), id + ".score"), v.ToString()); } catch { }
    }
}

public static class Col
{
    public static int Rgb(int r, int g, int b) => (Clamp(r) << 16) | (Clamp(g) << 8) | Clamp(b);
    static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    public static int Mix(int a, int b, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return Rgb(
            (int)(((a >> 16) & 255) + (((b >> 16) & 255) - ((a >> 16) & 255)) * t),
            (int)(((a >> 8) & 255) + (((b >> 8) & 255) - ((a >> 8) & 255)) * t),
            (int)((a & 255) + ((b & 255) - (a & 255)) * t));
    }

    public static int Scale(int a, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return Rgb((int)(((a >> 16) & 255) * t), (int)(((a >> 8) & 255) * t), (int)((a & 255) * t));
    }

    public static int Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        if (s < 0) s = 0;
        if (s > 1) s = 1;
        if (l < 0) l = 0;
        if (l > 1) l = 1;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Rgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}

public static class Ui
{
    public static void Bar(Canvas cv, int x, int y, int width, double frac, int c1, int c2, char full = '█', char empty = '░')
    {
        frac = Math.Clamp(frac, 0, 1);
        var n = (int)Math.Round(width * frac);
        for (var i = 0; i < width; i++)
            cv.Set(x + i, y, i < n ? full : empty, i < n ? Col.Mix(c1, c2, width <= 1 ? 0 : (double)i / (width - 1)) : 0x2a3040);
    }

    public static string[] Wrap(string s, int width)
    {
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (var word in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur.Length > 0 && cur.Length + 1 + word.Length > width)
            {
                lines.Add(cur.ToString());
                cur.Clear();
            }
            if (cur.Length > 0) cur.Append(' ');
            if (word.Length > width)
            {
                if (cur.Length > 0) { lines.Add(cur.ToString()); cur.Clear(); }
                for (var i = 0; i < word.Length; i += width)
                    lines.Add(word.Substring(i, Math.Min(width, word.Length - i)));
            }
            else cur.Append(word);
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        return lines.ToArray();
    }

    public static int WrapDraw(Canvas cv, int x, int y, string s, int width, int color, int maxLines = 99)
    {
        var lines = Wrap(s, width);
        for (var i = 0; i < lines.Length && i < maxLines; i++) cv.Text(x, y + i, lines[i], color);
        return lines.Length;
    }
}
