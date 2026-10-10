namespace LCC_CMS_Api.Services;

public static class AdmissionEmailComposer
{
    public static (string Subject, string Body) Compose(
        string decision,
        string applicantName,
        string programmeName,
        string applicationReference,
        string decisionRemarks)
    {
        var name = string.IsNullOrWhiteSpace(applicantName) ? "Applicant" : applicantName.Trim();
        var programme = string.IsNullOrWhiteSpace(programmeName) ? "the selected" : programmeName.Trim();
        var reference = string.IsNullOrWhiteSpace(applicationReference) ? "—" : applicationReference.Trim();
        var remarks = decisionRemarks?.Trim() ?? "";

        return decision switch
        {
            AdmissionReviewPolicy.Successful => (
                "LCCB Admission Application Successful",
                $"""
                Dear {name},

                We are pleased to inform you that your application for admission to the {programme} programme at Lutheran Church College Banz has been successful.

                You are required to present the original copies of all documents submitted with your application during School Registration Week. The original documents will be checked against the copies provided through the application system.

                Failure to present the required original documents, or any inconsistency between the original documents and the submitted copies, may affect your admission or registration.

                Application Reference: {reference}

                Registrar's Remarks:
                {remarks}

                Please follow the registration instructions provided by the College.

                Regards,
                Office of the Registrar
                Lutheran Church College Banz
                """),
            AdmissionReviewPolicy.PendingOriginals => (
                "LCCB Admission Application Successful, Subject to Original Document Verification",
                $"""
                Dear {name},

                Your application for admission to the {programme} programme at Lutheran Church College Banz has been successful, subject to verification of your original documents.

                The submitted information was sufficient for the Selection Committee to consider your application. However, one or more uploaded document copies require confirmation.

                Original document verification remains outstanding until the originals are inspected during School Registration Week. The uploaded copies have not been proved genuine.

                You must present the original copies of all documents submitted with your application during School Registration Week. The original documents will be compared with the uploaded copies.

                Your admission may be cancelled if:
                - an original document is not presented
                - an original does not match the uploaded copy
                - a document is altered, invalid, or cannot be authenticated
                - the original information is inconsistent with the application

                Application Reference: {reference}

                Registrar's Remarks:
                {remarks}

                Regards,
                Office of the Registrar
                Lutheran Church College Banz
                """),
            AdmissionReviewPolicy.Unsuccessful => (
                "Outcome of Your LCCB Admission Application",
                $"""
                Dear {name},

                Thank you for applying for admission to the {programme} programme at Lutheran Church College Banz.

                After reviewing your application and the documents submitted, the Selection Committee has determined that the application did not provide sufficient acceptable evidence to meet the admission assessment requirements.

                Application Reference: {reference}

                Registrar's Remarks:
                {remarks}

                We appreciate your interest in Lutheran Church College Banz.

                Regards,
                Office of the Registrar
                Lutheran Church College Banz
                """),
            _ => throw new ArgumentException("This decision does not have an applicant email."),
        };
    }
}
