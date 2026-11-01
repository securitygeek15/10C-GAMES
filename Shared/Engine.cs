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

    // CRT-style post-processing, applied per cell while presenting.
    public double Scanlines;
    public double Vignette;
    double[]? _shade;
    double _shadeScan = -1;
    double _shadeVig = -1;

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

    /// <summary>Darkens everything drawn so far. Unlike OverlayK this only touches
    /// the current contents, so a card drawn afterwards stays at full brightness.</summary>
    public void Dim(double k)
    {
        if (k >= 0.999) return;
        for (var i = 0; i < _fg.Length; i++)
            if (_fg[i] != 0) _fg[i] = Col.Scale(_fg[i], k);
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
        var shade = Shade();
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
                if (shade != null && c != ' ' && fg != 0)
                {
                    var m = shade[i];
                    if (m < 0.999) fg = Col.Scale(fg, m);
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

    // Brightness multipliers for scanlines + vignette, rebuilt only when the
    // strength changes (toggling settings at runtime stays cheap).
    double[]? Shade()
    {
        if (Scanlines <= 0.001 && Vignette <= 0.001) return null;
        if (_shade != null && Math.Abs(_shadeScan - Scanlines) < 0.001 && Math.Abs(_shadeVig - Vignette) < 0.001)
            return _shade;
        _shade ??= new double[W * H];
        var cx = (W - 1) / 2.0;
        var cy = (H - 1) / 2.0;
        var maxD = Math.Sqrt(cx * cx + cy * cy);
        if (maxD <= 0) maxD = 1;
        for (var y = 0; y < H; y++)
        {
            var dy = (y - cy) / maxD;
            for (var x = 0; x < W; x++)
            {
                var dx = (x - cx) / maxD;
                var d = Math.Sqrt(dx * dx + dy * dy);
                var v = 1 - Vignette * d * d * 1.6;
                if (v < 0.15) v = 0.15;
                var s = Scanlines > 0.001 && (y & 1) == 1 ? 1 - 0.30 * Scanlines : 1.0;
                _shade[y * W + x] = v * s;
            }
        }
        _shadeScan = Scanlines;
        _shadeVig = Vignette;
        return _shade;
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

    /// <summary>Slow, leafy confetti rain from above the screen.</summary>
    public void Confetti(int n, double w, double h, int c1, int c2)
    {
        for (var i = 0; i < n; i++)
            Spawn(Rng.NextDouble() * w, -Rng.NextDouble() * h * 0.5,
                (Rng.NextDouble() - 0.5) * 3.5, 2 + Rng.NextDouble() * 4,
                3.2 + Rng.NextDouble() * 2.2, c1, c2, '\0', 5.5, 0.12);
    }

    /// <summary>Tiny quick sparkle.</summary>
    public void Spark(double x, double y, int c, int n = 8, double speed = 9)
        => Burst(x, y, n, c, null, speed, 0.32, 0, 4.5, '·');

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
    static readonly Dictionary<ConsoleKey, double> _heldAt = new();
    static double _clock;

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    public static void Poll()
    {
        _hit.Clear();
        _clock += 1.0 / 60;
        try
        {
            while (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true);
                _hit.Add(k.Key);
                _heldAt[k.Key] = _clock;
            }
        }
        catch { }
    }

    public static bool Hit(ConsoleKey k) => _hit.Contains(k);
    public static bool AnyHit => _hit.Count > 0;
    public static bool Quit => Hit(ConsoleKey.Q);

    public static bool Held(ConsoleKey k)
    {
        // Windows reports the physical key state. Terminals elsewhere only
        // repeat key events while a key is down, so treat a recent press as held.
        if (OperatingSystem.IsWindows())
            try
            {
                if ((GetAsyncKeyState((int)k) & 0x8000) != 0) return true;
            }
            catch { }
        if (_hit.Contains(k)) return true;
        return _heldAt.TryGetValue(k, out var at) && _clock - at < 0.22;
    }

    public static bool HeldAny(params ConsoleKey[] keys)
    {
        foreach (var k in keys) if (Held(k)) return true;
        return false;
    }

    // Shared intent mapping so every game reads the same keys.
    public static bool MenuUp => Hit(ConsoleKey.W) || Hit(ConsoleKey.UpArrow);
    public static bool MenuDown => Hit(ConsoleKey.S) || Hit(ConsoleKey.DownArrow);
    public static bool MenuLeft => Hit(ConsoleKey.A) || Hit(ConsoleKey.LeftArrow);
    public static bool MenuRight => Hit(ConsoleKey.D) || Hit(ConsoleKey.RightArrow);
    public static bool Confirm => Hit(ConsoleKey.Enter) || Hit(ConsoleKey.Spacebar);
    public static bool Back => Hit(ConsoleKey.Escape);
    public static bool Pause => Hit(ConsoleKey.Escape) || Hit(ConsoleKey.P);
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

    static Dictionary<string, int>? _cfg;

    public static int GetInt(string key, int def = 0)
    {
        var c = Cfg();
        return c.TryGetValue(key, out var v) ? v : def;
    }

    public static void SetInt(string key, int value)
    {
        var c = Cfg();
        c[key] = value;
        try
        {
            var sb = new StringBuilder();
            foreach (var kv in c) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            File.WriteAllText(Path.Combine(Dir(), "settings.cfg"), sb.ToString());
        }
        catch { }
    }

    static Dictionary<string, int> Cfg()
    {
        if (_cfg != null) return _cfg;
        _cfg = new Dictionary<string, int>();
        try
        {
            var p = Path.Combine(Dir(), "settings.cfg");
            if (!File.Exists(p)) return _cfg;
            foreach (var line in File.ReadAllLines(p))
            {
                var i = line.IndexOf('=');
                if (i > 0 && int.TryParse(line[(i + 1)..], out var v)) _cfg[line[..i]] = v;
            }
        }
        catch { }
        return _cfg;
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
    // Shared palette so every game's chrome matches.
    public const int Ink      = 0xE9EEF7;
    public const int InkDim   = 0x94A0B8;
    public const int InkFaint = 0x5B657A;
    public const int Line     = 0x2A3242;
    public const int Card     = 0x0D1119;
    public const int CardHi   = 0x151C28;
    public const int Pill     = 0x252C40;
    public const int Accent   = 0x9B7BFF;
    public const int Accent2  = 0x46E3C0;
    public const int Hot      = 0xFF4D6D;
    public const int Gold     = 0xFFD166;
    public const int Lime     = 0xC6FF3E;

    static readonly string[] DefaultRanks =
        { "cooked", "mid", "ok i guess", "decent", "slay", "ate", "aura farmer" };

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

    // ---- chrome -------------------------------------------------------

    /// <summary>Rounded card with a filled background and optional title in the top border.</summary>
    public static void Panel(Canvas cv, int x0, int y0, int x1, int y1, string? title = null,
        int accent = Accent, int bg = Card)
    {
        if (x1 - x0 < 3 || y1 - y0 < 2) return;
        cv.Fill(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ' ', bg);
        for (var x = x0 + 1; x < x1; x++)
        {
            cv.Set(x, y0, '─', Line);
            cv.Set(x, y1, '─', Line);
        }
        for (var y = y0 + 1; y < y1; y++)
        {
            cv.Set(x0, y, '│', Line);
            cv.Set(x1, y, '│', Line);
        }
        cv.Set(x0, y0, '╭', accent);
        cv.Set(x1, y0, '╮', accent);
        cv.Set(x0, y1, '╰', accent);
        cv.Set(x1, y1, '╯', accent);
        if (string.IsNullOrEmpty(title)) return;
        var label = "▸ " + title;
        for (var i = 0; i < label.Length && x0 + 2 + i < x1; i++)
        {
            var ch = label[i];
            cv.Set(x0 + 2 + i, y0, ch, ch == ' ' ? 0 : accent);
        }
        var end = x0 + 2 + label.Length;
        if (end < x1) cv.Set(end, y0, ' ', 0);
    }

    /// <summary>Draws a centred card clamped to the canvas and returns its bounds.</summary>
    public static (int X0, int Y0, int X1, int Y1) CenterCard(Canvas cv, int w, int h, string? title = null,
        int accent = Accent, int bg = Card)
    {
        w = Math.Min(w, cv.W - 2);
        h = Math.Min(h, cv.H - 2);
        var x0 = (cv.W - w) / 2;
        var y0 = (cv.H - h) / 2;
        Panel(cv, x0, y0, x0 + w - 1, y0 + h - 1, title, accent, bg);
        return (x0, y0, x0 + w - 1, y0 + h - 1);
    }

    /// <summary>Solid pill behind text. Returns the width drawn.</summary>
    public static int Badge(Canvas cv, int x, int y, string text, int fg, int bg)
    {
        var inner = " " + text + " ";
        cv.Fill(x, y, x + inner.Length - 1, y, ' ', bg);
        cv.Text(x, y, inner, fg);
        return inner.Length;
    }

    public static void Divider(Canvas cv, int x0, int x1, int y, int color = Line)
    {
        for (var x = Math.Max(0, x0); x <= x1 && x < cv.W; x++) cv.Set(x, y, '─', color);
    }

    /// <summary>Key + label pair, e.g. [SPACE] dash. Returns the width drawn.</summary>
    public static int KeyHint(Canvas cv, int x, int y, string key, string label,
        int accent = Accent, int labelCol = InkDim)
    {
        var k = " " + key + " ";
        cv.Fill(x, y, x + k.Length - 1, y, ' ', Pill);
        cv.Text(x, y, k, accent);
        cv.Text(x + k.Length + 1, y, label, labelCol);
        return k.Length + 1 + label.Length;
    }

    /// <summary>Centred footer of key hints. Pass key, label, key, label…</summary>
    public static void Hints(Canvas cv, int y, params string[] pairs)
    {
        if (y < 0 || y >= cv.H) return;
        var total = 0;
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            total += pairs[i].Length + 2 + 1 + pairs[i + 1].Length + 3;
        var x = Math.Max(1, (cv.W - total) / 2);
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            x += KeyHint(cv, x, y, pairs[i], pairs[i + 1]) + 3;
    }

    public static void ScoreRow(Canvas cv, int x0, int x1, int y, string label, string value,
        int valueCol = Ink, int labelCol = InkDim)
    {
        cv.Text(x0, y, label, labelCol);
        cv.TextRight(value, x1, y, valueCol);
    }

    public static void TextShadow(Canvas cv, int x, int y, string s, int col, int shadow = 0x05070B)
    {
        cv.Text(x + 1, y + 1, s, shadow);
        cv.Text(x, y, s, col);
    }

    public static void TextShadowC(Canvas cv, int y, string s, int col, int shadow = 0x05070B)
    {
        var x = (cv.W - s.Length) / 2;
        cv.Text(x + 1, y + 1, s, shadow);
        cv.Text(x, y, s, col);
    }

    /// <summary>Trackspaced gradient title with a hard shadow.</summary>
    public static void BigTitle(Canvas cv, int y, string text, int c1, int c2, double t = 0)
    {
        var sb = new StringBuilder(text.Length * 2);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(text[i]);
        }
        var s = sb.ToString();
        var x = (cv.W - s.Length) / 2;
        for (var i = 0; i < s.Length; i++)
            if (s[i] != ' ') cv.Set(x + i + 1, y + 1, s[i], 0x05070B);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == ' ') continue;
            var u = s.Length <= 1 ? 0 : (double)i / (s.Length - 1);
            cv.Set(x + i, y, s[i], Col.Mix(c1, c2, (u + t * 0.08) % 1.0));
        }
    }

    /// <summary>Scrolling marquee across the full width.</summary>
    public static void Ticker(Canvas cv, int y, string text, double t, int c1, int c2,
        double speed = 9, string sep = "   ✦   ")
    {
        if (text.Length == 0) return;
        var unit = text + sep;
        var sb = new StringBuilder();
        while (sb.Length < cv.W + unit.Length) sb.Append(unit);
        var full = sb.ToString();
        var off = (int)(t * speed) % full.Length;
        for (var x = 0; x < cv.W; x++)
        {
            var ch = full[(off + x) % full.Length];
            var u = cv.W <= 1 ? 0 : (double)x / (cv.W - 1);
            cv.Set(x, y, ch, ch == ' ' ? 0 : Col.Mix(c1, c2, u));
        }
    }

    /// <summary>Gen Z rank for a score, scaled by the game's own thresholds.</summary>
    public static (string Name, int Tier) Rank(int value, int[] cuts, string[]? names = null)
    {
        var n = names ?? DefaultRanks;
        var tier = 0;
        for (var i = 0; i < cuts.Length; i++)
            if (value >= cuts[i]) tier = Math.Min(i + 1, n.Length - 1);
        return (n[tier], tier);
    }

    static readonly int[] RankRamp = { 0x6B7385, 0x94A0B8, 0x46E3C0, 0x9B7BFF, 0xFFD166, 0xFF4D6D, 0xC6FF3E };

    public static int RankColor(int tier) => RankRamp[Math.Clamp(tier, 0, RankRamp.Length - 1)];

    /// <summary>Pulsing gold ribbon shown when a run beats the saved best.</summary>
    public static void NewBest(Canvas cv, int cx, int y, double t)
    {
        const string label = "★ NEW PERSONAL BEST ★";
        var pulse = 0.5 + 0.5 * Math.Sin(t * 7);
        var bg = Col.Mix(Gold, 0xFFB020, pulse);
        var x = cx - (label.Length + 2) / 2;
        Badge(cv, x, y, label, 0x1A1205, bg);
    }

    public static void Fade(Canvas cv, double k, int color = 0x05070B)
    {
        cv.OverlayColor = color;
        cv.OverlayK = Math.Clamp(k, 0, 1);
    }

    public static double FadeIn(double t, double dur = 0.4) => t >= dur ? 0 : 1 - Math.Max(0, t) / dur;

    public static string OnOff(bool v) => v ? "on" : "off";
}

/// <summary>Simple vertical menu: wraps, takes 1-9 as shortcuts, Enter/Space to pick.</summary>
public sealed class Menu
{
    public readonly List<string> Items;
    public int Index;
    public int X = -1;
    public int Accent = Ui.Accent;
    int _w = 18;

    public Menu(params string[] items) => Items = new List<string>(items);

    public string Selected => Items.Count == 0 ? "" : Items[Index];
    public int Height => Items.Count;

    /// <summary>Returns the chosen index, or -1 while the menu is still open.</summary>
    public int Update()
    {
        if (Items.Count == 0) return -1;
        if (Input.MenuUp) Index = (Index - 1 + Items.Count) % Items.Count;
        else if (Input.MenuDown) Index = (Index + 1) % Items.Count;
        for (var i = 0; i < Items.Count && i < 9; i++)
            if (Input.Hit(ConsoleKey.D1 + i))
            {
                Index = i;
                return i;
            }
        return Input.Confirm ? Index : -1;
    }

    public void Draw(Canvas cv, int y)
    {
        if (Items.Count == 0) return;
        var widest = 0;
        foreach (var it in Items) widest = Math.Max(widest, it.Length);
        _w = Math.Clamp(widest + 8, 18, Math.Max(18, cv.W - 4));
        var x = X >= 0 ? Math.Clamp(X, 1, Math.Max(1, cv.W - _w - 1)) : (cv.W - _w) / 2;
        for (var i = 0; i < Items.Count; i++)
        {
            var yy = y + i;
            if (yy < 0 || yy >= cv.H) continue;
            var sel = i == Index;
            cv.Fill(x, yy, x + _w - 1, yy, ' ', sel ? Ui.Pill : Ui.Card);
            var pad = Math.Max(0, (widest - Items[i].Length) / 2);
            cv.Text(x + 4 + pad, yy, Items[i], sel ? Ui.Ink : Ui.InkDim);
            if (!sel) continue;
            cv.Set(x + 1, yy, '▸', Accent);
            cv.Set(x + _w - 2, yy, '◂', Accent);
        }
    }
}

/// <summary>Transient top-right notifications.</summary>
public sealed class Toasts
{
    struct Item
    {
        public string Text;
        public int Fg;
        public int Bg;
        public double Life;
        public double Max;
    }

    readonly List<Item> _items = new();

    public void Push(string text, int fg = Ui.Ink, int bg = Ui.CardHi, double life = 2.2)
    {
        _items.Add(new Item { Text = text, Fg = fg, Bg = bg, Life = life, Max = life });
        while (_items.Count > 5) _items.RemoveAt(0);
    }

    public void Update(double dt)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            it.Life -= dt;
            if (it.Life <= 0)
            {
                _items.RemoveAt(i);
                continue;
            }
            _items[i] = it;
        }
    }

    public void Draw(Canvas cv, int y = 1)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            var yy = y + i;
            if (yy >= cv.H) break;
            var a = Math.Clamp(it.Life / it.Max * 3, 0, 1);
            var w = it.Text.Length + 4;
            var x = Math.Max(0, cv.W - w - 1);
            cv.Fill(x, yy, x + w - 1, yy, ' ', Col.Scale(it.Bg, 0.55 + 0.45 * a));
            cv.Text(x + 2, yy, it.Text, Col.Scale(it.Fg, 0.3 + 0.7 * a));
        }
    }
}

/// <summary>Global, persisted presentation settings shared by every game.</summary>
public static class Settings
{
    public static bool Scanlines
    {
        get => Store.GetInt("scanlines", 1) != 0;
        set => Store.SetInt("scanlines", value ? 1 : 0);
    }

    public static bool Vignette
    {
        get => Store.GetInt("vignette", 1) != 0;
        set => Store.SetInt("vignette", value ? 1 : 0);
    }

    public static bool ReduceMotion
    {
        get => Store.GetInt("motion", 0) != 0;
        set => Store.SetInt("motion", value ? 1 : 0);
    }

    public static void Apply(Canvas cv)
    {
        var calm = ReduceMotion;
        cv.Scanlines = Scanlines && !calm ? 0.5 : 0;
        cv.Vignette = Vignette && !calm ? 0.32 : 0;
    }

    /// <summary>Pause menu entries, consistent across all ten games.</summary>
    public static Menu PauseMenu()
    {
        var m = new Menu("resume", "restart", "scanlines: " + Ui.OnOff(Scanlines), "vignette: " + Ui.OnOff(Vignette), "reduce motion: " + Ui.OnOff(ReduceMotion), "quit to title");
        return m;
    }

    /// <summary>Applies a pause-menu selection. Returns true when play should continue.</summary>
    public static bool PauseAction(int choice)
    {
        switch (choice)
        {
            case 0: return true;
            case 2:
                Scanlines = !Scanlines;
                return true;
            case 3:
                Vignette = !Vignette;
                return true;
            case 4:
                ReduceMotion = !ReduceMotion;
                return true;
            default: return false;
        }
    }

    public static void RefreshPauseLabels(Menu m)
    {
        if (m.Items.Count < 6) return;
        m.Items[2] = "scanlines: " + Ui.OnOff(Scanlines);
        m.Items[3] = "vignette: " + Ui.OnOff(Vignette);
        m.Items[4] = "reduce motion: " + Ui.OnOff(ReduceMotion);
    }
}
