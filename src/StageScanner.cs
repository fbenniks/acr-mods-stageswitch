using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AcrFfbGain
{
    public class ScannedStage
    {
        public string RallyId;
        public string StageId;
        public List<string> Variants = new List<string>();
    }

    /// <summary>
    /// Finds every stage shipped with the game by reading the asset index (.utoc) files.
    /// Each stage has pacenote tables named DT_Pacenote&lt;Stage&gt;&lt;Variant&gt;, e.g.
    /// DT_PacenoteBolleneFullForward. The rally a stage belongs to is taken from level
    /// asset names such as "MonteCarloS1Bollene..." in the same index files.
    /// New stages added by game updates are picked up automatically.
    /// </summary>
    public static class StageScanner
    {
        const string GameFolderName = "Assetto Corsa Rally";

        static readonly Regex PacenoteRx = new Regex(@"DT_Pacenote([A-Za-z0-9]+)", RegexOptions.Compiled);
        static readonly Regex VariantRx = new Regex(@"(Full|Cut\d+|Short\d+|Long\d+)?(Forward|Reverse)(Cut\d+|Short\d+)?$", RegexOptions.Compiled);

        // Typos that occur in the game's own asset names.
        static readonly Dictionary<string, string> RallyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Weles", "Wales" },
            { "Sweeden", "Sweden" },
            { "Montecarlo", "MonteCarlo" },
        };

        public static string FindGameDir()
        {
            var candidates = new List<string>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        var steam = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(steam))
                        {
                            steam = steam.Replace('/', '\\');
                            candidates.Add(steam);
                            string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                            if (File.Exists(vdf))
                            {
                                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                                    candidates.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                            }
                        }
                    }
                }
            }
            catch { }
            candidates.Add(@"C:\Program Files (x86)\Steam");
            candidates.Add(@"C:\Program Files\Steam");

            foreach (var lib in candidates)
            {
                string dir = Path.Combine(lib, @"steamapps\common\" + GameFolderName);
                if (IsGameDir(dir)) return dir;
            }
            return null;
        }

        public static bool IsGameDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(dir, @"acr\Content\Paks"));
        }

        public static List<ScannedStage> Scan(string gameDir)
        {
            string paks = Path.Combine(gameDir, @"acr\Content\Paks");
            var texts = new List<string>();
            foreach (var f in Directory.GetFiles(paks, "*.utoc"))
            {
                try { texts.Add(Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(f))); }
                catch (Exception ex) { Log.Write("Scan: cannot read " + f + ": " + ex.Message); }
            }

            // 1. Stage base names + variants from the pacenote tables.
            var stages = new Dictionary<string, ScannedStage>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in texts)
            {
                foreach (Match m in PacenoteRx.Matches(t))
                {
                    string token = m.Groups[1].Value;
                    var vm = VariantRx.Match(token);
                    if (!vm.Success || vm.Index == 0) continue;
                    string stageId = token.Substring(0, vm.Index);
                    ScannedStage s;
                    if (!stages.TryGetValue(stageId, out s))
                    {
                        s = new ScannedStage { StageId = stageId };
                        stages[stageId] = s;
                    }
                    if (!s.Variants.Contains(vm.Value)) s.Variants.Add(vm.Value);
                }
            }

            // 2. Which rally each stage belongs to: "<Rally>S<number><Stage>" level asset names.
            var allPrefixCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var perStage = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in stages.Values)
            {
                var rx = new Regex(@"(?<![A-Za-z])([A-Z][a-z]+(?:[A-Z][a-z]+)?)S\d+" + Regex.Escape(s.StageId));
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in texts)
                {
                    foreach (Match m in rx.Matches(t))
                    {
                        string p = NormalizeRally(m.Groups[1].Value);
                        int c; counts.TryGetValue(p, out c); counts[p] = c + 1;
                        allPrefixCounts.TryGetValue(p, out c); allPrefixCounts[p] = c + 1;
                    }
                }
                perStage[s.StageId] = counts;
            }

            foreach (var s in stages.Values)
            {
                var counts = perStage[s.StageId];
                if (counts.Count == 0) { s.RallyId = "Other"; continue; }
                s.RallyId = counts.OrderByDescending(kv => kv.Value).First().Key;
            }

            // Merge near-identical rally spellings onto the most common one (e.g. future typos).
            var rallies = stages.Values.Select(s => s.RallyId).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(r => { int c; allPrefixCounts.TryGetValue(r, out c); return c; }).ToList();
            var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rallies)
            {
                string target = r;
                foreach (var kv in canonical.Values.Distinct())
                {
                    if (!string.Equals(kv, r, StringComparison.OrdinalIgnoreCase) && Levenshtein(kv.ToLowerInvariant(), r.ToLowerInvariant()) <= 2 && Math.Min(kv.Length, r.Length) >= 5)
                    { target = kv; break; }
                }
                canonical[r] = target;
            }
            foreach (var s in stages.Values) s.RallyId = canonical[s.RallyId];

            return stages.Values.OrderBy(s => s.RallyId).ThenBy(s => s.StageId).ToList();
        }

        /// <summary>"Monte Carlo La Bollène" -> "montecarlolabollene"</summary>
        public static string Norm(string s)
        {
            string d = (s ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string NormalizeRally(string p)
        {
            string a;
            return RallyAliases.TryGetValue(p, out a) ? a : p;
        }

        public static string Pretty(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var sb = new StringBuilder();
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1])) sb.Append(' ');
                else if (i > 0 && char.IsDigit(c) && !char.IsDigit(id[i - 1])) sb.Append(' ');
                sb.Append(c == '_' ? ' ' : c);
            }
            return sb.ToString().Trim();
        }

        static int Levenshtein(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        /// <summary>Adds newly found rallies/stages to the config. Existing values are kept.</summary>
        public static int Merge(AppConfig cfg, List<ScannedStage> scanned)
        {
            int added = 0;
            foreach (var s in scanned)
            {
                var rally = cfg.FindRally(s.RallyId);
                if (rally == null)
                {
                    rally = new RallyConfig { Id = s.RallyId, Name = Pretty(s.RallyId), Gain = cfg.DefaultGain };
                    cfg.Rallies.Add(rally);
                }
                var stage = rally.Stages.FirstOrDefault(x => string.Equals(x.Id, s.StageId, StringComparison.OrdinalIgnoreCase));
                if (stage == null)
                {
                    // A stage may have been auto-detected earlier under another rally; move it.
                    foreach (var r in cfg.Rallies)
                    {
                        var old = r.Stages.FirstOrDefault(x => string.Equals(x.Id, s.StageId, StringComparison.OrdinalIgnoreCase));
                        if (old != null) { r.Stages.Remove(old); stage = old; break; }
                    }
                    if (stage == null)
                    {
                        stage = new StageConfig { Id = s.StageId, Name = Pretty(s.StageId), Gain = rally.Gain };
                        added++;
                    }
                    stage.Detected = false;
                    rally.Stages.Add(stage);
                }
            }
            // Drop telemetry-added entries that turned out to be a known stage (e.g. "Monte Carlo La Bollène").
            var known = scanned.Select(s => Norm(s.StageId)).Where(n => n.Length > 0).ToList();
            // Telemetry-only names under a known rally (e.g. "Wales Cwmbiga") aren't separate stages either.
            foreach (var r in cfg.Rallies)
                r.Stages.RemoveAll(st => st.Detected && (r.Id != "Other" || known.Any(n => Norm(st.Id).Contains(n))));

            cfg.Rallies.RemoveAll(r => r.Stages.Count == 0);
            foreach (var r in cfg.Rallies) r.Stages.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            cfg.Rallies.Sort((a, b) =>
            {
                bool ao = a.Id == "Other", bo = b.Id == "Other";
                if (ao != bo) return ao ? 1 : -1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return added;
        }
    }
}
