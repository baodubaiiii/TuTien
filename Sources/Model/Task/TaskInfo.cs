namespace TuTien.Model.Task
{
    public class TaskInfo
    {
        public Task? MainTask { get; set; }
        public List<short> Completed { get; set; } = new List<short>();

        public TaskInfo()
        {
        }
    }
}
