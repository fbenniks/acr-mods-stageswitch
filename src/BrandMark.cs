using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AcrFfbGain
{
    /// <summary>
    /// Simplified car-make marks drawn in code (the game's own artwork is packed and not readable).
    /// Unknown makes get a neutral tile with initials.
    /// </summary>
    public static class BrandMark
    {
        static readonly string[][] Brands =
        {
            // normalised prefix, brand key
            new[] { "alfaromeo", "alfa" },
            new[] { "alpine", "alpine" },
            new[] { "audi", "audi" },
            new[] { "citroen", "citroen" },
            new[] { "fiat", "fiat" },
            new[] { "hyundai", "hyundai" },
            new[] { "lancia", "lancia" },
            new[] { "mini", "mini" },
            new[] { "peugeot", "peugeot" },
            new[] { "skoda", "skoda" },
            new[] { "subaru", "subaru" },
            new[] { "volkswagen", "vw" },
            new[] { "vw", "vw" },
        };

        public static string BrandOf(string carIdOrName)
        {
            string n = StageScanner.Norm(carIdOrName);
            foreach (var b in Brands) if (n.StartsWith(b[0])) return b[1];
            return null;
        }

        /// <summary>Draws the mark for a car inside <paramref name="r"/> (about 3:2, e.g. 33x22).</summary>
        public static void Draw(Graphics g, RectangleF r, string carIdOrName)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            string brand = BrandOf(carIdOrName);
            switch (brand)
            {
                case "audi": Audi(g, r); break;
                case "citroen": Citroen(g, r); break;
                case "subaru": Subaru(g, r); break;
                case "vw": Vw(g, Square(r)); break;
                case "hyundai": Hyundai(g, r); break;
                case "alpine": Alpine(g, Square(r)); break;
                case "mini": Mini(g, r); break;
                case "fiat": Fiat(g, r); break;
                case "lancia": Lancia(g, Square(r)); break;
                case "peugeot": Peugeot(g, Square(r)); break;
                case "skoda": Skoda(g, Square(r)); break;
                case "alfa": Alfa(g, Square(r)); break;
                default: Unknown(g, r, carIdOrName); break;
            }
            g.Restore(state);
        }

        /// <summary>Small bitmap for menus.</summary>
        public static Bitmap Bitmap(string carIdOrName, int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Draw(g, new RectangleF(0.5f, 0.5f, w - 1, h - 1), carIdOrName);
            }
            return bmp;
        }

        // ------------------------------------------------------------------ helpers

        static RectangleF Square(RectangleF r)
        {
            float s = Math.Min(r.Width, r.Height);
            return new RectangleF(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        static readonly Color Silver = C(214, 218, 226);

        static void Text(Graphics g, string s, RectangleF r, Color c, float sizePx, FontStyle style, float dy = 0)
        {
            using (var f = new Font("Segoe UI", sizePx, style, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(s, f, b, new RectangleF(r.X - 4, r.Y + dy, r.Width + 8, r.Height), sf);
        }

        static PointF[] Star(float cx, float cy, float outer, float inner)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
            }
            return pts;
        }

        static GraphicsPath Shield(RectangleF r)
        {
            var p = new GraphicsPath();
            p.AddLine(r.X + r.Width * 0.12f, r.Y + r.Height * 0.06f, r.Right - r.Width * 0.12f, r.Y + r.Height * 0.06f);
            p.AddLine(r.Right - r.Width * 0.12f, r.Y + r.Height * 0.06f, r.Right - r.Width * 0.12f, r.Y + r.Height * 0.55f);
            p.AddBezier(r.Right - r.Width * 0.12f, r.Y + r.Height * 0.55f, r.Right - r.Width * 0.14f, r.Y + r.Height * 0.8f, r.X + r.Width * 0.62f, r.Y + r.Height * 0.9f, r.X + r.Width * 0.5f, r.Bottom - r.Height * 0.02f);
            p.AddBezier(r.X + r.Width * 0.5f, r.Bottom - r.Height * 0.02f, r.X + r.Width * 0.38f, r.Y + r.Height * 0.9f, r.X + r.Width * 0.14f, r.Y + r.Height * 0.8f, r.X + r.Width * 0.12f, r.Y + r.Height * 0.55f);
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------------ marks

        static void Audi(Graphics g, RectangleF r)
        {
            float d = r.Width * 0.34f, step = r.Width * 0.215f;
            float x0 = r.X + (r.Width - (d + 3 * step)) / 2, y = r.Y + (r.Height - d) / 2;
            using (var p = new Pen(Silver, Math.Max(1.2f, r.Width * 0.055f)))
                for (int i = 0; i < 4; i++) g.DrawEllipse(p, x0 + i * step, y, d, d);
        }

        static void Citroen(Graphics g, RectangleF r)
        {
            using (var path = Theme.Round(r, r.Height * 0.25f))
            using (var b = new SolidBrush(C(218, 41, 28))) g.FillPath(b, path);
            float cx = r.X + r.Width / 2, w = r.Width * 0.30f, h = r.Height * 0.16f;
            using (var p = new Pen(Color.White, Math.Max(1.6f, r.Height * 0.13f)) { LineJoin = LineJoin.Miter, StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                foreach (float cy in new[] { r.Y + r.Height * 0.40f, r.Y + r.Height * 0.68f })
                    g.DrawLines(p, new[] { new PointF(cx - w, cy + h / 2), new PointF(cx, cy - h), new PointF(cx + w, cy + h / 2) });
            }
        }

        static void Subaru(Graphics g, RectangleF r)
        {
            using (var b = new SolidBrush(C(10, 46, 120))) g.FillEllipse(b, r);
            using (var p = new Pen(Silver, 1f)) g.DrawEllipse(p, r);
            using (var b = new SolidBrush(Color.White))
            {
                float u = r.Height;
                g.FillPolygon(b, Star(r.X + r.Width * 0.32f, r.Y + u * 0.42f, u * 0.20f, u * 0.08f));
                var small = new[] { new PointF(0.56f, 0.30f), new PointF(0.70f, 0.40f), new PointF(0.62f, 0.58f), new PointF(0.78f, 0.62f), new PointF(0.48f, 0.66f) };
                foreach (var s in small) g.FillPolygon(b, Star(r.X + r.Width * s.X, r.Y + u * s.Y, u * 0.09f, u * 0.037f));
            }
        }

        static void Vw(Graphics g, RectangleF s)
        {
            using (var b = new SolidBrush(C(0, 30, 80))) g.FillEllipse(b, s);
            float pw = Math.Max(1.2f, s.Width * 0.07f);
            using (var p = new Pen(Color.White, pw)) g.DrawEllipse(p, s.X + pw / 2 + 0.5f, s.Y + pw / 2 + 0.5f, s.Width - pw - 1, s.Height - pw - 1);
            Func<float, float, PointF> P = (x, y) => new PointF(s.X + x * s.Width, s.Y + y * s.Height);
            using (var p = new Pen(Color.White, Math.Max(1.1f, s.Width * 0.065f)) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(p, new[] { P(0.33f, 0.18f), P(0.5f, 0.52f), P(0.67f, 0.18f) });
                g.DrawLines(p, new[] { P(0.17f, 0.36f), P(0.34f, 0.80f), P(0.5f, 0.50f), P(0.66f, 0.80f), P(0.83f, 0.36f) });
            }
        }

        static void Hyundai(Graphics g, RectangleF r)
        {
            using (var b = new SolidBrush(C(0, 44, 95))) g.FillEllipse(b, r);
            using (var p = new Pen(Silver, 1.2f)) g.DrawEllipse(p, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            Text(g, "H", r, Color.White, r.Height * 0.62f, FontStyle.Bold | FontStyle.Italic);
        }

        static void Alpine(Graphics g, RectangleF s)
        {
            using (var b = new SolidBrush(C(0, 88, 164))) g.FillEllipse(b, s);
            Func<float, float, PointF> P = (x, y) => new PointF(s.X + x * s.Width, s.Y + y * s.Height);
            using (var p = new Pen(Color.White, Math.Max(1.3f, s.Width * 0.09f)) { LineJoin = LineJoin.Miter })
            {
                g.DrawLines(p, new[] { P(0.28f, 0.76f), P(0.5f, 0.22f), P(0.72f, 0.76f) });
                // the arrow through the A
                g.DrawLine(p, P(0.22f, 0.60f), P(0.70f, 0.48f));
            }
            using (var b = new SolidBrush(Color.White))
                g.FillPolygon(b, new[] { P(0.84f, 0.45f), P(0.66f, 0.38f), P(0.70f, 0.56f) });
        }

        static void Mini(Graphics g, RectangleF r)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = r.Height * 0.36f;
            float pw = Math.Max(1.2f, r.Height * 0.08f);
            using (var p = new Pen(Silver, pw))
            {
                g.DrawEllipse(p, cx - rad, cy - rad, 2 * rad, 2 * rad);
                // wings: three tapering lines each side
                for (int i = 0; i < 3; i++)
                {
                    float y = cy - rad * 0.45f + i * rad * 0.45f;
                    float len = r.Width * (0.26f - i * 0.05f);
                    g.DrawLine(p, cx - rad - 1.5f, y, cx - rad - 1.5f - len, y - rad * 0.12f);
                    g.DrawLine(p, cx + rad + 1.5f, y, cx + rad + 1.5f + len, y - rad * 0.12f);
                }
            }
            Text(g, "M", new RectangleF(cx - rad, cy - rad, 2 * rad, 2 * rad), Silver, rad * 1.05f, FontStyle.Bold);
        }

        static void Fiat(Graphics g, RectangleF r)
        {
            using (var path = Theme.Round(r, r.Height * 0.2f))
            using (var b = new SolidBrush(C(181, 18, 27))) g.FillPath(b, path);
            Text(g, "FIAT", r, Color.White, r.Height * 0.5f, FontStyle.Bold, 0.5f);
        }

        static void Lancia(Graphics g, RectangleF s)
        {
            using (var path = Shield(s))
            {
                using (var b = new SolidBrush(C(10, 58, 117))) g.FillPath(b, path);
                using (var p = new Pen(Silver, 1.1f)) g.DrawPath(p, path);
            }
            Text(g, "L", s, Color.White, s.Height * 0.55f, FontStyle.Bold, -s.Height * 0.04f);
        }

        static void Peugeot(Graphics g, RectangleF s)
        {
            using (var path = Shield(s))
            {
                using (var b = new SolidBrush(C(28, 30, 36))) g.FillPath(b, path);
                using (var p = new Pen(Silver, 1.2f)) g.DrawPath(p, path);
            }
            Text(g, "P", s, Silver, s.Height * 0.55f, FontStyle.Bold, -s.Height * 0.04f);
        }

        static void Skoda(Graphics g, RectangleF s)
        {
            using (var b = new SolidBrush(C(78, 168, 46))) g.FillEllipse(b, s);
            Func<float, float, PointF> P = (x, y) => new PointF(s.X + x * s.Width, s.Y + y * s.Height);
            using (var b = new SolidBrush(Color.White))
            {
                // winged arrow pointing right
                g.FillPolygon(b, new[] { P(0.16f, 0.36f), P(0.52f, 0.46f), P(0.56f, 0.34f), P(0.84f, 0.52f), P(0.52f, 0.66f), P(0.50f, 0.56f), P(0.22f, 0.50f) });
            }
            using (var b = new SolidBrush(C(78, 168, 46))) g.FillEllipse(b, s.X + s.Width * 0.58f, s.Y + s.Height * 0.46f, s.Width * 0.1f, s.Height * 0.1f);
        }

        static void Alfa(Graphics g, RectangleF s)
        {
            var inner = new RectangleF(s.X + s.Width * 0.12f, s.Y + s.Height * 0.12f, s.Width * 0.76f, s.Height * 0.76f);
            using (var b = new SolidBrush(C(30, 60, 110))) g.FillEllipse(b, s);
            var state = g.Save();
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(inner);
                g.SetClip(clip);
                // left: red cross on white, right: green serpent on light blue
                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, inner.X, inner.Y, inner.Width / 2, inner.Height);
                using (var b = new SolidBrush(C(160, 200, 230))) g.FillRectangle(b, inner.X + inner.Width / 2, inner.Y, inner.Width / 2, inner.Height);
                using (var b = new SolidBrush(C(200, 16, 46)))
                {
                    g.FillRectangle(b, inner.X, inner.Y + inner.Height * 0.42f, inner.Width / 2, inner.Height * 0.16f);
                    g.FillRectangle(b, inner.X + inner.Width * 0.18f, inner.Y, inner.Width * 0.14f, inner.Height);
                }
                using (var p = new Pen(C(0, 120, 60), Math.Max(1.2f, s.Width * 0.08f)))
                    g.DrawBezier(p, inner.X + inner.Width * 0.62f, inner.Y + inner.Height * 0.15f, inner.X + inner.Width * 0.95f, inner.Y + inner.Height * 0.35f,
                        inner.X + inner.Width * 0.55f, inner.Y + inner.Height * 0.6f, inner.X + inner.Width * 0.8f, inner.Y + inner.Height * 0.9f);
            }
            g.Restore(state);
            using (var p = new Pen(Silver, 1f)) g.DrawEllipse(p, s.X + 0.5f, s.Y + 0.5f, s.Width - 1, s.Height - 1);
        }

        static void Unknown(Graphics g, RectangleF r, string name)
        {
            using (var path = Theme.Round(r, r.Height * 0.25f))
            using (var b = new SolidBrush(Theme.Track)) g.FillPath(b, path);
            string n = (name ?? "?").Trim();
            string initials = n.Length >= 2 ? n.Substring(0, 2).ToUpperInvariant() : n.ToUpperInvariant();
            Text(g, initials, r, Theme.Text, r.Height * 0.48f, FontStyle.Bold);
        }
    }
}
