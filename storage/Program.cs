using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Newtonsoft.Json.Linq;

namespace StorageAccount
{
    class Program
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
            var credential = new ClientSecretCredential(tenantId, servicePrincipalId, servicePrincipalSecret, new TokenCredentialOptions {AuthorityHost = AuthorityHost});
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
            
            // Create resource group.
            try
            {
                Console.WriteLine(String.Format("Creating a resource group with name: {0}", resourceGroupName));
                ArmOperation<ResourceGroupResource> operation = await resourceGroups.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceGroupName, resourceGroupData);
                resourceGroup = operation.Value;
            }
            catch (Exception ex)
            {
                resourceGroup = null;
                Console.WriteLine(String.Format("Could not create resource group {0}. Exception: {1}", resourceGroupName, ex.Message));
            }

            if (resourceGroup != null)
            {
                string storageAccountName = "csharpsamplestorage";
                StorageAccountResource storageAccount;
                
                // Create storage account.
                try
                {
                    StorageSku sku = new StorageSku(StorageSkuName.StandardLrs);
                    StorageKind kind = StorageKind.Storage;
                    StorageAccountCreateOrUpdateContent parameters = new StorageAccountCreateOrUpdateContent(sku, kind, location);
                    // Get a collection of all storage accounts.
                    StorageAccountCollection accountCollection = resourceGroup.GetStorageAccounts();
                    storageAccount = accountCollection.CreateOrUpdate(Azure.WaitUntil.Completed, storageAccountName, parameters).Value;
                }
                catch (Exception ex)
                {
                    storageAccount = null;
                    Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccountName, ex.Message));
                }

                if (storageAccount != null)
                {
                    // Get | regenerate storage account access keys.
                    try
                    {
                        var storageAccountKeys = storageAccount.GetKeys().ToList();
                        foreach (var key in storageAccountKeys)
                        {
                            // Call "key.Value" for the key's value
                            Console.WriteLine(String.Format("Storage account key name: {0}", key.KeyName));
                        }
                        var StorageAccountRegenerateKeyContent = new StorageAccountRegenerateKeyContent(storageAccountKeys[0].KeyName);
                        Console.WriteLine("Regenerating first storage account access key");
                        var regeneratedStorageAccountKey = storageAccount.RegenerateKey(StorageAccountRegenerateKeyContent).ToList();
                        foreach (var key in regeneratedStorageAccountKey)
                        {
                            // Call "key.Value" for the key's value
                            Console.WriteLine(String.Format("Storage account key name: {0}", key.KeyName));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(String.Format("Could not get or regenerate storage account keys for storage account {0}. Exception: {1}", storageAccountName, ex.Message));
                    }
                    
                    // Update storage account by enabling encryption.
                    try
                    {
                        var storageAccountPatch = new StorageAccountPatch();
                        storageAccountPatch.Encryption = new StorageAccountEncryption()
                        {
                            KeySource = StorageAccountKeySource.Storage,
                            Services = new StorageAccountEncryptionServices
                            {
                                Blob = new StorageEncryptionService { IsEnabled = true, KeyType = "Service" },
                                File = new StorageEncryptionService { IsEnabled = true, KeyType = "Service" }
                            }
                        };

                        Console.WriteLine(String.Format("Enabling blob encryption for the storage account: {0}", storageAccountName));
                        var storageAccountResource = storageAccount.Update(storageAccountPatch);
                        var status = storageAccountResource.Value.Data.Encryption.Services.Blob.IsEnabled;
                        if (status.HasValue && status.Value)
                        {
                            Console.WriteLine(String.Format("Encryption of the blob and file for storage account {0} is enabled", storageAccountName));
                        }
                        else
                        {
                            Console.WriteLine(String.Format("Encryption of the blob and file for storage account {0} failed to be enabled", storageAccountName));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(String.Format("Could not enable blob encryption for storage account {0}. Exception: {1}", storageAccountName, ex.Message));
                    }

                    // Delete storage accounts.
                    try
                    {
                        storageAccount.Delete(Azure.WaitUntil.Completed);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(String.Format("Could not delete storage accounts {0}. Exception: {1}", storageAccountName, ex.Message));
                    }
                }
            }

            if (resourceGroup != null)
            {
                // Delete resource group.
                resourceGroup = subscription.GetResourceGroup(resourceGroupName);
                Console.WriteLine(String.Format("Deleting a resource group with name: {0}", resourceGroupName));
                resourceGroup.Delete(Azure.WaitUntil.Completed);
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
