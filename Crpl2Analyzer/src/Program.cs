// Crpl2Analyzer - ADOFAI Creplay replay analyzer
// Copyright (C) 2026 InbottlesN235
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crpl2
{
    // 自绘用的双缓冲 Panel：避免鼠标经过时整块重绘闪烁
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }

        // 背景在 Paint 中一并绘制（双缓冲内），不额外擦除
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    public class ViewHost : Control
    {
        public IView View;
        public AppState State;
        public event EventHandler FilesDropped;

        public ViewHost()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AllowDrop = true;
            BackColor = Theme.Bg;
            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) { DroppedFiles = files; var h = FilesDropped; if (h != null) h(this, EventArgs.Empty); }
            };
        }

        public string[] DroppedFiles;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            using (var b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, ClientRectangle);
            var r = new Rectangle(16, 12, Width - 32, Height - 24);
            if (View != null) View.Render(g, r, State);
        }
    }

    public class MainForm : Form
    {
        readonly AppState st = new AppState();
        readonly IView[] views = { new OverviewView(), new AccuracyView(), new XAccView(), new ErrorView(), new TimelineView(), new KeysView(), new CompareView(), new DataView() };
        ScorePanel scorePanel;
        const int ScoreTab = 7;   // TabNames 里「评分估算」的位置
        int viewIndex = 0;
        int hoverTab = -1;
        string status = "就绪 · 拖入 .crpl2 回放文件，或点击「打开回放」";

        ViewHost host;
        Panel nav;
        Panel header;
        Panel statusBar;
        ComboBox cbo;

        public MainForm()
        {
            Text = "CRPL2 回放分析器 · 冰与火之舞";
            ClientSize = new Size(1380, 880);
            MinimumSize = new Size(1120, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            Font = Theme.F(9f, false);
            KeyPreview = true;

            statusBar = new DoubleBufferedPanel { Dock = DockStyle.Bottom, Height = 26, BackColor = Theme.Bg2 };
            statusBar.Paint += (s, e) =>
            {
                Theme.Prepare(e.Graphics);
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0);
                Chart.Text(e.Graphics, status, Theme.F(8.5f, false), Theme.Faint, 14, 5);
            };

            host = new ViewHost { Dock = DockStyle.Fill, State = st, View = views[0] };
            host.FilesDropped += (s, e) => LoadFiles(host.DroppedFiles);

            nav = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.Bg2 };
            nav.Paint += NavPaint;
            nav.MouseMove += (s, e) =>
            {
                int h = TabAt(e.X);
                if (h == hoverTab) return;
                int old = hoverTab;
                hoverTab = h;
                if (old >= 0) nav.Invalidate(TabRect(old));
                if (h >= 0) nav.Invalidate(TabRect(h));
            };
            nav.MouseLeave += (s, e) =>
            {
                if (hoverTab < 0) return;
                int old = hoverTab;
                hoverTab = -1;
                nav.Invalidate(TabRect(old));
            };
            nav.MouseDown += (s, e) => { int h = TabAt(e.X); if (h >= 0) SelectView(h); };

            header = new DoubleBufferedPanel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Bg };
            header.Paint += HeaderPaint;

            scorePanel = new ScorePanel { Dock = DockStyle.Fill, Visible = false, State = st };
            Controls.Add(host);
            Controls.Add(scorePanel);
            Controls.Add(nav);
            Controls.Add(header);
            Controls.Add(statusBar);
            scorePanel.BringToFront();

            BuildHeaderControls();
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) LoadFiles(files);
            };
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == System.Windows.Forms.Keys.O) { OpenDialog(); e.Handled = true; } };
        }

        void BuildHeaderControls()
        {
            int bw = 108, bh = 32, y = 18, x = 0;
            var btnOpen = MakeButton("打开回放", x = 250);
            btnOpen.Click += (s, e) => OpenDialog();
            var btnPng = MakeButton("导出图表", x += bw + 8);
            btnPng.Click += (s, e) => ExportPng();
            var btnData = MakeButton("导出数据", x += bw + 8);
            btnData.Click += (s, e) => ExportData();
            var btnAbout = MakeButton("关于", x += bw + 8);
            btnAbout.Click += (s, e) => ShowAbout();
            var btnClose = MakeButton("清空", x += bw + 8);
            btnClose.Click += (s, e) => { st.Files.Clear(); st.Selected = 0; RefreshFiles(); };

            cbo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel2,
                ForeColor = Theme.Text,
                Width = 250,
                Height = 26
            };
            cbo.SelectedIndexChanged += (s, e) =>
            {
                if (cbo.SelectedIndex >= 0 && cbo.SelectedIndex < st.Files.Count)
                {
                    st.Selected = cbo.SelectedIndex;
                    host.Invalidate();
                    if (scorePanel != null) scorePanel.SyncFromReplay();
                    nav.Invalidate();
                    UpdateStatus();
                }
            };
            header.Controls.Add(cbo);
            header.Resize += (s, e) => LayoutHeader();
            LayoutHeader();
        }

        Button MakeButton(string text, int x)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel2,
                ForeColor = Theme.Text,
                Size = new Size(108, 32),
                Location = new Point(x, 16),
                Cursor = Cursors.Hand,
                Font = Theme.F(9f, false),
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Panel;
            header.Controls.Add(b);
            return b;
        }

        void LayoutHeader()
        {
            int right = header.ClientSize.Width - 16;
            foreach (Control c in header.Controls)
            {
                if (!(c is Button)) continue;
                c.Top = 16;
            }
            // 按钮右对齐
            var buttons = header.Controls.OfType<Button>().OrderByDescending(b => b.Left).ToList();
            int bx = right;
            foreach (var b in buttons)
            {
                bx -= b.Width;
                b.Left = bx;
                bx -= 8;
            }
            if (cbo != null)
            {
                cbo.Left = 306;
                cbo.Top = 19;
                cbo.Width = Math.Max(150, Math.Min(330, bx - 320));
            }
        }

        string[] TabNames = { "总览", "准度分布", "X-Acc曲线", "误差", "时间轴", "按键", "对比", "评分估算", "数据" };

        int TabAt(int x)
        {
            int cx = 16;
            for (int k = 0; k < TabNames.Length; k++)
            {
                int w = TabWidth(k);
                if (x >= cx && x < cx + w) return k;
                cx += w;
            }
            return -1;
        }

        int TabWidth(int k)
        {
            return 26 + TabNames[k].Length * 20;
        }

        Rectangle TabRect(int k)
        {
            int x = 16;
            for (int i = 0; i < k; i++) x += TabWidth(i);
            return new Rectangle(x - 2, 4, TabWidth(k) + 4, nav.Height - 8);
        }

        void NavPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            using (var b = new SolidBrush(Theme.Bg2)) g.FillRectangle(b, nav.ClientRectangle);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, nav.Height - 1, nav.Width, nav.Height - 1);

            int x = 16;
            for (int k = 0; k < TabNames.Length; k++)
            {
                int w = TabWidth(k);
                var r = new Rectangle(x, 8, w, nav.Height - 16);
                bool sel = k == viewIndex;
                if (sel) Chart.Fill(g, r, 8f, Theme.Panel);
                else if (k == hoverTab) Chart.Fill(g, r, 8f, Color.FromArgb(22, 255, 255, 255));
                Chart.TextCenter(g, TabNames[k], Theme.F(10f, sel), sel ? Theme.Text : Theme.Dim, r);
                if (sel)
                    Chart.Fill(g, new RectangleF(r.X + 10, r.Bottom - 4, r.Width - 20, 3), 2f, Theme.Accent);
                x += w;
            }
            if (st.Files.Count > 0)
            {
                var cur = st.Current;
                string info = (cur == null ? "" : cur.FileName) + "  ·  " + st.Files.Count + " 个文件";
                int tabsW = 16;
                for (int k = 0; k < TabNames.Length; k++) tabsW += TabWidth(k);
                float maxW = nav.Width - tabsW - 26f;
                if (maxW > 120f)
                {
                    maxW = Math.Min(maxW, 640f);
                    Chart.TextRight(g, Chart.Ellipsis(g, info, Theme.F(8.5f, false), maxW),
                        Theme.F(8.5f, false), Theme.Faint, nav.Width - 18, 17, maxW);
                }
            }
        }

        void HeaderPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            using (var b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, header.ClientRectangle);
            Chart.Fill(g, new RectangleF(16, 16, 40, 32), 8f, Theme.Accent);
            Chart.TextCenter(g, "CR", Theme.F(11f, true), Color.White, new RectangleF(16, 16, 40, 32));
            Chart.Text(g, "CRPL2 回放分析器", Theme.F(13f, true), Theme.Text, 68, 12);
            Chart.Text(g, "Creplay 回放 · 判定分布 · 准确率可视化", Theme.F(8.5f, false), Theme.Faint, 68, 34);
        }

        // ------------------------------------------------------------------
        void OpenDialog()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "选择 Creplay 回放文件";
                d.Filter = "Creplay 回放 (*.crpl2;*.pcrpl2)|*.crpl2;*.pcrpl2|所有文件 (*.*)|*.*";
                d.Multiselect = true;
                if (d.ShowDialog(this) == DialogResult.OK) LoadFiles(d.FileNames);
            }
        }

        public void LoadPaths(string[] paths) { LoadFiles(paths); }

        // tab 序号 → views 下标（评分估算页不是 IView）
        int ViewIndexFor(int tab)
        {
            if (tab < ScoreTab) return tab;
            return tab - 1;
        }

        public void SelectView(int index)
        {
            if (index < 0 || index >= TabNames.Length) return;
            viewIndex = index;
            bool score = index == ScoreTab;
            if (scorePanel != null) scorePanel.Visible = score;
            host.Visible = !score;
            if (!score)
            {
                host.View = views[ViewIndexFor(index)];
                host.Invalidate();
            }
            else
            {
                scorePanel.SyncFromReplay();
                scorePanel.Invalidate();
            }
            nav.Invalidate();
        }

        void LoadFiles(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            var sw = Stopwatch.StartNew();
            int first = st.Files.Count;
            var errors = new List<string>();
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                var rp = Replay.Load(p);
                if (!rp.IsLoaded) { errors.Add(Path.GetFileName(p) + " → " + rp.LoadError); continue; }
                if (st.Files.Any(x => x.Path.Equals(p, StringComparison.OrdinalIgnoreCase))) continue;
                st.Files.Add(rp);
            }
            sw.Stop();
            if (st.Files.Count > first) st.Selected = st.Files.Count - 1;
            RefreshFiles();
            status = string.Format("已加载 {0} 个回放（本次 {1} 个，耗时 {2} ms）", st.Files.Count,
                st.Files.Count - first, sw.ElapsedMilliseconds);
            statusBar.Invalidate();
            if (errors.Count > 0)
                MessageBox.Show(this, "以下文件无法解析：\n\n" + string.Join("\n", errors.ToArray()),
                    "解析失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void RefreshFiles()
        {
            cbo.Items.Clear();
            foreach (var f in st.Files) cbo.Items.Add(ShortName(f));
            if (st.Files.Count > 0) { cbo.SelectedIndex = st.Selected; cbo.Visible = true; }
            else { cbo.Visible = false; }
            host.Invalidate();
            nav.Invalidate();
            UpdateStatus();
        }

        static string ShortName(Replay r)
        {
            string n = r.FileName;
            return n.Length > 40 ? n.Substring(0, 38) + "…" : n;
        }

        void UpdateStatus()
        {
            var c = st.Current;
            if (c == null)
            {
                status = "就绪 · 拖入 .crpl2 回放文件，或点击「打开回放」";
            }
            else
            {
                status = string.Format("{0} · {1} 击 · 准确率 {2} · 时长 {3} · {4} KB",
                    c.SongName, c.HitCount, Fmt.Pct(c.EffectiveAccuracy, "0.00"), c.DurationText, c.Size / 1024);
            }
            statusBar.Invalidate();
        }

        // ------------------------------------------------------------------
        void ExportPng()
        {
            if (st.Ok == null && viewIndex != ScoreTab) { MessageBox.Show(this, "先打开一个回放文件。"); return; }
            using (var d = new SaveFileDialog())
            {
                d.Filter = "PNG 图片 (*.png)|*.png";
                d.FileName = TabNames[viewIndex] + ".png";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var size = new Size(Math.Max(host.Width, 1200), Math.Max(host.Height, 760));
                using (var bmp = new Bitmap(size.Width, size.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        Theme.Prepare(g);
                        using (var br = new SolidBrush(Theme.Bg)) g.FillRectangle(br, 0, 0, bmp.Width, bmp.Height);
                        if (viewIndex == ScoreTab) scorePanel.RenderStatic(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
                        else views[ViewIndexFor(viewIndex)].Render(g, new Rectangle(16, 12, bmp.Width - 32, bmp.Height - 24), st);
                    }
                    bmp.Save(d.FileName, ImageFormat.Png);
                }
                status = "已导出图表：" + Path.GetFileName(d.FileName);
                statusBar.Invalidate();
            }
        }

        void ExportData()
        {
            var c = st.Current;
            if (c == null || !c.IsLoaded) { MessageBox.Show(this, "先打开一个回放文件。"); return; }
            using (var d = new SaveFileDialog())
            {
                d.Filter = "JSON 数据 (*.json)|*.json|按键 CSV (*.csv)|*.csv|判定 CSV (*.csv)|*.csv";
                d.FileName = Path.GetFileNameWithoutExtension(c.FileName) + ".json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (d.FilterIndex == 2) File.WriteAllText(d.FileName, c.PressCsv(), System.Text.Encoding.UTF8);
                else if (d.FilterIndex == 3) File.WriteAllText(d.FileName, JudgmentCsv(c), System.Text.Encoding.UTF8);
                else File.WriteAllText(d.FileName, c.ToJson(), System.Text.Encoding.UTF8);
                status = "已导出数据：" + Path.GetFileName(d.FileName);
                statusBar.Invalidate();
            }
        }

        static string JudgmentCsv(Replay r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("judgment,count,share");
            for (int k = 0; k < r.Judgment.Length; k++)
            {
                if (r.Judgment[k] == 0) continue;
                sb.AppendLine(string.Format("{0},{1},{2:0.000000}", r.Longs[k], r.Judgment[k],
                    r.JudgmentTotal > 0 ? (double)r.Judgment[k] / r.JudgmentTotal : 0));
            }
            return sb.ToString();
        }

        void ShowAbout()
        {
            MessageBox.Show(this,
                "CRPL2 回放分析器  v1.1\n\n" +
                "用于解析 ADOFAI（冰与火之舞）Creplay 模组录制的 .crpl2 回放，\n" +
                "并可视化判定分布、准确率构成与逐次命中误差。\n\n" +
                "支持容器：\"CRP2\"(v2) 与 \"CRP4\"(v4)，内部 formatVersion 2 / 3\n" +
                "  均为 AES-256-CBC(PKCS7)，key=SHA-256(\"qwerty\")，IV=SHA-256(\"potato\")[0:16]\n\n" +
                "formatVersion 3（Creplay 2.17 / ADOFAI 3.4.0）新增：\n" +
                "  · 判定槽位扩到 16，Perfect 拆成三档（中心 ±12.5° 单独一档）\n" +
                "  · 每次命中的角度误差与时间误差（可画误差散点与分布）\n" +
                "  · keyCodes 由 ushort 变为 byte\n\n" +
                "X-Acc 权重：完美类 1.00 / 早·晚完美 0.75 / 偏差档 0.40 / 严重偏差 0.20",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                RunMain(args);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                        ex.GetType().FullName + "\n" + ex.Message + "\n" + ex.StackTrace + "\n", System.Text.Encoding.UTF8);
                }
                catch { }
                throw;
            }
        }

        static void RunMain(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && args[0] == "--shot")
            {
                Screenshot(args[1], args[2], args.Length > 3 ? int.Parse(args[3]) : 0);
                return;
            }

            if (args.Length >= 2 && args[0] == "--selftest")
            {
                SelfTest(args[1], args.Length >= 3 ? args[2] : ".");
                return;
            }

            var f = new MainForm();
            var open = args.Where(File.Exists).ToArray();
            if (open.Length > 0) f.Shown += (s, e) => f.LoadPaths(open);
            Application.Run(f);
        }

        static void Screenshot(string file, string png, int viewIndex)
        {
            var f = new MainForm();
            f.ClientSize = new Size(1380, 880);
            f.LoadPaths(new string[] { file });
            f.SelectView(viewIndex);
            f.Show();
            Application.DoEvents();
            System.Threading.Thread.Sleep(400);
            Application.DoEvents();
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(png, ImageFormat.Png);
            }
            f.Close();
        }

        static void SelfTest(string file, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var st = new AppState();
            var rp = Replay.Load(file);
            st.Files.Add(rp);
            st.Selected = 0;

            IView[] views = { new OverviewView(), new AccuracyView(), new XAccView(), new ErrorView(), new TimelineView(), new KeysView(), new CompareView(), new DataView() };
            string[] names = { "1-overview", "2-accuracy", "3-xacc", "4-error", "5-timeline", "6-keys", "7-compare", "8-score", "9-data" };
            var log = new System.Text.StringBuilder();
            log.AppendLine("file=" + file);
            if (!rp.IsLoaded) log.AppendLine("error=" + rp.LoadError);
            else
            {
                log.AppendLine("hits=" + rp.HitCount);
                log.AppendLine("judgment=" + string.Join(",", rp.Judgment.Select(x => x.ToString()).ToArray()));
                log.AppendLine("formatVersion=" + rp.FormatVersion + " container=" + rp.ContainerMagic + " v" + rp.ContainerVersion);
                log.AppendLine("bpm=" + rp.Bpm.ToString("0.###") + " derived=" + rp.DerivedBpm.ToString("0.###"));
                log.AppendLine("errors=" + (rp.HasHitErrors ? rp.HitTimeErr.Length.ToString() : "none"));
                log.AppendLine("xaccValid=" + rp.XAccValid + " xaccFinal=" + rp.XAccFinal.ToString("R") + " match=" + rp.XAccMatchesFile);
                log.AppendLine("derived=" + (rp.DerivedJudge == null ? "none" : string.Join(",", rp.DerivedJudge.Select(x => x.ToString()).ToArray())));
                log.AppendLine("acc=" + rp.Accuracy.ToString("R"));
                log.AppendLine("xacc=" + rp.AccuracyFile.ToString("R"));
                log.AppendLine("presses=" + rp.PressCount);
                log.AppendLine("keys=" + rp.KeyUsage.Count);
                log.AppendLine("chords20=" + rp.ChordPairs);
                log.AppendLine("duration=" + rp.LastEvent);
            }

            var size = new Size(1380, 800);
            ScorePanel sp = null;
            for (int k = 0; k < names.Length; k++)
            {
                using (var bmp = new Bitmap(size.Width, size.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        Theme.Prepare(g);
                        using (var br = new SolidBrush(Theme.Bg)) g.FillRectangle(br, 0, 0, bmp.Width, bmp.Height);
                        if (k == 7)
                        {
                            if (sp == null) { sp = new ScorePanel(); sp.Size = size; sp.M.PerformSample(); }
                            sp.RenderStatic(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
                        }
                        else
                        {
                            views[k < 7 ? k : k - 1].Render(g, new Rectangle(16, 12, bmp.Width - 32, bmp.Height - 24), st);
                        }
                    }
                    string p = Path.Combine(outDir, names[k] + ".png");
                    bmp.Save(p, ImageFormat.Png);
                    log.AppendLine("png=" + p);
                }
            }
            File.WriteAllText(Path.Combine(outDir, "selftest.txt"), log.ToString(), System.Text.Encoding.UTF8);
        }
    }
}
