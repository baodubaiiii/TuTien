namespace TuTien.Model.Template
{
    public class RaceTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Style { get; set; }
        public int KhiHuyetPercent { get; set; }
        public int ChanLucPercent { get; set; }
        public int PhapLucPercent { get; set; }
        public int LinhGiacPercent { get; set; }
        public int SatCoPercent { get; set; }

        public RaceTemplate()
        {
            Name = string.Empty;
            Style = string.Empty;
        }
    }
}
