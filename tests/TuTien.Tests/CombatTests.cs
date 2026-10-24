using TuTien.Application.Handlers.Skill;
using TuTien.Model.Info;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Manager;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;

namespace TuTien.Tests
{
    public class CombatTests
    {
        [Theory]
        [InlineData(200, 80, 0, 0, 9999, 120, false, false)]
        [InlineData(200, 80, 100, 0, 9999, 240, true, false)]
        [InlineData(200, 80, 100, 100, 9999, 0, false, true)]
        [InlineData(50, 100, 100, 0, 9999, 0, true, false)]
        [InlineData(200, 80, 10, 0, 999, 240, true, false)]
        [InlineData(200, 80, 10, 0, 1000, 120, false, false)]
        [InlineData(200, 80, 0, 10, 999, 0, false, true)]
        [InlineData(200, 80, 0, 10, 1000, 120, false, false)]
        public void DamageUsesDefenseAndExactPercentageBoundary(long attack, long defense, int crit, int dodge,
            int roll, long expected, bool expectedCrit, bool expectedDodge)
        {
            var source = new InfoChar { DamageFull = attack, CritFull = crit };
            var target = new InfoChar { DefenceFull = defense, DodgeFull = dodge };
            long damage = SkillHandler.GetDamage(source, target, out bool critical, out bool evaded, maximum => roll);
            Assert.Equal(expected, damage);
            Assert.Equal(expectedCrit, critical);
            Assert.Equal(expectedDodge, evaded);
        }

        [Fact]
        public void DodgeStopsBeforeTheCriticalRollAndDamageNeverOverflows()
        {
            int calls = 0;
            var source = new InfoChar { DamageFull = long.MaxValue, CritFull = 100 };
            var target = new InfoChar { DodgeFull = 100 };
            Assert.Equal(0, SkillHandler.GetDamage(source, target, out bool crit, out bool dodge, maximum => { calls++; return 0; }));
            Assert.Equal(1, calls);
            Assert.False(crit);
            Assert.True(dodge);
            target.DodgeFull = 0;
            Assert.Equal(long.MaxValue, SkillHandler.GetDamage(source, target, out _, out _, maximum => 0));
        }

        [Fact]
        public async Task CombatPersistsBothCharactersAndRollsBackWhenEitherRevisionIsStale()
        {
            string folder = Path.Combine(Path.GetTempPath(), "TuTienCombatTests", Guid.NewGuid().ToString("N"));
            try
            {
                var config = new ConfigManager(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:SqliteFile"] = Path.Combine(folder, "game.sqlite"),
                    ["Game:CritPerSatCo"] = "0",
                    ["Game:DodgePerLinhGiac"] = "0",
                    ["Game:AttackCooldown"] = "5000"
                }).Build());
                var db = new DbContextAsync(config, NullLogger<DbContextAsync>.Instance);
                await db.Init();
                var cache = GameplayTests.Catalog();
                var users = new UserDB(db, NullLogger<UserDB>.Instance);
                var characters = new CharacterDB(db, cache, NullLogger<CharacterDB>.Instance, config);
                var clients = new ClientManager(config);
                var map = new TuTien.Application.Threading.Map { Id = 1 };
                var zone = new TuTien.Model.Map.Zone { Id = 1, Map = map };
                map.Zones.Add(zone);
                map.Start();
                var values = new List<TuTien.Model.Character.Character>();
                foreach (string name in new[] { "Attacker", "Victim" })
                {
                    var user = await users.CreateUser(name, "demo-password");
                    var value = await characters.Create(user!.Id, new CharacterCreate { Slot = 1, Name = name, ClassId = 1, Gender = 0, AppearanceId = 101 });
                    value = await characters.Enter(value!.Id, user.Id);
                    value!.TalentSeen = true;
                    value.PlayToken = Guid.NewGuid().ToString("N");
                    value.Zone = zone;
                    ((CharacterHandler)value.CharacterHandler!).InitGame();
                    Assert.True(await characters.SaveGame(value));
                    zone.JoinZone(value);
                    clients.Add(value);
                    values.Add(value);
                }
                var attacker = values[0];
                var target = values[1];
                ((CharacterHandler)attacker.CharacterHandler!).ChangePotential(new[] { 0, 100, 0, 0, 0 }, out _);
                Assert.True(await characters.SaveGame(attacker));
                long hp = target.InfoGame.Hp;
                var skills = new SkillHandler(cache, config, characters, clients);
                var hit = skills.AttackPlayer(attacker, target.Id, attacker.PlayToken);
                await map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                var result = await hit;
                Assert.NotNull(result);
                Assert.True(result!.Damage > 0);
                Assert.Equal(hp - result.Damage, target.InfoGame.Hp);
                Assert.Equal(target.InfoGame.Hp, (await characters.Load(target.Id))!.InfoGame.Hp);
                Assert.Equal(attacker.InfoGame.LastAttackTime, (await characters.Load(attacker.Id))!.InfoGame.LastAttackTime);
                Assert.Single(zone.Attacks);
                var tooSoon = skills.AttackPlayer(attacker, target.Id, attacker.PlayToken);
                await map.UpdateGame(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Assert.Null(await tooSoon);
                var draft = attacker.Clone();
                var stale = target.Clone();
                stale.GameRevision++;
                draft.InfoGame.Hp = 1;
                Assert.False(await characters.SaveCombat(draft, stale));
                Assert.Equal(attacker.InfoGame.Hp, (await characters.Load(attacker.Id))!.InfoGame.Hp);
                Assert.Equal(attacker.GameRevision, (await characters.Load(attacker.Id))!.GameRevision);
                map.Close();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
