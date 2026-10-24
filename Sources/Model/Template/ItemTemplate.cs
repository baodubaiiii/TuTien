using TuTien.Model.Option;

namespace TuTien.Model.Template
{
    public class ItemTemplate
    {
        public short Id { get; set; }
        public int Type { get; set; }
        public int Skill { get; set; }
        public int Gender { get; set; }
        public string Name { get; set; }
        public string SubName { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int IconId { get; set; }
        public int Part { get; set; }
        public bool IsUpToUp { get; set; }
        public bool IsDrop { get; set; }
        public long Require { get; set; }
        public bool IsExpires { get; set; }
        public long SecondsExpires { get; set; }
        public long SaleCoin { get; set; }
        public List<OptionItem> Options { get; set; }
        public int ClassId { get; set; }
        public int RealmId { get; set; }
        public int ElementId { get; set; }
        public int Rarity { get; set; }
        public int MaxStack { get; set; }
        public string AssetKey { get; set; }
        public int EffectStatId { get; set; }
        public int EffectParam { get; set; }
        public int DurationSeconds { get; set; }
        public int RestorePercent { get; set; }

        public bool IsTypeBody()
        {
            return Type >= 0 && Type <= 5;
        }

        public ItemTemplate()
        {
            Name = string.Empty;
            SubName = string.Empty;
            Description = string.Empty;
            AssetKey = string.Empty;
            Options = new List<OptionItem>();
        }
    }
}
