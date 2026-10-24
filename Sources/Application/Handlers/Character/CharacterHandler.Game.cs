using TuTien.Application.Threading;
using TuTien.Database.Player;
using GameCharacter = TuTien.Model.Character.Character;
using GameMap = TuTien.Application.Threading.Map;

namespace TuTien.Application.Handlers.Character
{
    public partial class CharacterHandler
    {
        public async Task<GameCharacter?> Handle(string playToken, long revision, CharacterDB database,
            Func<GameCharacter, string?> action, Action<string> onError, CancellationToken cancellationToken, bool allowTrial = false)
        {
            return await Handle(() => SaveInfo((GameCharacter)Character, playToken, revision, database, action, onError, allowTrial), onError, cancellationToken);
        }

        public async Task<T?> Handle<T>(Func<Task<T?>> action, Action<string> onError, CancellationToken cancellationToken) where T : class
        {
            if (Character is not GameCharacter character || character.Zone?.Map is not GameMap map || _cache == null || _config == null)
            {
                onError("Nhân vật chưa sẵn sàng. Hãy kết nối lại game.");
                return null;
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var token = deadline.Token;
            var zone = character.Zone;
            var completion = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var command = new MapCommand(async () =>
            {
                if (token.IsCancellationRequested)
                {
                    completion.TrySetCanceled(token);
                    return;
                }

                try
                {
                    if (IsClosed || character.IsChangingMap || !ReferenceEquals(character.Zone, zone) ||
                        !ReferenceEquals(character.Zone.Characters.GetValueOrDefault(character.Id), character))
                    {
                        onError("Phiên chơi đã kết thúc. Hãy vào lại game.");
                        completion.TrySetResult(null);
                        return;
                    }

                    completion.TrySetResult(await action());
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
            }, () => completion.TrySetResult(null));

            if (!map.AddCommand(command))
            {
                onError("Máy chủ đang bận. Hãy thử lại sau.");
                return null;
            }

            return await completion.Task.WaitAsync(token);
        }

        private async Task<GameCharacter?> SaveInfo(GameCharacter character, string playToken, long revision,
            CharacterDB database, Func<GameCharacter, string?> action, Action<string> onError, bool allowTrial)
        {
            if (!character.TalentSeen)
            {
                onError("Hãy xác nhận Thiên Phú trước khi thao tác.");
                return null;
            }
            if (character.InfoGame.Hp <= 0)
            {
                onError("Hãy hồi sinh trước khi sử dụng vật phẩm hoặc đổi chỉ số.");
                return null;
            }
            if (character.InfoGame.Breakthrough.Active && !allowTrial)
            {
                onError("Không thể thực hiện thao tác này khi đang độ kiếp.");
                return null;
            }

            if (string.IsNullOrEmpty(playToken) || !string.Equals(playToken, character.PlayToken, StringComparison.Ordinal))
            {
                onError("Phiên chơi đã thay đổi. Hãy vào lại game.");
                return null;
            }

            if (character.GameRevision != revision)
            {
                onError("Trạng thái đã thay đổi. Hãy kiểm tra lại hành trang/chỉ số.");
                return null;
            }

            var draft = character.Clone();
            draft.CharacterHandler = new CharacterHandler(draft, _cache!, _config!);
            string? error = action(draft);
            if (error != null)
            {
                onError(error);
                return null;
            }

            if (!await database.SaveGame(draft))
            {
                Close();
                onError("Dữ liệu đã được thay đổi từ kết nối khác. Hãy vào lại game.");
                return null;
            }

            SetData(draft);
            return character.Clone();
        }

        internal void SetData(GameCharacter value)
        {
            var character = (GameCharacter)Character;
            character.InfoGame = value.InfoGame;
            character.InfoChar = value.InfoChar;
            character.ItemBag = value.ItemBag;
            character.ItemBody = value.ItemBody;
            character.ItemBox = value.ItemBox;
            character.InfoTask = value.InfoTask;
            character.Skills = value.Skills;
            character.RealmId = value.RealmId;
            if (character.InfoGame.Breakthrough.Active)
            {
                character.TargetX = value.Position.X;
                character.TargetY = value.Position.Y;
            }
            character.GameRevision = value.GameRevision;
            character.MapId = value.MapId;
            character.Position.X = value.Position.X;
            character.Position.Y = value.Position.Y;
            character.StateVersion++;
            character.PositionDirty = false;
            character.LastPositionSave = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
