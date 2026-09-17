namespace LCC_CMS_Api.Services;

/// <summary>
/// Official Lutheran Church College Banz contact record.
/// Bound from the <c>Contact</c> section in appsettings.json.
/// </summary>
public sealed class ContactSettings
{
    public const string SectionName = "Contact";

    public string Institution { get; set; } = "Lutheran Church College Banz";

    public string PostalLine1 { get; set; } = "P.O. Box 72";

    public string PostalLine2 { get; set; } = "Mt. Hagen";

    public string PostalLine3 { get; set; } = "Western Highlands Province";

    public string PostalLine4 { get; set; } = "Papua New Guinea";

    public string Phone { get; set; } = "(675) 74017162";

    public string WhatsApp { get; set; } = "74017162";

    public string Fax { get; set; } = "(675) 546 2212";

    public string PrimaryEmail { get; set; } = "luthcol.banz@gmail.com";

    public string ApplicationContact { get; set; } = "Dean of Studies";

    public string ApplicationEmail { get; set; } = "sam.walep2021313008@gmail.com";

    public string BankAccountName { get; set; } = "Lutheran Church College";

    public string BankAccountNumber { get; set; } = "1000873958";

    public string Bank { get; set; } = "BSP Mt. Hagen";

    public string PostalOneLine =>
        string.Join(", ", new[] { PostalLine1, PostalLine2, PostalLine3, PostalLine4 }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
