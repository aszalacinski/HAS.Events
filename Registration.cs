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
            var processingEventName = "NewUserRegistered";

            var processingEnvironment = Environment.GetEnvironmentVariable("MPY.IdentityServer.RegistrationEventSvc.Environment");

            // deserialize the message on queue
            var regObj = JsonConvert.DeserializeObject<RegisterNewUserMsg>(myQueueItem);

            // get auth discovery document... needed to get token endpoints
            var disco = await _authClient.GetDiscoveryDocumentAsync(Environment.GetEnvironmentVariable("MPY.IdentityServer.Authority"));
            
            if (disco.IsError)
            {
                log.LogError(disco.Error);
                return EventLog.Create(processingEventName, disco.Error, regObj, 400, processingEnvironment).ToJson();
            }
            else
            {
                // get a request token from auth server using requestclientcredentialstokenasync
                var tokenResponse = await _authClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    // pass in discovery tokenendpoint
                    Address = disco.TokenEndpoint,
                    // pass in clientid
                    ClientId = Environment.GetEnvironmentVariable("MPY.IdentityServer.RegistrationEventSvc.ClientId"),
                    // pass in secret
                    ClientSecret = Environment.GetEnvironmentVariable("MPY.IdentityServer.RegistrationEventSvc.ClientSecret"),
                    // pass in allowed scope
                    Scope = Environment.GetEnvironmentVariable("MPY.IdentityServer.RegistrationEventSvc.Scopes")
                });

                if (tokenResponse.IsError)
                {
                    log.LogError(tokenResponse.Error);
                    return EventLog.Create(processingEventName, tokenResponse.Error, regObj, 400, processingEnvironment).ToJson();
                }
                else
                {
                    // using the received token
                    // set the token as a Bearer token
                    _profileClient.SetBearerToken(tokenResponse.AccessToken);
                    
                    // call the POST profile api, passing in the proper body
                    var response = await _profileClient.PostAsJsonAsync<RegisterNewUserMsg>($"{Environment.GetEnvironmentVariable("MPY.API.Profile.Authority")}", regObj);

                    var content = await response.Content.ReadAsStringAsync();

                    var evLogObj = EventLog.Create(processingEventName, content, regObj, (int)response.StatusCode, processingEnvironment);
                    
                    return evLogObj.ToJson();
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
