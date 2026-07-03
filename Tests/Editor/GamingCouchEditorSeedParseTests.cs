using NUnit.Framework;

public sealed class GamingCouchEditorSeedParseTests
{
    [Test]
    public void TryParseSeedAcceptsPlainInteger()
    {
        var parsed = GCDevJsonLocalPlaySessionProvider.TryParseSeed("12345", out var value);

        Assert.That(parsed, Is.True);
        Assert.That(value, Is.EqualTo(12345));
    }

    [Test]
    public void TryParseSeedRejectsThousandsSeparator()
    {
        var parsed = GCDevJsonLocalPlaySessionProvider.TryParseSeed("1,000", out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TryParseSeedRejectsSurroundingWhitespace()
    {
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed(" 5", out _), Is.False);
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("5 ", out _), Is.False);
    }

    [Test]
    public void TryParseSeedRejectsLeadingSign()
    {
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("+5", out _), Is.False);
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("-5", out _), Is.False);
    }
}
