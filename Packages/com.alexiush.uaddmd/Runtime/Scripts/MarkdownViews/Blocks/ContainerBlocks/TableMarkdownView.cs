using Markdig.Extensions.Tables;
using Markdig.Syntax;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Markdown view that processes tables.
/// </summary>
[CreateAssetMenu(fileName = "Table", menuName = "ScriptableObjects/MarkdownView/ContainerBlocks/Table")]
public class TableMarkdownView : MarkdownView<Table>
{
    protected override void RenderTyped(MarkdownContext context, Table mdObject, VisualElement layout, Func<MarkdownObject, IMarkdownView> viewSelector)
    {
        if (mdObject == null)
        {
            return;
        }

        layout.style.borderLeftWidth = 1;
        layout.style.borderRightWidth = 1;
        layout.style.borderTopWidth = 1;
        layout.style.borderBottomWidth = 1;
        layout.style.borderLeftColor = new Color(1f, 1f, 1f, 0.18f);
        layout.style.borderRightColor = new Color(1f, 1f, 1f, 0.18f);
        layout.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
        layout.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);

        int fallbackColumnCount = 0;
        foreach (var rowBlock in mdObject)
        {
            if (rowBlock is TableRow tableRow)
            {
                fallbackColumnCount = Mathf.Max(fallbackColumnCount, CountRenderableColumns(tableRow));
            }
        }

        // Markdig may keep a trailing empty column definition for pipe tables ending with `|`.
        // Drive layout from the actual rendered rows so we do not append a phantom last column.
        int columnCount = fallbackColumnCount > 0
            ? fallbackColumnCount
            : (mdObject.ColumnDefinitions?.Count ?? 0);

        foreach (var rowBlock in mdObject)
        {
            if (rowBlock is not TableRow row)
            {
                continue;
            }

            var rowElement = new VisualElement();
            rowElement.AddToClassList("TableRow");
            rowElement.AddToClassList(row.IsHeader ? "TableHeaderRow" : "TableBodyRow");
            rowElement.style.display = DisplayStyle.Flex;
            rowElement.style.flexDirection = FlexDirection.Row;
            rowElement.style.flexWrap = Wrap.NoWrap;
            rowElement.style.borderBottomWidth = 1;
            rowElement.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);

            int renderableCellCount = CountRenderableCells(row);
            int currentColumn = 0;
            int renderedCellIndex = 0;
            foreach (var cellBlock in row)
            {
                if (cellBlock is not TableCell cell)
                {
                    continue;
                }

                if (!ShouldRenderCell(cell, renderedCellIndex, renderableCellCount))
                {
                    renderedCellIndex++;
                    continue;
                }

                int span = Mathf.Max(1, cell.ColumnSpan);
                var cellElement = new VisualElement();
                cellElement.AddToClassList("TableCell");
                cellElement.AddToClassList(row.IsHeader ? "TableHeaderCell" : "TableBodyCell");
                string alignmentClass = ResolveAlignmentClass(mdObject, currentColumn);
                if (!string.IsNullOrWhiteSpace(alignmentClass))
                {
                    cellElement.AddToClassList(alignmentClass);
                }

                cellElement.style.display = DisplayStyle.Flex;
                cellElement.style.flexDirection = FlexDirection.Column;
                cellElement.style.flexBasis = 0;
                cellElement.style.flexGrow = span;
                cellElement.style.flexShrink = 1;
                cellElement.style.borderRightWidth = currentColumn + span < columnCount ? 1 : 0;
                cellElement.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);

                var cellContent = new VisualElement();
                cellContent.AddToClassList("TableCellContent");
                cellElement.Add(cellContent);

                cellContent.Add(new Label(ExtractCellText(cell)));

                rowElement.Add(cellElement);
                currentColumn += span;
                renderedCellIndex++;
            }

            if (currentColumn < columnCount)
            {
                for (int missing = currentColumn; missing < columnCount; missing++)
                {
                    var emptyCell = new VisualElement();
                    emptyCell.AddToClassList("TableCell");
                    emptyCell.AddToClassList("TableBodyCell");
                    emptyCell.AddToClassList("TableCell--empty");
                    emptyCell.style.flexBasis = 0;
                    emptyCell.style.flexGrow = 1;
                    emptyCell.style.flexShrink = 1;
                    rowElement.Add(emptyCell);
                }
            }

            layout.Add(rowElement);
        }

        if (layout.childCount > 0)
        {
            layout[layout.childCount - 1].style.borderBottomWidth = 0;
        }
    }

    private static string ResolveAlignmentClass(Table table, int columnIndex)
    {
        if (table?.ColumnDefinitions == null || columnIndex < 0 || columnIndex >= table.ColumnDefinitions.Count)
        {
            return null;
        }

        return table.ColumnDefinitions[columnIndex].Alignment switch
        {
            TableColumnAlign.Left => "TableCell--align-left",
            TableColumnAlign.Center => "TableCell--align-center",
            TableColumnAlign.Right => "TableCell--align-right",
            _ => null
        };
    }

    private static string ExtractCellText(TableCell cell)
    {
        if (cell == null)
        {
            return string.Empty;
        }

        string html = cell.ToHtml();
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        string withNewLines = html
            .Replace("<br />", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br>", "\n");
        string plainText = Regex.Replace(withNewLines, "<[^>]*>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(plainText).Trim();
    }

    private static int CountRenderableCells(TableRow row)
    {
        if (row == null)
        {
            return 0;
        }

        int totalCells = row.Count;
        if (totalCells <= 0)
        {
            return 0;
        }

        int renderable = totalCells;
        if (row[totalCells - 1] is TableCell lastCell && string.IsNullOrWhiteSpace(ExtractCellText(lastCell)))
        {
            renderable--;
        }

        return Mathf.Max(renderable, 0);
    }

    private static int CountRenderableColumns(TableRow row)
    {
        if (row == null)
        {
            return 0;
        }

        int renderableCellCount = CountRenderableCells(row);
        if (renderableCellCount <= 0)
        {
            return 0;
        }

        int cellIndex = 0;
        int columns = 0;
        foreach (var cellBlock in row)
        {
            if (cellBlock is not TableCell cell)
            {
                continue;
            }

            if (!ShouldRenderCell(cell, cellIndex, renderableCellCount))
            {
                cellIndex++;
                continue;
            }

            columns += Mathf.Max(1, cell.ColumnSpan);
            cellIndex++;
        }

        return columns;
    }

    private static bool ShouldRenderCell(TableCell cell, int cellIndex, int renderableCellCount)
    {
        if (cell == null)
        {
            return false;
        }

        if (cellIndex < renderableCellCount)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ExtractCellText(cell));
    }
}
