using TuTien.Application.Interfaces.Item;
using TuTien.Database;
using TuTien.Model.Effect;
using TuTien.Model.Option;
using GameCharacter = TuTien.Model.Character.Character;
using GameItem = TuTien.Model.Item.Item;

namespace TuTien.Application.Handlers.Item
{
    public partial class ItemHandler : IItemHandler
    {
        private readonly Cache _cache;

        public ItemHandler()
        {
            _cache = new Cache();
        }

        public ItemHandler(Cache cache)
        {
            _cache = cache;
        }

        public virtual GameItem? GetItemDefault(short id)
        {
            if (!_cache.ItemTemplates.TryGetValue(id, out var template))
            {
                return null;
            }

            var item = new GameItem
            {
                Id = id,
                Uid = Guid.NewGuid().ToString("N"),
                Quantity = 1
            };
            foreach (var option in template.Options)
            {
                item.Options.Add(new OptionItem
                {
                    Id = option.Id,
                    Param = option.Param,
                    Type = option.Type
                });
            }

            return item;
        }

        public bool UseItemBag(GameCharacter character, string uid, long timeServer, out string error)
        {
            error = string.Empty;
            var item = character.ItemBag.Find(value => value.Uid == uid);
            if (item == null || item.Quantity < 1 || !_cache.ItemTemplates.TryGetValue(item.Id, out var template))
            {
                error = "Vật phẩm không còn trong hành trang.";
                return false;
            }

            if (!_cache.RealmTemplates.TryGetValue(character.RealmId, out var realm) ||
                !_cache.RealmTemplates.TryGetValue(template.RealmId, out var required) || realm.Rank < required.Rank)
            {
                error = "Cảnh giới chưa đủ để sử dụng vật phẩm.";
                return false;
            }

            if (template.IsTypeBody())
            {
                return UseItemBody(character, item, out error);
            }

            switch (template.Type)
            {
                case 6:
                    if (template.EffectStatId < 1 || template.EffectStatId > 5 || template.DurationSeconds < 1 ||
                        template.DurationSeconds > 86400 || template.RestorePercent < 0 || template.RestorePercent > 100 || template.EffectParam < 0)
                    {
                        error = "Đan dược chưa có cấu hình sử dụng hợp lệ.";
                        return false;
                    }

                    var active = character.InfoGame.Effects.Find(effect => effect.StatId == template.EffectStatId && effect.ExpireTime > timeServer);
                    if (active != null && active.Param > template.EffectParam)
                    {
                        error = "Đạo hữu đang có hiệu lực đan dược mạnh hơn.";
                        return false;
                    }

                    character.InfoGame.Effects.RemoveAll(effect => effect.StatId == template.EffectStatId);
                    character.InfoGame.Effects.Add(new ItemEffect
                    {
                        ItemId = item.Id,
                        StatId = template.EffectStatId,
                        Param = template.EffectParam,
                        ExpireTime = timeServer + template.DurationSeconds * 1000L
                    });
                    item.Quantity--;
                    if (item.Quantity == 0)
                    {
                        character.ItemBag.Remove(item);
                    }

                    character.CharacterHandler!.SetUpInfo();
                    if (template.EffectStatId == 1)
                    {
                        character.InfoGame.Hp = Math.Min(character.InfoChar.HpFull, character.InfoGame.Hp + character.InfoChar.HpFull * template.RestorePercent / 100);
                    }
                    else if (template.EffectStatId == 3)
                    {
                        character.InfoGame.Mp = Math.Min(character.InfoChar.MpFull, character.InfoGame.Mp + character.InfoChar.MpFull * template.RestorePercent / 100);
                    }

                    character.CharacterHandler.SetUpInfo();
                    return true;

                default:
                    error = "Vật phẩm này không thể dùng trực tiếp từ hành trang.";
                    return false;
            }
        }

        private bool UseItemBody(GameCharacter character, GameItem item, out string error)
        {
            error = string.Empty;
            var template = _cache.ItemTemplates[item.Id];
            if (item.Quantity != 1 || (template.ClassId > 0 && template.ClassId != character.ClassId) ||
                (template.Gender != 3 && template.Gender != character.Gender))
            {
                error = "Trang bị không phù hợp với pháp khí hoặc giới tính của nhân vật.";
                return false;
            }

            int bagSlot = item.IndexUI;
            var previous = character.ItemBody.Find(value => value.IndexUI == template.Type);
            character.ItemBag.Remove(item);
            if (previous != null)
            {
                character.ItemBody.Remove(previous);
                previous.IndexUI = bagSlot;
                character.ItemBag.Add(previous);
            }

            item.IndexUI = template.Type;
            character.ItemBody.Add(item);
            character.CharacterHandler!.SetUpInfo();
            return true;
        }

        public bool RemoveItemBody(GameCharacter character, int slot, int capacity, out string error)
        {
            error = string.Empty;
            var item = character.ItemBody.Find(value => value.IndexUI == slot);
            int empty = -1;
            for (int index = 0; index < capacity; index++)
            {
                if (character.ItemBag.Find(value => value.IndexUI == index) == null)
                {
                    empty = index;
                    break;
                }
            }

            if (item == null || empty < 0)
            {
                error = item == null ? "Ô trang bị đang trống." : "Hành trang đã đầy.";
                return false;
            }

            character.ItemBody.Remove(item);
            item.IndexUI = empty;
            character.ItemBag.Add(item);
            character.CharacterHandler!.SetUpInfo();
            return true;
        }
    }
}
