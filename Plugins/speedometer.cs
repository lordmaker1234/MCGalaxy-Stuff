using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PluginSpeedometer {
    public sealed class Speedometer : Plugin {
        public override string name { get { return "Speedometer"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
        public override string creator { get { return "123DontMessWitMe"; } }
        public override int build { get { return 1; } }

        static readonly Dictionary<string, List<SpeedTime>> speeds = new Dictionary<string, List<SpeedTime>>();
        static readonly Dictionary<string, byte> players = new Dictionary<string, byte>();

        public override void Load(bool auto) {
            OnPlayerMoveEvent.Register(OnPlayerMoveCall, Priority.Normal);
            OnPlayerDisconnectEvent.Register(OnPlayerDisconnectCall, Priority.Low);
            Command.Register(new CmdSpeedometer());
        }

        public override void Unload(bool auto) {
            Command.Unregister(Command.Find("Speedometer"));
            OnPlayerDisconnectEvent.Unregister(OnPlayerDisconnectCall);
            OnPlayerMoveEvent.Unregister(OnPlayerMoveCall);
            speeds.Clear();
            players.Clear();
        }

        public static void ClearPlayer(Player p) {
            // Remove player speeds and settings.
            speeds.Remove(p.name);
            players.Remove(p.name);

            // Sleep to prevent OnPlayerMoveCall racing replacing the empty lines.
            Thread.Sleep(50);

            // Clear all CPE message lines
            p.SendCpeMessage(CpeMessageType.BottomRight3, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.BottomRight2, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.BottomRight1, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.Status1, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.Status2, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.Status3, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.Announcement, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.BigAnnouncement, " ", PersistentMessagePriority.High);
            p.SendCpeMessage(CpeMessageType.SmallAnnouncement, " ", PersistentMessagePriority.High);
        }

        public static void UpdatePlayer(Player p, string message) {
            byte isEnabled;

            // Check if 'advanced' is specified as an argument
            bool advanced = !string.IsNullOrWhiteSpace(message) && message.CaselessEq("advanced");

            // If player is not currently in the players dictionary, enable speedometer.
            if (!players.TryGetValue(p.name, out isEnabled)) {
                // Add player and enable speedometer
                if (advanced) {
                    players[p.name] = (byte)2;
                    p.Message("Advanced Speedometer &AEnabled&S across whole screen");
                    p.Message(" Top-Right: &FCurrent &CX&F, &AY&F, and &9Z&F Speeds");
                    p.Message(" Bottom-Right: &F3 Second Average &CX&F, &AY&F, and &9Z&F Speeds");
                    p.Message(" Announcements");
                    p.Message("  1st: &F3 Second Average &CX&AY&9Z&F(3D) Speed Dial");
                    p.Message("  2nd: &F3 Second Average &CX&9Z&F(2D/Horizontal) Speed Dial");
                    p.Message("  3rd: &F3 Second Average &CX&9Z&F Speed Readout");
                    return;
                }
                players[p.name] = 1;
                p.Message("Basic Speedometer &AEnabled&S in bottom right corner.");
                p.Message(" 1st: &F2 second average speed dial");
                p.Message(" 2nd: &F2 second average speed readout");
                p.Message(" 3rd: &FCurrent speed");
            } else {
                // Disable speedometer and clear CPE messages
                p.Message("Speedometer: &CDisabled");
                ClearPlayer(p);
            }

        }

        public void OnPlayerMoveCall(Player p, Position next, byte yaw, byte pitch, ref bool cancel) {
            // Check if player has enabled the speedometer, also don't bother if they don't support CPE message types
            byte isEnabled;
            if (!players.TryGetValue(p.name, out isEnabled) ||
                isEnabled == 0 || !p.Supports(CpeExt.MessageTypes)) { return; }

            DateTime curT = DateTime.UtcNow;
            Position curP = next;

            // Initialize player data if they just enabled the speedometer
            if (!speeds.ContainsKey(p.name)) {
                speeds[p.name] = new List<SpeedTime> {
                    new SpeedTime(curP, curT)
                };
                return;
            }

            List<SpeedTime> st = speeds[p.name];

            // Only keep 3 seconds of entries
            st.RemoveAll(s => (curT - s.Time).TotalMilliseconds >= 3000);

            if (st.Count > 0) {
                SpeedTime lastST = st[st.Count - 1];

                double tDelta = (curT - lastST.Time).TotalSeconds;
                Position lastSTP = lastST.Position;
                if (tDelta < 0.05) return; //Packet racing issue, causes speed to jump up to 300+ blocks/s sometimes

                double xDis = CalcDis(lastSTP, curP,1) / 32.0;
                double yDis = CalcDis(lastSTP, curP, 2) / 32.0;
                double zDis = CalcDis(lastSTP, curP, 3) / 32.0;
                double xzDis = CalcDis(lastSTP, curP, 5) / 32.0;
                double xyzDis = CalcDis(lastSTP, curP, 4) / 32.0;
                double xSpeed = xDis / tDelta;
                double ySpeed = yDis / tDelta;
                double zSpeed = zDis / tDelta;
                double xzSpeed = xzDis / tDelta;
                double xyzSpeed = xyzDis / tDelta;
                st.Add(new SpeedTime(curP, xSpeed, ySpeed, zSpeed, xzSpeed, xyzSpeed, curT));

                // Display speed in bottom right CPE messages
                double xavr = CalcAvg(st, 1);
                double yavr = CalcAvg(st, 2);
                double zavr = CalcAvg(st, 3);
                double xzavr = CalcAvg(st, 5);
                double xyzavr = CalcAvg(st, 4);
                if (players[p.name] == 2) {
                    p.SendCpeMessage(CpeMessageType.BottomRight3, FormatLine(xavr, "&CX"));
                    p.SendCpeMessage(CpeMessageType.BottomRight2, FormatLine(yavr, "&AY"));
                    p.SendCpeMessage(CpeMessageType.BottomRight1, FormatLine(zavr, "&9Z"));
                    p.SendCpeMessage(CpeMessageType.Status1, FormatLine(xSpeed, "&CX"));
                    p.SendCpeMessage(CpeMessageType.Status2, FormatLine(ySpeed, "&AY"));
                    p.SendCpeMessage(CpeMessageType.Status3, FormatLine(zSpeed, "&9Z"));
                    p.SendCpeMessage(CpeMessageType.Announcement, "&S" + GetDial(xyzavr) + "[&CX&AY&9Z&S]" + GetDial(xyzSpeed));
                    p.SendCpeMessage(CpeMessageType.BigAnnouncement, "&S" + GetDial(xzavr) + "[&CX&9Z&S]" + GetDial(xzSpeed));
                    p.SendCpeMessage(CpeMessageType.SmallAnnouncement, string.Format("{0:F3} &Sblocks/s [&CX&9Z&S]", CalcAvg(st, 5)));
                } else {
                    const string xyz = "&CX&AY&9Z";
                    p.SendCpeMessage(CpeMessageType.BottomRight3, string.Format("&S{0}[{1}]", GetDial(xyzavr), xyz));
                    p.SendCpeMessage(CpeMessageType.BottomRight2, string.Format("{0:F3} &Sblocks/s 3s avg [{1}&S]", xyzavr, xyz));
                    p.SendCpeMessage(CpeMessageType.BottomRight1, string.Format("{0:F3} &Sblocks/s current [{1}&S]", xyzSpeed, xyz));
                }
            } else {
                // No data within last 2 seconds
                st.Add(new SpeedTime(curP, curT));
            }
        }

        string FormatLine(double speed, string dir) {
            return string.Format("&S[&F{0:F2}&Sb/s][{1}&S]{2}", speed, dir, GetDial(speed));
        }

        public string GetDial(double speed) {
            StringBuilder sb = new StringBuilder();
            int floor = (int)Math.Floor(speed);
            int shift = (int)Math.Round((speed - floor) * 6);

            // Build the full string before trimming
            for (int i = floor - 1; i <= floor + 2; i++) {
                string num = i < 0 ? "_" : i.ToString();
                sb.Append(num.PadRight(6, '_'));
            }

            // Use a substring to fit it into a 13 character wide string
            string dial = sb.ToString();
            string first = dial.Substring(shift, 6);
            string mid = dial.Substring(shift + 6, 1);
            string last = dial.Substring(shift + 7, 6);

            // Return dial [______0_____1]
            return string.Format("[&F{0}&C{1}&F{2}&S]", first, mid, last);
        }

        private void OnPlayerDisconnectCall(Player p, string reason) {
            speeds.Remove(p.name);
            players.Remove(p.name);
        }

        private double CalcDis(Position p1, Position p2, byte dir) {
            // Dir 1 = X, Dir 2 = Y, Dir 3 = Z, Dir 4 = XYZ, Dir 5 = XZ
            bool x = dir == 1 || dir == 4 || dir == 5, y = dir == 2 || dir == 4, z = dir == 3 || dir == 4 || dir == 5;
            double dX = x ? Math.Abs(p2.X - p1.X) : 0;
            double dY = y ? Math.Abs(p2.Y - p1.Y) : 0;
            double dZ = z ? Math.Abs(p2.Z - p1.Z) : 0;
            return Math.Sqrt((dX * dX) + (dY * dY) + (dZ * dZ));
        }

        private double CalcAvg(List<SpeedTime> speedTimes, byte dir) {
            double totalSpeed = 0;
            foreach (SpeedTime speedTime in speedTimes) {
                switch (dir) {
                    case 1:
                        totalSpeed += speedTime.XSpeed;
                        break;
                    case 2:
                        totalSpeed += speedTime.YSpeed;
                        break;
                    case 3:
                        totalSpeed += speedTime.ZSpeed;
                        break;
                    case 4:
                        totalSpeed += speedTime.XYZSpeed;
                        break;
                    case 5:
                        totalSpeed += speedTime.XZSpeed;
                        break;
                    default:
                        break;
                }
            }
            return totalSpeed / speedTimes.Count;
        }
    }

    public class SpeedTime {
        public Position Position { get; set; }
        public double XSpeed { get; set; }
        public double YSpeed { get; set; }
        public double ZSpeed { get; set; }
        public double XZSpeed { get; set; }
        public double XYZSpeed { get; set; }
        public DateTime Time { get; set; }

        public SpeedTime(Position position, DateTime time) {
            Position = position;
            XSpeed = 0;
            YSpeed = 0;
            ZSpeed = 0;
            XZSpeed = 0;
            XYZSpeed = 0;
            Time = time;
        }

        public SpeedTime(Position position, double xSpeed, double ySpeed, double zSpeed, double xzSpeed, double xyzSpeed, DateTime time) {
            Position = position;
            XSpeed = xSpeed;
            YSpeed = ySpeed;
            ZSpeed = zSpeed;
            XZSpeed = xzSpeed;
            XYZSpeed = xyzSpeed;
            Time = time;
        }
    }

    public class CmdSpeedometer : Command {
        public override string name { get { return "Speedometer"; } }
        public override string type { get { return CommandTypes.Other; } }

        public override void Use(Player p, string message) {
            Speedometer.UpdatePlayer(p, message);
        }

        public override void Help(Player p) {
            p.Message("%T/Speedometer");
            p.Message("%HToggles whether or not you have a speedometer displayed at the bottom right side of your screen.");
        }
    }
}