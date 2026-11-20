using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AcrFfbGain
{
    public enum Surface { Unknown, Gravel, Tarmac, Snow }

    /// <summary>
    /// Look of each rally location: country, flag (drawn in code), surface and colour.
    /// New locations from game updates fall back to a neutral tile with their initials.
    /// </summary>
    public class RallyStyle
    {
        public string Country;
        public Surface Surface;
        public Action<Graphics, RectangleF> Flag;

        static readonly Dictionary<string, RallyStyle> Known = new Dictionary<string, RallyStyle>(StringComparer.OrdinalIgnoreCase)
        {
            { "Wales",      new RallyStyle { Country = "Wales",  Surface = Surface.Gravel, Flag = DrawWales } },
            { "Sweden",     new RallyStyle { Country = "Sweden", Surface = Surface.Snow,   Flag = DrawSweden } },
            { "MonteCarlo", new RallyStyle { Country = "Monaco", Surface = Surface.Tarmac, Flag = DrawMonaco } },
            { "Greece",     new RallyStyle { Country = "Greece", Surface = Surface.Gravel, Flag = DrawGreece } },
            { "Alsace",     new RallyStyle { Country = "France", Surface = Surface.Tarmac, Flag = DrawFrance } },
        };

        public static RallyStyle For(string rallyId, string rallyName)
        {
            RallyStyle s;
            if (rallyId != null && Known.TryGetValue(rallyId, out s)) return s;
            string initials = Initials(rallyName ?? rallyId ?? "?");
            return new RallyStyle { Country = null, Surface = Surface.Unknown, Flag = (g, r) => DrawUnknown(g, r, initials) };
        }

        public static Color SurfaceColor(Surface s)
        {
            switch (s)
            {
                case Surface.Gravel: return Color.FromArgb(201, 140, 74);
                case Surface.Tarmac: return Color.FromArgb(140, 150, 170);
                case Surface.Snow: return Color.FromArgb(120, 200, 255);
                default: return Theme.Border;
            }
        }

        public static string SurfaceName(Surface s)
        {
            return s == Surface.Unknown ? null : s.ToString();
        }

        /// <summary>Draws the flag with rounded corners and a hairline border.</summary>
        public void DrawFlag(Graphics g, RectangleF r)
        {
            var oldSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var clip = Theme.Round(r, 3f))
            {
                var state = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                Flag(g, r);
                g.Restore(state);
                using (var p = new Pen(Color.FromArgb(70, 0, 0, 0), 1f)) g.DrawPath(p, clip);
            }
            g.SmoothingMode = oldSmooth;
        }

        /// <summary>Small surface icon: pebbles for gravel, a dashed road for tarmac, a snowflake for snow.</summary>
        public static void DrawSurfaceIcon(Graphics g, RectangleF r, Surface s, Color c)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, u = Math.Min(r.Width, r.Height) / 10f;
            switch (s)
            {
                case Surface.Gravel:
                    using (var b = new SolidBrush(c))
                    {
                        g.FillEllipse(b, cx - 4.5f * u, cy + 0.5f * u, 4 * u, 3 * u);
                        g.FillEllipse(b, cx + 0.5f * u, cy + 1.0f * u, 4 * u, 3.2f * u);
                        g.FillEllipse(b, cx - 2.0f * u, cy - 3.5f * u, 4.2f * u, 3.4f * u);
                    }
                    break;
                case Surface.Tarmac:
                    using (var p = new Pen(c, 1.4f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(p, cx - 3.5f * u, cy + 4.5f * u, cx - 1.8f * u, cy - 4.5f * u);
                        g.DrawLine(p, cx + 3.5f * u, cy + 4.5f * u, cx + 1.8f * u, cy - 4.5f * u);
                    }
                    using (var p = new Pen(c, 1.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(p, cx, cy + 3.8f * u, cx, cy + 1.8f * u);
                        g.DrawLine(p, cx, cy - 0.6f * u, cx, cy - 2.4f * u);
                    }
                    break;
                case Surface.Snow:
                    using (var p = new Pen(c, 1.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            double a = Math.PI / 3 * i + Math.PI / 2;
                            float dx = (float)Math.Cos(a) * 4.5f * u, dy = (float)Math.Sin(a) * 4.5f * u;
                            g.DrawLine(p, cx - dx, cy - dy, cx + dx, cy + dy);
                        }
                    }
                    break;
            }
            g.SmoothingMode = old;
        }

        static string Initials(string name)
        {
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ flags

        static void Fill(Graphics g, Color c, float x, float y, float w, float h)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
        }

        static void DrawFrance(Graphics g, RectangleF r)
        {
            float w = r.Width / 3;
            Fill(g, Color.FromArgb(0, 85, 164), r.X, r.Y, w + 0.5f, r.Height);
            Fill(g, Color.White, r.X + w, r.Y, w + 0.5f, r.Height);
            Fill(g, Color.FromArgb(239, 65, 53), r.X + 2 * w, r.Y, w + 0.5f, r.Height);
        }

        static void DrawMonaco(Graphics g, RectangleF r)
        {
            Fill(g, Color.FromArgb(206, 17, 38), r.X, r.Y, r.Width, r.Height / 2 + 0.5f);
            Fill(g, Color.White, r.X, r.Y + r.Height / 2, r.Width, r.Height / 2);
        }

        static void DrawSweden(Graphics g, RectangleF r)
        {
            // 16:10 proportions: cross 2 units wide, vertical bar after 5 units
            var blue = Color.FromArgb(0, 106, 167);
            var yellow = Color.FromArgb(254, 204, 2);
            Fill(g, blue, r.X, r.Y, r.Width, r.Height);
            float uw = r.Width / 16f, uh = r.Height / 10f;
            Fill(g, yellow, r.X + 5 * uw, r.Y, 2 * uw, r.Height);
            Fill(g, yellow, r.X, r.Y + 4 * uh, r.Width, 2 * uh);
        }

        static void DrawGreece(Graphics g, RectangleF r)
        {
            var blue = Color.FromArgb(13, 94, 175);
            float sh = r.Height / 9f;
            for (int i = 0; i < 9; i++)
                Fill(g, i % 2 == 0 ? blue : Color.White, r.X, r.Y + i * sh, r.Width, sh + 0.3f);
            float cs = sh * 5; // square canton over the top 5 stripes
            Fill(g, blue, r.X, r.Y, cs, cs);
            Fill(g, Color.White, r.X + cs * 0.4f, r.Y, cs * 0.2f, cs);
            Fill(g, Color.White, r.X, r.Y + cs * 0.4f, cs, cs * 0.2f);
        }

        static void DrawWales(Graphics g, RectangleF r)
        {
            Fill(g, Color.White, r.X, r.Y, r.Width, r.Height / 2 + 0.5f);
            Fill(g, Color.FromArgb(0, 177, 64), r.X, r.Y + r.Height / 2, r.Width, r.Height / 2);

            // Simplified red dragon, facing left, in flag-relative coordinates.
            Func<float, float, PointF> P = (x, y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var red = Color.FromArgb(211, 7, 49);
            using (var b = new SolidBrush(red))
            using (var p = new Pen(red, Math.Max(1f, r.Height * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                // body
                g.FillEllipse(b, r.X + 0.34f * r.Width, r.Y + 0.44f * r.Height, 0.34f * r.Width, 0.22f * r.Height);
                // neck + head
                g.FillPolygon(b, new[] { P(0.38f, 0.52f), P(0.27f, 0.30f), P(0.20f, 0.30f), P(0.18f, 0.37f), P(0.25f, 0.38f), P(0.34f, 0.60f) });
                // wing
                g.FillPolygon(b, new[] { P(0.45f, 0.48f), P(0.50f, 0.16f), P(0.58f, 0.26f), P(0.66f, 0.14f), P(0.64f, 0.50f) });
                // legs
                g.DrawLine(p, P(0.40f, 0.64f), P(0.37f, 0.80f));
                g.DrawLine(p, P(0.62f, 0.64f), P(0.66f, 0.80f));
                // tail curling up
                g.DrawBezier(p, P(0.66f, 0.56f), P(0.80f, 0.62f), P(0.84f, 0.40f), P(0.80f, 0.30f));
            }
        }

        static void DrawUnknown(Graphics g, RectangleF r, string initials)
        {
            Fill(g, Theme.Track, r.X, r.Y, r.Width, r.Height);
            using (var f = new Font("Segoe UI", Math.Max(6f, r.Height * 0.45f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(Theme.Text))
            {
                var sz = g.MeasureString(initials, f);
                g.DrawString(initials, f, b, r.X + (r.Width - sz.Width) / 2, r.Y + (r.Height - sz.Height) / 2);
            }
        }
    }
}
