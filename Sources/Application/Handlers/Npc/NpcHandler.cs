using TuTien.Application.Interfaces.Npc;
using GameNpc = TuTien.Model.Map.Npc;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Npc
{
    public class NpcHandler : INpcHandler
    {
        public static bool CanInteract(GameCharacter character, int npcId, int range)
        {
            if (character.Zone is not TuTien.Model.Map.Zone zone || character.InfoGame.Hp <= 0 || character.InfoGame.Breakthrough.Active)
            {
                return false;
            }
            var npc = zone.Npcs.Find(value => value.Id == npcId && value.Status == 1);
            return npc != null && (npc.X - character.Position.X) * (npc.X - character.Position.X) +
                (npc.Y - character.Position.Y) * (npc.Y - character.Position.Y) <= range * range;
        }
        protected GameNpc Npc { get; }

        public NpcHandler(GameNpc npc)
        {
            Npc = npc;
        }

        public virtual void Chat()
        {
        }

        public virtual void Chat(string text)
        {
        }

        public virtual void Update(long timeServer)
        {
        }

        public object? OpenMenu(GameCharacter character, string playToken, int range, out string error)
        {
            error = string.Empty;
            float dx = Npc.X - character.Position.X;
            float dy = Npc.Y - character.Position.Y;
            if (!ReferenceEquals(Npc.Zone, character.Zone) || Npc.Status != 1 || character.InfoGame.Hp <= 0 || character.InfoGame.Breakthrough.Active ||
                !character.TalentSeen || string.IsNullOrEmpty(playToken) || character.PlayToken != playToken || dx * dx + dy * dy > range * range)
            {
                error = "Hãy đến gần NPC để trò chuyện.";
                return null;
            }
            return new
            {
                Npc.Id,
                Npc.Name,
                text = Npc.Chats.Count == 0 ? "Đạo hữu muốn tìm hiểu khu vực này?" : string.Join("\n", Npc.Chats),
                menu = new[] { "Nhiệm vụ", "Truyền công", "Đổi khu vực", "Đóng" }
            };
        }
    }
}
