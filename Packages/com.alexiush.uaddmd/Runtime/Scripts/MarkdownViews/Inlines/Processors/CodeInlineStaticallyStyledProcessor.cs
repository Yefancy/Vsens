using Markdig.Syntax.Inlines;
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// InlineProcessor for CodeInline with stylesheet independent styling
/// </summary>
[CreateAssetMenu(fileName = "CodeInlineStaticallyStyledProcessor", menuName = "ScriptableObjects/MarkdownView/InlineProcessors/CodeInlineStaticallyStyledProcessor")]
public class CodeInlineStaticallyStyledProcessor : GenericLeafInlineProcessor<CodeInline>
{
    public override void ProcessTyped(InlineRenderingState renderingState, Func<Inline, InlineProcessor> processorSelector, CodeInline inline)
    {
        renderingState.PushDraft();
        renderingState.Draft.ResetText();

        var codeLabel = new Label(inline?.Content ?? string.Empty)
        {
            enableRichText = false
        };
        codeLabel.AddToClassList("InlineCode");
        renderingState.RenderedElements.Add(codeLabel);
    }
}
