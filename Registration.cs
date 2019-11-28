using IdentityModel.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HAS.Events
{
    public static class Registration
    {
        private static HttpClient _authClient = new HttpClient();
        private static HttpClient _profileClient = new HttpClient();

        [FunctionName("OnMPYRegisterCreateMPYProfile")]
        [return: Queue("log-event-mpy")]
        public static async Task<string> Run(
            [QueueTrigger("mpy-registration-complete", 
            Connection = "AzureWebJobsStorage")]string myQueueItem,
            ILogger log,
            ExecutionContext context)
        {
            var processingEventName = "mpy-registration-complete";

            var processingEnvironment = Environment.GetEnvironmentVariable("MPY:IdentityServer:RegistrationEventSvc:Environment");

            // deserialize the object
            var regObj = JsonConvert.DeserializeObject<RegisterNewUserMsg>(myQueueItem);

            // build service to send post request to API
            // service to service call

            var disco = await _authClient.GetDiscoveryDocumentAsync(Environment.GetEnvironmentVariable("MPY:IdentityServer:Authority"));
            if (disco.IsError)
            {
                log.LogError(disco.Error);
                return EventLog.Create(processingEventName, disco.Error, regObj, 400, processingEnvironment).ToJson();
            }
            else
            {
                // get a request token using requestclientcredentialstokenasync
                // pass in discovery tokenendpoint
                // pass in clientid
                var clientId = Environment.GetEnvironmentVariable("MPY:IdentityServer:RegistrationEventSvc:ClientId");
                // pass in secret
                var secret = Environment.GetEnvironmentVariable("MPY:IdentityServer:RegistrationEventSvc:ClientSecret");
                // pass in allowed scope
                var scopes = Environment.GetEnvironmentVariable("MPY:IdentityServer:RegistrationEventSvc:Scopes");

                var tokenResponse = await _authClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = clientId,
                    ClientSecret = secret,
                    Scope = scopes
                });

                if (tokenResponse.IsError)
                {
                    log.LogError(tokenResponse.Error);
                    return EventLog.Create(processingEventName, disco.Error, regObj, 400, processingEnvironment).ToJson();
                }
                else
                {
                    log.LogDebug(tokenResponse.AccessToken);

                    // using the received token
                    // call the POST profile api, passing in the proper body
                    // set the token as a Bearer token
                    _profileClient.SetBearerToken(tokenResponse.AccessToken);

                    var profileUri = Environment.GetEnvironmentVariable("MPY:API:Profile:Authority");

                    var response = await _profileClient.PostAsJsonAsync<RegisterNewUserMsg>($"{profileUri}", regObj);

                    var content = await response.Content.ReadAsStringAsync();

                    var evLogObj = EventLog.Create(processingEventName, content, regObj, (int)response.StatusCode, processingEnvironment);
                    
                    var fResponse = evLogObj.ToJson();

                    return fResponse;
                }
            }
        }

        public class RegisterNewUserMsg
        {
            public string UserId { get; set; }
            public string Email { get; set; }
        }
    }

}
