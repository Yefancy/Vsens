using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.UI;

public class ChatInteractionOptionsViewTests
{
    private GameObject _root;
    private ChatInteractionOptionsView _view;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("ChatInteractionOptionsViewRoot", typeof(RectTransform));
        _view = _root.AddComponent<ChatInteractionOptionsView>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void Initialize_RendersOneButtonPerOption_ForSingleSelect()
    {
        _view.Initialize(
            new[]
            {
                new ChatInteractionOptionData("coffee", "Coffee maker", "Near the counter"),
                new ChatInteractionOptionData("sink", "Sink", "Dishwashing area")
            },
            allowMultiple: false,
            onSubmit: _ => { });

        var buttons = _root.GetComponentsInChildren<Button>();

        Assert.That(buttons.Length, Is.EqualTo(2));
        Assert.That(GetButtonLabels(buttons), Is.EqualTo(new[]
        {
            "Coffee maker\nNear the counter",
            "Sink\nDishwashing area"
        }));
    }

    [Test]
    public void SingleSelectButtonClick_SubmitsSelectedOptionImmediately()
    {
        string[] capturedSelection = null;

        _view.Initialize(
            new[]
            {
                new ChatInteractionOptionData("coffee", "Coffee maker"),
                new ChatInteractionOptionData("sink", "Sink")
            },
            allowMultiple: false,
            onSubmit: selectedIds => capturedSelection = selectedIds);

        var buttons = _root.GetComponentsInChildren<Button>();
        buttons[1].onClick.Invoke();

        Assert.That(capturedSelection, Is.EqualTo(new[] { "sink" }));
    }

    [Test]
    public void MultiSelect_ShowsSubmitButton_AndSubmitsAccumulatedSelection()
    {
        string[] capturedSelection = null;

        _view.Initialize(
            new[]
            {
                new ChatInteractionOptionData("coffee", "Coffee maker"),
                new ChatInteractionOptionData("sink", "Sink")
            },
            allowMultiple: true,
            onSubmit: selectedIds => capturedSelection = selectedIds);

        var buttons = _root.GetComponentsInChildren<Button>();
        Assert.That(GetButtonLabels(buttons), Is.EqualTo(new[]
        {
            "Coffee maker",
            "Sink",
            "Submit selection"
        }));

        buttons[0].onClick.Invoke();
        buttons[1].onClick.Invoke();
        buttons[2].onClick.Invoke();

        Assert.That(capturedSelection, Is.EqualTo(new[] { "coffee", "sink" }));
    }

    [Test]
    public void Initialize_ConfiguresOptionButtonForDynamicMultilineHeight()
    {
        _view.Initialize(
            new[]
            {
                new ChatInteractionOptionData(
                    "coffee",
                    "Coffee maker placement",
                    "This option has enough text to wrap onto multiple lines in the chat message UI.")
            },
            allowMultiple: false,
            onSubmit: _ => { });

        var optionButton = _root.GetComponentsInChildren<Button>().First();

        Assert.That(optionButton.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Assert.That(optionButton.GetComponent<ContentSizeFitter>(), Is.Not.Null);
    }

    private static string[] GetButtonLabels(Button[] buttons)
    {
        return buttons
            .Select(button => button.GetComponentInChildren<TextMeshProUGUI>()?.text ?? string.Empty)
            .ToArray();
    }
}
