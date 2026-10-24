namespace TuTien.Model.Client
{
    public class PotentialChange
    {
        public int[] Points { get; set; }
        public long Revision { get; set; }
        public string PlayToken { get; set; }

        public PotentialChange()
        {
            Points = new int[5];
            PlayToken = string.Empty;
        }
    }
}
