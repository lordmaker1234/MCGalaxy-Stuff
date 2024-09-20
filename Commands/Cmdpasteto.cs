//reference System.dll
//reference System.Core.dll
using MCGalaxy.Commands;
using MCGalaxy.Drawing;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Maths;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public class CmdPasteTo : Command {
        public override string name { get { return "PasteTo"; } }
        public override string shortcut { get { return "pt"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }

        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("PasteNotTo", "not"), new CommandAlias("pnt", "not") }; }
        }

        public override void Use(Player p, string message) {
            // Convert message to string list of args, return Help is no args are used.
            List<string> cmdArgs = message.SplitSpaces().ToList();
            if (cmdArgs.Count == 0) { Help(p); return; }

            // Check if "not" is specified as first arg to determine if specified blocks are not to be pasted.
            bool useNot = !string.IsNullOrEmpty(cmdArgs[0]) && cmdArgs[0].CaselessEq("not");
            if (useNot) cmdArgs.RemoveAt(0);

            // Requires at least 3 args: X, Y, Z
            if (cmdArgs.Count < 3) { Help(p); return; }

            // Parse specified coordinates to paste to.
            Vec3S32 coord = p.Pos.FeetBlockCoords;
            if (!ParseCoords(cmdArgs, p, ref coord)) return;
            cmdArgs.RemoveRange(0, 3);

            // Build paste brush args.
            string argNot = useNot ? "not " : "";
            string brushArgs = cmdArgs.Count >= 1 ? argNot + cmdArgs.Join(" ") : "";
            BrushArgs args = new BrushArgs(p, brushArgs, Block.Air);
            if (!BrushFactory.Find("Paste").Validate(args)) return;

            // Paste the blocks.
            Vec3S32[] coords = new Vec3S32[1];
            coords[0] = coord;
            DoPaste(p, coords, args);
        }

        private void DoPaste(Player p, Vec3S32[] m, BrushArgs args) {
            Brush brush = BrushFactory.Find("Paste").Construct(args);
            if (brush == null) return;

            CopyState cState = p.CurrentCopy;
            PasteDrawOp op = new PasteDrawOp {
                CopyState = cState
            };

            m[0] += cState.Offset;
            DrawOpPerformer.Do(op, brush, p, m);
        }

        private bool ParseCoords(List<string> cmdArgs, Player p, ref Vec3S32 P) {
            // Skip checking for a single coord as there should always be 3 specified
            string[] args = cmdArgs.ToArray();
            AdjustArg(ref args[0], ref P.X, "X", p.lastClick.X);
            AdjustArg(ref args[1], ref P.Y, "Y", p.lastClick.Y);
            AdjustArg(ref args[2], ref P.Z, "Z", p.lastClick.Z);
            return CommandParser.GetCoords(p, args, 0, ref P);
        }

        private void AdjustArg(ref string arg, ref int value, string axis, int last) {
            if (!arg.CaselessStarts(axis)) return;
            if (arg.Length == 1) {
                arg = NumberUtils.StringifyInt(last);
            } else {
                arg = arg.Substring(1);
                value = last;
            }
        }

        public override void Help(Player p) {
            p.Message("&T/PasteTo [x] [y] [z] &H- Pastes the stored copy to the specified coordinates.");
            p.Message("&T/PasteTo [x] [y] [z] [block] [block2] ... &H- Pastes only the specified blocks from the copy to the specified coordinates.");
            p.Message("&T/PasteTo not [x] [y] [z] [block] [block2] ... &H- Pastes all blocks from the copy, except for the specified blocks, to the specified coordinates.");
            p.Message("&4BEWARE: &SThe blocks will always be pasted in a set direction");
        }
    }
}