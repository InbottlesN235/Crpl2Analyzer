// Crpl2Analyzer - ADOFAI Creplay replay analyzer
// Copyright (C) 2026 InbottlesN235
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Crpl2
{
    public class PressEvent
    {
        public double Time;
        public int Vk;
        public PressEvent(double t, int vk) { Time = t; Vk = vk; }
    }

    public class Replay
    {
        public string Path;
        public string FileName;
        public long Size;
        public string Sha256;
        public string ContainerMagic = "CRP2";
        public int ContainerVersion;
        public int FormatVersion;

        public Dictionary<string, string> S = new Dictionary<string, string>();
        public Dictionary<string, bool> B = new Dictionary<string, bool>();
        public Dictionary<string, int> I = new Dictionary<string, int>();
        public Dictionary<string, double> D = new Dictionary<string, double>();

        public int[] KeyCodes;
        public int[] KeyPresses;
        public double[] SongPos;
        public int[] FloorIds;
        public double[] CurAngles;
        public float[] Overload;
        public double[] CachedAngles;
        public double[] TargetExit;
        public int[] FreeRoam;
        public int[] Flags;

        // ---- format v3 (CRP4) extras ----
        public double[] HitTimeErr;      // 每次命中的时间误差（秒，正=晚）
        public double[] HitAngleErr;     // 每次命中的角度误差（弧度，正=晚）
        public double DerivedBpm;        // 由 角度误差/时间误差 反推出的 BPM
        public int[] UnknownEvent1;
        public int[] UnknownEvent2;

        // ---- 实时 X-Acc（由逐次角度误差反推判定）----
        public int[] HitMarginIdx;          // 每击的判定序号
        public int[] DerivedJudge;          // 反推出的判定分布
        public double[] XAccSeries;         // 累计 X-Acc
        public double[] XAccRolling;        // 滑动窗口 X-Acc
        public double XAccFinal;
        public bool XAccValid;
        public bool XAccMatchesFile;
        public double XpDeg = 12.5, PureDeg = 30, PerfectDeg = 45, CountedDeg = 60;

        public int[] Judgment;
        public int[] CoreIdx = new int[0];
        public int JudgmentTotal;
        public double Accuracy;
        public double AccuracyFile;
        public List<PressEvent> Presses = new List<PressEvent>();
        public int HitCount;
        public int PressCount;
        public double PressFirst;
        public double PressLast;
        public double LastEvent;
        public int ChordPairs;
        public int ChordPairs30;
        public Dictionary<int, int> KeyUsage = new Dictionary<int, int>();
        public int[] SecondDensity = new int[1];
        public int MaxDensity;
        public int MaxDensitySecond;
        public double AvgPps;
        public int AutoHits;
        public int NoFailHits;
        public int MidspinHits;
        public int FreeRoamHits;
        public int DistinctFloors;
        public int StartTile;
        public int EndTile;
        public int OverloadPeakIndex;
        public double OverloadPeak;
        public List<double> Gaps = new List<double>();
        public string LoadError;

        public bool IsLoaded { get { return LoadError == null; } }
        public bool HasHitErrors { get { return HitTimeErr != null && HitTimeErr.Length > 0; } }

        // ===================== 判定表 =====================
        // v2 (CRP2 / formatVersion 2) —— ADOFAI 3.3.1 HitMargin 枚举，12 槽
        static readonly string[] V2Short = { "过早", "很过", "早完美", "完美", "晚完美", "很晚", "过晚", "连按", "Miss", "过载", "自动", "过按" };
        static readonly string[] V2Long = { "Too Early", "Very Early", "Early Perfect", "Perfect", "Late Perfect", "Very Late", "Too Late", "Multipress", "FailMiss", "FailOverload", "Auto", "OverPress" };
        static readonly double[] V2Weight = { 0.20, 0.40, 0.75, 1.00, 0.75, 0.40, 0.20, 0.00, 0.00, 0.00, 1.00, 0.00 };

        // v3 (CRP4 / formatVersion 3) —— 3.4.0 起 Perfect 拆成 完美- / X完美 / 完美+
        static readonly string[] V3Short = { "过早", "很过", "早完美", "完美-", "X完美", "完美+", "晚完美", "很晚", "过晚", "连按", "Miss", "过载", "自动", "过按", "扩展14", "扩展15" };
        static readonly string[] V3Long = { "Too Early", "Very Early", "Early Perfect", "Perfect- (Early)", "XPerfect", "Perfect+ (Late)", "Late Perfect", "Very Late", "Too Late", "Multipress", "FailMiss", "FailOverload", "Auto", "OverPress", "Ext14", "Ext15" };
        static readonly double[] V3Weight = { 0.20, 0.40, 0.75, 1.00, 1.00, 1.00, 0.75, 0.40, 0.20, 0.00, 0.00, 0.00, 1.00, 0.00, 1.00, 1.00 };

        public string[] Shorts { get { return FormatVersion >= 3 ? V3Short : V2Short; } }
        public string[] Longs { get { return FormatVersion >= 3 ? V3Long : V2Long; } }
        public double[] Weights { get { return FormatVersion >= 3 ? V3Weight : V2Weight; } }

        public string SongName { get { return GetS("song_name", "(无标题)"); } }
        public string LevelPath { get { return GetS("level_path", ""); } }
        public string JudgeMode { get { return GetS("judge_mode", ""); } }
        public string GameVersion { get { return GetS("GameVersion", ""); } }
        public string ModVersion { get { return GetS("ModVersion", ""); } }
        public string Mods { get { return GetS("LoadedMods", ""); } }
        public string DeviceId { get { return GetS("deviceID", ""); } }

        public double Bpm
        {
            get
            {
                double v = GetD("bpm", 0);
                return v > 0 ? v : DerivedBpm;
            }
        }
        public double PlaybackSpeed { get { return GetD("playbackSpeed", 1); } }
        public double SpeedTrail { get { return GetD("SpeedTrail", 1); } }

        public string GetS(string k, string def)
        {
            string v;
            return S.TryGetValue(k, out v) ? v : def;
        }
        public double GetD(string k, double def)
        {
            double v;
            return D.TryGetValue(k, out v) ? v : def;
        }
        public int GetI(string k, int def)
        {
            int v;
            return I.TryGetValue(k, out v) ? v : def;
        }

        public double EffectiveAccuracy { get { return AccuracyFile > 0 ? AccuracyFile : Accuracy; } }

        public int PerfectTierCount
        {
            get
            {
                if (Judgment == null) return 0;
                if (FormatVersion >= 3)
                    return Idx(3) + Idx(4) + Idx(5);
                return Idx(3);
            }
        }

        public int Idx(int i)
        {
            return Judgment != null && i >= 0 && i < Judgment.Length ? Judgment[i] : 0;
        }

        public int MissCount
        {
            get
            {
                // v2: 8=FailMiss 9=FailOverload ; v3: 10=FailMiss 11=FailOverload
                return FormatVersion >= 3 ? Idx(10) + Idx(11) : Idx(8) + Idx(9);
            }
        }

        public string DurationText
        {
            get
            {
                double s = LastEvent > 0 ? LastEvent : 0;
                int m = (int)(s / 60);
                return string.Format("{0}:{1:00}", m, (int)(s - m * 60));
            }
        }

        public string ContainerText
        {
            get { return ContainerMagic + " v" + ContainerVersion + " / AES-256-CBC"; }
        }

        public string FormatText
        {
            get
            {
                if (FormatVersion >= 3) return "formatVersion 3 (XPerfect)";
                return "formatVersion " + FormatVersion;
            }
        }

        // =====================================================================
        public static Replay Load(string path)
        {
            var r = new Replay();
            r.Path = path;
            r.FileName = System.IO.Path.GetFileName(path);
            try { r.LoadCore(); }
            catch (Exception ex) { r.LoadError = ex.Message; }
            return r;
        }

        void LoadCore()
        {
            byte[] raw = File.ReadAllBytes(Path);
            Size = raw.Length;
            using (var sha = SHA256.Create())
                Sha256 = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();

            if (raw.Length < 16) throw new Exception("文件太小，不是有效的回放");
            string magic = Encoding.ASCII.GetString(raw, 0, 4);
            if (magic != "CRP2" && magic != "CRP4")
                throw new Exception("魔数不是 CRP2/CRP4（可能不是 Creplay 回放文件）");

            ContainerMagic = magic;
            ContainerVersion = BitConverter.ToInt32(raw, 4);
            byte[] body = new byte[raw.Length - 8];
            Array.Copy(raw, 8, body, 0, body.Length);

            byte[] plain = null;
            try { plain = Decrypt(body); }
            catch { plain = null; }
            if (plain == null || !LooksLikeReplay(plain))
            {
                if (LooksLikeReplay(body)) plain = body;
            }
            if (plain == null) throw new Exception("解密失败：未知的 CRP4/CRP2 变体");

            Parse(plain);
        }

        static bool LooksLikeReplay(byte[] d)
        {
            if (d == null || d.Length < 16) return false;
            int v = BitConverter.ToInt32(d, 0);
            if (v < 0 || v > 8) return false;
            int n = BitConverter.ToInt32(d, 4);
            return n >= 0 && n < 4096;
        }

        static byte[] Decrypt(byte[] body)
        {
            byte[] key, iv;
            using (var sha = SHA256.Create()) key = sha.ComputeHash(Encoding.UTF8.GetBytes("qwerty"));
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes("potato"));
                iv = new byte[16];
                Array.Copy(h, iv, 16);
            }
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var dec = aes.CreateDecryptor())
                {
                    try { return dec.TransformFinalBlock(body, 0, body.Length); }
                    catch
                    {
                        using (var aes2 = new AesCryptoServiceProvider())
                        {
                            aes2.Key = key; aes2.IV = iv; aes2.Mode = CipherMode.CBC; aes2.Padding = PaddingMode.None;
                            using (var d2 = aes2.CreateDecryptor()) return d2.TransformFinalBlock(body, 0, body.Length);
                        }
                    }
                }
            }
        }

        class Reader
        {
            readonly byte[] d;
            int o;
            public Reader(byte[] data) { d = data; }
            public int Offset { get { return o; } set { o = value; } }
            public int Length { get { return d.Length; } }
            public bool Done { get { return o >= d.Length; } }
            public int Remaining { get { return d.Length - o; } }
            public int I32() { int v = BitConverter.ToInt32(d, o); o += 4; return v; }
            public ushort U16() { ushort v = BitConverter.ToUInt16(d, o); o += 2; return v; }
            public float F32() { float v = BitConverter.ToSingle(d, o); o += 4; return v; }
            public double F64() { double v = BitConverter.ToDouble(d, o); o += 8; return v; }
            public int U8() { return d[o++]; }
            public string Str()
            {
                int n = 0, shift = 0;
                while (true)
                {
                    byte c = d[o++];
                    n |= (c & 0x7F) << shift;
                    if ((c & 0x80) == 0) break;
                    shift += 7;
                    if (shift > 28) throw new Exception("字符串长度字段损坏");
                }
                string s = Encoding.UTF8.GetString(d, o, n);
                o += n;
                return s;
            }
            public Dictionary<string, string> SD()
            {
                int n = I32(); var m = new Dictionary<string, string>();
                for (int k = 0; k < n; k++) { string key = Str(); m[key] = Str(); }
                return m;
            }
            public Dictionary<string, bool> BD()
            {
                int n = I32(); var m = new Dictionary<string, bool>();
                for (int k = 0; k < n; k++) { string key = Str(); m[key] = U8() != 0; }
                return m;
            }
            public Dictionary<string, int> ID()
            {
                int n = I32(); var m = new Dictionary<string, int>();
                for (int k = 0; k < n; k++) { string key = Str(); m[key] = I32(); }
                return m;
            }
            public Dictionary<string, double> DD()
            {
                int n = I32(); var m = new Dictionary<string, double>();
                for (int k = 0; k < n; k++) { string key = Str(); m[key] = F64(); }
                return m;
            }
            public int Count() { return I32(); }
            public int[] Bytes(int expect)
            {
                int n = Count();
                if (n < 0 || n > Remaining) throw new Exception("数组长度越界（期望 " + expect + "）");
                var a = new int[n];
                for (int k = 0; k < n; k++) a[k] = U8();
                return a;
            }
            public int[] I32s()
            {
                int n = Count();
                if (n < 0 || n * 4L > Remaining) throw new Exception("int 数组长度越界");
                var a = new int[n];
                for (int k = 0; k < n; k++) a[k] = I32();
                return a;
            }
            public ushort[] U16s()
            {
                int n = Count();
                if (n < 0 || n * 2L > Remaining) throw new Exception("ushort 数组长度越界");
                var a = new ushort[n];
                for (int k = 0; k < n; k++) a[k] = U16();
                return a;
            }
            public float[] F32s()
            {
                int n = Count();
                if (n < 0 || n * 4L > Remaining) throw new Exception("float 数组长度越界");
                var a = new float[n];
                for (int k = 0; k < n; k++) a[k] = F32();
                return a;
            }
            public double[] F64s()
            {
                int n = Count();
                if (n < 0 || n * 8L > Remaining) throw new Exception("double 数组长度越界");
                var a = new double[n];
                for (int k = 0; k < n; k++) a[k] = F64();
                return a;
            }
        }

        void Parse(byte[] plain)
        {
            var rd = new Reader(plain);
            FormatVersion = rd.I32();
            S = rd.SD();
            B = rd.BD();
            I = rd.ID();
            D = rd.DD();
            if (FormatVersion < 2 || FormatVersion > 3)
                throw new Exception("未知的内部版本 formatVersion " + FormatVersion);

            int bodyStart = rd.Offset;
            bool v2 = FormatVersion == 2;
            string firstError = null;

            // 先按该版本的正式布局解析；失败则回退到 v2 布局（防止版本号与内容不一致）
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (v2) ParseV2Body(rd); else ParseV3Body(rd);
                    if (rd.Remaining != 0)
                        throw new Exception(string.Format("剩余 {0} 字节未解析", rd.Remaining));
                    ComputeStats();
                    return;
                }
                catch (Exception ex)
                {
                    if (firstError == null) firstError = ex.Message;
                    ClearArrays();
                    rd.Offset = bodyStart;
                    v2 = !v2;
                }
            }
            throw new Exception("解析失败：" + firstError);
        }

        void ClearArrays()
        {
            KeyCodes = null; KeyPresses = null; SongPos = null; FloorIds = null;
            CurAngles = null; Overload = null; CachedAngles = null; TargetExit = null;
            FreeRoam = null; Flags = null; HitTimeErr = null; HitAngleErr = null;
            UnknownEvent1 = null; UnknownEvent2 = null;
        }

        // ---------------- v2 (CRP2) ----------------
        void ParseV2Body(Reader rd)
        {
            ushort[] kc = rd.U16s();
            KeyCodes = kc.Select(x => (int)x).ToArray();
            KeyPresses = rd.I32s();
            SongPos = rd.F64s();
            FloorIds = rd.I32s();
            CurAngles = rd.F64s();
            Overload = rd.F32s();
            CachedAngles = rd.F64s();
            TargetExit = rd.F64s();
            FreeRoam = rd.I32s();
            Flags = rd.Bytes(FloorIds.Length);
        }

        // ---------------- v3 (CRP4) ----------------
        // 实测布局（Creplay 2.17 / ADOFAI 3.4.0）：
        //   byte[]  keyCodes            VK 码
        //   byte[]  ?（新，键盘录制时全 0）
        //   byte[]  ?（新，键盘录制时全 0）
        //   byte[]  keyPresses          0/1
        //   double[] keySongPositions
        //   int[]    hitCurrentFloorIDs
        //   double[] hitCurrAngles      角度误差（弧度，正=晚）
        //   float[]  hitOverloadCounters
        //   double[] hitCachedAngles
        //   double[] hitTargetExitAngles
        //   int[]    hitCurFreeRoamSections
        //   byte[]   hitFlags           (v3 新增 bit5)
        //   double[] hitErrorSeconds    << 3.4.0 新增：逐次时间误差
        void ParseV3Body(Reader rd)
        {
            int[] kc = rd.Bytes(-1);
            KeyCodes = kc;
            UnknownEvent1 = rd.Bytes(-1);
            UnknownEvent2 = rd.Bytes(-1);
            KeyPresses = rd.Bytes(-1);
            SongPos = rd.F64s();

            FloorIds = rd.I32s();
            int hits = FloorIds.Length;
            CurAngles = rd.F64s();
            Overload = rd.F32s();
            CachedAngles = rd.F64s();
            TargetExit = rd.F64s();
            FreeRoam = rd.I32s();
            Flags = rd.Bytes(hits);
            HitTimeErr = rd.F64s();
            HitAngleErr = CurAngles;
        }

        void ComputeStats()
        {
            HitCount = FloorIds == null ? 0 : FloorIds.Length;
            AccuracyFile = GetD("percentXacc", 0);

            string jl = GetS("JudgmentList", "");
            var list = ParseIntList(jl);
            Judgment = list.ToArray();
            JudgmentTotal = Judgment.Sum();
            if (JudgmentTotal == 0) JudgmentTotal = HitCount;

            double num = 0;
            var w = Weights;
            for (int k = 0; k < Judgment.Length; k++) num += Judgment[k] * (k < w.Length ? w[k] : 1.0);
            Accuracy = JudgmentTotal > 0 ? num / JudgmentTotal : 0;

            if (FormatVersion >= 3)
                CoreIdx = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
            else
                CoreIdx = new int[] { 0, 1, 2, 3, 4, 5, 6 };

            StartTile = GetI("start_tile", 0);
            EndTile = GetI("end_tile", 0);

            if (FloorIds != null && FloorIds.Length > 0)
            {
                DistinctFloors = FloorIds.Distinct().Count();
                int peak = 0;
                for (int k = 0; k < Overload.Length; k++)
                    if (Overload[k] > Overload[peak]) peak = k;
                OverloadPeak = Overload.Length > 0 ? Overload[peak] : 0;
                OverloadPeakIndex = peak;

                for (int k = 0; k < Flags.Length; k++)
                {
                    if ((Flags[k] & 2) != 0) AutoHits++;
                    if ((Flags[k] & 1) != 0) NoFailHits++;
                    if ((Flags[k] & 8) != 0) MidspinHits++;
                }
                if (FreeRoam != null) FreeRoamHits = FreeRoam.Count(x => x != 0);
            }

            if (HitAngleErr != null && HitTimeErr != null && HitTimeErr.Length == HitAngleErr.Length)
                DerivedBpm = EstimateBpm(HitAngleErr, HitTimeErr);

            ComputeXAcc();

            int cnt = Math.Min(KeyCodes.Length, Math.Min(KeyPresses.Length, SongPos.Length));
            for (int k = 0; k < cnt; k++)
            {
                if (KeyPresses[k] == 0) continue;
                Presses.Add(new PressEvent(SongPos[k], KeyCodes[k]));
                int c;
                KeyUsage.TryGetValue(KeyCodes[k], out c);
                KeyUsage[KeyCodes[k]] = c + 1;
            }
            Presses.Sort((a, b) => a.Time.CompareTo(b.Time));
            PressCount = Presses.Count;
            if (PressCount > 0)
            {
                PressFirst = Presses[0].Time;
                PressLast = Presses[PressCount - 1].Time;
                for (int k = 0; k < PressCount - 1; k++)
                {
                    double g = Presses[k + 1].Time - Presses[k].Time;
                    Gaps.Add(g);
                    if (g < 0.020 && Presses[k].Vk != Presses[k + 1].Vk) ChordPairs++;
                    if (g < 0.030 && Presses[k].Vk != Presses[k + 1].Vk) ChordPairs30++;
                }
                double span = PressLast - PressFirst;
                if (span > 0) AvgPps = PressCount / span;
            }
            LastEvent = SongPos.Length > 0 ? SongPos[SongPos.Length - 1] : 0;

            int secs = (int)Math.Ceiling(Math.Max(LastEvent, 1)) + 1;
            if (secs < 1) secs = 1;
            SecondDensity = new int[secs];
            foreach (var p in Presses)
            {
                int idx = (int)p.Time;
                if (idx >= 0 && idx < secs) SecondDensity[idx]++;
            }
            for (int k = 0; k < secs; k++)
                if (SecondDensity[k] > MaxDensity) { MaxDensity = SecondDensity[k]; MaxDensitySecond = k; }
        }

        // 由逐次角度误差反推每击判定，再累加成实时 X-Acc
        void ComputeXAcc()
        {
            XAccValid = false;
            if (HitAngleErr == null || HitAngleErr.Length == 0 || FormatVersion < 3) return;

            double bpmEff = (Bpm > 0 ? Bpm : 0) * (PlaybackSpeed > 0 ? PlaybackSpeed : 1);
            double pitch = GetI("pitch", 100);
            pitch = pitch > 10 ? pitch / 100.0 : (pitch > 0 ? pitch : 1.0);
            bpmEff *= pitch;
            if (bpmEff <= 0) bpmEff = 150;
            // 与 ADOFAI 一致：静态基准角与「最小时间×BPM」取大者
            CountedDeg = Math.Max(60.0, 0.065 * bpmEff * 3);
            PerfectDeg = Math.Max(45.0, 0.030 * bpmEff * 3);
            PureDeg = Math.Max(30.0, 0.020 * bpmEff * 3);
            XpDeg = Math.Max(12.5, 0.0166667 * bpmEff * 3);

            int n = HitAngleErr.Length;
            HitMarginIdx = new int[n];
            DerivedJudge = new int[Math.Max(Judgment.Length, 16)];

            for (int k = 0; k < n; k++)
            {
                double deg = HitAngleErr[k] * 57.29577951308232;
                double a = Math.Abs(deg);
                int idx;
                if (a < XpDeg) idx = 4;                       // X完美
                else if (a < PureDeg) idx = deg < 0 ? 3 : 5;  // 完美- / 完美+
                else if (a < PerfectDeg) idx = deg < 0 ? 2 : 6; // 早/晚完美
                else if (a < CountedDeg) idx = deg < 0 ? 1 : 7; // 很过/很晚
                else idx = deg < 0 ? 0 : 8;                     // 过早/过晚
                HitMarginIdx[k] = idx;
                DerivedJudge[idx]++;
            }

            var w = Weights;
            XAccSeries = new double[n];
            double acc = 0;
            for (int k = 0; k < n; k++)
            {
                int idx = HitMarginIdx[k];
                acc += idx < w.Length ? w[idx] : 1.0;
                XAccSeries[k] = acc / (k + 1);
            }
            XAccFinal = XAccSeries[n - 1];

            int win = Math.Min(25, Math.Max(5, n / 8));
            XAccRolling = new double[n];
            for (int k = 0; k < n; k++)
            {
                int lo = Math.Max(0, k - win + 1);
                double s = 0;
                for (int j = lo; j <= k; j++)
                {
                    int idx = HitMarginIdx[j];
                    s += idx < w.Length ? w[idx] : 1.0;
                }
                XAccRolling[k] = s / (k - lo + 1);
            }

            bool same = true;
            for (int k = 0; k < n && same; k++)
            {
                int a = k < Judgment.Length ? Judgment[k] : 0;
                int b = k < DerivedJudge.Length ? DerivedJudge[k] : 0;
                if (a != b) same = false;
            }
            XAccMatchesFile = same;
            if (AccuracyFile > 0 && Math.Abs(XAccFinal - AccuracyFile) > 1e-6) XAccMatchesFile = false;
            XAccValid = true;
        }

        static double EstimateBpm(double[] angleRad, double[] timeSec)
        {
            var ratios = new List<double>();
            for (int k = 0; k < angleRad.Length; k++)
            {
                if (Math.Abs(timeSec[k]) < 0.004) continue;    // 误差太小，比值不可靠
                ratios.Add(Math.Abs(angleRad[k] / timeSec[k]));
            }
            if (ratios.Count < 5) return 0;
            ratios.Sort();
            double med = ratios[ratios.Count / 2];
            return med * 60.0 / Math.PI;
        }

        static List<int> ParseIntList(string json)
        {
            var res = new List<int>();
            if (string.IsNullOrEmpty(json)) return res;
            var sb = new StringBuilder();
            for (int k = 0; k < json.Length; k++)
            {
                char c = json[k];
                if (c == '-' || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0)
                {
                    int v;
                    if (int.TryParse(sb.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) res.Add(v);
                    sb.Length = 0;
                }
            }
            if (sb.Length > 0)
            {
                int v;
                if (int.TryParse(sb.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) res.Add(v);
            }
            return res;
        }

        // ---------------------- export ----------------------
        public string PressCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("index,song_position_sec,vk_code,key_name");
            for (int k = 0; k < Presses.Count; k++)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.####},{2},{3}",
                    k, Presses[k].Time, Presses[k].Vk, Keys.Name(Presses[k].Vk)));
            return sb.ToString();
        }

        public string HitCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("index,floor_id,angle_err_rad,angle_err_deg,time_err_ms,judgment,weight,xacc_after_hit");
            var w = Weights;
            for (int k = 0; k < FloorIds.Length; k++)
            {
                double ang = HitAngleErr != null && k < HitAngleErr.Length ? HitAngleErr[k] : 0;
                double t = HitTimeErr != null && k < HitTimeErr.Length ? HitTimeErr[k] * 1000 : 0;
                string jd = "";
                string wt = "";
                string xa = "";
                if (HitMarginIdx != null && k < HitMarginIdx.Length)
                {
                    int idx = HitMarginIdx[k];
                    jd = idx < Longs.Length ? Shorts[idx] : idx.ToString();
                    wt = (idx < w.Length ? w[idx] : 1.0).ToString("0.00", CultureInfo.InvariantCulture);
                    xa = XAccSeries[k].ToString("0.######", CultureInfo.InvariantCulture);
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.######},{3:0.####},{4:0.####},{5},{6},{7}",
                    k, FloorIds[k], ang, ang * 57.29577951308232, t, jd, wt, xa));
            }
            return sb.ToString();
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"file\": ").Append(JStr(FileName)).Append(",\n");
            sb.Append("  \"container\": ").Append(JStr(ContainerText)).Append(",\n");
            sb.Append("  \"formatVersion\": ").Append(FormatVersion).Append(",\n");
            sb.Append("  \"size\": ").Append(Size).Append(",\n");
            sb.Append("  \"sha256\": ").Append(JStr(Sha256)).Append(",\n");
            sb.Append("  \"accuracy\": ").Append(Accuracy.ToString("0.##########", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"percentXacc\": ").Append(AccuracyFile.ToString("0.##########", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"bpm\": ").Append(Bpm.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"bpmDerived\": ").Append(DerivedBpm.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"judgment\": [").Append(string.Join(",", Judgment.Select(x => x.ToString()).ToArray())).Append("],\n");
            sb.Append("  \"judgmentNames\": [").Append(string.Join(",", Longs.Take(Judgment.Length).Select(JStr).ToArray())).Append("],\n");
            sb.Append("  \"hits\": ").Append(HitCount).Append(",\n");
            sb.Append("  \"presses\": ").Append(PressCount).Append(",\n");
            sb.Append("  \"s\": {");
            bool first = true;
            foreach (var kv in S)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(JStr(kv.Key)).Append(": ").Append(JStr(kv.Value));
            }
            sb.Append("},\n  \"i\": {");
            first = true;
            foreach (var kv in I)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(JStr(kv.Key)).Append(": ").Append(kv.Value);
            }
            sb.Append("},\n  \"d\": {");
            first = true;
            foreach (var kv in D)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(JStr(kv.Key)).Append(": ").Append(kv.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append("}\n}\n");
            return sb.ToString();
        }

        static string JStr(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }
    }

    public static class Keys
    {
        static readonly Dictionary<int, string> Map = new Dictionary<int, string>
        {
            {1, "鼠标左键"}, {2, "鼠标右键"}, {4, "鼠标中键"},
            {8, "Backspace"}, {9, "Tab"}, {13, "Enter"}, {16, "Shift"}, {17, "Ctrl"},
            {18, "Alt"}, {19, "Pause"}, {20, "CapsLock"}, {27, "Esc"}, {32, "Space"},
            {33, "PageUp"}, {34, "PageDown"}, {35, "End"}, {36, "Home"},
            {37, "←"}, {38, "↑"}, {39, "→"}, {40, "↓"},
            {44, "PrintScreen"}, {45, "Insert"}, {46, "Delete"},
            {91, "Win"}, {92, "Win"}, {93, "Menu"},
            {96, "Num0"}, {97, "Num1"}, {98, "Num2"}, {99, "Num3"}, {100, "Num4"},
            {101, "Num5"}, {102, "Num6"}, {103, "Num7"}, {104, "Num8"}, {105, "Num9"},
            {106, "Num*"}, {107, "Num+"}, {109, "Num-"}, {110, "Num."}, {111, "Num/"},
            {144, "NumLock"}, {145, "ScrollLock"},
            {160, "左Shift"}, {161, "右Shift"}, {162, "左Ctrl"}, {163, "右Ctrl"},
            {164, "左Alt"}, {165, "右Alt"},
            {186, ";"}, {187, "="}, {188, ","}, {189, "-"}, {190, "."}, {191, "/"},
            {192, "`"}, {219, "["}, {220, "\\"}, {221, "]"}, {222, "'"}
        };

        static readonly Dictionary<string, string> ShortMap = new Dictionary<string, string>
        {
            { "Backspace", "Bksp" }, { "Space", "Spc" }, { "Mouse L", "LMB" },
            { "LeftAlt", "LAlt" }, { "NumLock", "NumLk" }, { "CapsLock", "Caps" },
            { "PrintScreen", "PrtSc" }, { "鼠标左键", "LMB" }
        };

        static Keys()
        {
            for (int k = 0; k < 10; k++) Map[48 + k] = k.ToString();
            for (int k = 0; k < 26; k++) Map[65 + k] = ((char)('A' + k)).ToString();
            for (int k = 0; k < 12; k++) Map[112 + k] = "F" + (k + 1);
        }

        public static string Name(int vk)
        {
            string s;
            if (Map.TryGetValue(vk, out s)) return s;
            return "VK" + vk;
        }

        public static string Short(int vk)
        {
            string s = Name(vk), t;
            return ShortMap.TryGetValue(s, out t) ? t : s;
        }
    }
}
