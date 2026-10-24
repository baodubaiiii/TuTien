using GameCharacter = TuTien.Model.Character.Character;
using GameItem = TuTien.Model.Item.Item;
using TuTien.Model.Template;

namespace TuTien.Application.Handlers.Item
{
    public partial class ItemHandler
    {
        public bool AddItemToBag(GameCharacter character, short id, int quantity, out string error)
        {
            var item = GetItemDefault(id);
            if (item == null)
            {
                error = "Vật phẩm không hợp lệ.";
                return false;
            }
            item.Quantity = quantity;
            return AddItemToBag(character, item, out error);
        }

        public bool AddItemToBag(GameCharacter character, GameItem reward, out string error)
        {
            error = string.Empty;
            if (!_cache.ItemTemplates.TryGetValue(reward.Id, out var template) || reward.Quantity < 1 || reward.Options == null)
            {
                error = "Vật phẩm không hợp lệ.";
                return false;
            }
            int quantity = reward.Quantity;
            long space = Math.Max(0, character.BagCapacity - character.ItemBag.Count) * (long)template.MaxStack;
            foreach (var item in character.ItemBag)
            {
                if (CanStack(item, reward, template))
                {
                    space += template.MaxStack - item.Quantity;
                }
            }
            if (space < quantity)
            {
                error = "Hành trang không đủ chỗ.";
                return false;
            }
            foreach (var item in character.ItemBag)
            {
                if (CanStack(item, reward, template))
                {
                    int amount = Math.Min(quantity, template.MaxStack - item.Quantity);
                    item.Quantity += amount;
                    quantity -= amount;
                    if (quantity == 0)
                    {
                        return true;
                    }
                }
            }
            for (int index = 0; index < character.BagCapacity && quantity > 0; index++)
            {
                if (character.ItemBag.Find(item => item.IndexUI == index) != null)
                {
                    continue;
                }
                var item = reward.Clone();
                item.Uid = Guid.NewGuid().ToString("N");
                item.Quantity = Math.Min(quantity, template.MaxStack);
                item.IndexUI = index;
                character.ItemBag.Add(item);
                quantity -= item.Quantity;
            }
            return quantity == 0;
        }

        private static bool CanStack(GameItem item, GameItem reward, ItemTemplate template)
        {
            return item.Id == template.Id && template.MaxStack > 1 && item.Quantity < template.MaxStack &&
                item.Options.Count == reward.Options.Count && reward.Options.All(option =>
                    item.Options.Any(value => value.Id == option.Id && value.Param == option.Param && value.Type == option.Type));
        }
    }
}
