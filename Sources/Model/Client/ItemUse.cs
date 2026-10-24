namespace TuTien.Model.Client
{
    public class ItemUse
    {
        public string Uid { get; set; }
        public long Revision { get; set; }
        public string PlayToken { get; set; }

        public ItemUse()
        {
            Uid = string.Empty;
            PlayToken = string.Empty;
        }
    }
}
