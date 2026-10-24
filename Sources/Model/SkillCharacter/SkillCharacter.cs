namespace TuTien.Model.SkillCharacter
{
    public class SkillCharacter
    {
        public short Id { get; set; }
        public short TemplateId { get; set; }
        public int Level { get; set; }
        public long LastTimeUse { get; set; }

        public SkillCharacter()
        {
        }
    }
}
