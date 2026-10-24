namespace TuTien.Model.Template
{
    public class AppearanceTemplate
    {
        public int Id { get; set; }
        public int Gender { get; set; }
        public string Name { get; set; }

        public AppearanceTemplate()
        {
            Name = string.Empty;
        }
    }
}
