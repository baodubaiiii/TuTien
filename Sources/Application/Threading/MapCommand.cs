using System.Threading.Channels;

namespace TuTien.Application.Threading
{
    public class MapCommand
    {
        public Func<Task> Execute { get; }
        public Action Cancel { get; }

        public MapCommand(Func<Task> execute, Action cancel)
        {
            Execute = execute;
            Cancel = cancel;
        }
    }
}
