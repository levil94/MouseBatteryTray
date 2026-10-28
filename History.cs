using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MouseBatteryTray
{
    internal sealed class Sample
    {
        public DateTime Time;
        public int Percent;
        public int Millivolts;
    }

    /// <summary>电量历史：CSV 落盘，保留 30 天，提供区间查询与放电速率估算。</summary>
    internal static class HistoryStore
    {
        private const int MaxAgeDays = 30;
        private const int MinSampleGapSeconds = 120;   // 同一电量下最多每 2 分钟记一次

        private static readonly List<Sample> _samples = new List<Sample>();
        private static bool _loaded;

        public static string FilePath
        {
            get { return Path.Combine(Program.DataDir, "history.csv"); }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                DateTime cutoff = DateTime.Now.AddDays(-MaxAgeDays);
                bool pruned = false;

                foreach (string line in File.ReadLines(FilePath))
                {
                    string[] parts = line.Split(',');
                    if (parts.Length < 3) continue;

                    DateTime t;
                    int pct, mv;
                    if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out t)) continue;
                    if (!int.TryParse(parts[1], out pct)) continue;
                    if (!int.TryParse(parts[2], out mv)) continue;

                    if (t < cutoff) { pruned = true; continue; }
                    _samples.Add(new Sample { Time = t, Percent = pct, Millivolts = mv });
                }

                _samples.Sort(delegate (Sample a, Sample b) { return a.Time.CompareTo(b.Time); });
                if (pruned) Rewrite();
            }
            catch (Exception ex)
            {
                Program.Log("读取历史失败: " + ex.Message);
            }
        }

        public static void Add(int percent, int millivolts)
        {
            EnsureLoaded();
            DateTime now = DateTime.Now;

            if (_samples.Count > 0)
            {
                Sample last = _samples[_samples.Count - 1];
                double gap = (now - last.Time).TotalSeconds;
                if (last.Percent == percent && gap < MinSampleGapSeconds) return;
                if (gap < 20) return;
            }

            var s = new Sample { Time = now, Percent = percent, Millivolts = millivolts };
            _samples.Add(s);

            try
            {
                File.AppendAllText(FilePath, string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2}{3}", now.ToString("yyyy-MM-dd HH:mm:ss"), percent, millivolts, Environment.NewLine));
            }
            catch (Exception ex)
            {
                Program.Log("写入历史失败: " + ex.Message);
            }
        }

        private static void Rewrite()
        {
            try
            {
                using (var writer = new StreamWriter(FilePath, false))
                {
                    foreach (Sample s in _samples)
                    {
                        writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2}", s.Time.ToString("yyyy-MM-dd HH:mm:ss"), s.Percent, s.Millivolts));
                    }
                }
            }
            catch { }
        }

        public static List<Sample> Query(TimeSpan span)
        {
            EnsureLoaded();
            DateTime from = DateTime.Now - span;
            var list = new List<Sample>();
            foreach (Sample s in _samples)
            {
                if (s.Time >= from) list.Add(s);
            }
            return list;
        }

        /// <summary>区间内的百分比变化（正数表示掉电）。</summary>
        public static int? DeltaPercent(TimeSpan span)
        {
            List<Sample> list = Query(span);
            if (list.Count < 2) return null;
            return list[0].Percent - list[list.Count - 1].Percent;
        }

        /// <summary>放电速率：百分比 / 小时。数据不足或未掉电时返回 null。</summary>
        public static double? DischargeRatePerHour(TimeSpan span)
        {
            List<Sample> list = Query(span);
            if (list.Count < 2) return null;

            Sample first = list[0];
            Sample last = list[list.Count - 1];
            double hours = (last.Time - first.Time).TotalHours;
            if (hours < 0.2) return null;                 // 至少 12 分钟跨度

            int drop = first.Percent - last.Percent;
            if (drop <= 0) return null;

            return drop / hours;
        }

        /// <summary>剩余可用小时数，基于最近 6 小时的放电速率。</summary>
        public static double? RemainingHours(int currentPercent)
        {
            double? rate = DischargeRatePerHour(TimeSpan.FromHours(6))
                           ?? DischargeRatePerHour(TimeSpan.FromHours(24));
            if (!rate.HasValue || rate.Value <= 0.02) return null;

            double hours = currentPercent / rate.Value;
            if (hours <= 0 || hours > 200) return null;
            return hours;
        }

        public static string FormatDuration(double hours)
        {
            int totalMinutes = (int)Math.Round(hours * 60);
            if (totalMinutes < 60) return totalMinutes + " 分钟";
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;
            return m == 0 ? h + " 小时" : string.Format("{0} 小时 {1} 分", h, m);
        }

        /// <summary>最近一小时波动摘要，供 tooltip 使用。</summary>
        public static string TrendSummary()
        {
            List<Sample> hour = Query(TimeSpan.FromHours(1));
            if (hour.Count < 2) return "";
            int min = 100, max = 0;
            foreach (Sample s in hour)
            {
                if (s.Percent < min) min = s.Percent;
                if (s.Percent > max) max = s.Percent;
            }
            int delta = hour[0].Percent - hour[hour.Count - 1].Percent;
            string dir = delta > 0 ? "-" + delta : (delta < 0 ? "+" + (-delta) : "±0");
            return string.Format("1h {0}% ({1}-{2}%)", dir, min, max);
        }
    }
}
