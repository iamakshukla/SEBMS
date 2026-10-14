using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SEBMS.Domain.Enums;
using SEBMS.Web.Models;

namespace SEBMS.Web.Services;

/// <summary>Renders a <see cref="MonthlyReport"/> as an Excel workbook or a PDF.</summary>
public static class MonthlyReportBuilder
{
    private const string Indigo = "#4F46E5";
    private const string Green = "#16A34A";
    private const string Red = "#DC2626";
    private const string Muted = "#6B7385";
    private const string Border = "#E5E8F0";
    private const string Rupee = "\"₹\"#,##0.00";

    static MonthlyReportBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;   // free for individuals and small organisations
    }

    // ---------- Excel ----------

    public static byte[] ToExcel(MonthlyReport r)
    {
        using var wb = new XLWorkbook();

        var s = wb.Worksheets.Add("Summary");
        s.Cell("A1").Value = $"SEBMS monthly report - {r.Title}";
        s.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);
        s.Cell("A2").Value = r.UserName;
        s.Cell("A2").Style.Font.FontColor = XLColor.FromHtml(Muted);

        var row = 4;
        void Pair(string label, decimal value, string? color = null)
        {
            s.Cell(row, 1).Value = label;
            var c = s.Cell(row, 2);
            c.Value = value;
            c.Style.NumberFormat.Format = Rupee;
            if (color is not null) c.Style.Font.FontColor = XLColor.FromHtml(color);
            row++;
        }
        Pair("Income", r.Summary.TotalIncome, Green);
        Pair("Expenses", r.Summary.TotalExpense, Red);
        Pair("Net balance", r.Summary.Net, r.Summary.Net < 0 ? Red : Green);
        s.Cell(row, 1).Value = "Transactions";
        s.Cell(row, 2).Value = r.Summary.TransactionCount;
        row += 2;

        s.Cell(row++, 1).Value = "Spending by category";
        s.Cell(row - 1, 1).Style.Font.SetBold();
        Header(s, row++, "Category", "Total", "Share %");
        foreach (var i in r.Spending.Items)
        {
            s.Cell(row, 1).Value = i.CategoryName;
            s.Cell(row, 2).Value = i.Total;
            s.Cell(row, 2).Style.NumberFormat.Format = Rupee;
            s.Cell(row, 3).Value = i.Percentage;
            s.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            row++;
        }
        if (r.Spending.Items.Count == 0) s.Cell(row++, 1).Value = "No expenses this month.";
        row++;

        s.Cell(row++, 1).Value = "Budgets";
        s.Cell(row - 1, 1).Style.Font.SetBold();
        Header(s, row++, "Category", "Budget", "Spent", "Remaining");
        foreach (var b in r.Budgets)
        {
            s.Cell(row, 1).Value = b.CategoryName;
            s.Cell(row, 2).Value = b.Amount;
            s.Cell(row, 3).Value = b.Spent;
            s.Cell(row, 4).Value = b.Remaining;
            s.Range(row, 2, row, 4).Style.NumberFormat.Format = Rupee;
            if (b.Remaining < 0) s.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml(Red);
            row++;
        }
        if (r.Budgets.Count == 0) s.Cell(row, 1).Value = "No budgets set for this month.";
        s.Column(1).Width = 28;   // not auto-fit: the long title in A1 would make it far too wide
        s.Columns(2, 4).Width = 16;

        var t = wb.Worksheets.Add("Transactions");
        Header(t, 1, "Date", "Type", "Category", "Amount", "Currency", "Note");
        var tr = 2;
        foreach (var x in r.Transactions)
        {
            t.Cell(tr, 1).Value = x.TransactionDate.ToDateTime(TimeOnly.MinValue);
            t.Cell(tr, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            t.Cell(tr, 2).Value = x.Type.ToString();
            t.Cell(tr, 3).Value = x.CategoryName;
            t.Cell(tr, 4).Value = x.Amount;
            t.Cell(tr, 4).Style.NumberFormat.Format = "#,##0.00";
            t.Cell(tr, 4).Style.Font.FontColor = XLColor.FromHtml(x.Type == TransactionType.Income ? Green : Red);
            t.Cell(tr, 5).Value = x.CurrencyCode;
            t.Cell(tr, 6).Value = x.Note;
            tr++;
        }
        t.SheetView.FreezeRows(1);
        t.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Header(IXLWorksheet ws, int row, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++) ws.Cell(row, i + 1).Value = titles[i];
        var range = ws.Range(row, 1, row, titles.Length);
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(Indigo);
    }

    // ---------- PDF ----------

    public static byte[] ToPdf(MonthlyReport r) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(x => x.FontSize(10));

        page.Header().PaddingBottom(10).Column(h =>
        {
            h.Item().Text("SEBMS").FontSize(10).FontColor(Indigo).Bold();
            h.Item().Text($"Monthly report - {r.Title}").FontSize(20).Bold();
            h.Item().Text(r.UserName).FontColor(Muted);
        });

        page.Content().Column(col =>
        {
            col.Spacing(14);

            col.Item().Row(row =>
            {
                row.Spacing(10);
                Stat(row.RelativeItem(), "Income", Money.Format(r.Summary.TotalIncome), Green);
                Stat(row.RelativeItem(), "Expenses", Money.Format(r.Summary.TotalExpense), Red);
                Stat(row.RelativeItem(), "Net balance", Money.Format(r.Summary.Net), r.Summary.Net < 0 ? Red : Green);
            });

            col.Item().Text("Spending by category").FontSize(13).Bold();
            if (r.Spending.Items.Count == 0)
                col.Item().Text("No expenses this month.").FontColor(Muted);
            else
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(1); });
                    HeaderRow(t, 1, "Category", "Total", "Share");
                    foreach (var i in r.Spending.Items)
                    {
                        Cell(t, i.CategoryName);
                        Cell(t, Money.Format(i.Total), right: true);
                        Cell(t, $"{i.Percentage:0.#}%", right: true);
                    }
                });

            col.Item().Text("Budgets").FontSize(13).Bold();
            if (r.Budgets.Count == 0)
                col.Item().Text("No budgets set for this month.").FontColor(Muted);
            else
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                    HeaderRow(t, 1, "Category", "Budget", "Spent", "Remaining");
                    foreach (var b in r.Budgets)
                    {
                        Cell(t, b.CategoryName ?? "");
                        Cell(t, Money.Format(b.Amount), right: true);
                        Cell(t, Money.Format(b.Spent), right: true);
                        Cell(t, Money.Format(b.Remaining), right: true, color: b.Remaining < 0 ? Red : null);
                    }
                });

            col.Item().Text($"Transactions ({r.Transactions.Count})").FontSize(13).Bold();
            if (r.Transactions.Count == 0)
                col.Item().Text("No transactions this month.").FontColor(Muted);
            else
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.ConstantColumn(74); c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(2); });
                    HeaderRow(t, 3, "Date", "Category", "Note", "Amount");
                    foreach (var x in r.Transactions)
                    {
                        var income = x.Type == TransactionType.Income;
                        Cell(t, x.TransactionDate.ToString("yyyy-MM-dd"));
                        Cell(t, x.CategoryName ?? "");
                        Cell(t, x.Note ?? "");
                        Cell(t, (income ? "+" : "-") + Money.Format(x.Amount), right: true, color: income ? Green : Red);
                    }
                });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
            t.Span($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} - page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    })).GeneratePdf();

    private static void Stat(IContainer c, string label, string value, string color) =>
        c.Border(1).BorderColor(Border).Padding(10).Column(x =>
        {
            x.Item().Text(label.ToUpperInvariant()).FontSize(8).FontColor(Muted);
            x.Item().Text(value).FontSize(15).Bold().FontColor(color);
        });

    /// <summary>Header row; columns from <paramref name="rightFrom"/> onwards are right-aligned (the numeric ones).</summary>
    private static void HeaderRow(TableDescriptor t, int rightFrom, params string[] titles) =>
        t.Header(h =>
        {
            for (var i = 0; i < titles.Length; i++)
            {
                var right = i >= rightFrom;
                var cell = h.Cell().Background(Indigo).PaddingVertical(4).PaddingHorizontal(6);
                (right ? cell.AlignRight() : cell).Text(titles[i]).FontColor(Colors.White).Bold();
            }
        });

    private static void Cell(TableDescriptor t, string text, bool right = false, string? color = null)
    {
        var cell = t.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(6);
        var content = right ? cell.AlignRight() : cell;
        var span = content.Text(text);
        if (color is not null) span.FontColor(color);
    }
}
