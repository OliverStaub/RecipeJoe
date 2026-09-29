using NSubstitute;

namespace RecipeJoe.UnitTests;

[TestClass]
public sealed class SanityTests
{
    [TestMethod]
    public void Sanity_check_the_unit_test_project_runs()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UnixEpoch);

        Assert.AreEqual(DateTimeOffset.UnixEpoch, timeProvider.GetUtcNow());
    }
}
