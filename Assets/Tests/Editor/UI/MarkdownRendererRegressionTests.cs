using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace VsensAgent.Tests.Editor.UI
{
    public class MarkdownRendererRegressionTests
    {
        [Test]
        public void MarkdownRenderer_RendersInlineCodeAsDedicatedElement()
        {
            var views = Resources.Load<ViewsCollection>("VsensAgent/ChatMarkdownViews");
            Assert.That(views, Is.Not.Null);

            var root = new VisualElement();
            var renderer = new MarkdownRenderer();
            renderer.Initialize(renderer, views);
            root.Add(renderer);

            renderer.Text = "before `code` after";

            var inlineCode = renderer.Q<Label>(className: "InlineCode");
            Assert.That(inlineCode, Is.Not.Null);
            Assert.That(inlineCode.text, Is.EqualTo("code"));
            Assert.That(inlineCode.enableRichText, Is.False);
        }

        [Test]
        public void MarkdownRenderer_RendersListItemsWithTopAlignedMarker()
        {
            var views = Resources.Load<ViewsCollection>("VsensAgent/ChatMarkdownViews");
            Assert.That(views, Is.Not.Null);

            var renderer = new MarkdownRenderer();
            renderer.Initialize(renderer, views);
            renderer.Text = "1. hello";

            var listItem = renderer.Q<VisualElement>(className: "ListItem");
            Assert.That(listItem, Is.Not.Null);
            Assert.That(listItem.style.alignItems.value, Is.EqualTo(Align.FlexStart));

            var identifier = renderer.Q<Label>(className: "ListIdentifier");
            Assert.That(identifier, Is.Not.Null);
            Assert.That(identifier.text, Is.EqualTo("1."));
        }
    }
}
