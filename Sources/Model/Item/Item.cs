using TuTien.Model.ModelBase;

namespace TuTien.Model.Item
{
    public class Item : ItemBase
    {
        public int IndexUI { get; set; }
        public string Uid { get; set; }

        public Item()
        {
            Uid = string.Empty;
        }

        public Item Clone()
        {
            var copy = (Item)MemberwiseClone();
            copy.Options = Options.Select(option => new TuTien.Model.Option.OptionItem
            {
                Id = option.Id,
                Param = option.Param,
                Type = option.Type
            }).ToList();
            return copy;
        }
    }
}
