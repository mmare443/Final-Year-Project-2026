using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly ContactSettings _contact;

    public ContactController(IOptions<ContactSettings> contact)
    {
        _contact = contact.Value;
    }

    [AllowAnonymous]
    [HttpGet]
    public ActionResult<ContactRecord> Get() => Ok(ContactRecord.From(_contact));
}

public class ContactRecord
{
    public string Institution { get; set; } = "";
    public string PostalLine1 { get; set; } = "";
    public string PostalLine2 { get; set; } = "";
    public string PostalLine3 { get; set; } = "";
    public string PostalLine4 { get; set; } = "";
    public string PostalOneLine { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string Fax { get; set; } = "";
    public string PrimaryEmail { get; set; } = "";
    public string ApplicationContact { get; set; } = "";
    public string ApplicationEmail { get; set; } = "";
    public string BankAccountName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";
    public string Bank { get; set; } = "";

    public static ContactRecord From(ContactSettings settings) => new()
    {
        Institution = settings.Institution,
        PostalLine1 = settings.PostalLine1,
        PostalLine2 = settings.PostalLine2,
        PostalLine3 = settings.PostalLine3,
        PostalLine4 = settings.PostalLine4,
        PostalOneLine = settings.PostalOneLine,
        Phone = settings.Phone,
        WhatsApp = settings.WhatsApp,
        Fax = settings.Fax,
        PrimaryEmail = settings.PrimaryEmail,
        ApplicationContact = settings.ApplicationContact,
        ApplicationEmail = settings.ApplicationEmail,
        BankAccountName = settings.BankAccountName,
        BankAccountNumber = settings.BankAccountNumber,
        Bank = settings.Bank,
    };
}
