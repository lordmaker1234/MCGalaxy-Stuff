//reference System.dll
//reference System.Core.dll
using MCGalaxy.Config;
using MCGalaxy.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace MCGalaxy {
    public class CmdCCapi : Command {
        public override string name { get { return "CCapi"; } }
        public override string type { get { return CommandTypes.Information; } }

        // Dictionary of flags and their values.
        private readonly Dictionary<string, string> lookupDict = new Dictionary<string, string>{
            { "a", "Website Admin" }, { "b", "&4Banned from old forums&F" },
            { "d", "ClassiCube Developer" }, { "e", "ClassiCube Blog Editor" },
            { "m", "Forum Moderator" }, { "p", "&6ClassiCube Patron&F" },
            { "r", "Recovering Account" }, { "u", "Unverified" }
        };

        public override void Use(Player p, string message) {
            // User own player name if none specified.
            string userSearch = p.truename, data = "";
            if (!string.IsNullOrWhiteSpace(message)) userSearch = message;

            // Although a valid username, '..' cannot be looked up due to how the API works.
            if (userSearch == "..") { p.Message("&WSorry, but that is one of the only names that can't be looked up."); return; }

            // Check if the provided username is valid.
            if (!Formatter.ValidPlayerName(p, userSearch)) return;

            // Download Player APi data from ClassiCube website.
            using (WebClient client = HttpUtil.CreateWebClient()) {
                try {
                    data = client.DownloadString("https://www.classicube.net/api/player/" + userSearch);
                } catch (WebException e) {
                    if (e.Message.CaselessContains("too many requests")) {
                        p.Message("&4Server Rate Limited! Please try again in a bit."); return;
                    }
                } catch {
                    p.Message("&4Error! Failed to retrieve API data."); return;
                }
            }

            // De-serialize JSON string to PlayerAPI object.
            PlayerApi playerAPI;
            try {
                playerAPI = PlayerApi.FromString(data);
                string error = playerAPI.Error;
                if (!string.IsNullOrWhiteSpace(error)) {
                    p.Message("&WError: " + error); return;
                }
            } catch {
                p.Message("&WError parsing Player API data"); return;
            }

            // Show Username, Premium status, and user ID.
            p.Message("API info about user {1}&F{0}&S with ID:&F{2}", playerAPI.Username, playerAPI.Premium ? "&2☼" : "", playerAPI.Id);

            // Show Registered timestamp, DateTime, and TimeSpan.
            DateTime registered = DateTimeOffset.FromUnixTimeSeconds(playerAPI.Registered).DateTime;
            p.Message(" Registered[&F{0}&S]", playerAPI.Registered);
            p.Message("  &F{0}&S at &F{1}&S UTC", registered.ToLongDateString(), registered.ToLongTimeString());
            p.Message("  &F{0} ago.", TimeSpanToString(DateTime.UtcNow - registered));

            // Show Flags and their meanings.
            if (playerAPI.Flags.Any()) {
                string flagSTR = string.Join(", ", Array.ConvertAll(playerAPI.Flags, c => lookupDict[c]));
                p.Message(" Flags[&F{0}&S]", playerAPI.Flags.Join(","));
                p.Message("  &F" + flagSTR.TrimEnd(',', ' '));
            }

            // Show Forum Title.
            string fTitle = playerAPI.ForumTitle;
            if (!string.IsNullOrWhiteSpace(fTitle)) {
                p.Message(" Forum Title");
                p.Message("  &F" + fTitle);
            }
        }

        public static string TimeSpanToString(TimeSpan value) {
            string time = "";
            bool negate = value.TotalSeconds < 0;
            if (negate) value = -value;
            Add(ref time, value.Days, "day");
            Add(ref time, value.Hours, "hour");
            Add(ref time, value.Minutes, "minute");
            Add(ref time, value.Seconds, "second");

            if (string.IsNullOrWhiteSpace(time))
                time = "right now";
            time = time.TrimEnd(',');
            if (time.Contains(",")) {
                string beforeand = time.Substring(0, time.LastIndexOf(','));
                string afterand = time.Substring(time.LastIndexOf(',')).TrimStart(',');
                time = beforeand + ", and" + afterand;
            }
            return negate ? "-" + time : time;
        }

        public static void Add(ref string time, int amount, string suffix) {
            if (amount == 0) return;
            time = (time.Length == 0 ? "" : time + " ") + amount.ToString("N0") + " " + suffix + (amount != 1 ? "s," : ",");
        }

        public override void Help(Player p) {
            p.Message("&T/CCapi <username>");
            p.Message("&HShows ClassiCube Account information for a specified username.");
        }
    }

    public class PlayerApi {
        public string Error { get; set; }
        public string[] Flags { get; set; }
        public string ForumTitle { get; set; }
        public int Id { get; set; }
        public bool Premium { get; set; }
        public long Registered { get; set; }
        public string Username { get; set; }

        internal static PlayerApi FromString(string json) {
            PlayerApi player = new PlayerApi();

            // If no 'forum_title', JSON is not player API data
            if (!json.Contains("forum_title")) return null;

            JsonObject pairs = (JsonObject)new JsonReader(json).Parse();

            // Parse Error and return empty player if any error is thrown.
            player.Error = (string)pairs["error"];
            if (!string.IsNullOrWhiteSpace(player.Error)) return player;

            // Parse flags as a string[]
            List<string> flags = new List<string>();
            foreach (object entry in (JsonArray)pairs["flags"]) {
                flags.Add(entry.ToString());
            }
            player.Flags = flags.ToArray();

            // Parse the rest of the data.
            player.ForumTitle = (string)pairs["forum_title"];
            player.Id = int.Parse((string)pairs["id"]);
            player.Premium = bool.Parse((string)pairs["premium"]);
            player.Registered = int.Parse((string)pairs["registered"]);
            player.Username = (string)pairs["username"];

            return player;
        }
    }
}