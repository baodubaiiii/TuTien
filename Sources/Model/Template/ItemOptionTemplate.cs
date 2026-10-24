namespace TuTien.Model.Template
{
    public class ItemOptionTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }

        public ItemOptionTemplate()
        {
            Name = string.Empty;
        }
    }
}
