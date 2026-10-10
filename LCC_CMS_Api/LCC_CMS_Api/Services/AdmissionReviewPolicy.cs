namespace LCC_CMS_Api.Services;

public static class AdmissionReviewPolicy
{
    public const string Acceptable = "Acceptable";
    public const string Unclear = "UnclearButProvisionallyAcceptable";
    public const string DoesNotMeet = "DoesNotMeetStandard";
    public const string Missing = "Missing";

    public const string Successful = "ApplicationSuccessful";
    public const string PendingOriginals = "ApplicationSuccessfulPendingOriginalVerification";
    public const string Unsuccessful = "ApplicationUnsuccessful";
    public const string PendingCommittee = "PendingCommitteeReview";

    public static readonly string[] Outcomes = { Acceptable, Unclear, DoesNotMeet, Missing };
    public static readonly string[] Decisions = { Successful, PendingOriginals, Unsuccessful, PendingCommittee };

    public static bool IsOutcome(string? value) =>
        Outcomes.Contains(value ?? "", StringComparer.Ordinal);

    public static bool IsDecision(string? value) =>
        Decisions.Contains(value ?? "", StringComparer.Ordinal);

    public static bool CanEmail(string? decision) =>
        decision is Successful or PendingOriginals or Unsuccessful;

    public static bool CanProceedToApproval(string? decision) =>
        decision is Successful or PendingOriginals;

    public static IReadOnlyList<string> ConsistencyWarnings(
        string? status,
        string? selectionDecision,
        bool hasStudent,
        string? decisionRemark)
    {
        var warnings = new List<string>();
        var approved = string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);
        if (approved && selectionDecision == Unsuccessful)
        {
            warnings.Add("This admission is Approved, but the selection decision is unsuccessful.");
        }
        if (hasStudent && !approved)
        {
            warnings.Add("A student record exists, but the admission is not Approved.");
        }
        if (CanProceedToApproval(selectionDecision) && string.IsNullOrWhiteSpace(decisionRemark))
        {
            warnings.Add("The selection is successful, but there is no final decision remark.");
        }
        return warnings;
    }

    public static string DecisionLabel(string? decision) => decision switch
    {
        Successful => "Application Successful",
        PendingOriginals => "Application Successful, Subject to Original Document Verification",
        Unsuccessful => "Application Unsuccessful",
        PendingCommittee => "Pending Committee Review",
        _ => decision ?? "",
    };

    public static string OutcomeLabel(string? outcome) => outcome switch
    {
        Acceptable => "Acceptable for application assessment",
        Unclear => "Unclear, provisionally acceptable",
        DoesNotMeet => "Does not meet standard",
        Missing => "Missing",
        _ => "Not reviewed",
    };

    public static string DefaultRemark(string? decision) => decision switch
    {
        Successful =>
            "Your application has been successful. You are required to present the original copies of all documents submitted during School Registration Week.",
        PendingOriginals =>
            "Your application has been successful subject to verification of the original documents. Some uploaded copies require confirmation. You must present the original copies of all documents submitted during School Registration Week.",
        Unsuccessful =>
            "Your application was unsuccessful because the submitted documents did not provide sufficient clear and complete evidence for admission assessment.",
        _ => "",
    };

    public static bool DocumentAddressed(DateTime? openedAt, string? outcome) =>
        openedAt is not null || IsOutcome(outcome);

    public static IReadOnlyList<string> Warnings(
        IEnumerable<(string? Outcome, DateTime? OpenedAt)> documents)
    {
        var rows = documents.ToList();
        var warnings = new List<string>();
        var reviewed = rows.Count(row => IsOutcome(row.Outcome));
        var missing = rows.Count(row => row.Outcome == Missing);
        var below = rows.Count(row => row.Outcome == DoesNotMeet);
        var unclear = rows.Count(row => row.Outcome == Unclear);
        if (missing == 1) warnings.Add("1 required document is missing.");
        else if (missing > 1) warnings.Add($"{missing} required documents are missing.");
        if (below == 1) warnings.Add("1 document does not meet the required standard.");
        else if (below > 1) warnings.Add($"{below} documents do not meet the required standard.");
        if (unclear == 1) warnings.Add("1 document is unclear but may be checked against the original.");
        else if (unclear > 1) warnings.Add($"{unclear} documents are unclear but may be checked against the originals.");
        if (rows.Count > 0)
        {
            warnings.Add($"{reviewed} of {rows.Count} documents have been reviewed.");
        }
        return warnings;
    }
}
