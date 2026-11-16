// Crpl2Analyzer - ADOFAI Creplay replay analyzer
// Copyright (C) 2026 InbottlesN235
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Crpl2
{
    public static class Theme
    {
        public static Color Bg = Color.FromArgb(18, 20, 27);
        public static Color Bg2 = Color.FromArgb(23, 26, 34);
        public static Color Panel = Color.FromArgb(29, 33, 44);
        public static Color Panel2 = Color.FromArgb(36, 41, 54);
        public static Color Border = Color.FromArgb(48, 54, 69);
        public static Color Text = Color.FromArgb(232, 236, 244);
        public static Color Dim = Color.FromArgb(150, 158, 174);
        public static Color Faint = Color.FromArgb(96, 104, 120);
        public static Color Accent = Color.FromArgb(86, 166, 226);
        public static Color Green = Color.FromArgb(61, 190, 122);
        public static Color Yellow = Color.FromArgb(242, 193, 78);
        public static Color Orange = Color.FromArgb(232, 138, 62);
        public static Color Red = Color.FromArgb(224, 91, 79);
        public static Color Purple = Color.FromArgb(155, 107, 214);
        public static Color Cyan = Color.FromArgb(72, 196, 199);

        public static Color JudgeColor(int version, int index)
        {
            if (version >= 3)
            {
                switch (index)
                {
                    case 0: return Color.FromArgb(214, 76, 68);    // 过早
                    case 1: return Color.FromArgb(230, 130, 60);   // 很过
                    case 2: return Color.FromArgb(244, 206, 96);   // 早完美
                    case 3: return Color.FromArgb(196, 214, 96);   // 完美-
                    case 4: return Color.FromArgb(61, 190, 122);   // X完美
                    case 5: return Color.FromArgb(196, 214, 96);   // 完美+
                    case 6: return Color.FromArgb(244, 206, 96);   // 晚完美
                    case 7: return Color.FromArgb(226, 110, 70);   // 很晚
                    case 8: return Color.FromArgb(190, 60, 60);    // 过晚
                }
                return index == 12 ? Color.FromArgb(86, 166, 226) : Color.FromArgb(120, 60, 110);
            }
            switch (index)
            {
                case 0: return Color.FromArgb(214, 76, 68);
                case 1: return Color.FromArgb(230, 130, 60);
                case 2: return Color.FromArgb(244, 206, 96);
                case 3: return Color.FromArgb(61, 190, 122);
                case 4: return Color.FromArgb(240, 178, 72);
                case 5: return Color.FromArgb(226, 110, 70);
                case 6: return Color.FromArgb(190, 60, 60);
            }
            return index == 10 ? Color.FromArgb(86, 166, 226) : Color.FromArgb(120, 60, 110);
        }

        public static readonly Color[] JudgeColors =
        {
            Color.FromArgb(214, 76, 68),    // 过早
            Color.FromArgb(230, 130, 60),   // 很过
            Color.FromArgb(244, 206, 96),   // 早完美
            Color.FromArgb(61, 190, 122),   // 完美
            Color.FromArgb(240, 178, 72),   // 晚完美
            Color.FromArgb(226, 110, 70),   // 很晚
            Color.FromArgb(190, 60, 60),    // 过晚
            Color.FromArgb(120, 60, 110),   // Miss
            Color.FromArgb(110, 130, 170), Color.FromArgb(110, 130, 170),
            Color.FromArgb(110, 130, 170), Color.FromArgb(110, 130, 170)
        };

        static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();
        static readonly string[] Families = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma" };
        static string FamilyName;

        public static string Family
        {
            get
            {
                if (FamilyName != null) return FamilyName;
                try
                {
                    var installed = new InstalledFontCollection();
                    var names = new HashSet<string>(installed.Families.Select(f => f.Name));
                    foreach (var f in Families)
                        if (names.Contains(f)) { FamilyName = f; return FamilyName; }
                }
                catch { }
                FamilyName = "Tahoma";
                return FamilyName;
            }
        }

        public static Font F(float size, bool bold)
        {
            string key = size.ToString("0.##") + (bold ? "b" : "n");
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            f = new Font(Family, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            FontCache[key] = f;
            return f;
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    public static class Chart
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(r, rad))
            using (var pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        public static void Panel(Graphics g, Rectangle r, float rad, Color fill, Color border)
        {
            Fill(g, r, rad, fill);
            if (border.A > 0) Stroke(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad, border, 1f);
        }

        public static Rectangle Card(Graphics g, Rectangle r, string title, string subtitle)
        {
            Panel(g, r, 10f, Theme.Panel, Theme.Border);
            float y = r.Y + 14;
            if (!string.IsNullOrEmpty(title))
            {
                g.DrawString(title, Theme.F(11.5f, true), new SolidBrush(Theme.Text), r.X + 18, y);
                y += 24;
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                g.DrawString(subtitle, Theme.F(8.5f, false), new SolidBrush(Theme.Faint), r.X + 18, y);
                y += 18;
            }
            int ch = (int)(r.Bottom - y - 20);
            if (ch < 30) ch = 30;
            return new Rectangle(r.X + 18, (int)y + 6, Math.Max(r.Width - 36, 40), ch);
        }

        public static string Ellipsis(Graphics g, string text, Font f, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (g.MeasureString(text, f).Width <= maxWidth) return text;
            string cur = text;
            while (cur.Length > 1 && g.MeasureString(cur + "…", f).Width > maxWidth)
                cur = cur.Substring(0, cur.Length - 1);
            return cur + "…";
        }

        public static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            g.DrawString(s, f, new SolidBrush(c), x, y);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(s, f, new SolidBrush(c), r, sf);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, float right, float y)
        {
            TextRight(g, s, f, c, right, y, 400f);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, float right, float y, float width)
        {
            if (width < 20) width = 20;
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(s, f, new SolidBrush(c), new RectangleF(right - width, y, width, f.Height + 6), sf);
        }

        // ---------------- badge ----------------
        public static void Badge(Graphics g, float x, float y, string text, Color color, out float width)
        {
            Font f = Theme.F(9f, true);
            var size = g.MeasureString(text, f);
            var r = new RectangleF(x, y, size.Width + 20, 24);
            Fill(g, r, 12f, Color.FromArgb(38, color));
            Stroke(g, r, 12f, Color.FromArgb(120, color), 1f);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, new SolidBrush(color), r, sf);
            width = r.Width + 8;
        }

        // ---------------- legend ----------------
        public static void Legend(Graphics g, float x, float y, string[] labels, Color[] colors, int columns, float colWidth)
        {
            Font f = Theme.F(9f, false);
            for (int k = 0; k < labels.Length; k++)
            {
                int col = k % columns, row = k / columns;
                float px = x + col * colWidth, py = y + row * 22;
                using (var b = new SolidBrush(colors[k]))
                    g.FillRectangle(b, px, py + 5, 10, 10);
                g.DrawString(labels[k], f, new SolidBrush(Theme.Dim), px + 16, py);
            }
        }

        // ---------------- vertical bar chart ----------------
        public static void BarChart(Graphics g, Rectangle r, string[] labels, double[] values, Color[] colors,
                                    bool showShare, string valueFormat)
        {
            if (values.Length == 0) return;
            double max = values.Max();
            if (max <= 0)
            {
                TextCenter(g, "无数据", Theme.F(11f, false), Theme.Faint, r);
                return;
            }
            double total = values.Sum();
            float gutter = 36f;
            float plotLeft = r.X + gutter;
            float labelH = 22f;
            float plotBottom = r.Bottom - labelH - 24;
            float plotTop = r.Y + 24;
            float plotH = plotBottom - plotTop;
            if (plotH < 24) return;
            float slot = (r.Right - plotLeft) / values.Length;
            float bw = Math.Min(slot * 0.56f, 76f);

            double niceMax, nstep;
            int nticks;
            NiceScale(max, 4, out niceMax, out nstep, out nticks);

            // gridlines
            for (int k = 0; k <= nticks; k++)
            {
                float y = plotBottom - plotH * k / (float)nticks;
                using (var pen = new Pen(Color.FromArgb(34, 255, 255, 255), 1f))
                    g.DrawLine(pen, plotLeft, y, r.Right, y);
                TextRight(g, Fmt(nstep * k), Theme.F(8f, false), Theme.Faint, plotLeft - 6, y - 7);
            }

            for (int k = 0; k < values.Length; k++)
            {
                float cx = plotLeft + slot * k + slot / 2f;
                float h = niceMax > 0 ? (float)(plotH * values[k] / niceMax) : 0;
                var bar = new RectangleF(cx - bw / 2f, plotBottom - h, bw, Math.Max(h, 0.1f));
                using (var p = Round(bar, Math.Min(6, bw / 3)))
                {
                    if (h > 1f)
                    {
                        using (var b = new LinearGradientBrush(new RectangleF(bar.X, bar.Y - 1, bar.Width, bar.Height + 2),
                                   colors[k], Color.FromArgb(150, colors[k]), 90f))
                            g.FillPath(b, p);
                    }
                }
                string vtxt = values[k] > 0 ? values[k].ToString("0") : "0";
                if (showShare && total > 0)
                    vtxt += "  (" + (values[k] / total * 100).ToString("0.0") + "%)";
                if (values[k] > 0)
                {
                    float ly = plotBottom - h - 20;
                    if (ly < r.Y + 2) ly = plotBottom - h + 4;
                    TextCenter(g, vtxt, Theme.F(9.5f, true), colors[k],
                        new RectangleF(cx - slot * 0.95f, ly, slot * 1.9f, 20));
                }
                TextCenter(g, labels[k], Theme.F(9f, false), Theme.Dim,
                    new RectangleF(cx - slot / 2, plotBottom + 4, slot, 20));
            }

            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, plotLeft, plotBottom, r.Right, plotBottom);
        }

        public static void NiceScale(double max, int targetTicks, out double niceMax, out double step, out int ticks)
        {
            if (max <= 0) { niceMax = 1; step = 0.25; ticks = 4; return; }
            double raw = max / targetTicks;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double niceStep;
            if (norm <= 1) niceStep = 1;
            else if (norm <= 2) niceStep = 2;
            else if (norm <= 2.5) niceStep = 2.5;
            else if (norm <= 5) niceStep = 5;
            else niceStep = 10;
            step = niceStep * mag;
            niceMax = step * Math.Ceiling(max / step);
            ticks = (int)Math.Round(niceMax / step);
            if (ticks < 2) ticks = 2;
            if (ticks > 10) ticks = 10;
        }

        static string Fmt(double v)
        {
            if (v >= 1000) return (v / 1000).ToString("0.#") + "k";
            return v.ToString("0");
        }

        // ---------------- donut ----------------
        public static void Donut(Graphics g, RectangleF area, double[] values, Color[] colors,
                                 string centerTop, string centerBottom, string caption)
        {
            if (area.Width < 60 || area.Height < 60) return;
            float size = Math.Min(area.Width, area.Height);
            var rect = new RectangleF(area.X + (area.Width - size) / 2, area.Y + (area.Height - size) / 2, size, size);
            rect.Inflate(-6, -6);
            double total = values.Sum();
            if (total <= 0) total = 1;

            using (var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                g.FillEllipse(b, rect);

            float start = -90f;
            double acc = 0;
            for (int k = 0; k < values.Length; k++)
            {
                if (values[k] <= 0) continue;
                float sweep = (float)(360.0 * values[k] / total);
                acc += values[k];
                using (var b = new SolidBrush(colors[k]))
                    g.FillPie(b, rect.X, rect.Y, rect.Width, rect.Height, start, sweep);
                start += sweep;
            }

            float holeRatio = 0.62f;
            var hole = new RectangleF(rect.X + rect.Width * (1 - holeRatio) / 2,
                                      rect.Y + rect.Height * (1 - holeRatio) / 2,
                                      rect.Width * holeRatio, rect.Height * holeRatio);
            using (var b = new SolidBrush(Theme.Panel))
                g.FillEllipse(b, hole);

            var center = new RectangleF(hole.X, hole.Y + hole.Height * 0.30f, hole.Width, hole.Height * 0.30f);
            TextCenter(g, centerTop, Theme.F(22f, true), Theme.Text, center);
            TextCenter(g, caption, Theme.F(9f, false), Theme.Dim,
                new RectangleF(hole.X, hole.Y + hole.Height * 0.56f, hole.Width, 22));
            TextCenter(g, centerBottom, Theme.F(9f, false), Theme.Faint,
                new RectangleF(hole.X, hole.Y + hole.Height * 0.70f, hole.Width, 22));
        }

        // ---------------- early / late symmetry ----------------
        public static void Symmetric(Graphics g, Rectangle r, string[] cats, double[] early, double[] late)
        {
            double max = Math.Max(early.Max(), late.Max());
            if (max <= 0) max = 1;
            float labelW = 116f;
            float plotX = r.X + labelW;
            float plotW = r.Width - labelW;
            float axis = plotX + plotW / 2f;
            float rowH = (float)r.Height / cats.Length;
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, axis, r.Y, axis, r.Bottom);

            float half = plotW / 2f - 30;
            for (int k = 0; k < cats.Length; k++)
            {
                float cy = r.Y + rowH * k + rowH / 2f;
                float bh = Math.Min(rowH * 0.5f, 26f);
                float we = (float)(half * early[k] / max);
                float wl = (float)(half * late[k] / max);

                Chart.TextRight(g, cats[k], Theme.F(9f, false), Theme.Dim, plotX - 10, cy - 9);
                if (we > 1)
                    Fill(g, new RectangleF(axis - we, cy - bh / 2, Math.Max(we - 2, 1), bh), 4f, Theme.Orange);
                if (wl > 1)
                    Fill(g, new RectangleF(axis + 2, cy - bh / 2, wl, bh), 4f, Theme.Cyan);
                if (early[k] > 0) TextRight(g, early[k].ToString("0"), Theme.F(8.5f, true), Theme.Orange, axis - 6, cy - 8);
                if (late[k] > 0) Text(g, late[k].ToString("0"), Theme.F(8.5f, true), Theme.Cyan, axis + 8, cy - 8);
            }
            TextCenter(g, "早 (Early)", Theme.F(9f, true), Theme.Orange, new RectangleF(plotX, r.Y - 22, half, 18));
            TextCenter(g, "晚 (Late)", Theme.F(9f, true), Theme.Cyan, new RectangleF(axis, r.Y - 22, half, 18));
        }

        // ---------------- horizontal bars ----------------
        public static void HBars(Graphics g, Rectangle r, string[] labels, double[] values, Color[] colors,
                                 string suffix, bool showPct, string valueFormat)
        {
            if (values.Length == 0) return;
            double max = values.Max();
            if (max <= 0) max = 1;
            double total = values.Sum();
            float labelW = 96f;
            float valW = 132f;
            float rowH = Math.Min((float)r.Height / values.Length, 34f);
            float barW = r.Width - labelW - valW;
            Font f = Theme.F(9.5f, false);

            for (int k = 0; k < values.Length; k++)
            {
                float y = r.Y + rowH * k;
                if (y + rowH > r.Bottom) break;
                float bh = Math.Min(rowH - 10, 18f);
                float by = y + (rowH - bh) / 2;
                string lab = Ellipsis(g, labels[k], f, labelW - 8);
                g.DrawString(lab, f, new SolidBrush(Theme.Dim), r.X, by + 1);
                var bg = new RectangleF(r.X + labelW, by, barW, bh);
                Fill(g, bg, bh / 2, Color.FromArgb(26, 255, 255, 255));
                float w = (float)(barW * values[k] / max);
                if (w > 2)
                    Fill(g, new RectangleF(bg.X, by, w, bh), bh / 2, colors[k % colors.Length]);
                string vt = values[k].ToString(valueFormat) + suffix;
                if (showPct && total > 0) vt += "  " + (values[k] / total * 100).ToString("0.0") + "%";
                TextRight(g, vt, Theme.F(9f, false), Theme.Text, r.Right, by);
            }
        }

        // ---------------- area / density chart ----------------
        public static void Area(Graphics g, Rectangle r, double[] series, Color color, string xUnit)
        {
            if (series == null || series.Length == 0) return;
            double max = series.Max();
            if (max <= 0) max = 1;
            float plotBottom = r.Bottom - 22;
            float plotH = plotBottom - r.Y - 12;

            for (int k = 0; k <= 4; k++)
            {
                float y = plotBottom - plotH * k / 4f;
                using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
                    g.DrawLine(pen, r.X, y, r.Right, y);
                Text(g, Fmt(max * k / 4), Theme.F(8f, false), Theme.Faint, r.X - 2, y - 14);
            }

            var pts = new List<PointF>();
            float step = (float)r.Width / series.Length;
            for (int k = 0; k < series.Length; k++)
                pts.Add(new PointF(r.X + step * k + step / 2, plotBottom - (float)(plotH * series[k] / max)));

            using (var path = new GraphicsPath())
            {
                path.AddLines(pts.ToArray());
                path.AddLine(pts[pts.Count - 1].X, plotBottom, pts[0].X, plotBottom);
                path.CloseFigure();
                using (var brush = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, plotH),
                           Color.FromArgb(150, color), Color.FromArgb(16, color), 90f))
                    g.FillPath(brush, path);
            }
            if (pts.Count > 1)
                using (var pen = new Pen(color, 1.6f))
                    g.DrawLines(pen, pts.ToArray());

            int ticks = 8;
            for (int k = 0; k <= ticks; k++)
            {
                int idx = (int)((double)series.Length * k / ticks);
                if (idx >= series.Length) idx = series.Length - 1;
                float x = r.X + step * idx;
                TextCenter(g, idx + xUnit, Theme.F(8f, false), Theme.Faint,
                    new RectangleF(x - 26, plotBottom + 4, 52, 16));
            }
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, r.X, plotBottom, r.Right, plotBottom);
        }

        // ---------------- density chart: raw bars + smoothed line ----------------
        public static void DensityChart(Graphics g, Rectangle r, int[] raw, int smooth, Color barColor, Color lineColor)
        {
            if (raw == null || raw.Length == 0) return;
            double max = raw.Max();
            if (max <= 0) max = 1;
            float gutter = 36f;
            float plotLeft = r.X + gutter;
            float plotBottom = r.Bottom - 20;
            float plotTop = r.Y + 14;
            float plotH = plotBottom - plotTop;
            if (plotH < 20 || r.Right - plotLeft < 40) return;
            float step = (r.Right - plotLeft) / raw.Length;

            for (int k = 0; k <= 4; k++)
            {
                float y = plotBottom - plotH * k / 4f;
                using (var pen = new Pen(Color.FromArgb(32, 255, 255, 255), 1f))
                    g.DrawLine(pen, plotLeft, y, r.Right, y);
                TextRight(g, Fmt(max * k / 4), Theme.F(8f, false), Theme.Faint, plotLeft - 6, y - 7);
            }

            using (var b = new SolidBrush(barColor))
            {
                for (int k = 0; k < raw.Length; k++)
                {
                    if (raw[k] <= 0) continue;
                    float h = (float)(plotH * raw[k] / max);
                    float x = plotLeft + step * k;
                    g.FillRectangle(b, x + 0.4f, plotBottom - h, Math.Max(step - 0.9f, 1f), h);
                }
            }

            var sm = new double[raw.Length];
            int half = Math.Max(smooth / 2, 1);
            for (int k = 0; k < raw.Length; k++)
            {
                int a = Math.Max(0, k - half), z = Math.Min(raw.Length - 1, k + half);
                double sum = 0; int n = 0;
                for (int j = a; j <= z; j++) { sum += raw[j]; n++; }
                sm[k] = n > 0 ? sum / n : 0;
            }
            var pts = new List<PointF>();
            for (int k = 0; k < sm.Length; k++)
                pts.Add(new PointF(plotLeft + step * k + step / 2, plotBottom - (float)(plotH * sm[k] / max)));
            if (pts.Count > 1)
                using (var pen = new Pen(lineColor, 1.8f))
                    g.DrawLines(pen, pts.ToArray());

            int ticks = 10;
            for (int k = 0; k <= ticks; k++)
            {
                int idx = (int)((double)raw.Length * k / ticks);
                if (idx >= raw.Length) idx = raw.Length - 1;
                TextCenter(g, idx + "s", Theme.F(8f, false), Theme.Faint,
                    new RectangleF(plotLeft + step * idx - 24, plotBottom + 3, 48, 16));
            }
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, plotLeft, plotBottom, r.Right, plotBottom);
        }

        // ---------------- multi-series line chart ----------------
        public static void LineChart(Graphics g, Rectangle r, double[][] series, Color[] colors, string[] names,
                                     double yMin, double yMax, bool asPercent)
        {
            if (series == null || series.Length == 0 || series[0].Length < 2) return;
            float gutter = 56f;
            float px = r.X + gutter, pw = r.Right - px - 6;
            float py = r.Y + 26, ph = r.Bottom - py - 24;
            if (pw < 60 || ph < 40) return;
            double span = yMax - yMin;
            if (span <= 0) span = 1;

            for (int k = 0; k <= 4; k++)
            {
                float y = (float)(py + ph - ph * k / 4.0);
                using (var pen = new Pen(Color.FromArgb(32, 255, 255, 255), 1f))
                    g.DrawLine(pen, px, y, px + pw, y);
                double v = yMin + span * k / 4.0;
                string lab = asPercent ? (v * 100).ToString(span < 0.02 ? "0.00" : "0.0") + "%" : v.ToString("0.###");
                TextRight(g, lab, Theme.F(8f, false), Theme.Faint, px - 6, y - 7);
            }

            int n = series[0].Length;
            for (int s = 0; s < series.Length; s++)
            {
                var pts = new List<PointF>();
                for (int k = 0; k < n; k++)
                {
                    float x = px + pw * k / (float)(n - 1);
                    double v = (series[s][k] - yMin) / span;
                    float y = (float)(py + ph - ph * v);
                    if (y < py) y = py;
                    if (y > py + ph) y = py + ph;
                    pts.Add(new PointF(x, y));
                }
                if (pts.Count > 1)
                    using (var pen = new Pen(colors[s % colors.Length], s == 0 ? 2f : 1.2f))
                        g.DrawLines(pen, pts.ToArray());
            }

            int ticks = 8;
            for (int k = 0; k <= ticks; k++)
            {
                int idx = (int)((double)(n - 1) * k / ticks);
                TextCenter(g, "#" + (idx + 1).ToString(), Theme.F(8f, false), Theme.Faint,
                    new RectangleF(px + pw * k / ticks - 26, py + ph + 4, 52, 16));
            }
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, px, py + ph, px + pw, py + ph);

            float lx = px + 6;
            for (int s = 0; s < series.Length && s < names.Length; s++)
            {
                using (var b = new SolidBrush(colors[s % colors.Length]))
                    g.FillRectangle(b, lx, py - 18, 12, 3);
                float w = g.MeasureString(names[s], Theme.F(8.5f, false)).Width;
                g.DrawString(names[s], Theme.F(8.5f, false), new SolidBrush(Theme.Dim), lx + 16, py - 24);
                lx += 16 + w + 22;
            }
        }

        // ---------------- histogram ----------------
        public static void Histogram(Graphics g, Rectangle r, string[] binLabels, int[] counts, Color color)
        {
            double[] v = counts.Select(x => (double)x).ToArray();
            Color[] cs = counts.Select(_ => color).ToArray();
            BarChart(g, r, binLabels, v, cs, true, "0");
        }

        // ---------------- key/value table ----------------
        public static void Table(Graphics g, Rectangle r, List<KeyValuePair<string, string>> rows, int colW)
        {
            float rowH = Math.Min(26f, (float)r.Height / Math.Max(rows.Count, 1));
            bool compact = rowH < 23f;
            Font fk = Theme.F(compact ? 8f : 9f, false);
            Font fv = Theme.F(compact ? 8f : 9f, false);
            for (int k = 0; k < rows.Count; k++)
            {
                float y = r.Y + rowH * k;
                if (y + rowH > r.Bottom) break;
                if (k % 2 == 0)
                    Fill(g, new RectangleF(r.X, y, r.Width, rowH - 2), 4f, Color.FromArgb(18, 255, 255, 255));
                string k1 = Ellipsis(g, rows[k].Key, fk, colW - 10);
                g.DrawString(k1, fk, new SolidBrush(Theme.Dim), r.X + 8, y + (rowH - 2 - fk.Height) / 2);
                string v1 = Ellipsis(g, rows[k].Value, fv, r.Width - colW - 16);
                g.DrawString(v1, fv, new SolidBrush(Theme.Text), r.X + colW, y + (rowH - 2 - fv.Height) / 2);
            }
        }

        // ---------------- big number tile ----------------
        public static void Tile(Graphics g, Rectangle r, string caption, string value, string hint, Color accent)
        {
            Panel(g, r, 10f, Theme.Panel, Theme.Border);
            using (var b = new SolidBrush(accent))
                g.FillRectangle(b, r.X + 1, r.Y + 14, 3, r.Height - 28);
            Text(g, caption, Theme.F(9f, false), Theme.Dim, r.X + 18, r.Y + 12);
            Text(g, value, Theme.F(20f, true), Theme.Text, r.X + 16, r.Y + 30);
            if (!string.IsNullOrEmpty(hint))
                Text(g, hint, Theme.F(8.5f, false), Theme.Faint, r.X + 18, r.Bottom - 26);
        }
    }
}
