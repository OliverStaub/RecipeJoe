using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api;

namespace RecipeJoe.ContractTests;

[TestClass]
public sealed class PendingModelChangesTests
{
    [TestMethod]
    public void Model_has_no_pending_changes_against_the_latest_migration()
    {
        var options = new DbContextOptionsBuilder<RecipeJoeDbContext>()
            .UseNpgsql("Host=localhost;Database=contract-tests")
            .Options;

        using var dbContext = new RecipeJoeDbContext(options);

        Assert.IsFalse(dbContext.Database.HasPendingModelChanges());
    }
}
