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
    }

    public class ContactForm
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("1stchertseyscoutgroupacs_COMMUNICATIONSERVICES") ?? string.Empty;
        private static readonly string Sender = Environment.GetEnvironmentVariable("Email_Sender") ?? string.Empty;
        private static readonly string BCCRecipient = Environment.GetEnvironmentVariable("Email_BCCRecipient") ?? string.Empty;

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

            string[]? recipients = GetRecipients(recipientsClient, formBody.Topic);
            if (recipients == null || recipients.Length == 0)
            {
                return new OkObjectResult(false);
            }

            var subject = $"New Enquiry Submitted: {formBody.Topic} - {formBody.FirstName} {formBody.LastName}";
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
            }


            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
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
            var emailContent = new EmailContent($"Website Enquiry: {subject}")
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
    <title>New Enquiry Notification</title>
    <style>
        body {{
            font-family: 'Helvetica Neue', Arial, sans-serif;
            background-color: #f4f4f7;
            color: #333;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 30px auto;
            background-color: #fff;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
            padding: 20px;
            line-height: 1.6;
        }}
        .header {{text - align: center;
            padding-bottom: 20px;
            border-bottom: 2px solid #eee;
        }}
        .header h2 {{
        color: #2d3748;
        }}
        .content {{
        padding: 20px 0;
        }}
        .details-table {{
        width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }}
        .details-table td {{
        padding: 10px;
            border-bottom: 1px solid #eee;
        }}
        .details-table td:first-child {{
        font-weight: bold;
            color: #2d3748;
        }}
        .details-table tr:last-child td {{
        border - bottom: none;
        }}
        .footer {{
        text-align: center;
            padding-top: 20px;
            border-top: 2px solid #eee;
            color: #888;
        }}
    </style>
</head>
<body>
    <div class=""email-container"">
        <div class=""header"">
            <h2>New Enquiry: {formBody.Topic} - {formBody.FirstName} {formBody.LastName}</h2>
        </div>
        <div class=""content"">
            <p>A new enquiry has been submitted through the website. Below are the details:</p>

            <table class=""details-table"">
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
                    <td>{formBody.Topic}</td>
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

            <p style=""margin-top: 20px;"">Please review and follow up as necessary.</p>
        </div>

        <div class=""footer"">
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
