using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using System;
using System.Collections.Generic;
using System.Text;

namespace PluginSpeedometer {
    public sealed class Speedometer : Plugin {
        public override string name { get { return "Speedometer"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
        public override string creator { get { return "123DontMessWitMe"; } }
        public override int build { get { return 1; } }

        public static Dictionary<string, List<SpeedTime>> speeds = new Dictionary<string, List<SpeedTime>>();
        public static Dictionary<string, bool> players = new Dictionary<string, bool>();

        public override void Load(bool auto) {
            OnPlayerMoveEvent.Register(OnPlayerMoveCall, Priority.Normal);
            OnPlayerDisconnectEvent.Register(OnPlayerDisconnectCall, Priority.Low);
            Command.Register(new CmdSpeedometer());
        }

        public override void Unload(bool auto) {
            Command.Unregister(Command.Find("Speedometer"));
            OnPlayerMoveEvent.Unregister(OnPlayerMoveCall);
            OnPlayerDisconnectEvent.Unregister(OnPlayerDisconnectCall);
        }

        public void OnPlayerMoveCall(Player p, Position next, byte yaw, byte pitch, ref bool cancel) {
            // Check if player has enabled the speedometer, also don't bother if they don't support CPE message types
            bool isEnabled;
            if (!players.TryGetValue(p.name, out isEnabled) ||
                !isEnabled || !p.Supports(CpeExt.MessageTypes)) { return; }

            DateTime currentTime = DateTime.Now;
            Position currentPosition = next;

            // Initialize player data if they just enabled the speedometer
            if (!speeds.ContainsKey(p.name)) {
                speeds[p.name] = new List<SpeedTime> {
                    new SpeedTime(currentPosition, 0, currentTime)
                };
                return;
            }

            List<SpeedTime> speedTimes = speeds[p.name];

            // Only keep 2 seconds of entries
            speedTimes.RemoveAll(st => (currentTime - st.Time).TotalSeconds > 2);

            if (speedTimes.Count > 0) {
                SpeedTime lastSpeedTime = speedTimes[speedTimes.Count - 1];

                double timeDelta = (currentTime - lastSpeedTime.Time).TotalSeconds;
                if (timeDelta > 0) {
                    double distance = CalculateDistance(lastSpeedTime.Position, currentPosition) / 32.0;
                    double currentSpeed = distance / timeDelta;
                    speedTimes.Add(new SpeedTime(currentPosition, currentSpeed, currentTime));

                    // Display speed in bottom right CPE messages
                    double avr = CalculateAverageSpeed(speedTimes);
                    p.SendCpeMessage(CpeMessageType.BottomRight3, GetDial(avr));
                    p.SendCpeMessage(CpeMessageType.BottomRight2,
                        string.Format("{0:F2} &Sblocks/s 2s avg", avr));
                    p.SendCpeMessage(CpeMessageType.BottomRight1,
                        string.Format("{0:F2} &Sblocks/s current", currentSpeed));
                }
            } else {
                // No data within last 2 seconds
                speedTimes.Add(new SpeedTime(currentPosition, 0, currentTime));
            }
        }

        public string GetDial(double speed) {
            StringBuilder sb = new StringBuilder();
            int floor = (int)Math.Floor(speed);
            double fractional = speed - floor;
            int shift = (int)Math.Round(fractional * 6);

            // Build the full string
            for (int i = floor - 1; i <= floor + 2; i++) {
                string num = i < 0 ? "-" : i.ToString();
                sb.Append(num.PadRight(6, '-'));
            }

            // Use a substring to fit it into a 15 character wide dial
            string dial = sb.ToString();
            StringBuilder result = new StringBuilder();
            result.Append(dial, shift, 6);
            result.Append("&C"); //Highlight the center character
            result.Append(dial, shift + 6, 1);
            result.Append("&F");
            result.Append(dial, shift + 7, 6);

            return "&S[&F" + result + "&S]";
        }

        private void OnPlayerDisconnectCall(Player p, string reason) {
            if (!players.ContainsKey(p.name)) return;
            speeds.Remove(p.name);
            players.Remove(p.name);
        }

        private double CalculateDistance(Position pos1, Position pos2) {
            int deltaX = pos2.X - pos1.X;
            int deltaY = pos2.Y - pos1.Y;
            int deltaZ = pos2.Z - pos1.Z;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
            if (double.IsNaN(distance)) {
                return 0; // Reset to 0 if the distance is NaN
            }
            return distance;
        }

        private double CalculateAverageSpeed(List<SpeedTime> speedTimes) {
            double totalSpeed = 0;
            foreach (SpeedTime speedTime in speedTimes) {
                totalSpeed += speedTime.Speed;
            }
            return totalSpeed / speedTimes.Count;
        }
    }

    public class SpeedTime {
        public Position Position { get; set; }
        public double Speed { get; set; }
        public DateTime Time { get; set; }

        public SpeedTime(Position position, double speed, DateTime time) {
            Position = position;
            Speed = speed;
            Time = time;
        }
    }

    public class CmdSpeedometer : Command {
        public override string name { get { return "Speedometer"; } }
        public override string type { get { return CommandTypes.Other; } }

        public override void Use(Player p, string message) {
            bool isEnabled;
            if (!Speedometer.players.TryGetValue(p.name, out isEnabled)) {
                // Add player and enable speedometer
                Speedometer.players[p.name] = true;
                return;
            }

            if (isEnabled) {
                // Disable speedometer and clear CPE messages
                p.SendCpeMessage(CpeMessageType.BottomRight3, "");
                p.SendCpeMessage(CpeMessageType.BottomRight2, "");
                p.SendCpeMessage(CpeMessageType.BottomRight1, "");
                Speedometer.speeds.Remove(p.name);
            }

            // Toggle the player's speedometer
            Speedometer.players[p.name] = !isEnabled;
        }

        public override void Help(Player p) {
            p.Message("%T/Speedometer");
            p.Message("%HToggles whether or not you have a speedometer displayed at the bottom right side of your screen.");
        }
    }
}