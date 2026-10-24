using TuTien.Application.Interfaces.Zone;
using TuTien.Model.Info;
using TuTien.Model.ModelBase;

namespace TuTien.Application.Interfaces.Character
{
    public interface ICharacter
    {
        int Id { get; set; }
        string Name { get; set; }
        Position Position { get; set; }
        InfoChar InfoChar { get; set; }
        IZone? Zone { get; set; }
        ICharacterHandler? CharacterHandler { get; set; }
    }
}
