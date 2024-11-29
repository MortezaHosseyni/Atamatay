using Atamatay.Models.DnD;
using System.Text.Json;

namespace Atamatay.Utilities
{
    public class DdDatabase
    {
        private readonly string _filePath;

        public DdDatabase(string filePath)
        {
            _filePath = filePath;

            if (!Directory.Exists("dnd"))
                Directory.CreateDirectory("dnd");

            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "[]");
        }

        private List<DdSession> LoadSessions()
        {
            string json;
            using (var reader = new StreamReader(_filePath))
            {
                json = reader.ReadToEnd();
            }
            return JsonSerializer.Deserialize<List<DdSession>>(json) ?? [];
        }

        private void SaveSessions(List<DdSession> sessions)
        {
            var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
            using var writer = new StreamWriter(_filePath, false);
            writer.Write(json);
        }

        public void AddSession(DdSession session)
        {
            var sessions = LoadSessions();
            sessions.Add(session);
            SaveSessions(sessions);
        }

        public DdSession? GetSession(ulong channelId)
        {
            return LoadSessions().FirstOrDefault(s => s.ChannelId == channelId);
        }

        public List<DdSession> GetAllSessions()
        {
            return LoadSessions();
        }

        public bool UpdateSession(DdSession updatedSession)
        {
            var sessions = LoadSessions();
            var index = sessions.FindIndex(s => s.ChannelId == updatedSession.ChannelId);
            if (index == -1) return false;
            sessions[index] = updatedSession;
            SaveSessions(sessions);
            return true;
        }

        public bool UpdateSessionRound(ulong channelId, int newRound)
        {
            var session = GetSession(channelId);
            if (session == null) return false;
            session.Round = newRound;
            return UpdateSession(session);
        }

        public bool UpdatePlayerIsAccepted(ulong channelId, ulong playerId, bool isAccepted)
        {
            var sessions = LoadSessions();
            var session = sessions.FirstOrDefault(s => s.ChannelId == channelId);

            var player = session?.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) return false;
            player.IsAccepted = isAccepted;
            SaveSessions(sessions);
            return true;
        }

        public bool AddDialogToSession(ulong channelId, DdDialog newDialog)
        {
            var session = GetSession(channelId);
            if (session == null) return false;
            session.Dialogs ??= [];
            session.Dialogs.Add(newDialog);
            return UpdateSession(session);
        }

        public bool AddTimelineToSession(ulong channelId, DdTimeline newTimeline)
        {
            var session = GetSession(channelId);
            if (session == null) return false;
            session.Timelines ??= [];
            session.Timelines.Add(newTimeline);
            return UpdateSession(session);
        }

        public bool DeleteSession(ulong channelId)
        {
            var sessions = LoadSessions();
            var sessionToRemove = sessions.FirstOrDefault(s => s.ChannelId == channelId);
            if (sessionToRemove == null) return false;
            sessions.Remove(sessionToRemove);
            SaveSessions(sessions);
            return true;
        }
    }
}
