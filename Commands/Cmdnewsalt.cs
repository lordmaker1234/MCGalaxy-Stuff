namespace MCGalaxy {
    public class CmdNewSalt : Command {
        public override string name { get { return "NewSalt"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message) {
            p.Message("Regenerating salts:");
            foreach (Network.Heartbeat hb in Network.Heartbeat.Heartbeats) {
                hb.Auth.Salt = Server.GenerateSalt();
                p.Message("&9 > " + hb.URL);
            }
            p.Message("Done!");
        }

        public override void Help(Player p) {
            p.Message("%T/NewSalt");
            p.Message("%HRegenerates the server salt on all authenticating heartbeats.");
            p.Message("%CWarning: &FWill break Resume and require people to rejoin from server list.");
        }
    }
}