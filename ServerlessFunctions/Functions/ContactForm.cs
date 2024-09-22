using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Communication.Email;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ServerlessFunctions.Models.Entities;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace ServerlessFunctions
{

    public class ContactFormBody
    {
        [JsonPropertyName("firstName")]
        public required string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("topic")]
        public required string Topic { get; set; }

        [JsonPropertyName("subject")]
        public required string Subject { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }

        [JsonPropertyName("altcha")]
        public required string Altcha { get; set; }

        public string FormattedTopic
        {
            get
            {
                var temp = Topic.Replace("-", " ");

                TextInfo myTI = new CultureInfo("en-GB", false).TextInfo;
                return myTI.ToTitleCase(temp);
            }
        }
    }

    public class ContactFormResponse
    {
        [JsonPropertyName("success")]
        public required bool Success { get; set; }
    }

    public class ContactForm
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("1stchertseyscoutgroupacs_COMMUNICATIONSERVICES") ?? string.Empty;
        private static readonly string Sender = Environment.GetEnvironmentVariable("Email_Sender") ?? string.Empty;
        private static readonly string BCCRecipient = Environment.GetEnvironmentVariable("Email_BCCRecipient") ?? string.Empty;

        private static readonly string AltchaUrl = Environment.GetEnvironmentVariable("Altcha_Url") ?? string.Empty;
        private static readonly string AltchaApiKey = Environment.GetEnvironmentVariable("Altcha_ApiKey") ?? string.Empty;

        private readonly ILogger<ContactForm> _logger;
        private readonly HttpClient _client;

        public ContactForm(ILogger<ContactForm> logger, HttpClient client)
        {
            _logger = logger;
            _client = client;
        }

        [Function(nameof(ContactForm))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [FromBody] ContactFormBody formBody,
            [TableInput(Constants.CosmosTable.RecipientsTable, Connection = Constants.CosmosTable.Connection)]
        TableClient recipientsClient)
        {
            if (ConnectionString == string.Empty)
            {
                throw new Exception("Environment variable 'derbinsacs_COMMUNICATIONSERVICES' is empty");
            }

            if (Sender == string.Empty)
            {
                throw new Exception("Environment variable 'Sender' is empty");
            }

            if (BCCRecipient == string.Empty)
            {
                throw new Exception("Environment variable 'BCCRecipient' is empty");
            }

            if (AltchaUrl == string.Empty)
            {
                throw new Exception("Environment variable 'AltchaUrl' is empty");
            }

            if (AltchaApiKey == string.Empty)
            {
                throw new Exception("Environment variable 'AltchaApiKey' is empty");
            }

            if (!IsValidAltcha(formBody.Altcha))
            {
                return new OkObjectResult(new ContactFormResponse() { Success = false });
            }


            string[]? recipients = GetRecipients(recipientsClient, formBody.Topic);
            if (recipients == null || recipients.Length == 0)
            {
                return new OkObjectResult(new ContactFormResponse() { Success = false });
            }

            var subject = $"Website Enquiry: {formBody.FormattedTopic} - {formBody.FirstName} {formBody.LastName}";
            var body = BuildEmailHtml(formBody);
            var emailMessage = BuildEmail(subject, body, recipients, formBody.Email);

            try
            {
                EmailClient emailClient = new EmailClient(ConnectionString);
                await emailClient.SendAsync(WaitUntil.Started, emailMessage);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Email send operation failed with error code: {ex.ErrorCode}, message: {ex.Message}");
                return new OkObjectResult(new ContactFormResponse() { Success = false });
            }


            return new OkObjectResult(new ContactFormResponse() { Success = true });
        }


        private bool IsValidAltcha(string altcha)
        {
            return true;
        }

        private string[]? GetRecipients(TableClient recipientsClient, string topic)
        {
            var recipient = recipientsClient.Query<RecipientEntity>($"PartitionKey eq '{topic}'").FirstOrDefault();
            if (recipient == null)
            {
                return null;
            }

            return recipient.Emails.Split(",");
        }

        private static EmailMessage BuildEmail(string subject, string body, string[] recipients, string replyTo)
        {
            var emailContent = new EmailContent(subject)
            {
                Html = body
            };

            var emailMessage = new EmailMessage(Sender, recipients.FirstOrDefault(), emailContent);

            foreach (var recipient in recipients)
            {
                emailMessage.Recipients.To.Add(new EmailAddress(recipient));
            }

            try
            {
                emailMessage.ReplyTo.Add(new EmailAddress(replyTo));
            }
            catch (System.Exception)
            {
                // Was unable to add the users email to the ReplyTo. Ignoring.
            }

            if (BCCRecipient != string.Empty)
            {
                emailMessage.Recipients.BCC.Add(new EmailAddress(BCCRecipient));
            }

            return emailMessage;
        }

        private static string BuildEmailHtml(ContactFormBody formBody)
        {


            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body>
    <div>
        <div>
            <h2>Website Enquiry: {formBody.FormattedTopic} - {formBody.FirstName} {formBody.LastName}</h2>
        </div>
        <div>
            <p>A new enquiry has been submitted through the website. Below are the details:</p>

            <table>
                <tr>
                    <td>Name:</td>
                    <td>{formBody.FirstName} {formBody.LastName}</td>
                </tr>
                <tr>
                    <td>Email:</td>
                    <td>{formBody.Email}</td>
                </tr>
                <tr>
                    <td>Topic:</td>
                    <td>{formBody.FormattedTopic}</td>
                </tr>
                <tr>
                    <td>Subject:</td>
                    <td>{formBody.Subject}</td>
                </tr>
                <tr>
                    <td>Message:</td>
                    <td>{formBody.Message}</td>
                </tr>
            </table>

            <p>Please review and follow up as necessary.</p>
        </div>

        <div>
            <p></p>
        </div>
    </div>
</body>
</html>
";

            return html;
        }
    }
}
