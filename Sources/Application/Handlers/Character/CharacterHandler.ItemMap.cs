using TuTien.Application.Handlers.Item;
using TuTien.Database.Player;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Handlers.Character
{
    public partial class CharacterHandler
    {
        public Task<GameCharacter?> PickItemMap(string id, string playToken, CharacterDB database, Action<string> onError, CancellationToken cancellationToken)
        {
            return Handle<GameCharacter>(async () =>
            {
                var character = (GameCharacter)Character;
                var zone = (GameZone)character.Zone!;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (character.InfoGame.Hp <= 0 || character.InfoGame.Breakthrough.Active || !character.TalentSeen || string.IsNullOrEmpty(playToken) || playToken != character.PlayToken ||
                    !zone.ItemMaps.TryGetValue(id, out var item) || item.LeftTime <= now)
                {
                    onError("Vật phẩm hoặc phiên chơi không hợp lệ.");
                    return null;
                }
                if (item.PlayerId != character.Id && now < item.OwnerTime)
                {
                    onError("Vật phẩm đang thuộc về đạo hữu khác.");
                    return null;
                }
                float dx = item.X - character.Position.X;
                float dy = item.Y - character.Position.Y;
                if (dx * dx + dy * dy > _config!.InteractionRange * _config.InteractionRange)
                {
                    onError("Hãy đến gần vật phẩm để nhặt.");
                    return null;
                }
                var draft = character.Clone();
                if (!new ItemHandler(_cache!).AddItemToBag(draft, item.ItemId, item.Quantity, out string error))
                {
                    onError(error);
                    return null;
                }
                if (!await database.SavePickItem(draft, item, now))
                {
                    onError("Vật phẩm đã được nhặt hoặc dữ liệu đã thay đổi. Hãy vào lại game.");
                    return null;
                }
                SetData(draft);
                zone.ItemMaps.Remove(item.Id);
                return character.Clone();
            }, onError, cancellationToken);
        }
    }
}
