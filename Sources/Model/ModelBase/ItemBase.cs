using TuTien.Model.Option;

namespace TuTien.Model.ModelBase
{
    public class ItemBase
    {
        public short Id { get; set; }
        public int Quantity { get; set; }
        public List<OptionItem> Options { get; set; }

        public ItemBase()
        {
            Quantity = 1;
            Options = new List<OptionItem>();
        }

        public OptionItem? GetOption(int id)
        {
            return Options.Find(option => option.Id == id);
        }

        public int GetParamOption(int id)
        {
            return GetOption(id)?.Param ?? 0;
        }
    }
}
