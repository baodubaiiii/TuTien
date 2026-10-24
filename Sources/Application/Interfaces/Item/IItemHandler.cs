using GameItem = TuTien.Model.Item.Item;

namespace TuTien.Application.Interfaces.Item
{
    public interface IItemHandler
    {
        GameItem? GetItemDefault(short id);
    }
}
