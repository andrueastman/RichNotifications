using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using RichNotifications.Models;

namespace RichNotifications.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly MyConfig _config;
        private readonly X509Certificate2 _certificate;

        public NotificationsController(MyConfig config)
        {
            this._config = config;
            this._certificate = new X509Certificate2(System.IO.File.ReadAllBytes(config.CertificatePath),this._config.CertificatePassword);
        }

        [HttpGet]
        public async Task<ActionResult<string>> Get()
        {
            var graphServiceClient = GetGraphClient();

            var subscription = new Subscription
            {
                ChangeType = "created,updated",
                IncludeResourceData = true,
                NotificationUrl = _config.Ngrok + "/api/notifications",
                Resource = "/teams/getAllMessages",
                ExpirationDateTime = DateTime.UtcNow.AddMinutes(5),
                ClientState = "SecretClientState",
                EncryptionCertificateId = _config.CustomId,

            };

            // Load the certificate into an X509Certificate object.
            subscription.AddPublicEncryptionCertificate(this._certificate);

            var newSubscription = await graphServiceClient
              .Subscriptions
              .Request()
              .AddAsync(subscription);

            return $"Subscribed. Id: {newSubscription.Id}, Expiration: {newSubscription.ExpirationDateTime}";
        }

        public async Task<ActionResult<string>> Post([FromQuery] string validationToken = null)
        {
            // handle validation
            if (!string.IsNullOrEmpty(validationToken))
            {
                Console.WriteLine($"Received Token: '{validationToken}'");
                return Ok(validationToken);
            }

            // handle notifications
            var graphServiceClient = GetGraphClient();
            var myTenantIds = new Guid[] { new Guid(_config.TenantId) };
            var myAppIds = new Guid[] { new Guid(_config.AppId) };

            var collection = graphServiceClient.HttpProvider.Serializer.DeserializeObject<ChangeNotificationCollection>(Request.Body);
            var areTokensValid = await collection.AreTokensValid(myTenantIds, myAppIds);
            foreach (var changeNotification in collection.Value)
            {
                var attachedChatMessage = await changeNotification.EncryptedContent.Decrypt<ChatMessage>((id, thumbprint) => Task.FromResult(this._certificate));
                if (areTokensValid)
                {
                    Console.WriteLine($"Message time: {attachedChatMessage.CreatedDateTime}");
                    Console.WriteLine($"Message content: {attachedChatMessage.Body.Content}");
                    Console.WriteLine();
                }
            }
            return Ok();
        }

        private GraphServiceClient GetGraphClient()
        {
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider((requestMessage) => {
                // get an access token for Graph
                var accessToken = GetAccessToken().Result;

                requestMessage
                    .Headers
                    .Authorization = new AuthenticationHeaderValue("bearer", accessToken);

                return Task.FromResult(0);
            }));

            return graphClient;
        }

        private async Task<string> GetAccessToken()
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(_config.AppId)
              .WithClientSecret(_config.AppSecret)
              .WithAuthority($"https://login.microsoftonline.com/{_config.TenantId}")
              .WithRedirectUri("https://daemon")
              .Build();

            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };
            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

            return result.AccessToken;
        }

    }
}