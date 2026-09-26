using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OMM.Public.Data;
using OMM.Public.Data.Entities;

namespace OMM.Integration.Tests;

public class PublicMemberDataModelTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        return context.Model;
    }

    public static TheoryData<Type> MemberEntityTypes => new()
    {
        typeof(MinerProfileEntity),
        typeof(MineEntity),
        typeof(MinePositionEntity),
        typeof(BurdenEntity),
        typeof(IncomeRecordEntity),
        typeof(ExpenseEntity),
        typeof(GoalEntity),
        typeof(GoalMineEntity),
        typeof(NotificationEntity),
    };

    [Fact]
    public void Model_contains_currency_master_entity()
    {
        var model = CreateModel();
        Assert.NotNull(model.FindEntityType(typeof(Currency)));
    }

    [Theory]
    [MemberData(nameof(MemberEntityTypes))]
    public void Model_contains_required_member_entities(Type entityType)
    {
        var model = CreateModel();
        Assert.NotNull(model.FindEntityType(entityType));
    }

    [Theory]
    [InlineData(typeof(MinerProfileEntity))]
    [InlineData(typeof(MineEntity))]
    [InlineData(typeof(MinePositionEntity))]
    [InlineData(typeof(BurdenEntity))]
    [InlineData(typeof(IncomeRecordEntity))]
    [InlineData(typeof(ExpenseEntity))]
    [InlineData(typeof(GoalEntity))]
    [InlineData(typeof(NotificationEntity))]
    public void User_owned_entities_have_required_user_id(Type entityType)
    {
        var entity = CreateModel().FindEntityType(entityType)!;
        var userId = entity.FindProperty(nameof(UserOwnedEntity.UserId));
        Assert.NotNull(userId);
        Assert.False(userId.IsNullable);
    }

    [Theory]
    [InlineData(typeof(MinerProfileEntity))]
    [InlineData(typeof(MineEntity))]
    [InlineData(typeof(BurdenEntity))]
    [InlineData(typeof(IncomeRecordEntity))]
    [InlineData(typeof(ExpenseEntity))]
    [InlineData(typeof(GoalEntity))]
    [InlineData(typeof(NotificationEntity))]
    public void User_owned_entities_have_currency_or_profile_currency_fk(Type entityType)
    {
        var entity = CreateModel().FindEntityType(entityType)!;
        if (entityType == typeof(MinerProfileEntity))
        {
            Assert.NotNull(entity.FindProperty(nameof(MinerProfileEntity.CurrencyId)));
            return;
        }

        Assert.NotNull(entity.FindProperty("CurrencyId"));
    }

    [Theory]
    [InlineData(typeof(MineEntity))]
    [InlineData(typeof(BurdenEntity))]
    [InlineData(typeof(IncomeRecordEntity))]
    [InlineData(typeof(ExpenseEntity))]
    [InlineData(typeof(GoalEntity))]
    [InlineData(typeof(NotificationEntity))]
    public void User_owned_entities_have_user_id_and_id_ownership_index(Type entityType)
    {
        var entity = CreateModel().FindEntityType(entityType)!;
        var index = entity.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 2
                && i.Properties.Any(p => p.Name == nameof(UserOwnedEntity.UserId))
                && i.Properties.Any(p => p.Name == "Id")
                && i.IsUnique);

        Assert.NotNull(index);
    }

    [Fact]
    public void Mine_position_relationship_points_to_mine()
    {
        var entity = CreateModel().FindEntityType(typeof(MinePositionEntity))!;
        var fk = entity.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(MineEntity));
        Assert.Equal(nameof(MinePositionEntity.MineId), fk.Properties.Single().Name);
    }

    [Fact]
    public void Goal_mine_relationships_point_to_goal_and_mine()
    {
        var entity = CreateModel().FindEntityType(typeof(GoalMineEntity))!;
        var principalTypes = entity.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToHashSet();
        Assert.Contains(typeof(GoalEntity), principalTypes);
        Assert.Contains(typeof(MineEntity), principalTypes);
    }

    [Theory]
    [InlineData(typeof(MineEntity))]
    [InlineData(typeof(MinePositionEntity))]
    [InlineData(typeof(BurdenEntity))]
    [InlineData(typeof(IncomeRecordEntity))]
    [InlineData(typeof(ExpenseEntity))]
    [InlineData(typeof(GoalEntity))]
    [InlineData(typeof(GoalMineEntity))]
    [InlineData(typeof(NotificationEntity))]
    public void Member_entities_use_soft_delete_query_filter(Type entityType)
    {
        var entity = CreateModel().FindEntityType(entityType)!;
        Assert.NotNull(entity.GetQueryFilter());
    }

    [Fact]
    public void Member_entities_do_not_map_to_admin_identity_user()
    {
        var model = CreateModel();
        var memberEntities = model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(MinerProfileEntity).Namespace)
            .ToList();

        Assert.NotEmpty(memberEntities);

        foreach (var entity in memberEntities)
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                Assert.NotEqual("OMM.Admin.Data.ApplicationUser", fk.PrincipalEntityType.ClrType.FullName);
            }
        }
    }

    [Fact]
    public void User_owned_entities_foreign_key_to_public_application_user()
    {
        var model = CreateModel();
        var mine = model.FindEntityType(typeof(MineEntity))!;
        var userFk = mine.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.Equal(nameof(UserOwnedEntity.UserId), userFk.Properties.Single().Name);
    }
}
