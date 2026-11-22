// Crpl2Analyzer - ADOFAI Creplay replay analyzer
// Copyright (C) 2026 InbottlesN235
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Crpl2
{
    public class AppState
    {
        public List<Replay> Files = new List<Replay>();
        public int Selected;
        public Replay Current
        {
            get
            {
                if (Files.Count == 0) return null;
                if (Selected < 0 || Selected >= Files.Count) Selected = 0;
                return Files[Selected];
            }
        }
        public Replay Ok
        {
            get { var c = Current; return c != null && c.IsLoaded ? c : null; }
        }
    }

    public interface IView
    {
        string Title { get; }
        void Render(Graphics g, Rectangle r, AppState st);
    }

    public static class L
    {
        public static Rectangle[] Cols(Rectangle r, int gap, params float[] weights)
        {
            float total = weights.Sum();
            var res = new Rectangle[weights.Length];
            float x = r.X;
            float usable = r.Width - gap * (weights.Length - 1);
            for (int k = 0; k < weights.Length; k++)
            {
                float w = usable * weights[k] / total;
                res[k] = new Rectangle((int)x, r.Y, (int)w, r.Height);
                x += w + gap;
            }
            return res;
        }
        public static Rectangle[] Rows(Rectangle r, int gap, params float[] weights)
        {
            float total = weights.Sum();
            var res = new Rectangle[weights.Length];
            float y = r.Y;
            float usable = r.Height - gap * (weights.Length - 1);
            for (int k = 0; k < weights.Length; k++)
            {
                float h = usable * weights[k] / total;
                res[k] = new Rectangle(r.X, (int)y, r.Width, (int)h);
                y += h + gap;
            }
            return res;
        }
    }

    public static class Fmt
    {
        public static string Pct(double v, string f) { return (v * 100).ToString(f) + "%"; }
        public static string Int(double v) { return v.ToString("#,0"); }
        public static string Ms(double s) { return (s * 1000).ToString("0.0") + " ms"; }
    }

    // 判定序列的取用工具
    public static class Judge
    {
        public static int[] ChartIndices(Replay rp)
        {
            var list = rp.CoreIdx.Where(i => i < rp.Judgment.Length).ToList();
            for (int i = rp.CoreIdx.Max() + 1; i < rp.Judgment.Length; i++)
                if (rp.Judgment[i] > 0) list.Add(i);
            return list.ToArray();
        }

        public static string[] Labels(Replay rp, int[] idx) { return idx.Select(i => rp.Shorts[i]).ToArray(); }
        public static double[] Values(Replay rp, int[] idx) { return idx.Select(i => (double)rp.Judgment[i]).ToArray(); }
        public static Color[] Colors(Replay rp, int[] idx) { return idx.Select(i => Theme.JudgeColor(rp.FormatVersion, i)).ToArray(); }

        public static int Dominant(Replay rp, int[] idx)
        {
            int best = idx[0];
            foreach (int i in idx) if (rp.Judgment[i] > rp.Judgment[best]) best = i;
            return best;
        }
    }

    // ========================================================================
    public class OverviewView : IView
    {
        public string Title { get { return "总览"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }

            int tileH = 92, bottomH = 196;
            var tiles = L.Cols(new Rectangle(r.X, r.Y, r.Width, tileH), 14, 1, 1, 1, 1);

            Chart.Tile(g, tiles[0], "准确率 (X-Acc)", Fmt.Pct(rp.EffectiveAccuracy, "0.0000"), "以判定权重加权", Theme.Green);
            Chart.Tile(g, tiles[1], "击打总数", Fmt.Int(rp.HitCount), string.Format("完美档 {0} ({1})",
                Fmt.Int(rp.PerfectTierCount), Fmt.Pct(rp.HitCount > 0 ? (double)rp.PerfectTierCount / rp.HitCount : 0, "0.0")), Theme.Accent);
            Chart.Tile(g, tiles[2], "游戏时长", rp.DurationText, "最后一个事件 " + rp.LastEvent.ToString("0.0") + " s", Theme.Purple);
            Chart.Tile(g, tiles[3], "按键密度", rp.AvgPps.ToString("0.00") + " /s", "峰值 " + rp.MaxDensity + " /s", Theme.Orange);

            var midRow = new Rectangle(r.X, r.Y + tileH + 14, r.Width, Math.Max(r.Height - tileH - 28 - bottomH, 120));
            var mid = L.Cols(midRow, 14, 0.40f, 1f);

            var donutText = Chart.Card(g, mid[0], "准确率构成", "每次命中按判定权重贡献");
            int[] ci = Judge.ChartIndices(rp);
            var vals = Judge.Values(rp, ci);
            var keep = Enumerable.Range(0, ci.Length).Where(i => vals[i] > 0).ToArray();
            Chart.Donut(g, new RectangleF(donutText.X, donutText.Y, donutText.Width, donutText.Height - 48),
                keep.Select(i => vals[i]).ToArray(), keep.Select(i => Theme.JudgeColor(rp.FormatVersion, ci[i])).ToArray(),
                Fmt.Pct(rp.EffectiveAccuracy, "0.00"), "判定总数 " + Fmt.Int(rp.JudgmentTotal), "X-Acc");
            Chart.Legend(g, donutText.X + 4, donutText.Bottom - 38,
                keep.Select(i => rp.Shorts[ci[i]] + " " + vals[i].ToString("0")).ToArray(),
                keep.Select(i => Theme.JudgeColor(rp.FormatVersion, ci[i])).ToArray(), 3, donutText.Width / 3f);

            var barText = Chart.Card(g, mid[1], "判定分布", "各档判定的次数与占比" + (rp.FormatVersion >= 3
                    ? "（含 " + rp.Shorts[3] + " / " + rp.Shorts[4] + " / " + rp.Shorts[5] + " 拆分）" : ""));
            Chart.BarChart(g, barText, Judge.Labels(rp, ci), vals, Judge.Colors(rp, ci), true, "0");

            var botRow = new Rectangle(r.X, r.Bottom - bottomH, r.Width, bottomH);
            var bot = L.Cols(botRow, 14, 1.1f, 1f);
            var infoText = Chart.Card(g, bot[0], "谱面信息", null);
            var items = new List<KeyValuePair<string, string>>();
            items.Add(new KeyValuePair<string, string>("曲名", rp.SongName));
            items.Add(new KeyValuePair<string, string>("BPM", rp.Bpm > 0 ? rp.Bpm.ToString("0.###") : "-"));
            items.Add(new KeyValuePair<string, string>("倍速", rp.PlaybackSpeed.ToString("0.##") + "x"));
            items.Add(new KeyValuePair<string, string>("判定模式", rp.JudgeMode + (rp.GameVersion.Length > 0 ? "  ·  游戏 " + rp.GameVersion : "")));
            items.Add(new KeyValuePair<string, string>("楼层", rp.StartTile + " → " + rp.EndTile));
            items.Add(new KeyValuePair<string, string>("按下", Fmt.Int(rp.PressCount) + " 次 / " + rp.KeyUsage.Count + " 键"));
            items.Add(new KeyValuePair<string, string>("格式", "格式 " + rp.FormatVersion + "  ·  " + rp.ContainerMagic + " v" + rp.ContainerVersion));
            Chart.Table(g, infoText, items, 78);

            var badgeText = Chart.Card(g, bot[1], "标记", "由回放内的判定与标志位推断");
            float bx = badgeText.X, by = badgeText.Y, w;
            int miss = rp.MissCount;
            Chart.Badge(g, bx, by, miss == 0 ? "全连 " + Fmt.Int(rp.HitCount) : "Miss " + miss, miss == 0 ? Theme.Green : Theme.Red, out w); bx += w;
            Chart.Badge(g, bx, by, rp.AutoHits == 0 ? "无自动" : "自动 " + rp.AutoHits, rp.AutoHits == 0 ? Theme.Green : Theme.Orange, out w); bx += w;
            Chart.Badge(g, bx, by, rp.NoFailHits == 0 ? "无 NoFail" : "NoFail " + rp.NoFailHits, rp.NoFailHits == 0 ? Theme.Green : Theme.Orange, out w); bx += w;
            bx = badgeText.X; by += 30;
            bool cleared = rp.EndTile > 0 && rp.FloorIds != null && rp.FloorIds.Length > 0 && rp.FloorIds.Max() >= rp.EndTile - 2;
            Chart.Badge(g, bx, by, cleared ? "打完全谱" : "未到末面", cleared ? Theme.Green : Theme.Orange, out w); bx += w;
            Chart.Badge(g, bx, by, "中旋 " + rp.MidspinHits, Theme.Accent, out w); bx += w;
            Chart.Badge(g, bx, by, "双压 " + rp.ChordPairs, Theme.Purple, out w); bx += w;

            float ty = by + 30;
            Chart.Text(g, "判定权重", Theme.F(9f, true), Theme.Dim, badgeText.X, ty);
            Chart.Text(g, rp.FormatVersion >= 3
                    ? "完美类 1.00 ｜ 早/晚完美 0.75 ｜ 偏差档 0.40 ｜ 严重偏差 0.20"
                    : "完美 1.00 ｜ 早/晚完美 0.75 ｜ 偏差档 0.40 ｜ 严重偏差 0.20",
                Theme.F(8.5f, false), Theme.Faint, badgeText.X, ty + 15);
            Chart.Text(g, rp.HasHitErrors ? "本文件含逐次命中误差 → 可看「误差」页" : "本版本不含逐次命中误差",
                Theme.F(8.5f, false), rp.HasHitErrors ? Theme.Green : Theme.Faint, badgeText.X, ty + 30);
            Chart.Text(g, "回放不含「每次命中对应时刻」，逐次误差按砖块序号展示",
                Theme.F(8.5f, false), Theme.Faint, badgeText.X, ty + 45);
        }
    }

    // ========================================================================
    public class AccuracyView : IView
    {
        public string Title { get { return "准度分布"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }

            var rows = L.Rows(r, 14, 1.05f, 0.86f);
            var top = L.Cols(rows[0], 14, 1.30f, 1f);
            var bot = L.Cols(rows[1], 14, 1f, 1.30f);

            int[] ci = Judge.ChartIndices(rp);
            var vals = Judge.Values(rp, ci);

            var barRect = Chart.Card(g, top[0], "判定分布",
                "共 " + Fmt.Int(rp.JudgmentTotal) + " 击（格式 " + rp.FormatVersion + (rp.FormatVersion >= 3 ? "  ·  含 " + rp.Shorts[3] + " / " + rp.Shorts[4] + " / " + rp.Shorts[5] + " 拆分" : "") + "）");
            var split = L.Rows(barRect, 8, 1.12f, 0.88f);
            Chart.BarChart(g, split[0], Judge.Labels(rp, ci), vals, Judge.Colors(rp, ci), true, "0");

            // 放大：剔除出现最多的那一档（v2 = 完美，v3 = 中心档）
            int dom = Judge.Dominant(rp, ci);
            var zoomIdx = ci.Where(i => i != dom).ToArray();
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, barRect.X, split[1].Y - 4, barRect.Right, split[1].Y - 4);
            Chart.Text(g, "放大视图（排除「" + rp.Shorts[dom] + "」，线性比例）", Theme.F(8.5f, true), Theme.Dim, split[1].X, split[1].Y - 22);
            Chart.BarChart(g, new Rectangle(split[1].X, split[1].Y + 4, split[1].Width, split[1].Height - 6),
                Judge.Labels(rp, zoomIdx), Judge.Values(rp, zoomIdx), Judge.Colors(rp, zoomIdx), true, "0");

            var pieRect = Chart.Card(g, top[1], "判定构成", "环形图");
            var keep = Enumerable.Range(0, ci.Length).Where(i => vals[i] > 0).ToArray();
            string center = rp.FormatVersion >= 3 ? rp.Shorts[4] + " " + Fmt.Int(rp.Idx(4)) : "完美 " + Fmt.Int(rp.Idx(3));
            Chart.Donut(g, new RectangleF(pieRect.X, pieRect.Y, pieRect.Width, pieRect.Height - 56),
                keep.Select(i => vals[i]).ToArray(), keep.Select(i => Theme.JudgeColor(rp.FormatVersion, ci[i])).ToArray(),
                Fmt.Pct(rp.EffectiveAccuracy, "0.00"), center, "X-Acc");
            Chart.Legend(g, pieRect.X + 2, pieRect.Bottom - 48,
                keep.Select(i => rp.Shorts[ci[i]] + " " + vals[i].ToString("0")).ToArray(),
                keep.Select(i => Theme.JudgeColor(rp.FormatVersion, ci[i])).ToArray(), 3, pieRect.Width / 3f);

            // 早 / 晚 平衡
            var symRect = Chart.Card(g, bot[0], "早 / 晚 平衡", "偏离完美窗口的方向分布");
            var cats = new List<string>();
            var early = new List<double>();
            var late = new List<double>();
            int[] pairIdx = rp.FormatVersion >= 3
                ? new int[] { 0, 8, 1, 7, 2, 6, 3, 5 }
                : new int[] { 0, 6, 1, 5, 2, 4 };
            for (int k = 0; k + 1 < pairIdx.Length; k += 2)
            {
                int a = pairIdx[k], b = pairIdx[k + 1];
                cats.Add(rp.Shorts[a] + " / " + rp.Shorts[b]);
                early.Add(rp.Idx(a));
                late.Add(rp.Idx(b));
            }
            Chart.Symmetric(g, new Rectangle(symRect.X + 6, symRect.Y + 30, symRect.Width - 12, symRect.Height - 52),
                cats.ToArray(), early.ToArray(), late.ToArray());
            int eSum = 0, lSum = 0;
            for (int k = 0; k < early.Count; k++) { eSum += (int)early[k]; lSum += (int)late[k]; }
            Chart.Text(g, string.Format("偏早 {0} 次 ｜ 偏晚 {1} 次 ｜ 共 {2} 次未落在完美中心", eSum, lSum, eSum + lSum),
                Theme.F(8.5f, false), Theme.Faint, symRect.X + 2, symRect.Bottom - 16);

            // 准度损失分解
            var lossRect = Chart.Card(g, bot[1], "准度损失分解", "各类判定造成的准确率损失（百分点）");
            var loss = new double[rp.Judgment.Length];
            var w = rp.Weights;
            for (int k = 0; k < rp.Judgment.Length; k++)
                loss[k] = (k < rp.AccuracySlots && rp.AccuracyN > 0)
                        ? rp.Judgment[k] * (1 - (k < w.Length ? w[k] : 1.0)) / rp.AccuracyN * 100 : 0;
            var keep2 = Enumerable.Range(0, rp.Judgment.Length).Where(i => loss[i] > 0).ToArray();
            if (keep2.Length == 0)
            {
                Chart.TextCenter(g, "满分，无损失", Theme.F(12f, true), Theme.Green, lossRect);
            }
            else
            {
                Chart.HBars(g, new Rectangle(lossRect.X, lossRect.Y + 2, lossRect.Width, lossRect.Height - 40),
                    keep2.Select(i => rp.Shorts[i]).ToArray(), keep2.Select(i => loss[i]).ToArray(),
                    keep2.Select(i => Theme.JudgeColor(rp.FormatVersion, i)).ToArray(), " pp", false, "0.0000");
                double totalLoss = 100 * (1 - rp.EffectiveAccuracy);
                Chart.Text(g, string.Format("理论满分 100%，本局损失 {0} 个百分点（{1} 次非满分判定）",
                    totalLoss.ToString("0.0000"), Fmt.Int(rp.JudgmentTotal - rp.PerfectTierCount)),
                    Theme.F(9f, true), Theme.Text, lossRect.X, lossRect.Bottom - 20);
            }
        }
    }

    // ========================================================================
    public class XAccView : IView
    {
        public string Title { get { return "X-Acc 曲线"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }
            if (!rp.XAccValid)
            {
                Chart.Card(g, new Rectangle(r.X, r.Y, r.Width, r.Height), "实时 X-Acc", null);
                Chart.TextCenter(g, rp.FormatVersion >= 3
                        ? "该回放没有逐次命中误差，无法反推每击判定"
                        : "旧格式（formatVersion 2）不记录逐次命中误差，无法反推每击判定",
                    Theme.F(12f, true), Theme.Dim, new RectangleF(r.X, r.Y + r.Height / 2f - 30, r.Width, 30));
                Chart.TextCenter(g, "需要用 Creplay 2.17+ / ADOFAI 3.4.0+ 录制的 CRP4 回放",
                    Theme.F(9.5f, false), Theme.Faint, new RectangleF(r.X, r.Y + r.Height / 2f + 2, r.Width, 24));
                return;
            }

            var rows = L.Rows(r, 14, 1.32f, 1f);
            bool flat = rp.XAccSeries.Min() > 0.99999;
            var chartText = Chart.Card(g, rows[0], "实时 X-Acc",
                string.Format("每命中一击后的累计 X-Acc ｜ {0} 击 ｜ 判定窗：x完美 ±{1:0.#}° / 完美带 ±{2:0.#}° / 有效 ±{3:0.#}°{4}",
                    rp.HitAngleErr.Length, rp.XpDeg, rp.PureDeg, rp.CountedDeg,
                    flat ? " ｜ 全程满分，曲线平直" : ""));

            double minV = Math.Min(rp.XAccSeries.Min(), rp.XAccRolling.Min());
            double yMin = Math.Min(minV, 1.0) - Math.Max(0.01, (1.0 - minV) * 0.15);
            double yMax = 1.002;
            DrawErrorBars(g, chartText, rp);
            Chart.LineChart(g, chartText,
                new double[][] { rp.XAccSeries, rp.XAccRolling },
                new Color[] { Theme.Green, Color.FromArgb(120, Theme.Accent) },
                new string[] { "累计 X-Acc", "最近 " + Math.Min(25, Math.Max(5, rp.HitAngleErr.Length / 8)) + " 击滑动" },
                yMin, yMax, true);


            var bot = L.Cols(rows[1], 14, 1.12f, 1f);

            // 分段表
            var secRect = Chart.Card(g, bot[0], "分段 X-Acc", "把整局切成 8 段看走势");
            int n = rp.HitAngleErr.Length;
            int chunks = 8;
            var rowsT = new List<KeyValuePair<string, string>>();
            for (int c = 0; c < chunks; c++)
            {
                int a = (int)((long)n * c / chunks);
                int b = (int)((long)n * (c + 1) / chunks) - 1;
                if (b < a) continue;
                double segAcc = Math.Min(rp.XAccSeries[Math.Min(b, n - 1)] * (b + 1) - (a > 0 ? rp.XAccSeries[a - 1] * a : 0), b - a + 1);
                segAcc /= (b - a + 1);
                int xp = 0;
                for (int k = a; k <= b; k++) if (rp.HitMarginIdx[k] == 4) xp++;
                rowsT.Add(new KeyValuePair<string, string>(
                    string.Format("#{0,3}–#{1,-3}", a + 1, b + 1),
                    string.Format("X-Acc {0,7}   x完美 {1,4} ({2})", Fmt.Pct(segAcc, "0.000"), xp, Fmt.Pct((double)xp / (b - a + 1), "0.0"))));
            }
            Chart.Table(g, secRect, rowsT, 92);

            // 验证卡片
            var verRect = Chart.Card(g, bot[1], "反推校验", "由角度误差逐击分类，再和文件内的判定汇总对照");
            var items = new List<KeyValuePair<string, string>>();
            items.Add(new KeyValuePair<string, string>(rp.Shorts[4],
                string.Format("文件 {0,4}  ／  反推 {1,4}", rp.Idx(4), rp.DerivedJudge[4])));
            if (rp.FormatVersion >= 3)
            {
                items.Add(new KeyValuePair<string, string>(rp.Shorts[3] + " / " + rp.Shorts[5],
                    string.Format("文件 {0,4}  ／  反推 {1,4}", rp.Idx(3) + rp.Idx(5), rp.DerivedJudge[3] + rp.DerivedJudge[5])));
            }
            else
            {
                items.Add(new KeyValuePair<string, string>("完美",
                    string.Format("文件 {0,4}  ／  反推 {1,4}", rp.Idx(3), rp.DerivedJudge[3])));
            }
            items.Add(new KeyValuePair<string, string>("最终 X-Acc",
                string.Format("文件 {0}  ／  反推 {1}", Fmt.Pct(rp.AccuracyFile, "0.0000"), Fmt.Pct(rp.XAccFinal, "0.0000"))));
            items.Add(new KeyValuePair<string, string>("一致性",
                rp.XAccMatchesFile ? "✓ 逐击分类与判定汇总完全吻合" : "✗ 存在偏差（可能用了检查点或自定义 marginScale）"));
            Chart.Table(g, verRect, items, 92);

            if (rp.XAccMatchesFile)
            {
                float bx = verRect.X, by = verRect.Bottom - 34, w;
                Chart.Badge(g, bx, by, "逐击反推可信", Theme.Green, out w);
            }
        }

        // 浅色柱：该击的误差绝对值（独立量纲，仅用于给曲线提供背景结构）
        static void DrawErrorBars(Graphics g, Rectangle r, Replay rp)
        {
            double[] deg = rp.HitAngleErr.Select(a => Math.Abs(a) * 57.29577951308232).ToArray();
            int n = deg.Length;
            if (n < 2) return;
            double maxDeg = Math.Max(deg.Max(), rp.XpDeg * 1.2);
            float gutter = 56f;
            float px = r.X + gutter, pw = r.Right - px - 6;
            float py = r.Y + 26, ph = r.Bottom - py - 24;
            if (pw < 60 || ph < 40) return;
            float bw = Math.Max(pw / n - 0.6f, 0.8f);
            for (int k = 0; k < n; k++)
            {
                float h = (float)(ph * 0.92 * deg[k] / maxDeg);
                float x = px + pw * k / (float)(n - 1);
                Color c = deg[k] < rp.XpDeg ? Color.FromArgb(40, Theme.Green)
                        : deg[k] < rp.PureDeg ? Color.FromArgb(60, Color.FromArgb(196, 214, 96))
                        : Color.FromArgb(70, Theme.Orange);
                using (var b = new SolidBrush(c))
                    g.FillRectangle(b, x - bw / 2, py + ph - h, bw, h);
            }
            Chart.TextRight(g, "柱 = |误差|（≤" + maxDeg.ToString("0.#") + "°，独立量纲）", Theme.F(7.5f, false), Theme.Faint, r.Right - 8, py - 22);
        }
    }

    // ========================================================================
    public class ErrorView : IView
    {
        public string Title { get { return "误差"; } }

        const double Xp = 12.5, Pure = 30.0, Perf = 45.0, Counted = 60.0;

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }
            if (!rp.HasHitErrors)
            {
                Chart.Card(g, new Rectangle(r.X, r.Y, r.Width, r.Height), "逐次误差数据", null);
                Chart.TextCenter(g, "该回放不含逐次命中误差（只有 formatVersion 3 / CRP4 才记录）",
                    Theme.F(12f, true), Theme.Dim, new RectangleF(r.X, r.Y + r.Height / 2f - 30, r.Width, 30));
                Chart.TextCenter(g, "请用 Creplay 2.17+ 与 ADOFAI 3.4.0+ 重新录制，即可看到误差散点与分布",
                    Theme.F(9.5f, false), Theme.Faint, new RectangleF(r.X, r.Y + r.Height / 2f + 2, r.Width, 24));
                return;
            }

            int[] byFloor = Enumerable.Range(0, rp.HitAngleErr.Length).ToArray();
            double[] deg = rp.HitAngleErr.Select(a => a * 57.29577951308232).ToArray();
            double maxAbs = Math.Max(deg.Max(x => Math.Abs(x)), Xp * 1.6);
            double limit = Math.Min(Math.Max(maxAbs * 1.12, Pure), Counted);

            var rows = L.Rows(r, 14, 1.35f, 1f);
            var scatterText = Chart.Card(g, rows[0], "逐次命中误差（散点）",
                string.Format("X = 砖块序号，Y = 角度误差（正=晚 / 负=早）｜ {0} 次命中 ｜ 推算 BPM {1}",
                    rp.HitAngleErr.Length, rp.DerivedBpm > 0 ? rp.DerivedBpm.ToString("0.#") : "-"));
            DrawScatter(g, scatterText, rp, byFloor, deg, limit);

            var bot = L.Cols(rows[1], 14, 1.12f, 1f);

            var histText = Chart.Card(g, bot[0], "误差分布", "每 2° 一档（负=早，正=晚）");
            int binW = 2;
            int nb = (int)(limit * 2 / binW) + 1;
            var counts = new int[nb];
            var labels = new string[nb];
            for (int k = 0; k < nb; k++)
            {
                double lo = -limit + k * binW;
                labels[k] = (k % 2 == 0) ? lo.ToString("0") : "";
                foreach (double v in deg)
                    if (v >= lo && v < lo + binW) counts[k]++;
            }
            var colors = new Color[nb];
            for (int k = 0; k < nb; k++)
            {
                double mid = -limit + k * binW + binW / 2.0;
                colors[k] = Math.Abs(mid) < Xp ? Theme.Green : Math.Abs(mid) < Pure ? Color.FromArgb(196, 214, 96) : Theme.Orange;
            }
            Chart.BarChart(g, histText, labels, counts.Select(x => (double)x).ToArray(), colors, false, "0");
            Chart.Text(g, "档位间距 2°；绿 = x完美窗 (±" + rp.XpDeg.ToString("0.#") + "°) 内，黄绿 = 完美带内", Theme.F(8f, false), Theme.Faint, histText.X, histText.Bottom - 14);

            var statText = Chart.Card(g, bot[1], "误差统计", null);
            double meanAbs = deg.Average(x => Math.Abs(x));
            double rms = Math.Sqrt(deg.Average(x => x * x));
            int inXp = deg.Count(x => Math.Abs(x) < Xp);
            int inPure = deg.Count(x => Math.Abs(x) < Pure);
            int early = deg.Count(x => x < 0), later = deg.Count(x => x > 0);
            var items = new List<KeyValuePair<string, string>>();
            items.Add(new KeyValuePair<string, string>("命中样本", deg.Length + " 次"));
            items.Add(new KeyValuePair<string, string>("平均 |误差|", meanAbs.ToString("0.0000") + "°   (" + (meanAbs * 60.0 / Math.Max(rp.Bpm, 1) / 180.0 * 1000).ToString("0.00") + " ms)"));
            items.Add(new KeyValuePair<string, string>("RMS", rms.ToString("0.0000") + "°"));
            items.Add(new KeyValuePair<string, string>("最大 |误差|", maxAbs.ToString("0.0000") + "°"));
            items.Add(new KeyValuePair<string, string>("x完美窗内 (±" + rp.XpDeg.ToString("0.#") + "°)", Fmt.Int(inXp) + "  (" + Fmt.Pct((double)inXp / deg.Length, "0.00") + ")"));
            items.Add(new KeyValuePair<string, string>("完美带内 (±" + rp.PureDeg.ToString("0.#") + "°)", Fmt.Int(inPure) + "  (" + Fmt.Pct((double)inPure / deg.Length, "0.00") + ")"));
            items.Add(new KeyValuePair<string, string>("偏早 / 偏晚", early + " / " + later));
            Chart.Table(g, statText, items, 132);
        }

        static void DrawScatter(Graphics g, Rectangle r, Replay rp, int[] idx, double[] deg, double limit)
        {
            if (r.Height < 80) return;
            float gutter = 46f;
            float px = r.X + gutter, pw = r.Width - gutter - 8;
            float py = r.Y + 8, ph = r.Height - 34;
            if (pw < 60 || ph < 40) return;
            float mid = py + ph / 2f;
            float scale = (float)(ph / 2 / limit);

            // 判定窗口色带
            double[] bounds = { Counted, Perf, Pure, Xp };
            Color[] bandColors =
            {
                Color.FromArgb(14, Theme.Orange), Color.FromArgb(16, Theme.Yellow),
                Color.FromArgb(18, Color.FromArgb(196, 214, 96)), Color.FromArgb(26, Theme.Green)
            };
            for (int k = 0; k < bounds.Length; k++)
            {
                float h = (float)(bounds[k] * scale);
                if (mid - h < py || mid + h > py + ph) continue;
                Chart.Fill(g, new RectangleF(px, mid - h, pw, h * 2), 3f, bandColors[k]);
                Chart.TextRight(g, "±" + bounds[k].ToString("0.#") + "°", Theme.F(7.5f, false), Theme.Faint, px - 6, mid - h - 8);
            }
            using (var pen = new Pen(Color.FromArgb(90, Theme.Text), 1f))
                g.DrawLine(pen, px, mid, px + pw, mid);

            int n = deg.Length;
            for (int k = 0; k < n; k++)
            {
                float x = px + pw * (k + 0.5f) / n;
                float y = mid - (float)(deg[k] * scale);
                if (y < py) y = py;
                if (y > py + ph) y = py + ph;
                double a = Math.Abs(deg[k]);
                Color c = a < Xp ? Theme.Green : a < Pure ? Color.FromArgb(196, 214, 96) : a < Perf ? Theme.Yellow : Theme.Orange;
                using (var b = new SolidBrush(Color.FromArgb(215, c)))
                    g.FillEllipse(b, x - 2.5f, y - 2.5f, 5f, 5f);
            }

            for (int k = 0; k <= 4; k++)
            {
                int i = (int)((double)n * k / 4);
                if (i >= n) i = n - 1;
                Chart.TextCenter(g, "#" + (rp.FloorIds != null && i < rp.FloorIds.Length ? rp.FloorIds[i].ToString() : i.ToString()),
                    Theme.F(8f, false), Theme.Faint, new RectangleF(px + pw * k / 4 - 24, py + ph + 4, 48, 16));
            }
        }
    }

    // ========================================================================
    public class TimelineView : IView
    {
        public string Title { get { return "时间轴"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }

            var rows = L.Rows(r, 14, 1.22f, 1f);
            var areaRect = Chart.Card(g, rows[0], "每秒按键密度",
                string.Format("共 {0} 次按下 ｜ 平均 {1:0.00}/s ｜ 峰值 {2} 次/秒 @ {3}s ｜ 浅色柱=原始，曲线=3 秒滑动平均",
                    Fmt.Int(rp.PressCount), rp.AvgPps, rp.MaxDensity, rp.MaxDensitySecond));

            int longestIdx = -1; double longest = 0;
            for (int k = 0; k < rp.Gaps.Count; k++)
                if (rp.Gaps[k] > longest) { longest = rp.Gaps[k]; longestIdx = k; }
            float gutter = 36f;
            var plot = new Rectangle(areaRect.X, areaRect.Y + 2, areaRect.Width - 6, areaRect.Height - 6);
            if (longest > 5 && longestIdx >= 0)
            {
                double t0 = rp.Presses[longestIdx].Time;
                double t1 = rp.Presses[longestIdx + 1].Time;
                float px0 = plot.X + gutter;
                float w = plot.Right - px0;
                float x0 = px0 + w * (float)(t0 / Math.Max(rp.LastEvent, 1));
                float x1 = px0 + w * (float)(t1 / Math.Max(rp.LastEvent, 1));
                if (x1 > x0 + 2)
                {
                    Chart.Fill(g, new RectangleF(x0, plot.Y, x1 - x0, plot.Height - 22), 4f, Color.FromArgb(30, Theme.Red));
                    Chart.Text(g, string.Format("无输入 {0} 秒", longest.ToString("0")), Theme.F(8.5f, true),
                        Theme.Red, Math.Min(x0 + 6, plot.Right - 90), plot.Y + 2);
                }
            }
            Chart.DensityChart(g, plot, rp.SecondDensity, 3, Theme.Accent, Color.FromArgb(150, 220, 240, 255));

            var bot = L.Cols(rows[1], 14, 1.2f, 1f);
            var secRect = Chart.Card(g, bot[0], "分段统计（每 30 秒）", "按键数 / 密度 / 双压对数 / 主要用键");
            var rowsT = new List<KeyValuePair<string, string>>();
            int win = 30;
            int maxRows = Math.Max(4, (secRect.Height - 4) / 19);
            for (int s = 0; s < rp.SecondDensity.Length && rowsT.Count < maxRows; s += win)
            {
                int sum = 0;
                for (int k = s; k < Math.Min(s + win, rp.SecondDensity.Length); k++) sum += rp.SecondDensity[k];
                if (sum == 0 && s > rp.PressLast) continue;
                var winPress = rp.Presses.Where(p => p.Time >= s && p.Time < s + win).ToList();
                int chords = 0;
                for (int k = 0; k < winPress.Count - 1; k++)
                    if (winPress[k + 1].Time - winPress[k].Time < 0.030f && winPress[k].Vk != winPress[k + 1].Vk) chords++;
                var tops = winPress.GroupBy(p => p.Vk).OrderByDescending(gr => gr.Count()).Take(2)
                                   .Select(gr => Keys.Name(gr.Key) + "×" + gr.Count());
                rowsT.Add(new KeyValuePair<string, string>(
                    string.Format("{0,3}–{1,3}s", s, s + win),
                    string.Format("{0,5} 次 {1,5:0.00}/s  双压 {2,2}  {3}", sum, sum / (double)win,
                        chords, string.Join("  ", tops.ToArray()))));
            }
            Chart.Table(g, secRect, rowsT, 84);

            var cumRect = Chart.Card(g, bot[1], "累计按键曲线", "累计按下次数随时间变化");
            var cum = new double[rp.SecondDensity.Length];
            double acc = 0;
            for (int k = 0; k < cum.Length; k++) { acc += rp.SecondDensity[k]; cum[k] = acc; }
            Chart.Area(g, new Rectangle(cumRect.X + 34, cumRect.Y + 2, cumRect.Width - 44, cumRect.Height - 8), cum, Theme.Green, "s");
        }
    }

    // ========================================================================
    public class KeysView : IView
    {
        public string Title { get { return "按键"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }

            var rows = L.Rows(r, 14, 1f, 0.9f);
            var top = L.Cols(rows[0], 14, 1.05f, 1f);

            var useRect = Chart.Card(g, top[0], "键位使用",
                string.Format("{0} 个不同键，共 {1} 次按下", rp.KeyUsage.Count, Fmt.Int(rp.PressCount)));
            var ku = rp.KeyUsage.OrderByDescending(x => x.Value).Take(10).ToArray();
            Chart.HBars(g, new Rectangle(useRect.X, useRect.Y + 2, useRect.Width, useRect.Height - 6),
                ku.Select(x => Keys.Name(x.Key)).ToArray(), ku.Select(x => (double)x.Value).ToArray(),
                new Color[] { Theme.Accent, Theme.Cyan, Theme.Green, Theme.Purple, Theme.Orange }, "", true, "0");

            var histRect = Chart.Card(g, top[1], "按键间隔分布", "相邻两次按下的时间差（毫秒）");
            double[] edges = { 0.02, 0.05, 0.08, 0.10, 0.13, 0.16, 0.20, 0.30, 0.50, 1.00, 2.00 };
            string[] labels = { "<20", "20-50", "50-80", "80-100", "100-130", "130-160", "160-200", "200-300", "300-500", "500-1k", "1-2k", ">2k" };
            int[] counts = new int[labels.Length];
            foreach (double gp in rp.Gaps)
            {
                int idx = labels.Length - 1;
                for (int k = 0; k < edges.Length; k++)
                    if (gp < edges[k]) { idx = k; break; }
                counts[idx]++;
            }
            Chart.BarChart(g, new Rectangle(histRect.X, histRect.Y, histRect.Width, histRect.Height - 4), labels,
                counts.Select(x => (double)x).ToArray(), Enumerable.Repeat(Theme.Accent, labels.Length).ToArray(), false, "0");

            var bot = L.Cols(rows[1], 14, 1f, 1.25f);
            var chordRect = Chart.Card(g, bot[0], "同时按下（双压）", "相邻两次按下几乎同一时刻");
            var items = new List<KeyValuePair<string, string>>();
            items.Add(new KeyValuePair<string, string>("间隔 <20 ms", Fmt.Int(rp.ChordPairs) + " 对"));
            items.Add(new KeyValuePair<string, string>("间隔 <30 ms", Fmt.Int(rp.ChordPairs30) + " 对"));
            items.Add(new KeyValuePair<string, string>("占按下比例", Fmt.Pct(rp.PressCount > 0 ? (double)rp.ChordPairs / rp.PressCount : 0, "0.0")));
            items.Add(new KeyValuePair<string, string>("平均间隔", rp.Gaps.Count > 0 ? Fmt.Ms(rp.Gaps.Average()) : "-"));
            items.Add(new KeyValuePair<string, string>("中位间隔", rp.Gaps.Count > 0 ? Fmt.Ms(Median(rp.Gaps)) : "-"));
            items.Add(new KeyValuePair<string, string>("最短间隔", rp.Gaps.Count > 0 ? Fmt.Ms(rp.Gaps.Min()) : "-"));
            items.Add(new KeyValuePair<string, string>("最长沉默", rp.Gaps.Count > 0 ? rp.Gaps.Max().ToString("0.0") + " 秒" : "-"));
            Chart.Table(g, chordRect, items, 100);

            var heatRect = Chart.Card(g, bot[1], "键位迁移热图", "相邻两次按下的键对出现次数（越亮越频繁）");
            var topKeys = rp.KeyUsage.OrderByDescending(x => x.Value).Take(6).Select(x => x.Key).ToArray();
            var idxOf = new Dictionary<int, int>();
            for (int k = 0; k < topKeys.Length; k++) idxOf[topKeys[k]] = k;
            var mat = new int[Math.Max(topKeys.Length, 1), Math.Max(topKeys.Length, 1)];
            int maxPair = 1;
            for (int k = 0; k < rp.Presses.Count - 1; k++)
            {
                int a, b;
                if (!idxOf.TryGetValue(rp.Presses[k].Vk, out a)) continue;
                if (!idxOf.TryGetValue(rp.Presses[k + 1].Vk, out b)) continue;
                mat[a, b]++;
                if (mat[a, b] > maxPair) maxPair = mat[a, b];
            }
            DrawHeat(g, heatRect, topKeys, mat, maxPair);
        }

        static double Median(List<double> xs)
        {
            var s = xs.OrderBy(x => x).ToArray();
            if (s.Length == 0) return 0;
            return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
        }

        static void DrawHeat(Graphics g, Rectangle r, int[] keys, int[,] mat, int maxPair)
        {
            int n = keys.Length;
            if (n == 0) return;
            float cell = Math.Min((r.Width - 70) / (float)n, (r.Height - 50) / (float)n);
            if (cell < 10) return;
            float ox = r.X + 64, oy = r.Y + 26;
            Font f = Theme.F(8f, false);
            for (int i = 0; i < n; i++)
                Chart.TextRight(g, Keys.Short(keys[i]), f, Theme.Dim, ox - 8, oy + cell * i + cell / 2 - 8);
            for (int j = 0; j < n; j++)
                Chart.TextCenter(g, Chart.Ellipsis(g, Keys.Short(keys[j]), Theme.F(8f, false), cell - 2), Theme.F(8f, false), Theme.Dim,
                    new RectangleF(ox + cell * j, oy - 19, cell, 16));
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double t = (double)mat[i, j] / maxPair;
                    var c = t <= 0 ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb((int)(55 + 200 * t), Theme.Accent);
                    float pad = cell > 26 ? 2f : 1f;
                    Chart.Fill(g, new RectangleF(ox + cell * j + pad, oy + cell * i + pad, cell - pad * 2, cell - pad * 2), 3f, c);
                    if (cell > 30 && mat[i, j] > 0)
                        Chart.TextCenter(g, mat[i, j].ToString(), Theme.F(7.5f, false),
                            t > 0.55 ? Color.White : Theme.Dim, new RectangleF(ox + cell * j, oy + cell * i, cell, cell));
                }
        }
    }

    // ========================================================================
    public class CompareView : IView
    {
        public string Title { get { return "对比"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var loaded = st.Files.Where(x => x.IsLoaded).ToList();
            if (loaded.Count == 0) { Views.DrawEmpty(g, r); return; }

            var rows = L.Rows(r, 14, 1f, 0.95f);
            var accRect = Chart.Card(g, rows[0], "已加载回放的准确率",
                loaded.Count == 1 ? "只加载了 1 个文件，可再打开多个回放做对比" : "共 " + loaded.Count + " 个回放，按准确率排序");

            var sorted = loaded.OrderByDescending(x => x.EffectiveAccuracy).Take(12).ToArray();
            int listH = Math.Min(accRect.Height - 6, sorted.Length * 46);
            Chart.HBars(g, new Rectangle(accRect.X, accRect.Y + 2, accRect.Width, listH),
                sorted.Select(x => TrimName(x.FileName)).ToArray(),
                sorted.Select(x => x.EffectiveAccuracy * 100).ToArray(),
                sorted.Select(x => x.EffectiveAccuracy >= 0.9999 ? Theme.Green :
                                   x.EffectiveAccuracy >= 0.99 ? Theme.Accent : Theme.Orange).ToArray(), "%", false, "0.0000");

            var stackRect = Chart.Card(g, rows[1], "判定构成对比", "每条 = 该回放的全部命中（100%）");
            DrawStacked(g, stackRect, sorted);

            if (loaded.Count > 1)
            {
                double best = sorted[0].EffectiveAccuracy, worst = sorted[sorted.Length - 1].EffectiveAccuracy;
                Chart.Text(g, string.Format("最高 {0} ｜ 最低 {1} ｜ 差距 {2} 个百分点",
                    Fmt.Pct(best, "0.0000"), Fmt.Pct(worst, "0.0000"), ((best - worst) * 100).ToString("0.0000")),
                    Theme.F(9f, false), Theme.Dim, stackRect.X, stackRect.Bottom - 2);
            }
        }

        static string TrimName(string n)
        {
            if (n.Length <= 24) return n;
            return n.Substring(0, 22) + "…";
        }

        static void DrawStacked(Graphics g, Rectangle r, Replay[] files)
        {
            float rowH = Math.Min((float)r.Height / Math.Max(files.Length, 1), 38f);
            float labelW = 250f;
            for (int i = 0; i < files.Length; i++)
            {
                float y = r.Y + rowH * i;
                if (y + rowH > r.Bottom - 26) break;
                var rp = files[i];
                Chart.Text(g, Chart.Ellipsis(g, TrimName(rp.FileName), Theme.F(9f, false), labelW - 14),
                    Theme.F(9f, false), Theme.Dim, r.X, y + rowH / 2 - 10);
                double total = Math.Max(rp.JudgmentTotal, 1);
                float x = r.X + labelW;
                float w = r.Width - labelW - 74;
                float bh = Math.Min(rowH - 8, 22f);
                int[] ci = Judge.ChartIndices(rp);
                for (int j = 0; j < ci.Length; j++)
                {
                    int k = ci[j];
                    if (rp.Judgment[k] <= 0) continue;
                    float seg = (float)(w * rp.Judgment[k] / total);
                    Chart.Fill(g, new RectangleF(x, y + (rowH - bh) / 2, Math.Max(seg, 1.2f), bh), 2f,
                        Theme.JudgeColor(rp.FormatVersion, k));
                    x += seg;
                }
                Chart.TextRight(g, Fmt.Pct(rp.EffectiveAccuracy, "0.0000"), Theme.F(9f, true), Theme.Text, r.Right, y + rowH / 2 - 9);
            }
            int[] legend = files[0].CoreIdx;
            Chart.Legend(g, r.X, r.Bottom - 22, Judge.Labels(files[0], legend), Judge.Colors(files[0], legend), 8, r.Width / 8f);
        }
    }

    // ========================================================================
    public class DataView : IView
    {
        public string Title { get { return "数据"; } }

        public void Render(Graphics g, Rectangle r, AppState st)
        {
            var rp = st.Ok;
            if (rp == null) { Views.DrawEmpty(g, r); return; }

            int topH = Math.Min(300, (int)(r.Height * 0.44f));
            var top = L.Cols(new Rectangle(r.X, r.Y, r.Width, topH), 14, 1f, 1f);

            var left = Chart.Card(g, top[0], "文件与容器", null);
            var l = new List<KeyValuePair<string, string>>();
            l.Add(new KeyValuePair<string, string>("文件名", rp.FileName));
            l.Add(new KeyValuePair<string, string>("大小", Fmt.Int(rp.Size) + " 字节"));
            l.Add(new KeyValuePair<string, string>("容器", rp.ContainerText));
            l.Add(new KeyValuePair<string, string>("内部版本", rp.FormatText));
            l.Add(new KeyValuePair<string, string>("SHA-256", rp.Sha256));
            l.Add(new KeyValuePair<string, string>("谱面路径", rp.LevelPath));
            l.Add(new KeyValuePair<string, string>("场景 / 判定", rp.GetS("scene_name", "") + " / " + rp.JudgeMode));
            Chart.Table(g, left, l, 82);

            var right = Chart.Card(g, top[1], "统计数据", null);
            var rr = new List<KeyValuePair<string, string>>();
            rr.Add(new KeyValuePair<string, string>("击打 / 判定", rp.HitCount + " / " + rp.JudgmentTotal));
            rr.Add(new KeyValuePair<string, string>("按下事件", rp.PressCount.ToString()));
            rr.Add(new KeyValuePair<string, string>("加权准确率", rp.Accuracy.ToString("0.0000000000")));
            rr.Add(new KeyValuePair<string, string>("percentXacc", rp.AccuracyFile.ToString("0.0000000000")));
            rr.Add(new KeyValuePair<string, string>("BPM", (rp.Bpm > 0 ? rp.Bpm.ToString("0.###") : "-") +
                (rp.DerivedBpm > 0 ? "（由误差推算）" : "")));
            rr.Add(new KeyValuePair<string, string>("楼层区间", rp.StartTile + " → " + rp.EndTile));
            rr.Add(new KeyValuePair<string, string>("标志位", "Auto " + rp.AutoHits + " / NoFail " + rp.NoFailHits + " / 中旋 " + rp.MidspinHits));
            rr.Add(new KeyValuePair<string, string>("逐次误差", rp.HasHitErrors ? rp.HitTimeErr.Length + " 条" : "该版本不提供"));
            Chart.Table(g, right, rr, 96);

            var bottom = new Rectangle(r.X, r.Y + topH + 14, r.Width, Math.Max(r.Height - topH - 14, 80));
            var bot = L.Cols(bottom, 14, 1f, 1f);

            var jd = Chart.Card(g, bot[0], "判定明细", "权重 / 次数 / 占比 / 损失");
            var jr = new List<KeyValuePair<string, string>>();
            var w = rp.Weights;
            for (int k = 0; k < rp.Judgment.Length; k++)
            {
                if (rp.Judgment[k] == 0) continue;
                double share = rp.JudgmentTotal > 0 ? (double)rp.Judgment[k] / rp.JudgmentTotal : 0;
                double wt = k < w.Length ? w[k] : 1.0;
                double loss = share * (1 - wt) * 100;
                jr.Add(new KeyValuePair<string, string>(rp.Longs[k],
                    string.Format("w={0:0.00}   {1,5} 次   {2}   {3} pp", wt, rp.Judgment[k],
                        Fmt.Pct(share, "0.00"), loss > 0 ? "-" + loss.ToString("0.0000") : "0.0000")));
            }
            jr.Add(new KeyValuePair<string, string>("合计", string.Format("{0} 次   准确率 {1}", Fmt.Int(rp.JudgmentTotal),
                Fmt.Pct(rp.EffectiveAccuracy, "0.0000"))));
            Chart.Table(g, jd, jr, 118);

            var mod = Chart.Card(g, bot[1], "录制环境（LoadedMods）", "录制时启用的模组");
            string[] lines = rp.Mods.Split('\n');
            float y = mod.Y;
            foreach (var line in lines)
            {
                if (y + 20 > mod.Bottom) break;
                string t = line.Trim();
                if (t.Length == 0) continue;
                Chart.Text(g, Chart.Ellipsis(g, t, Theme.F(9f, false), mod.Width - 4), Theme.F(9f, false), Theme.Dim, mod.X, y);
                y += 21;
            }
            if (y + 20 < mod.Bottom && rp.GameVersion.Length > 0)
            {
                y += 6;
                Chart.Text(g, "游戏版本 " + rp.GameVersion + " ｜ Creplay " + rp.ModVersion, Theme.F(9f, true), Theme.Text, mod.X, y);
            }
        }
    }

    // ========================================================================
    public static class Views
    {
        public static void DrawEmpty(Graphics g, Rectangle r)
        {
            Chart.Stroke(g, new RectangleF(r.X + 40, r.Y + 40, r.Width - 80, r.Height - 80), 14f, Theme.Border, 1.5f);
            Chart.TextCenter(g, "把 .crpl2 回放文件拖进来", Theme.F(17f, true), Theme.Dim,
                new RectangleF(r.X, r.Y + r.Height / 2f - 44, r.Width, 30));
            Chart.TextCenter(g, "或点击右上角「打开回放」选择文件（支持多选、支持拖放）",
                Theme.F(10f, false), Theme.Faint, new RectangleF(r.X, r.Y + r.Height / 2f - 10, r.Width, 24));
            Chart.TextCenter(g, "支持 CRP2 / CRP4 容器 · 格式版本 2 & 3 · 解密 + 解析 + 准度可视化",
                Theme.F(9f, false), Theme.Faint, new RectangleF(r.X, r.Y + r.Height / 2f + 24, r.Width, 24));
        }
    }
}
