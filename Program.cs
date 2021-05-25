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
    using System.Security.Cryptography.X509Certificates;

    class Program
    {
        private const string ComponentName = "DotnetSDKResourceManagementSample";

        static void runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint, string certPath)
        {
            var resourceGroup1Name = SdkContext.RandomResourceName("rgDotnetSdk", 24);
            var resourceGroup2Name = SdkContext.RandomResourceName("rgDotnetSdk", 24);

            Console.WriteLine("Get credential token");
            var adSettings = getActiveDirectoryServiceSettings(armEndpoint);
            var certificate = new X509Certificate2(certPath, servicePrincipalSecret);
            var credentials =  ApplicationTokenProvider.LoginSilentWithCertificateAsync(tenantId, new ClientAssertionCertificate(servicePrincipalId, certificate), adSettings).GetAwaiter().GetResult(); 
            Console.WriteLine("Instantiate resource management client");
            var rmClient = GetResourceManagementClient(new Uri(armEndpoint), credentials, subscriptionId);

            // Create resource group.
            try
            {
                Console.WriteLine(String.Format("Creating a resource group with name:{0}", resourceGroup1Name));
                var rm = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroup1Name,
                    new ProfileResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroup1Name, ex.Message));
            }

            // Update the resource group.
            try
            {
                Console.WriteLine(String.Format("Updating the resource group with name:{0}", resourceGroup1Name));
                var rmTag = rmClient.ResourceGroups.UpdateWithHttpMessagesAsync(resourceGroup1Name, new ProfileResourceManager.Models.ResourceGroupPatchable
                {
                    Tags = new Dictionary<string, string> { { "DotNetTag", "DotNetValue" } }
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not tag resource grooup {0}. Exception: {1}", resourceGroup1Name, ex.Message));
            }

            // Create another resource group.
            try
            {
                Console.WriteLine(String.Format("Creating a resource group with name:{0}", resourceGroup2Name));
                var rmNew = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroup2Name,
                    new ProfileResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroup2Name, ex.Message));
            }

            // List resource groups.
            try
            {
                Console.WriteLine("Listing all resource groups.");
                var rmList = rmClient.ResourceGroups.ListWithHttpMessagesAsync().GetAwaiter().GetResult();

                var resourceGroupResults = rmList.Body;
                foreach (var result in resourceGroupResults)
                {
                    Console.WriteLine(String.Format("Resource group name:{0}", result.Name));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Could not list resource groups. Exception: {0}", ex.Message));
            }

            // Delete a resource group.
            try
            {
                Console.WriteLine(String.Format("Deleting resource group with name:{0}", resourceGroup2Name));
                var rmDelete = rmClient.ResourceGroups.DeleteWithHttpMessagesAsync(resourceGroup2Name).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete resource group {0}. Exception: {1}", resourceGroup2Name, ex.Message));
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
            // Get variables
            var baseUriString = Environment.GetEnvironmentVariable("AZURE_ARM_ENDPOINT");
            var location = Environment.GetEnvironmentVariable("AZURE_LOCATION");
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var servicePrincipalId = Environment.GetEnvironmentVariable("AZURE_SP_CERT_ID");
            var servicePrincipalSecret = Environment.GetEnvironmentVariable("AZURE_SP_CERT_PASS");
            var certificatePath = Environment.GetEnvironmentVariable("AZURE_SP_CERT_PATH");
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");

            runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, baseUriString, certificatePath);
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
