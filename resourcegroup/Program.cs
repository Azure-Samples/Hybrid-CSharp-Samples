namespace Azure.Samples
{
    using Azure;
    using Azure.Core;
    using Azure.Identity;
    using Azure.ResourceManager;
    using Azure.ResourceManager.Resources;
    using System;
    using System.Threading.Tasks;
    using Xunit;

    public class Program
    {
        readonly DefaultAzureCredential Credentials;
        Uri ArmUri = new Uri(AzureStackConstants.ArmUri);
        ArmClient TestArmClient;

        public Program()
        {
            Environment.SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", AzureStackConstants.SubscriptionId);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", AzureStackConstants.ClientId); 
            Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", "sample");
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", AzureStackConstants.TenantId);

            var credOptions = new DefaultAzureCredentialOptions
            {
                AuthorityHost = new Uri(AzureStackConstants.StsUri),
                TenantId = AzureStackConstants.TenantId,
                DisableInstanceDiscovery = true
            };
            Credentials = new DefaultAzureCredential(credOptions);

            var options = new ArmClientOptions
            {
                Environment = new ArmEnvironment(ArmUri, AzureStackConstants.Audience),
            };

            options.SetApiVersionsFromProfile(AzureStackProfile.Profile20200901Hybrid);
            TestArmClient = new ArmClient(Credentials, AzureStackConstants.SubscriptionId, options);
        }

        static async Task Main(string[] args)
        {
            Program program = new Program();
            await program.CreateResourceGroup();
            program.GetResourceGroups();
        }

        [Fact]
        public void GetResourceGroups()
        {
            var subscription = TestArmClient.GetDefaultSubscription();
            Console.WriteLine($"Default Azure Subscription: {subscription.Id}");
            var resourceGroups = subscription.GetResourceGroups();

            foreach (var rg in resourceGroups)
            {
                Console.WriteLine(rg.Id);
                foreach (var resource in rg.GetGenericResources())
                {
                    Console.WriteLine($"\t{resource.Id}");
                }
            }

            Assert.True(subscription != null, "Expect default subscription to be not null.");
        }

        [Fact]
        public async Task CreateResourceGroup()
        {
            var subscription = TestArmClient.GetDefaultSubscription();
            var resourceGroups = subscription.GetResourceGroups();
            AzureLocation location = new AzureLocation("DevFabric");

            string resourceGroupName = "myRgName" + Guid.NewGuid().ToString();
            ResourceGroupData resourceGroupData = new ResourceGroupData(location);
            ArmOperation<ResourceGroupResource> operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
            ResourceGroupResource resourceGroup = operation.Value;
            await Console.Out.WriteLineAsync(resourceGroup.Id);

            var rgResource = resourceGroups.Get(resourceGroupName);
            await Console.Out.WriteLineAsync(rgResource.Value.ToString());
            Assert.True(rgResource != null, "Expect created resource group is nont null.");
        }
    }
}
