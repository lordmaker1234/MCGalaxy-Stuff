using System.Collections.Generic;
using System.Reflection;

namespace MCGalaxy {
    public sealed class CmdCPEInfo : Command {
        public override string name { get { return "CPEInfo"; } }
        public override string shortcut { get { return "CPE"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message) {
            string[] args = message.SplitSpaces();

            // Target player is either specified and online, or yourself
            Player target = message.Length >= 1 ? PlayerInfo.FindMatches(p, args[0]) ?? p : p;

            // Voodoo magic to grab current Classic Protocol Extensions of the target (Credit: icanttellyou)
            object value = target.Session.GetType().GetField("extensions",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target.Session);

            CpeExt[] allexts = value != null ? (CpeExt[])value : null;
            List<CpeExt> exts = new List<CpeExt>();

            foreach (CpeExt ext in allexts) { if (ext.ClientVersion >= 1) exts.Add(ext); }

            p.Message("&F{0} &Sis using &F{1}", ColName(target), target.Session.ClientName());

            if (exts.Count == 0) {
                p.Message("Which doesn't support any mutual Classic Protocol Extensions"); return;
            }

            p.Message("Which mutually supports the following &F{0} &SCPE protocols:", exts.Count);

            Paginator.Output(p, exts, null, PrintSpec, "CPEInfo " + target.truename,
                "CPE specs", args.Length > 1 ? args[1] : "", 15);
        }

        // Used with Paginator.Output() to print one item per line instead of combined into one string.
        private static void PrintSpec(Player p, CpeExt ext) {
            p.Message("  &F{0} &Sver &F{1}", ext.Name, ext.ClientVersion);
        }

        // Colored name of the player but without using their display name
        public static string ColName(Player p) { return p.color + p.name; }

        public override void Help(Player p) {
            p.Message("&T/CPEInfo [name]");
            p.Message("&HProvides current CPE information for a specified player.");
        }
    }
}