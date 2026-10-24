using TuTien.Application.Interfaces.Character;
using TuTien.Model.Info;

namespace TuTien.Application.Interfaces.Skill
{
    public interface ISkillHandler
    {
        Task<InfoAttack?> AttackPlayer(ICharacter character, int targetId, string playToken = "", Action<string>? onError = null, CancellationToken cancellationToken = default, int skillSlot = -1);
        Task<InfoAttack?> AttackMonster(ICharacter character, int targetId, string lifeId, string playToken,
            Action<string>? onError = null, CancellationToken cancellationToken = default, int skillSlot = -1);
    }
}
