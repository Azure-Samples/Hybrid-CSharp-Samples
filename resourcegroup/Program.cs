using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;

namespace ResourceGroup
{
    public class Program
    {
        // AuthorityHost is the login endpoint or the active directory authority.
        public static Uri AuthorityHost;
        public static string Audiences;
        static readonly HttpClient httpClient = new HttpClient();
        public static async Task runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint)
        {
            await SetEnvironmentEndpoints(armEndpoint);
            Console.WriteLine("Creating ClientSecretCredential...");
            string[] urlSegments = AuthorityHost.AbsoluteUri.Split('/');
            if (urlSegments[urlSegments.Length - 1] == "adfs")
            {
                tenantId = "adfs";
            }
            var credential = new ClientSecretCredential(tenantId, servicePrincipalId, servicePrincipalSecret, 
                                    new ClientSecretCredentialOptions
                                    {
                                        AuthorityHost = AuthorityHost,
                                        DisableInstanceDiscovery = true
                                    }
                                );
            Console.WriteLine("Creating ArmClient...");
            var armClientOptions = new ArmClientOptions {
                Environment = new ArmEnvironment(new Uri(armEndpoint), Audiences)
            };
            armClientOptions.SetApiVersionsFromProfile(AzureStackProfile.Profile20200901Hybrid);
            var armClient = new ArmClient(credential, subscriptionId, armClientOptions);
            SubscriptionResource subscription = armClient.GetDefaultSubscription();
            var resourceGroupName = "azure-sample-csharp-resourcegroup";
            ResourceGroupData resourceGroupData = new ResourceGroupData(location);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup;

            try
            {
                // Create resource group.
                Console.WriteLine(String.Format("Creating a resource group with name: {0}", resourceGroupName));
                ArmOperation<ResourceGroupResource> operation = await resourceGroups.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceGroupName, resourceGroupData);
                resourceGroup = operation.Value;

                // Add tag to resource group.
                var tagName = "tagName";
                var tagValue = "tagValue";
                resourceGroup.AddTag(tagName, tagValue);
                var tags = new Dictionary<string, string>()
                {
                    {"a", "b"},
                    {"c", "d"}
                };
                resourceGroup.SetTags(tags);
                resourceGroup.RemoveTag("a");

                // Can also update tags via the following code:
                // ResourceGroupPatch resourceGroupPatch = new ResourceGroupPatch();
                // resourceGroupPatch.Tags[tagName] = tagValue;
                // Console.WriteLine(String.Format("Adding tags '{0}:{1}' to resource group {2}", tagName, tagValue, resourceGroupName));
                // resourceGroup.Update(resourceGroupPatch);

                // Get resource group.
                Console.WriteLine(String.Format("Getting resource group {0}", resourceGroupName));
                ResourceGroupResource gotResourceGroup = subscription.GetResourceGroup(resourceGroupName);
                Console.WriteLine(String.Format("Got resource group: {0}", gotResourceGroup));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroupName, ex.Message));
            }
            finally
            {
                // Delete resource group.
                resourceGroup = subscription.GetResourceGroup(resourceGroupName);
                Console.WriteLine(String.Format("Deleting a resource group with name: {0}", resourceGroupName));
                await resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
            }
        }

        static async Task SetEnvironmentEndpoints(String armEndpoint)
        {
            try
            {
                string responseBody = await httpClient.GetStringAsync(string.Format("{0}/metadata/endpoints?api-version=2019-10-01", armEndpoint));
                var deserializedArray = JArray.Parse(responseBody);
                var deserializedObject = deserializedArray[0].Value<JObject>();
                var authenticationObj = deserializedObject.GetValue("authentication").Value<JObject>();
                var loginEndpoint = authenticationObj.GetValue("loginEndpoint").Value<string>();
                var audiencesObj = authenticationObj.GetValue("audiences").Value<JArray>();
                AuthorityHost = new Uri(loginEndpoint);
                Audiences = audiencesObj[0].Value<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not get Azure Stack environment service metadata. Exception: {0}", ex.Message));
            }
        }

        public static async Task Main(string[] args)
        {
            JObject secretServicePrincipalSettings = JObject.Parse(File.ReadAllText(@"..\azureSecretSpConfig.json"));
            var tenantId = secretServicePrincipalSettings.GetValue("tenantId").ToString();
            var servicePrincipalId = secretServicePrincipalSettings.GetValue("clientId").ToString();
            var servicePrincipalSecret = secretServicePrincipalSettings.GetValue("clientSecret").ToString();
            var subscriptionId = secretServicePrincipalSettings.GetValue("subscriptionId").ToString();
            var resourceManagerEndpointUrl = secretServicePrincipalSettings.GetValue("resourceManagerEndpointUrl").ToString();
            var location = secretServicePrincipalSettings.GetValue("location").ToString();

            await runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, resourceManagerEndpointUrl);
        }
    }
}
