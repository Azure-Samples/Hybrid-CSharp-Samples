using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;

namespace VirtualMachine
{
    class Program
    {
        // AuthorityHost is the login endpoint or the active directory authority.
        public static Uri AuthorityHost;
        public static string Audiences;
        static readonly HttpClient httpClient = new HttpClient();

        public static async Task runSample(string tenantId, string subscriptionId, string servicePrincipalId, string certPass, string location, string armEndpoint, string certPath)
        {
            await SetEnvironmentEndpoints(armEndpoint);
            Random random = new Random();
            int randomInt = random.Next(1000, 10000);
            string resourceGroupName = "azure-sample-csharp-vm";
            string vmName = "vmDotnetSdk" + randomInt;
            string vmNameManagedDisk = "vmManagedDotnetSdk" + randomInt;

            X509Certificate2 certificate = new X509Certificate2(certPath, certPass);
            Console.WriteLine("Creating ClientCertificateCredential...");
            string[] urlSegments = AuthorityHost.AbsoluteUri.Split('/');
            if (urlSegments[urlSegments.Length - 1] == "adfs")
            {
                tenantId = "adfs";
            }
            ClientCertificateCredential credential = new ClientCertificateCredential(tenantId, servicePrincipalId, certificate, new TokenCredentialOptions {AuthorityHost = AuthorityHost});
            Console.WriteLine("Creating ArmClient...");
            ArmClientOptions armClientOptions = new ArmClientOptions {
                Environment = new ArmEnvironment(new Uri(armEndpoint), Audiences)
            };
            armClientOptions.SetApiVersion(ResourceGroupResource.ResourceType, "2019-10-01");
            armClientOptions.SetApiVersion(StorageAccountResource.ResourceType, "2019-06-01");
            armClientOptions.SetApiVersion(NetworkInterfaceResource.ResourceType, "2018-11-01");
            armClientOptions.SetApiVersion(VirtualMachineResource.ResourceType, "2020-06-01");
            armClientOptions.SetApiVersion(VirtualNetworkResource.ResourceType, "2018-11-01");
            armClientOptions.SetApiVersion(AvailabilitySetResource.ResourceType, "2020-06-01");
            armClientOptions.SetApiVersion(PublicIPAddressResource.ResourceType, "2018-11-01");
            armClientOptions.SetApiVersion(ManagedDiskResource.ResourceType, "2019-07-01");

            ArmClient armClient = new ArmClient(credential, subscriptionId, armClientOptions);
            SubscriptionResource subscription = armClient.GetDefaultSubscription();
            ResourceGroupData resourceGroupData = new ResourceGroupData(location);
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup;

            // Create resource group.
            Console.WriteLine(String.Format("Creating a resource group with name: {0}", resourceGroupName));
            ArmOperation<ResourceGroupResource> operation = await resourceGroups.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceGroupName, resourceGroupData);
            resourceGroup = operation.Value;

            try {   
                // Create storage account.
                string storageAccountName = "csharpvmsamplestorage";
                StorageAccountResource storageAccount;
                StorageSku sku = new StorageSku(StorageSkuName.StandardLRS);
                StorageKind kind = StorageKind.Storage;
                StorageAccountCreateOrUpdateContent parameters = new StorageAccountCreateOrUpdateContent(sku, kind, location);
                
                // Get a collection of all storage accounts.
                StorageAccountCollection accountCollection = resourceGroup.GetStorageAccounts();
                storageAccount = accountCollection.CreateOrUpdate(Azure.WaitUntil.Completed, storageAccountName, parameters).Value;

                // Create availability set.
                string availabilitySetName = "availabilitySet" + randomInt;
                Console.WriteLine("Creating availability set...");
                AvailabilitySetData availabilitySetData = new AvailabilitySetData(location)
                {
                    PlatformUpdateDomainCount = 5,
                    PlatformFaultDomainCount = 2,
                    Sku = new ComputeSku { Name = "Aligned" }
                };
                AvailabilitySetCollection availabilitySetCollection = resourceGroup.GetAvailabilitySets();
                AvailabilitySetResource availabilitySetResource = availabilitySetCollection.CreateOrUpdate(Azure.WaitUntil.Completed, availabilitySetName, availabilitySetData).Value;

                // Create public IP Address.
                string ipName = "ipDotnetSdk" + randomInt;
                PublicIPAddressData ipAddressData = new PublicIPAddressData
                {
                    PublicIPAddressVersion = Azure.ResourceManager.Network.Models.IPVersion.IPv4,
                    PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                    Location = location,
                };
                PublicIPAddressResource ipAddress = resourceGroup.GetPublicIPAddresses().CreateOrUpdate(Azure.WaitUntil.Completed, ipName, ipAddressData).Value;

                // Create virtual network.
                string subnetAddressPrefix = "10.0.0.0/24";
                string vnetAddressesPrefix = "10.0.0.0/16";
                string subnetName = "subnetDotnetSdk" + randomInt;
                string vnetName = "vnetDotnetSdk" + randomInt;
                Console.WriteLine("Creating virtual network...");
                VirtualNetworkData virtualNetworkData = new VirtualNetworkData
                {
                    Location = location,
                    AddressPrefixes =  { vnetAddressesPrefix },
                    Subnets = {
                        new SubnetData
                        {
                            Name = subnetName,
                            AddressPrefix = subnetAddressPrefix,
                        }
                    }
                };
                VirtualNetworkCollection virtualNetworkContainer = resourceGroup.GetVirtualNetworks();
                VirtualNetworkResource virtualNetwork = virtualNetworkContainer.CreateOrUpdate(Azure.WaitUntil.Completed, vnetName, virtualNetworkData).Value;

                // Create Network Interface.
                string networkInterfaceName = "networkInterfaceDotnetSdk" + randomInt;
                Console.WriteLine("Creating network interface...");
                NetworkInterfaceData networkInterfaceData = new NetworkInterfaceData
                {
                    Location = location,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "Primary",
                            Primary = true,
                            Subnet = new SubnetData { Id = virtualNetwork.Data.Subnets.First().Id },
                            PrivateIPAllocationMethod = IPAllocationMethod.Dynamic,
                            PublicIPAddress = new PublicIPAddressData { Id = ipAddress.Data.Id }
                        }
                    }
                };
                NetworkInterfaceCollection networkInterfacesCollection = resourceGroup.GetNetworkInterfaces();
                NetworkInterfaceResource networkInterface = networkInterfacesCollection.CreateOrUpdate(Azure.WaitUntil.Completed, networkInterfaceName, networkInterfaceData).Value;

                // Create a data disk.
                Console.WriteLine("Creating data disk...");
                string diskName = "diskDotnetSdk" + randomInt;
                ManagedDiskData managedDiskData = new ManagedDiskData(location)
                {
                    Sku = new DiskSku()
                    {
                        Name = DiskStorageAccountTypes.StandardLRS
                    },
                    CreationData = new DiskCreationData(DiskCreateOption.Empty),
                    DiskSizeGB = 1,
                };
                ManagedDiskCollection diskCollection = resourceGroup.GetManagedDisks();
                ManagedDiskResource managedDisk = diskCollection.CreateOrUpdate(Azure.WaitUntil.Completed, diskName, managedDiskData).Value;

                // Create VM.
                Console.WriteLine("Creating VM...");
                string osDiskName = "osDisk";
                // string osDiskVhdUri = string.Format("{0}test/{1}.vhd", storageAccount.Data.PrimaryEndpoints.Blob, osDiskName);
                string dataDiskName = "dataDisk";
                // string dataDiskVhdUri = string.Format("{0}test/{1}.vhd", storageAccount.Data.PrimaryEndpoints.Blob, dataDiskName);
                string vmUsername = "username";
                string vmPassword = "yourPasswordHere!";
                VirtualMachineData virtualMachineData = new VirtualMachineData(location)
                {
                    HardwareProfile = new HardwareProfile { VmSize = "Standard_A1"},
                    OSProfile = new OSProfile { 
                        ComputerName = vmName,
                        AdminUsername = vmUsername,
                        AdminPassword = vmPassword
                    },
                    NetworkProfile = new NetworkProfile { 
                        NetworkInterfaces = {
                                new NetworkInterfaceReference {
                                Id = networkInterface.Id, 
                                Primary = true
                            }
                        }
                    },
                    StorageProfile = new StorageProfile {
                        OSDisk = new OSDisk (DiskCreateOptionTypes.FromImage) {
                            Name = osDiskName,
                            Caching = CachingTypes.ReadWrite,
                            OSType = OperatingSystemTypes.Linux
                            // VhdUri = new Uri(osDiskVhdUri)
                        },
                        ImageReference = new ImageReference {
                            Publisher = "Canonical",
                            Offer = "UbuntuServer",
                            Sku = "16.04-LTS",
                            Version = "latest"
                        },
                        DataDisks = {
                            new DataDisk(1, DiskCreateOptionTypes.Empty) {
                                Caching = CachingTypes.ReadOnly,
                                DiskSizeGB = 1,
                                Name = dataDiskName,
                                // VhdUri = new Uri (dataDiskVhdUri),
                                ManagedDisk = new ManagedDiskParameters() {
                                    StorageAccountType = StorageAccountType.StandardLRS.ToString(),
                                    Id = managedDisk.Id
                                }
                            }
                        }
                    },
                    AvailabilitySetId = availabilitySetResource.Id
                };
                VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
                VirtualMachineResource virtualMachine = vmCollection.CreateOrUpdate(Azure.WaitUntil.Completed, vmName, virtualMachineData).Value;

                // Detach data disk.
                Console.WriteLine("Detaching data disk...");
                virtualMachineData.StorageProfile.DataDisks.RemoveAt(0);
                virtualMachine = vmCollection.CreateOrUpdate(Azure.WaitUntil.Completed, vmName, virtualMachineData).Value;

                // Restart virtual machine.
                Console.WriteLine("Restarting VM...");
                virtualMachine.PowerOff(Azure.WaitUntil.Completed);

                // Delete virtual machine.
                Console.WriteLine("Deleting VM...");
                virtualMachine.Delete(Azure.WaitUntil.Completed);
            }
            catch (Exception e) {
                Console.WriteLine(String.Format("Unexpected error: {0}", e.Message));
            }
            finally {
                // Delete resource group.
                resourceGroup = subscription.GetResourceGroup(resourceGroupName);
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
            JObject secretServicePrincipalSettings = JObject.Parse(File.ReadAllText(@"..\azureCertSpConfig.json"));
            var tenantId = secretServicePrincipalSettings.GetValue("tenantId").ToString();
            var servicePrincipalId = secretServicePrincipalSettings.GetValue("clientId").ToString();
            var certPass = secretServicePrincipalSettings.GetValue("certPass").ToString();
            var subscriptionId = secretServicePrincipalSettings.GetValue("subscriptionId").ToString();
            var resourceManagerEndpointUrl = secretServicePrincipalSettings.GetValue("resourceManagerEndpointUrl").ToString();
            var location = secretServicePrincipalSettings.GetValue("location").ToString();
            var certificatePath = secretServicePrincipalSettings.GetValue("certPath").ToString();

            await runSample(tenantId, subscriptionId, servicePrincipalId, certPass, location, resourceManagerEndpointUrl, certificatePath);
        }
    }
}
