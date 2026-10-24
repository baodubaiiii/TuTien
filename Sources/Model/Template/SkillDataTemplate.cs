namespace TuTien.Model.Template
{
    public class SkillDataTemplate
    {
        public int SkillId { get; set; }
        public int Point { get; set; }
        public long PowRequire { get; set; }
        public int ManaUse { get; set; }
        public int CoolDown { get; set; }
        public int Dx { get; set; }
        public int Dy { get; set; }
        public int MaxFight { get; set; }
        public int Damage { get; set; }
        public int Price { get; set; }
        public long TuViCost { get; set; }
        public int RealmId { get; set; }
        public string MoreInfo { get; set; }
        public int IconId { get; set; }

        public SkillDataTemplate()
        {
            MoreInfo = string.Empty;
        }
    }
}
