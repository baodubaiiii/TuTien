using TuTien.Protocol;
using TuTien.Database;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Main
{
    public static class Service
    {
        public static object Bootstrap(ConfigManager config)
        {
            return new
            {
                mode = "gameplay",
                protocolVersion = 1,
                contentVersion = "base",
                cdnBaseUrl = string.IsNullOrWhiteSpace(config.CdnBaseUrl) ? "/cdn/base" : config.CdnBaseUrl,
                transport = "tcp",
                tcpHost = config.TcpHost,
                tcpPort = config.TcpPort,
                tcpEnabled = config.TcpEnabled,
                webSocketPath = "/ws",
                gameplay = true,
                characterCreation = true,
                reviveDelay = config.ReviveDelay,
                interactionRange = config.InteractionRange,
                gameSocketPath = "/ws/game/{id}",
                worldProtocol = 1
            };
        }

        public static object? CharacterInfo(GameCharacter? character, Cache cache)
        {
            if (character == null)
            {
                return null;
            }

            return new
            {
                character.Id,
                character.Slot,
                character.Name,
                character.ClassId,
                className = cache.ClassTemplates.GetValueOrDefault(character.ClassId)?.Name,
                character.Gender,
                character.AppearanceId,
                appearanceName = cache.AppearanceTemplates.GetValueOrDefault(character.AppearanceId)?.Name,
                character.RaceId,
                character.ElementId,
                race = cache.RaceTemplates.GetValueOrDefault(character.RaceId),
                element = cache.ElementTemplates.GetValueOrDefault(character.ElementId),
                character.TalentSeen,
                character.RealmId,
                character.MapId,
                realm = cache.RealmTemplates.GetValueOrDefault(character.RealmId),
                map = cache.MapTemplates.GetValueOrDefault(character.MapId),
                character.GameRevision,
                character.StateVersion,
                character.ZoneId,
                character.BagCapacity,
                character.MoveSpeed,
                character.PlayToken,
                character.AttackRange,
                character.AttackCooldown,
                character.Skills,
                character.InfoTask,
                position = new { character.Position.X, character.Position.Y },
                game = new
                {
                    character.InfoGame.Version,
                    character.InfoGame.PointBudget,
                    character.InfoGame.Points,
                    character.InfoGame.Hp,
                    character.InfoGame.Mp,
                    character.InfoGame.Effects,
                    character.InfoGame.LastAttackTime,
                    character.InfoGame.TuVi,
                    character.InfoGame.DeathTime,
                    character.InfoGame.LastMapChangeTime,
                    character.InfoGame.SkillSlots,
                    character.InfoGame.Breakthrough,
                    character.ItemBag,
                    character.ItemBody,
                    character.ItemBox
                },
                stats = character.InfoChar
            };
        }

        public static ServerMessage ServerMessage(ulong requestSequence, string message)
        {
            return new ServerMessage
            {
                Result = new CommandResult
                {
                    RequestSequence = requestSequence,
                    Success = false,
                    Code = "base_only",
                    Message = message
                }
            };
        }

        public static ServerMessage Pong(double clientTime)
        {
            return new ServerMessage
            {
                Pong = new Pong
                {
                    ClientTime = clientTime,
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            };
        }
    }
}
