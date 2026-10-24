namespace TuTien.Model.Client
{
    public class CharacterAttack
    {
        public int TargetId { get; set; }
        public string PlayToken { get; set; }

        public CharacterAttack()
        {
            PlayToken = string.Empty;
        }
    }
}
