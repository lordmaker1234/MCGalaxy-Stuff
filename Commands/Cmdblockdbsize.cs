//reference System.dll
//reference System.Core.dll
using System;
using System.IO;

namespace MCGalaxy {
    public class CmdBlockDBSize : Command {
        public override string name { get { return "BlockDBSize"; } }
        public override string shortcut { get { return "dbsize"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }

        public override void Use(Player p, string message) {
            string[] args = message.SplitSpaces();

            if (!string.IsNullOrWhiteSpace(args[0]) && args[0].Contains("*")) {
                UseList(p, args); return;
            }

            message = args[0];
            Level lvl = string.IsNullOrWhiteSpace(message) ? p.level : null;
            if (lvl == null) {
                message = Matcher.FindMaps(p, message);
                if (message == null) return;
                lvl = LevelInfo.FindExact(message);
            }

            string worldName = lvl != null ? lvl.name : message;
            FileInfo fi = new FileInfo("blockdb/" + worldName + ".cbdb");
            if (fi.Exists) {
                p.Message("File: &F{0}&S.cbdb", worldName);
                p.Message(" - Size: &F{0}", FormatSize(fi.Length));
                p.Message(" - Last Modified: &F{0}", fi.LastWriteTime.ToString("yyyy-MM-dd '&Sat &F'HH:mm"));
            } else { p.Message("&cThat world does not have a BlockDB file at this time."); }
        }

        void UseList(Player p, string[] args) {
            string pattern = args[0] + ".cbdb";

            if (!LevelInfo.ValidName(pattern.Replace("*", "^").Replace("?", "$"))) {
                p.Message("Sorry, that is not a valid map name search pattern.");
                return;
            }

            DirectoryInfo di = new DirectoryInfo("blockdb");
            try {
                // Find matching files.
                FileInfo[] files = di.GetFiles(pattern, SearchOption.TopDirectoryOnly);
                if (files.Length == 0) { p.Message("No Block DB files matching: " + pattern); return; }

                // Sort files by size.
                Array.Sort(files, (file1, file2) => file2.Length.CompareTo(file1.Length));

                // Check for 'all' pagination usage when more than 128 results
                if (args.Length > 1 && args[1].CaselessEq("all") && files.Length > 128) {
                    // Don't allow ranks below admin from seeing more than 128 files as it can be resource intensive
                    if (p.Rank < LevelPermission.Admin) {
                        args[1] = "0";
                        p.Message("Sorry, your rank is too low to see that many results");
                    } else { // Allow admins to see all files when confirmed
                        if (args.Length < 3 || !args[2].CaselessEq("confirm")) {
                            p.Message("&CWarning: &FThere are {0} results, if you are sure you want to get spammed with that much chat,"
                                + " please use the command again appended with \"confirm\"", files.Length);
                            return;
                        }
                    }
                }

                p.Message("Largest BlockDB Files: (Total: &F{0})", FormatSize(TotalSize(files)));
                p.Message(" (size - last modified - file name)");
                Paginator.Output(p, files, null, PrintSpec, shortcut + " " + args[0],
                    "files", args.Length > 1 ? args[1] : "", 20);
            } catch (Exception ex) {
                if (ex.Message.CaselessContains("move up directories")) {
                    p.Message("&4Funny guy, eh?");
                } else {
                    p.Message("&C" + ex.Message);
                    Logger.LogError(ex);
                }
            }
        }

        // Used with Paginator.Output() to print one item per line instead of combined into one string.
        void PrintSpec(Player p, FileInfo fi) {
            p.Message("&F  " + FormatSize(fi.Length) + " - &F" + fi.LastWriteTime.ToString("yy-MM-dd HH:mm") + " &S- &F" + fi.Name);
        }

        readonly string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        string FormatSize(float size) {
            int order = 0;
            while (size >= 1024 && order < sizes.Length - 1) {
                order++; size /= 1024;
            }
            return string.Format("{0:0.00} &S{1}", size, sizes[order]);
        }
        long TotalSize(FileInfo[] files) {
            long size = 0;
            for (int i = 0; i < files.Length; i++) {
                size += files[i].Length;
            }
            return size;
        }

        public override void Help(Player p) {
            p.Message("&T/BlockDBSize <world or search-pattern> <offset>");
            p.Message("&HShow BlockDB size/info of specified world or current world.");
            p.Message("&HSearch pattern supports ? and * wildcards. Use a * to see all.");
        }
    }
}