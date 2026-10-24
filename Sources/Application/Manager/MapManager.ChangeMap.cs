using TuTien.Application.Handlers.Character;
using TuTien.Model.ModelBase;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Manager
{
    public partial class MapManager
    {
        public async Task<GameCharacter?> ChangeMap(int playerId, int characterId, int sessionId, string playToken,
            int mapId, int zoneId, bool revive, Action<string> onError, CancellationToken cancellationToken)
        {
            if (!IsWorldReady || _characters == null)
            {
                onError("Máy chủ chưa sẵn sàng.");
                return null;
            }
            var gate = _joinGates[(int)((uint)playerId % _joinGates.Length)];
            await gate.WaitAsync(cancellationToken);
            GameCharacter? source = null;
            GameZone? destination = null;
            int sourceMap = 0;
            bool detached = false;
            try
            {
                if (!_players.TryGetValue(playerId, out var active) || active.CharacterId != characterId || active.SessionId != sessionId)
                {
                    onError("Phiên chơi không hợp lệ.");
                    return null;
                }
                sourceMap = active.MapId;
                if (revive)
                {
                    mapId = 1;
                    zoneId = 0;
                }
                if (!_cache.MapTemplates.TryGetValue(mapId, out var targetTemplate) || zoneId < 0 || zoneId > targetTemplate.ZoneNumber)
                {
                    onError("Bản đồ hoặc khu vực không hợp lệ.");
                    return null;
                }
                source = await Run<GameCharacter>(sourceMap, map =>
                {
                    var character = GetCharacter(map, playerId, characterId, sessionId);
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (character == null || character.IsChangingMap || character.InfoGame.Breakthrough.Active || !character.TalentSeen || string.IsNullOrEmpty(playToken) || character.PlayToken != playToken ||
                        ((CharacterHandler)character.CharacterHandler!).IsClosed || now - character.InfoGame.LastMapChangeTime < _config.MapChangeCooldown)
                    {
                        onError("Nhân vật chưa sẵn sàng chuyển khu vực.");
                        return Task.FromResult<GameCharacter?>(null);
                    }
                    if (revive)
                    {
                        if (character.InfoGame.Hp > 0 || now - character.InfoGame.DeathTime < _config.ReviveDelay)
                        {
                            onError("Chưa thể hồi sinh lúc này.");
                            return Task.FromResult<GameCharacter?>(null);
                        }
                    }
                    else
                    {
                        if (character.InfoGame.Hp <= 0 || _cache.RealmTemplates[character.RealmId].Rank < _cache.RealmTemplates[targetTemplate.RealmId].Rank)
                        {
                            onError("Nhân vật đã chết hoặc cảnh giới chưa đủ.");
                            return Task.FromResult<GameCharacter?>(null);
                        }
                        if (mapId == sourceMap)
                        {
                            if (zoneId == 0 || zoneId == character.ZoneId)
                            {
                                onError("Đạo hữu đang ở khu vực này.");
                                return Task.FromResult<GameCharacter?>(null);
                            }
                        }
                        else
                        {
                            var point = _cache.MapTemplates[sourceMap].Waypoint.Find(value => value.MapId == mapId);
                            if (point == null || Distance(character.Position.X, character.Position.Y, point.X, point.Y) > _config.InteractionRange)
                            {
                                onError("Hãy đến gần lối ra bản đồ.");
                                return Task.FromResult<GameCharacter?>(null);
                            }
                        }
                    }
                    character.IsChangingMap = true;
                    character.TargetX = character.Position.X;
                    character.TargetY = character.Position.Y;
                    return Task.FromResult<GameCharacter?>(character);
                }, cancellationToken);
                if (source == null)
                {
                    return null;
                }
                destination = await Run<GameZone>(mapId, map =>
                {
                    var zone = map.Zones.OfType<GameZone>().FirstOrDefault(value => (zoneId == 0 || value.Id == zoneId) &&
                        (revive || mapId != sourceMap || value.Id != source.ZoneId) &&
                        value.Characters.Count + value.Reservations.Count - (ReferenceEquals(value, source.Zone) ? 1 : 0) < targetTemplate.MaxPlayer);
                    if (zone != null)
                    {
                        zone.Reservations.Add(characterId);
                    }
                    return Task.FromResult(zone);
                }, cancellationToken);
                if (destination == null)
                {
                    onError("Khu vực đã đầy hoặc chưa sẵn sàng.");
                    return null;
                }
                var draft = source.Clone();
                draft.MapId = mapId;
                draft.ZoneId = destination.Id;
                draft.InfoGame.LastMapChangeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (mapId != sourceMap || revive)
                {
                    draft.Position = new Position { X = targetTemplate.SpawnX, Y = targetTemplate.SpawnY };
                }
                if (revive)
                {
                    draft.CharacterHandler = new CharacterHandler(draft, _cache, _config);
                    ((CharacterHandler)draft.CharacterHandler).Update(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    draft.CharacterHandler.SetUpInfo();
                    draft.InfoGame.Hp = draft.InfoChar.HpFull;
                    draft.InfoGame.Mp = draft.InfoChar.MpFull;
                    draft.InfoGame.DeathTime = 0;
                    draft.CharacterHandler.SetUpInfo();
                }
                if (!CanStand(mapId, draft.Position.X, draft.Position.Y))
                {
                    onError("Vị trí đến không hợp lệ.");
                    return null;
                }
                var saved = await Run<GameCharacter>(sourceMap, async map =>
                {
                    if (!await _characters.SaveGame(draft))
                    {
                        source.CharacterHandler!.Close();
                        onError("Dữ liệu đã thay đổi. Hãy vào lại game.");
                        return null;
                    }
                    ((GameZone)source.Zone!).OutZone(source.Id);
                    _clients.RemoveCharacter(source);
                    ((ICollection<KeyValuePair<int, (int MapId, int CharacterId, int SessionId)>>)_players)
                        .Remove(new KeyValuePair<int, (int, int, int)>(playerId, active));
                    return draft;
                }, waitForSpace: true);
                if (saved == null)
                {
                    return null;
                }
                detached = true;
                return await Run<GameCharacter>(mapId, map =>
                {
                    ((CharacterHandler)source.CharacterHandler!).SetData(saved);
                    source.Zone = destination;
                    source.ZoneId = destination.Id;
                    source.TargetX = source.Position.X;
                    source.TargetY = source.Position.Y;
                    source.PlayToken = Guid.NewGuid().ToString("N");
                    source.IsChangingMap = false;
                    if (!destination.JoinZone(source))
                    {
                        source.CharacterHandler!.Close();
                        return Task.FromResult<GameCharacter?>(null);
                    }
                    if (!_clients.Add(source))
                    {
                        destination.OutZone(source.Id);
                        source.CharacterHandler!.Close();
                        return Task.FromResult<GameCharacter?>(null);
                    }
                    _players[playerId] = (mapId, source.Id, sessionId);
                    return Task.FromResult<GameCharacter?>(source);
                }, waitForSpace: true);
            }
            finally
            {
                try
                {
                    if (destination != null)
                    {
                        await Run<GameZone>(mapId, map =>
                        {
                            destination.Reservations.Remove(characterId);
                            return Task.FromResult<GameZone?>(destination);
                        }, waitForSpace: true);
                    }
                    if (source != null && source.IsChangingMap)
                    {
                        if (detached)
                        {
                            source.CharacterHandler!.Close();
                        }
                        else
                        {
                            await Run<GameCharacter>(sourceMap, map =>
                            {
                                source.IsChangingMap = false;
                                return Task.FromResult<GameCharacter?>(source);
                            }, waitForSpace: true);
                        }
                    }
                }
                finally
                {
                    gate.Release();
                }
            }
        }

        public Task<object?> GetZones(int playerId, int characterId, int sessionId, CancellationToken cancellationToken)
        {
            if (!_players.TryGetValue(playerId, out var active) || active.CharacterId != characterId || active.SessionId != sessionId)
            {
                return Task.FromResult<object?>(null);
            }
            return Run<object>(active.MapId, map => Task.FromResult<object?>(new
            {
                mapId = map.Id,
                zones = map.Zones.OfType<GameZone>().Select(zone => new
                {
                    zone.Id,
                    count = zone.Characters.Count,
                    capacity = _cache.MapTemplates[map.Id].MaxPlayer
                }).ToArray()
            }), cancellationToken);
        }

        private static float Distance(float x, float y, float otherX, float otherY)
        {
            return MathF.Sqrt((x - otherX) * (x - otherX) + (y - otherY) * (y - otherY));
        }
    }
}
