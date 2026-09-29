using System.Net;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

[TestClass]
public sealed class IpClassifierTests
{
    [TestMethod]
    [DataRow("8.8.8.8")]
    [DataRow("93.184.216.34")]
    [DataRow("172.15.255.255")]
    [DataRow("172.32.0.0")]
    [DataRow("100.63.255.255")]
    [DataRow("100.128.0.0")]
    [DataRow("2606:4700:4700::1111")]
    [DataRow("::ffff:8.8.8.8")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.IsTrue(IpClassifier.IsPublic(IPAddress.Parse(address)));

    [TestMethod]
    [DataRow("0.0.0.0")]
    [DataRow("127.0.0.1")]
    [DataRow("127.255.255.254")]
    [DataRow("10.0.0.1")]
    [DataRow("172.16.0.1")]
    [DataRow("172.31.255.255")]
    [DataRow("192.168.1.1")]
    [DataRow("169.254.169.254")]
    [DataRow("100.64.0.1")]
    [DataRow("100.127.255.255")]
    [DataRow("224.0.0.1")]
    [DataRow("239.255.255.255")]
    [DataRow("255.255.255.255")]
    [DataRow("::")]
    [DataRow("::1")]
    [DataRow("fc00::1")]
    [DataRow("fd12:3456::1")]
    [DataRow("fe80::1")]
    [DataRow("ff02::1")]
    [DataRow("fec0::1")]
    [DataRow("64:ff9b::7f00:1")]
    [DataRow("2002:7f00:1::1")]
    [DataRow("::ffff:127.0.0.1")]
    [DataRow("::ffff:169.254.169.254")]
    public void Non_public_addresses_are_rejected(string address) =>
        Assert.IsFalse(IpClassifier.IsPublic(IPAddress.Parse(address)));
}
