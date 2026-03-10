using NUnit.Framework;
using VsensAgent.UI;

public class ChatProtocolInputParserTests
{
    [Test]
    public void TryParseSelection_MapsSingleNumberToOptionId()
    {
        var parsed = ChatProtocolInputParser.TryParseSelection(
            "2",
            new[] { "fridge", "coffee_maker" },
            new[] { "Fridge", "Coffee maker" },
            allowMultiple: false,
            out var selectedIds
        );

        Assert.That(parsed, Is.True);
        Assert.That(selectedIds, Is.EqualTo(new[] { "coffee_maker" }));
    }

    [Test]
    public void TryParseSelection_MapsExactLabel()
    {
        var parsed = ChatProtocolInputParser.TryParseSelection(
            "Coffee maker",
            new[] { "fridge", "coffee_maker" },
            new[] { "Fridge", "Coffee maker" },
            allowMultiple: false,
            out var selectedIds
        );

        Assert.That(parsed, Is.True);
        Assert.That(selectedIds, Is.EqualTo(new[] { "coffee_maker" }));
    }

    [Test]
    public void TryParseSelection_RejectsFreeFormNote()
    {
        var parsed = ChatProtocolInputParser.TryParseSelection(
            "Move it slightly left",
            new[] { "cand_1", "cand_2" },
            new[] { "East wall", "North wall" },
            allowMultiple: false,
            out var selectedIds
        );

        Assert.That(parsed, Is.False);
        Assert.That(selectedIds, Is.Empty);
    }
}
