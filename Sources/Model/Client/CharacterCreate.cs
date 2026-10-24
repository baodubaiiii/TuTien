namespace TuTien.Model.Client
{
    public class CharacterCreate
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public int ClassId { get; set; }
        public int Gender { get; set; }
        public int AppearanceId { get; set; }

        public CharacterCreate()
        {
            Name = string.Empty;
        }
    }
}
