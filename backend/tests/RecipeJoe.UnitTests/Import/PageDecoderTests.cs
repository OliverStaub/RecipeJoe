using System.Text;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

[TestClass]
public sealed class PageDecoderTests
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    [TestMethod]
    public void Uses_the_charset_from_the_header()
    {
        var bytes = Latin1.GetBytes("<p>Käse</p>");

        Assert.AreEqual("<p>Käse</p>", PageDecoder.Decode(new FetchedContent(bytes, "text/html; charset=iso-8859-1")));
    }

    [TestMethod]
    public void Header_wins_over_the_meta_tag()
    {
        var bytes = Latin1.GetBytes("<meta charset=\"utf-8\"><p>Käse</p>");

        StringAssert.Contains(
            PageDecoder.Decode(new FetchedContent(bytes, "text/html; charset=ISO-8859-1")),
            "Käse"
        );
    }

    [TestMethod]
    [DataRow("<meta charset=\"iso-8859-1\">")]
    [DataRow("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=ISO-8859-1\">")]
    public void Falls_back_to_the_meta_tag(string meta)
    {
        var bytes = Latin1.GetBytes($"<html><head>{meta}</head><p>Käse</p>");

        StringAssert.Contains(PageDecoder.Decode(new FetchedContent(bytes, "text/html")), "Käse");
    }

    [TestMethod]
    public void Defaults_to_utf8()
    {
        var bytes = Encoding.UTF8.GetBytes("<p>Käse</p>");

        Assert.AreEqual("<p>Käse</p>", PageDecoder.Decode(new FetchedContent(bytes, null)));
    }

    [TestMethod]
    public void Unknown_charset_falls_back_to_utf8()
    {
        var bytes = Encoding.UTF8.GetBytes("<p>Käse</p>");

        Assert.AreEqual("<p>Käse</p>", PageDecoder.Decode(new FetchedContent(bytes, "text/html; charset=nonsense")));
    }
}
