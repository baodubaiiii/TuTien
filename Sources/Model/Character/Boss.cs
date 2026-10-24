using TuTien.Application.Handlers.Character;
using TuTien.Model.ModelBase;

namespace TuTien.Model.Character
{
    public class Boss : CharacterBase
    {
        public Boss()
        {
            CharacterHandler = new BossHandler(this);
        }
    }
}
