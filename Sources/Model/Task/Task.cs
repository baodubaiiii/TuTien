using TuTien.Model.ModelBase;

namespace TuTien.Model.Task
{
    public class Task : TaskBase
    {
        public int Index { get; set; }
        public int Max { get; set; }
        public short Count { get; set; }

        public Task()
        {
        }
    }
}
