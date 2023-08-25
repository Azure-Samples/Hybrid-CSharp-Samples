namespace ResourceGroup
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    using ProfileResourceManager = Microsoft.Azure.Management.Profiles.hybrid_2020_09_01.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure.Authentication;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;

    class Program
    {
        private const string ComponentName = "DotnetSDKResourceManagementSample";

        static void runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint)
        {
            var resourceGroupName = "azure-sample-csharp-resourcegroup";

            Console.WriteLine("Get credential token");
            var adSettings = getActiveDirectoryServiceSettings(armEndpoint); 
            var credentials = ApplicationTokenProvider.LoginSilentAsync(tenantId, servicePrincipalId, servicePrincipalSecret, adSettings).GetAwaiter().GetResult();

            Console.WriteLine("Instantiate resource management client");
            var rmClient = GetResourceManagementClient(new Uri(armEndpoint), credentials, subscriptionId);

            // Create resource group.
            try
            {
                Console.WriteLine(String.Format("Creating a resource group with name: {0}", resourceGroupName));
                var rmCreateTask = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    new ProfileResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    });
                rmCreateTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroupName, ex.Message));
            }

            // Update the resource group.
            try
            {
                Console.WriteLine(String.Format("Updating the resource group with name: {0}", resourceGroupName));
                var rmTagTask = rmClient.ResourceGroups.UpdateWithHttpMessagesAsync(resourceGroupName, new ProfileResourceManager.Models.ResourceGroupPatchable
                {
                    Tags = new Dictionary<string, string> { { "DotNetTag", "DotNetValue" } }
                });

                rmTagTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not tag resource grooup {0}. Exception: {1}", resourceGroupName, ex.Message));
            }

            // Get the resource groups.
            try
            {
                Console.WriteLine("Getting the created resource group.");
                var rmListTask = rmClient.ResourceGroups.GetWithHttpMessagesAsync(resourceGroupName);
                rmListTask.Wait();
                var resourceGroupResults = rmListTask.Result.Body;
                Console.WriteLine("Resource group:");
                Console.WriteLine(JsonConvert.SerializeObject(resourceGroupResults));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Could not list resource groups. Exception: {0}", ex.Message));
            }

            // Delete a resource group.
            try
            {
                Console.WriteLine(String.Format("Deleting resource group with name: {0}", resourceGroupName));
                var rmDeleteTask = rmClient.ResourceGroups.DeleteWithHttpMessagesAsync(resourceGroupName);
                rmDeleteTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete resource group {0}. Exception: {1}", resourceGroupName, ex.Message));
            }
        }

        static ActiveDirectoryServiceSettings getActiveDirectoryServiceSettings(string armEndpoint)
        {
            var settings = new ActiveDirectoryServiceSettings();

            try
            {
                var request = (HttpWebRequest)HttpWebRequest.Create(string.Format("{0}/metadata/endpoints?api-version=2019-10-01", armEndpoint));
                request.Method = "GET";
                request.UserAgent = ComponentName;
                request.Accept = "application/xml";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        var rawResponse = sr.ReadToEnd();
                        var deserializedArray = JArray.Parse(rawResponse);
                        var deserializedObject = deserializedArray[0].Value<JObject>();
                        var authenticationObj = deserializedObject.GetValue("authentication").Value<JObject>();
                        var loginEndpoint = authenticationObj.GetValue("loginEndpoint").Value<string>();
                        var audiencesObj = authenticationObj.GetValue("audiences").Value<JArray>();

                        settings.AuthenticationEndpoint = new Uri(loginEndpoint);
                        settings.TokenAudience = new Uri(audiencesObj[0].Value<string>());
                        settings.ValidateAuthority = loginEndpoint.TrimEnd('/').EndsWith("/adfs", StringComparison.OrdinalIgnoreCase) ? false : true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not get AD service settings. Exception: {0}", ex.Message));
            }
            return settings;
        }

        static void Main(string[] args)
        {
            JObject secretServicePrincipalSettings = JObject.Parse(File.ReadAllText(@"..\azureAppSpConfig.json"));
            var tenantId = secretServicePrincipalSettings.GetValue("tenantId").ToString();
            var servicePrincipalId = secretServicePrincipalSettings.GetValue("clientId").ToString();
            var servicePrincipalSecret = secretServicePrincipalSettings.GetValue("clientSecret").ToString();
            var subscriptionId = secretServicePrincipalSettings.GetValue("subscriptionId").ToString();
            var resourceManagerUrl = secretServicePrincipalSettings.GetValue("resourceManagerUrl").ToString();
            var location = secretServicePrincipalSettings.GetValue("location").ToString();

            runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, resourceManagerUrl);
        }

        private static ProfileResourceManager.ResourceManagementClient GetResourceManagementClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new ProfileResourceManager.ResourceManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }
    }
}
