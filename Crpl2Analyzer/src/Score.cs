// Crpl2Analyzer - ADOFAI Creplay replay analyzer
// Copyright (C) 2026 InbottlesN235
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Crpl2
{
    /// <summary>TUF 等级基础分（来源：TUF 官方 Notion / 论坛评分标准）。</summary>
    public static class Tuf
    {
        public static readonly double[] PBase =
        { 0.1, 0.15, 0.2, 0.3, 0.4, 0.6, 0.8, 1, 1.5, 2, 3, 5, 7, 10, 15, 20, 30, 45, 60, 75 };
        public static readonly double[] GBase =
        { 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250, 275, 300, 350, 425 };
        public static readonly double[] UBase =
        { 500, 600, 700, 850, 1000, 1250, 1500, 1750, 2000, 2500, 3000, 4000 };   // U1..U12；U13+ 每谱单独设定

        public static readonly List<string> TierKeys = MakeKeys();

        static List<string> MakeKeys()
        {
            var l = new List<string>();
            for (int i = 1; i <= 20; i++) l.Add("P" + i);
            for (int i = 1; i <= 20; i++) l.Add("G" + i);
            for (int i = 1; i <= 20; i++) l.Add("U" + i);
            return l;
        }

        public static double BaseOf(string key, out bool fixedBase)
        {
            fixedBase = true;
            if (string.IsNullOrEmpty(key) || key.Length < 2) { fixedBase = false; return 0; }
            int n;
            if (!int.TryParse(key.Substring(1), out n)) { fixedBase = false; return 0; }
            switch (key[0])
            {
                case 'P': if (n >= 1 && n <= PBase.Length) return PBase[n - 1]; break;
                case 'G': if (n >= 1 && n <= GBase.Length) return GBase[n - 1]; break;
                case 'U': if (n >= 1 && n <= UBase.Length) return UBase[n - 1]; break;
            }
            fixedBase = false;
            return 0;
        }

        /// <summary>宽恕漏击数 = floor(物量 / 315)</summary>
        public static int Grace(int tiles) { return tiles > 0 ? tiles / 315 : 0; }

        /// <summary>U13+ 官方参考：中位分 / 高位分（TUF 文档「Cleared UQ3 and Above Score Range」）。</summary>
        public static bool RefRange(string tier, out double mid, out double high)
        {
            mid = 0; high = 0;
            if (string.IsNullOrEmpty(tier)) return false;
            switch (tier)
            {
                case "U13": mid = 20000; high = 30000; return true;
                case "U14": mid = 30000; high = 40000; return true;
                case "U15": mid = 50000; high = 60000; return true;
                case "U16": mid = 60000; high = 70000; return true;
            }
            return false;
        }

        public static string Family(string key)
        {
            if (string.IsNullOrEmpty(key)) return "?";
            if (key[0] == 'P') return "Planetary（行星）";
            if (key[0] == 'G') return "Galactic（银河）";
            return "Universal（宇宙）";
        }
    }

    /// <summary>“评分估算”页的输入与计算模型。</summary>
    public class ScoreModel
    {
        public string Tier = "U5";
        public double Base;
        public bool BaseFixed = true;
        public int Tiles;
        public int Misses;

        // X-Acc 倍率：TUF 曲线未公开，用「PP 倍率 × 准度^指数」拟合
        public double AccPct = 100;         // X-Acc（%）
        public double PPMul = 6.0;          // 满准度时的倍率（官方：至少 6×，难度越高越大）
        public double AccExp = 114.7;      // 曲线指数（按一次实测反推：P17 99.31% → 89.20）
        public double AccMul = 6.0;         // 实际参与计算的倍率
        public bool AccManual;

        public double SpeedPct = 100;
        public double SpeedMul = 1.0;
        public bool SpeedManual;
        public string SpeedNote = "100% 标准速度，×1.00";

        public bool NonStandardHold;
        public double MissMul = 1.1;
        public bool MissManual;

        public int Grace { get { return Tuf.Grace(Tiles); } }
        public int OverGrace { get { return Math.Max(0, Misses - Grace); } }
        public double HoldMul { get { return NonStandardHold ? 0.95 : 1.0; } }
        public double Final { get { return Base * AccMul * SpeedMul * MissMul * HoldMul; } }

        // 官方曲线：过 (95%, 基础分) 与两个内点的双曲线 y = A + B/(x - C)
        public bool UseHyper;
        public double HA, HB, HC;
        public string FitNote = "";

        /// <summary>XAcc 倍率：拟合成功时用官方双曲线，否则用 PP 倍率 × 准度^指数。</summary>
        public void ApplyAcc()
        {
            if (AccManual) return;
            double a = Math.Max(0.0001, Math.Min(1.0, AccPct / 100.0));
            if (UseHyper && Base > 0)
            {
                double d = a - HC;
                if (Math.Abs(d) < 1e-9) d = d < 0 ? -1e-9 : 1e-9;
                AccMul = Math.Max(0.0, HA + HB / d) / Base;
                return;
            }
            AccMul = PPMul * Math.Pow(a, AccExp);
        }

        /// <summary>TUF 官方曲线：以 (95%, 基础分) + 两个实测点为锚，拟合双曲线 y = A + B/(x - C)。</summary>
        public bool FitHyper(double x1, double y1, double x2, double y2)
        {
            UseHyper = false;
            if (Base <= 0) { FitNote = "基础分为 0，无法拟合"; return false; }
            if (x1 >= 1 || x2 >= 1) { FitNote = "锚点准度必须小于 100%"; return false; }
            if (Math.Abs(x1 - x2) < 0.002) { FitNote = "两个锚点太接近（准度差 < 0.2%），拟合不稳定"; return false; }

            // 由两点解 A(C)、B(C)，再要求 y(0.95) = Base
            double g = 0;
            Func<double, double> f = (c) =>
            {
                double d1 = 1.0 / (x1 - c), d2 = 1.0 / (x2 - c), d95 = 0.95 - c;
                if (Math.Abs(d1 - d2) < 1e-12 || Math.Abs(d95) < 1e-6) return double.NaN;
                double b = (y1 - y2) / (d1 - d2);
                double a = y1 - b * d1;
                g = b;
                return a + b / d95 - Base;
            };

            double bestC = 0;
            double prevC = 0.9510, prevF = f(prevC);
            for (double c = 0.9515; c <= 1.60; c += 0.0005)
            {
                if (Math.Abs(c - x1) < 3e-3 || Math.Abs(c - x2) < 3e-3 || Math.Abs(c - 0.95) < 3e-3) { prevC = c; prevF = f(c); continue; }
                double cur = f(c);
                if (!double.IsNaN(prevF) && !double.IsNaN(cur) && prevF * cur < 0)
                {
                    double lo = prevC, hi = c;
                    for (int k = 0; k < 80; k++)
                    {
                        double mid = (lo + hi) / 2;
                        double fm = f(mid);
                        if (double.IsNaN(fm)) break;
                        if (f(lo) * fm <= 0) hi = mid; else lo = mid;
                    }
                    double cc = (lo + hi) / 2;
                    double d1 = 1.0 / (x1 - cc), d2 = 1.0 / (x2 - cc);
                    double b2 = (y1 - y2) / (d1 - d2);
                    double a2 = y1 - b2 * d1;
                    bool rising = b2 < 0;                       // y' = -b/(x-c)^2 > 0
                    double s95 = a2 + b2 / (0.95 - cc);
                    double s100 = a2 + b2 / (1.0 - cc);
                    if (rising && s100 > s95 && s95 > 0 && cc > 0.95)
                    {
                        bestC = cc; HA = a2; HB = b2; HC = cc; UseHyper = true;
                        FitNote = string.Format("已按官方双曲线拟合：极点 C = {0:0.00000}，顶端倍率 G = {1:0.0000}×",
                            cc, s100 / Base);
                        return true;
                    }
                }
                prevC = c; prevF = cur;
            }
            FitNote = "找不到合理的双曲线（锚点可能不在同一条曲线上）";
            return false;
        }

        /// <summary>TUF 速度倍率按区间取值（官方只公布了部分区间）。</summary>
        public void ApplySpeedBand()
        {
            if (SpeedManual) { SpeedNote = "手动填写"; return; }
            double v = SpeedPct;
            if (v >= 110 && v <= 150)
            {
                SpeedMul = 0.65;
                SpeedNote = "110%–150% 固定 ×0.65（官方：此区间惩罚最重）";
            }
            else if (v > 150 && v < 200)
            {
                // 官方只说 "scales toward 1×"：从 150% 的 0.65 线性回到 200% 的 1.00
                SpeedMul = 0.65 + (v - 150.0) / 50.0 * 0.35;
                SpeedNote = "150%–200% 官方：scales toward 1×（按 0.65→1.00 线性插值）";
            }
            else if (v >= 200) { SpeedMul = 1.0; SpeedNote = "200% 以上 ×1.00"; }
            else if (v >= 70 && v < 110)
            {
                SpeedMul = 1.0;
                SpeedNote = Math.Abs(v - 100) < 1e-9
                    ? "100% 标准速度，×1.00"
                    : "70%–110% 官方：scales toward 1×（未给具体缩放），暂按 ×1.00";
            }
            else { SpeedMul = 1.0; SpeedNote = "低于 70% 官方未公布，暂按 ×1.00"; }
        }

        /// <summary>0 漏击 → ×1.1；宽恕范围内 → ×1.0；超出宽恕部分官方未公开系数，暂按 1.0。</summary>
        public void ApplyAutoMiss()
        {
            if (MissManual) return;
            MissMul = Misses == 0 ? 1.1 : 1.0;
        }

        public void ApplyTier(string tier, bool keepBase)
        {
            Tier = tier;
            bool fixedBase;
            double b = Tuf.BaseOf(tier, out fixedBase);
            BaseFixed = fixedBase;
            if (!keepBase) Base = b;
        }

        /// <summary>给自测/截图填一组有代表性的数据。</summary>
        public void PerformSample()
        {
            ApplyTier("U14", false);
            Base = 5000;
            Tiles = 1817;
            Misses = 0;
            AccPct = 99.31;
            PPMul = 6.0;
            AccExp = 114.7;
            SpeedPct = 100;
            NonStandardHold = false;
            MissManual = false;
            SpeedManual = false;
            AccManual = false;
            ApplyAcc();
            ApplyAutoMiss();
            ApplySpeedBand();
        }
    }

    /// <summary>“评分估算”页：左侧填写等级与各项倍率，右侧实时算分。</summary>
    public class ScorePanel : Panel
    {
        public ScoreModel M = new ScoreModel();

        TextBox txtBase, txtAccPct, txtAcc, txtPPMul, txtExp, txtSpeedPct, txtSpeed, txtMissMul;
        TextBox txtX1, txtY1, txtX2, txtY2;
        NumericUpDown numTiles, numMiss;
        CheckBox chkMissManual, chkSpeedManual, chkAccManual;

        int tierIndex = Tuf.TierKeys.IndexOf("U5");
        int holdIndex;

        /// <summary>当前回放来源（由主窗口注入）</summary>
        public AppState State;
        public string SourceNote = "手工填写";

        static readonly Dictionary<string, Image> IconCache = new Dictionary<string, Image>();

        public ScorePanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            BuildControls();
            M.ApplyTier(Tuf.TierKeys[tierIndex], false);
            M.ApplyAcc();
            M.ApplyAutoMiss();
            M.ApplySpeedBand();
            SyncControls();
            Resize += (s, e) => LayoutControls();
        }

        void BuildControls()
        {
            txtBase = MakeBox();
            txtBase.TextChanged += (s, e) => { double v; if (double.TryParse(txtBase.Text, out v)) { M.Base = v; Invalidate(); } };

            numTiles = MakeNum(0, 0, 1000000);
            numTiles.ValueChanged += (s, e) => { M.Tiles = (int)numTiles.Value; Invalidate(); };

            numMiss = MakeNum(0, 0, 100000);
            numMiss.ValueChanged += (s, e) =>
            {
                M.Misses = (int)numMiss.Value;
                M.ApplyAutoMiss();
                SyncMiss();
                Invalidate();
            };

            txtAccPct = MakeBox();
            txtAccPct.Text = "100";
            txtAccPct.TextChanged += (s, e) =>
            {
                double v;
                if (double.TryParse(txtAccPct.Text, out v)) { M.AccPct = v; M.ApplyAcc(); SyncAcc(); Invalidate(); }
            };

            txtPPMul = MakeBox();
            txtPPMul.Text = "6.00";
            txtPPMul.TextChanged += (s, e) =>
            {
                double v;
                if (double.TryParse(txtPPMul.Text, out v)) { M.PPMul = v; M.ApplyAcc(); SyncAcc(); Invalidate(); }
            };

            txtExp = MakeBox();
            txtExp.Text = "114.7";
            txtExp.TextChanged += (s, e) =>
            {
                double v;
                if (double.TryParse(txtExp.Text, out v)) { M.AccExp = v; M.ApplyAcc(); SyncAcc(); Invalidate(); }
            };

            txtAcc = MakeBox();
            txtAcc.Text = "6.0000";
            txtAcc.Enabled = false;
            txtAcc.TextChanged += (s, e) => { double v; if (double.TryParse(txtAcc.Text, out v)) { M.AccMul = v; Invalidate(); } };

            chkAccManual = new CheckBox { Text = "手填", FlatStyle = FlatStyle.Flat, ForeColor = Theme.Dim };
            chkAccManual.CheckedChanged += (s, e) =>
            {
                M.AccManual = chkAccManual.Checked;
                txtAcc.Enabled = M.AccManual;
                if (!M.AccManual) M.ApplyAcc();
                SyncAcc();
                Invalidate();
            };

            txtSpeedPct = MakeBox();
            txtSpeedPct.Text = "100";
            txtSpeedPct.TextChanged += (s, e) =>
            {
                double v;
                if (double.TryParse(txtSpeedPct.Text, out v)) { M.SpeedPct = v; M.ApplySpeedBand(); SyncSpeed(); Invalidate(); }
            };

            txtSpeed = MakeBox();
            txtSpeed.Text = "1.0000";
            txtSpeed.Enabled = false;
            txtSpeed.TextChanged += (s, e) => { double v; if (double.TryParse(txtSpeed.Text, out v)) { M.SpeedMul = v; Invalidate(); } };

            chkSpeedManual = new CheckBox { Text = "手填", FlatStyle = FlatStyle.Flat, ForeColor = Theme.Dim };
            chkSpeedManual.CheckedChanged += (s, e) =>
            {
                M.SpeedManual = chkSpeedManual.Checked;
                txtSpeed.Enabled = M.SpeedManual;
                if (!M.SpeedManual) M.ApplySpeedBand();
                SyncSpeed();
                Invalidate();
            };

            txtX1 = MakeBox(); txtY1 = MakeBox(); txtX2 = MakeBox(); txtY2 = MakeBox();

            chkMissManual = new CheckBox { Text = "手动填漏击倍率", FlatStyle = FlatStyle.Flat, ForeColor = Theme.Dim };
            chkMissManual.CheckedChanged += (s, e) =>
            {
                M.MissManual = chkMissManual.Checked;
                txtMissMul.Enabled = M.MissManual;
                if (!M.MissManual) M.ApplyAutoMiss();
                SyncMiss();
                Invalidate();
            };

            txtMissMul = MakeBox();
            txtMissMul.Text = "1.1000";
            txtMissMul.Enabled = false;
            txtMissMul.TextChanged += (s, e) => { double v; if (double.TryParse(txtMissMul.Text, out v)) { M.MissMul = v; Invalidate(); } };

            Controls.Add(txtBase); Controls.Add(numTiles); Controls.Add(numMiss);
            Controls.Add(txtAccPct); Controls.Add(txtPPMul); Controls.Add(txtExp);
            Controls.Add(txtAcc); Controls.Add(chkAccManual);
            Controls.Add(txtSpeedPct); Controls.Add(txtSpeed); Controls.Add(chkSpeedManual);
            Controls.Add(chkMissManual); Controls.Add(txtMissMul);
            Controls.Add(txtX1); Controls.Add(txtY1); Controls.Add(txtX2); Controls.Add(txtY2);

            foreach (Control c in Controls)
            {
                c.Font = Theme.F(9.5f, false);
                c.TabStop = false;
                if (c is TextBox || c is NumericUpDown) { c.BackColor = Theme.Panel2; c.ForeColor = Theme.Text; }
            }
        }

        TextBox MakeBox()
        {
            return new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Panel2, ForeColor = Theme.Text };
        }

        NumericUpDown MakeNum(int val, int min, int max)
        {
            return new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = val,
                BackColor = Theme.Panel2, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle, ThousandsSeparator = true
            };
        }

        void SyncAcc()
        {
            if (!M.AccManual) txtAcc.Text = M.AccMul.ToString("0.0000");
        }

        void SyncSpeed()
        {
            if (!M.SpeedManual) txtSpeed.Text = M.SpeedMul.ToString("0.0000");
        }

        void SyncMiss()
        {
            if (!M.MissManual) txtMissMul.Text = M.MissMul.ToString("0.0000");
        }

        void SyncControls()
        {
            txtBase.Text = M.BaseFixed ? M.Base.ToString("0.###") : "";
            txtBase.ForeColor = M.BaseFixed ? Theme.Text : Theme.Yellow;
            SyncAcc();
            SyncSpeed();
            SyncMiss();
        }

        /// <summary>把当前选中回放的物量 / 漏击 / 游玩速度 / 准度同步进来。</summary>
        public void SyncFromReplay()
        {
            var rp = State == null ? null : State.Ok;
            if (rp == null)
            {
                SourceNote = "未加载回放，当前为手工填写";
                Invalidate();
                return;
            }
            int tiles = rp.EndTile > 0 ? rp.EndTile : rp.HitCount;
            int miss = rp.Idx(0);
            double pct = rp.PlaybackSpeed > 0 ? rp.PlaybackSpeed * 100.0 : 100.0;
            double acc = rp.EffectiveAccuracy > 0 ? rp.EffectiveAccuracy * 100.0 : 100.0;

            M.Tiles = tiles;
            M.Misses = miss;
            M.SpeedPct = pct;
            M.AccPct = acc;
            M.ApplyAutoMiss();
            M.ApplySpeedBand();
            M.ApplyAcc();

            numTiles.Value = Math.Max(numTiles.Minimum, Math.Min(numTiles.Maximum, tiles));
            numMiss.Value = Math.Max(numMiss.Minimum, Math.Min(numMiss.Maximum, miss));
            txtSpeedPct.Text = pct.ToString("0.#");
            txtAccPct.Text = acc.ToString("0.####");
            SyncControls();
            SourceNote = "已同步：" + rp.FileName;
            Invalidate();
        }

        // ---------------- 布局 ----------------
        const int Pad = 16;
        const int RowH = 62;

        Rectangle InputCard { get { return new Rectangle(Pad, 84, (int)((Width - Pad * 3) * 0.50f), Height - Pad - 84); } }
        Rectangle ResultCard { get { var l = InputCard; return new Rectangle(l.Right + Pad, 84, Width - l.Right - Pad * 2, Height - Pad - 84); } }

        Rectangle Field(int col, int row)
        {
            var card = InputCard;
            int gap = 14;
            int w = (card.Width - 36 - gap) / 2;
            int x = card.X + 18 + col * (w + gap);
            int y = card.Y + 52 + row * RowH;
            return new Rectangle(x, y + 18, w, 26);
        }

        void LayoutControls()
        {
            if (txtBase == null) return;
            txtBase.Bounds = Field(1, 0);
            numTiles.Bounds = Field(0, 1);
            numMiss.Bounds = Field(1, 1);

            txtSpeedPct.Bounds = Field(0, 2);
            var sr = Field(1, 2);
            txtSpeed.Bounds = new Rectangle(sr.X, sr.Y, sr.Width - 86, sr.Height);
            chkSpeedManual.Bounds = new Rectangle(sr.X + sr.Width - 80, sr.Y + 4, 80, 22);

            txtAccPct.Bounds = Field(0, 3);
            var ar = Field(1, 3);
            txtAcc.Bounds = new Rectangle(ar.X, ar.Y, ar.Width - 86, ar.Height);
            chkAccManual.Bounds = new Rectangle(ar.X + ar.Width - 80, ar.Y + 4, 80, 22);

            txtPPMul.Bounds = Field(0, 4);
            txtExp.Bounds = Field(1, 4);

            var mr = Field(1, 5);
            txtMissMul.Bounds = new Rectangle(mr.X, mr.Y, mr.Width - 132, mr.Height);
            chkMissManual.Bounds = new Rectangle(mr.X + mr.Width - 126, mr.Y + 4, 126, 22);

            txtX1.Bounds = Field(0, 6); txtY1.Bounds = Field(1, 6);
            txtX2.Bounds = Field(0, 7); txtY2.Bounds = Field(1, 7);
        }

        // ---------------- 交互 ----------------
        Rectangle SyncButton
        {
            get { var c = InputCard; return new Rectangle(c.Right - 118, c.Y + 12, 100, 24); }
        }

        Rectangle FitButton
        {
            get { var c = InputCard; return new Rectangle(c.Right - 226, c.Y + 12, 100, 24); }
        }

        /// <summary>按 TUF 官方做法：用 (95%→基础分) 与两个实测锚点拟合双曲线。</summary>
        void Fit()
        {
            double x1, y1, x2, y2;
            x1 = 0; y1 = 0; x2 = 0; y2 = 0;
            bool ok = double.TryParse(txtX1.Text, out x1);
            ok &= double.TryParse(txtY1.Text, out y1);
            ok &= double.TryParse(txtX2.Text, out x2);
            ok &= double.TryParse(txtY2.Text, out y2);
            if (!ok)
            {
                M.FitNote = "请把两个锚点的准度与分数都填上";
                Invalidate();
                return;
            }
            bool done = M.FitHyper(x1 / 100.0, y1, x2 / 100.0, y2);
            if (done)
            {
                M.AccManual = false;
                chkAccManual.Checked = false;
                M.ApplyAcc();
                SyncAcc();
            }
            Invalidate();
        }

        void ChangeTier(int delta)
        {
            int n = Tuf.TierKeys.Count;
            tierIndex = ((tierIndex + delta) % n + n) % n;
            M.ApplyTier(Tuf.TierKeys[tierIndex], false);
            SyncControls();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (SyncButton.Contains(e.Location)) { SyncFromReplay(); return; }
            if (FitButton.Contains(e.Location)) { Fit(); return; }
            var tr = Field(0, 0);
            if (tr.Contains(e.Location)) { ChangeTier(e.X < tr.X + tr.Width / 2 ? -1 : 1); return; }
            var hr = Field(0, 5);
            if (hr.Contains(e.Location))
            {
                holdIndex = 1 - holdIndex;
                M.NonStandardHold = holdIndex == 1;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Field(0, 0).Contains(e.Location)) { ChangeTier(e.Delta > 0 ? -1 : 1); return; }
            if (Field(0, 5).Contains(e.Location))
            {
                holdIndex = 1 - holdIndex;
                M.NonStandardHold = holdIndex == 1;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = Field(0, 0).Contains(e.Location) || Field(0, 5).Contains(e.Location) || SyncButton.Contains(e.Location) || FitButton.Contains(e.Location);
            Cursor = over ? Cursors.Hand : Cursors.Default;
        }

        // ---------------- 绘制 ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Prepare(e.Graphics);
            using (var b = new SolidBrush(Theme.Bg)) e.Graphics.FillRectangle(b, ClientRectangle);
            RenderStatic(e.Graphics, ClientRectangle);
        }

        public void RenderStatic(Graphics g, Rectangle r)
        {
            Theme.Prepare(g);
            Chart.Text(g, "TUF 评分估算", Theme.F(15f, true), Theme.Text, Pad, 20);
            Chart.Text(g, "基础分 × X-Acc 倍率 × 速度倍率 × 漏击倍率 × 长按倍率　（等级自行填写，数值自动取自当前回放）",
                Theme.F(9f, false), Theme.Faint, Pad, 50);

            var inCard = InputCard;
            var outCard = ResultCard;
            Chart.Panel(g, inCard, 12f, Theme.Panel, Theme.Border);
            Chart.Panel(g, outCard, 12f, Theme.Panel, Theme.Border);

            Chart.Text(g, "输入", Theme.F(11f, true), Theme.Text, inCard.X + 18, inCard.Y + 14);
            var fb = FitButton;
            Chart.Fill(g, fb, 6f, Theme.Panel2);
            Chart.Stroke(g, new RectangleF(fb.X + 0.5f, fb.Y + 0.5f, fb.Width - 1, fb.Height - 1), 6f, Theme.Border, 1f);
            Chart.TextCenter(g, "拟合官方曲线", Theme.F(8.5f, false), Theme.Accent, fb);

            var sb = SyncButton;
            Chart.Fill(g, sb, 6f, Theme.Panel2);
            Chart.Stroke(g, new RectangleF(sb.X + 0.5f, sb.Y + 0.5f, sb.Width - 1, sb.Height - 1), 6f, Theme.Border, 1f);
            Chart.TextCenter(g, "↻ 同步回放", Theme.F(8.5f, false), Theme.Accent, sb);
            float noteW = Math.Max(60f, inCard.Width - 290f);
            Chart.TextRight(g, Chart.Ellipsis(g, SourceNote, Theme.F(8f, false), noteW),
                Theme.F(8f, false), Theme.Faint, inCard.Right - 236, inCard.Y + 18, noteW);

            string[] labels =
            {
                "等级（点击左右切换 / 滚轮）", "基础分（PP）",
                "谱面物量（砖数）", "漏击数（Too Early）",
                "游玩速度（%）", "速度倍率（按区间自动）",
                "X-Acc（%）", "X-Acc 倍率（自动估算）",
                "PP 倍率（满准度时的倍率）", "曲线指数 n",
                "长按方式（点击切换）", "", "锚点① 准度（%）", "锚点① 分数（0 漏击）", "锚点② 准度（%）", "锚点② 分数（0 漏击）"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].Length == 0) continue;
                var f = Field(i % 2, i / 2);
                Chart.Text(g, labels[i], Theme.F(8.5f, false), Theme.Dim, f.X, f.Y - 17);
            }

            DrawStepper(g, Field(0, 0), Tuf.TierKeys[tierIndex]);
            DrawStepper(g, Field(0, 5), holdIndex == 0 ? "标准长按 ×1.00" : "非标准长按 ×0.95");



            int ry = inCard.Bottom - 116;
            Chart.Fill(g, new RectangleF(inCard.X + 18, ry - 14, inCard.Width - 36, 1), 0f, Theme.Border);
            Chart.Text(g, "规则速查", Theme.F(9f, true), Theme.Dim, inCard.X + 18, ry);
            string[] rules =
            {
                "0 漏击（无 Too Early）→ ×1.1；宽恕 = floor(物量 ÷ 315)",
                "速度：110–150% 固定 ×0.65；≥200% ×1.00；其余区间未公开",
                "非标准长按 → ×0.95；U13+ 无固定基础分",
                "X-Acc 曲线 = 过 (95%, 基础分) 与两个锚点的双曲线（官方做法）"
            };
            float y2 = ry + 20;
            foreach (var line in rules)
            {
                Chart.Text(g, "· " + line, Theme.F(8.5f, false), Theme.Faint, inCard.X + 18, y2);
                y2 += 17;
            }

            DrawResult(g, outCard);
        }

        void DrawStepper(Graphics g, Rectangle box, string text)
        {
            Chart.Fill(g, box, 5f, Theme.Panel2);
            Chart.Stroke(g, new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), 5f, Theme.Border, 1f);
            Chart.TextCenter(g, "◀", Theme.F(9f, true), Theme.Dim, new RectangleF(box.X + 2, box.Y, 22, box.Height));
            Chart.TextCenter(g, "▶", Theme.F(9f, true), Theme.Dim, new RectangleF(box.Right - 24, box.Y, 22, box.Height));
            Chart.TextCenter(g, text, Theme.F(9.5f, false), Theme.Text, new RectangleF(box.X + 24, box.Y, box.Width - 48, box.Height));
        }

        void DrawResult(Graphics g, Rectangle card)
        {
            int x = card.X + 22, y = card.Y + 18;

            var icon = LoadIcon(M.Tier);
            int iconSize = 132;
            var iconRect = new Rectangle(card.Right - iconSize - 22, y, iconSize, iconSize);
            if (icon != null)
            {
                float scale = Math.Min((float)iconSize / icon.Width, (float)iconSize / icon.Height);
                int w = (int)(icon.Width * scale), h = (int)(icon.Height * scale);
                g.DrawImage(icon, new Rectangle(iconRect.X + (iconSize - w) / 2, iconRect.Y + (iconSize - h) / 2, w, h));
            }
            else
            {
                Chart.Fill(g, new RectangleF(iconRect.X, iconRect.Y, iconSize, iconSize), iconSize / 2f, Theme.Panel2);
                Chart.TextCenter(g, M.Tier, Theme.F(22f, true), Theme.Text, iconRect);
            }

            Chart.Text(g, "最终得分", Theme.F(9f, false), Theme.Dim, x, y);
            Chart.Text(g, M.Final.ToString("#,0.######"), Theme.F(30f, true), Theme.Green, x - 2, y + 16);

            Chart.Text(g, M.Tier + "　" + Tuf.Family(M.Tier), Theme.F(10f, true), Theme.Text, x, y + 62);
            if (!M.BaseFixed)
                Chart.Text(g, "该等级无官方固定基础分（U13+），请手填基础分", Theme.F(8.5f, false), Theme.Yellow, x, y + 82);

            double refMid, refHigh;
            if (Tuf.RefRange(M.Tier, out refMid, out refHigh))
                Chart.Text(g, string.Format("官方参考：中位 {0:N0} ／ 高位 {1:N0}", refMid, refHigh),
                    Theme.F(8.5f, false), Theme.Faint, x, y + 100);

            int by = y + 112;
            Chart.Text(g, "倍率拆解", Theme.F(9f, true), Theme.Dim, x, by);
            by += 22;
            AddRow(g, x, ref by, "基础分", M.Base <= 0 ? "—" : M.Base.ToString("#,0.###"), Theme.Dim, Theme.Text);
            AddRow(g, x, ref by, "× X-Acc 倍率", M.AccMul.ToString("0.0000") + "（准度 " + M.AccPct.ToString("0.####") + "%）", Theme.Dim, Theme.Text);
            AddRow(g, x, ref by, "× 速度倍率", M.SpeedMul.ToString("0.0000") + "（" + M.SpeedPct.ToString("0.#") + "%）", Theme.Dim, Theme.Text);
            AddRow(g, x, ref by, "× 漏击倍率", M.MissMul.ToString("0.0000") +
                (M.MissManual ? "（手填）" : (M.Misses == 0 ? "（0 漏击 ×1.1）" : "（在宽恕内 ×1.0）")), Theme.Dim, Theme.Text);
            AddRow(g, x, ref by, "× 长按倍率", M.HoldMul.ToString("0.0000"), Theme.Dim, Theme.Text);

            by += 8;
            Chart.Fill(g, new RectangleF(x, by, card.Width - 44, 1), 0f, Theme.Border);
            by += 12;
            AddRow(g, x, ref by, "= 最终得分", M.Final.ToString("#,0.######"), Theme.Dim, Theme.Green);

            int ny = card.Bottom - 116;
            Chart.Text(g, string.Format("宽恕漏击 = floor(物量 ÷ 315) = {0} 次", M.Grace), Theme.F(9f, false), Theme.Dim, x, ny);
            if (M.Misses > M.Grace)
                Chart.Text(g, string.Format("漏击 {0} 次，超出宽恕 {1} 次 —— 官方未公开超出部分的系数，当前按 ×1.0 计",
                    M.Misses, M.OverGrace), Theme.F(8.5f, false), Theme.Yellow, x, ny + 19);
            else if (M.Misses > 0)
                Chart.Text(g, string.Format("漏击 {0} 次，仍在宽恕范围内，不漏分", M.Misses), Theme.F(8.5f, false), Theme.Faint, x, ny + 19);

            // 分数-准度曲线（95% → 100%）
            DrawCurve(g, card, x, by + 30);

            Chart.Text(g, M.SpeedNote, Theme.F(8.5f, false),
                M.SpeedManual || M.SpeedMul == 0.65 || M.SpeedPct >= 200 ? Theme.Faint : Theme.Yellow, x, ny + 40);
            Chart.Text(g, "未拟合时用「PP 倍率 × 准度^指数」估算；填两个锚点后可精确复现官方曲线。",
                Theme.F(8f, false), Theme.Faint, x, ny + 60);
        }

        /// <summary>画 95%→100% 的分数曲线（拟合成功用官方双曲线，否则用指数模型）。</summary>
        void DrawCurve(Graphics g, Rectangle card, int x, int y)
        {
            if (M.Base <= 0) return;
            int w = card.Width - 300, h = 150;
            if (w < 160 || y + h > card.Bottom - 130) return;
            var plot = new Rectangle(x, y, w, h);

            double topScore = M.UseHyper
                ? Math.Max(M.HA + M.HB / (1.0 - M.HC), 1)
                : M.Base * M.PPMul;
            topScore *= 1.05;

            Chart.Text(g, "分数 vs 准度（95% → 100%）", Theme.F(8.5f, true), Theme.Dim, plot.X, plot.Y - 2);
            var area = new Rectangle(plot.X + 42, plot.Y + 16, plot.Width - 52, plot.Height - 40);
            Chart.Fill(g, area, 4f, Color.FromArgb(16, 255, 255, 255));
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, area);

            var pts = new List<PointF>();
            for (int k = 0; k <= 100; k++)
            {
                double acc = 0.95 + 0.05 * k / 100.0;
                double score;
                if (M.UseHyper)
                {
                    double d = acc - M.HC;
                    if (Math.Abs(d) < 1e-9) d = d < 0 ? -1e-9 : 1e-9;
                    score = M.HA + M.HB / d;
                }
                else
                {
                    score = M.Base * M.PPMul * Math.Pow(acc, M.AccExp);
                }
                float px = area.X + (float)(area.Width * (acc - 0.95) / 0.05);
                float py = area.Bottom - (float)(area.Height * Math.Min(Math.Max(score / topScore, 0), 1));
                pts.Add(new PointF(px, py));
            }
            if (pts.Count > 1)
                using (var pen = new Pen(Theme.Accent, 1.8f))
                    g.DrawLines(pen, pts.ToArray());

            // 当前准度点
            float cx = area.X + (float)(area.Width * Math.Min(Math.Max((M.AccPct / 100.0 - 0.95) / 0.05, 0), 1));
            float cy = area.Bottom - (float)(area.Height * Math.Min(Math.Max(M.AccMul * M.Base / topScore, 0), 1));
            using (var b = new SolidBrush(Theme.Green))
                g.FillEllipse(b, cx - 3, cy - 3, 6, 6);

            Chart.Text(g, "95%", Theme.F(7.5f, false), Theme.Faint, area.X, area.Bottom + 2);
            Chart.TextRight(g, "100%", Theme.F(7.5f, false), Theme.Faint, area.Right, area.Bottom + 2, 40);
            Chart.TextRight(g, topScore.ToString("#,0"), Theme.F(7.5f, false), Theme.Faint, area.Right, area.Y - 12, 80);
            Chart.Text(g, "0", Theme.F(7.5f, false), Theme.Faint, area.X - 10, area.Bottom - 8);

            Chart.Text(g, M.UseHyper ? M.FitNote : "未拟合：用「PP 倍率 × 准度^指数」估算（可填两个锚点改用官方双曲线）",
                Theme.F(8f, false), M.UseHyper ? Theme.Green : Theme.Faint, plot.X, plot.Bottom + 2);
        }

        void AddRow(Graphics g, int x, ref int y, string k, string v, Color kc, Color vc)
        {
            Chart.Text(g, k, Theme.F(9f, false), kc, x, y);
            Chart.TextRight(g, v, Theme.F(9f, false), vc, ResultCard.Right - 24, y, 340);
            y += 21;
        }

        static Image LoadIcon(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return null;
            Image img;
            if (IconCache.TryGetValue(tier, out img)) return img;
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
            foreach (var name in new string[] { tier + ".png", tier + "-1.png" })
            {
                string p = Path.Combine(dir, name);
                try
                {
                    if (File.Exists(p))
                    {
                        using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read))
                        using (var tmp = Image.FromStream(fs))
                            img = new Bitmap(tmp);
                        IconCache[tier] = img;
                        return img;
                    }
                }
                catch { }
            }
            IconCache[tier] = null;
            return null;
        }
    }
}
