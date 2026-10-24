namespace TuTien.Model.Template
{
    public class SkillTemplate
    {
        public short Id { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; }
        public int MaxPoint { get; set; }
        public int ManaUseType { get; set; }
        public int Type { get; set; }
        public int IconId { get; set; }
        public string DamageInfo { get; set; }
        public string Description { get; set; }
        public int Cooldown { get; set; }
        public int RealmId { get; set; }
        public int ElementId { get; set; }
        public bool IsImplemented { get; set; }
        public short TeacherNpcId { get; set; }
        public string AssetKey { get; set; }
        public List<SkillDataTemplate> SkillDataTemplates { get; set; }

        public SkillTemplate()
        {
            Name = string.Empty;
            DamageInfo = string.Empty;
            Description = string.Empty;
            AssetKey = string.Empty;
            SkillDataTemplates = new List<SkillDataTemplate>();
        }
    }
}
