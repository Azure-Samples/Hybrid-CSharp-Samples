namespace VirtualMachine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using ProfileCompute = Microsoft.Azure.Management.Profiles.hybrid_2020_09_01.Compute;
    using ProfileNetwork = Microsoft.Azure.Management.Profiles.hybrid_2020_09_01.Network;
    using ProfileResourceManager = Microsoft.Azure.Management.Profiles.hybrid_2020_09_01.ResourceManager;
    using ProfileStorage = Microsoft.Azure.Management.Profiles.hybrid_2020_09_01.Storage;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure.Authentication;
    using Newtonsoft.Json.Linq;
    using System.Security.Cryptography.X509Certificates;

    class Program
    {
        private const string ComponentName = "DotnetSDKVirtualMachineManagementSample";
        private const string vhdURItemplate = "https://{0}.blob.{1}/vhds/{2}.vhd";

        static void runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint, string certPath)
        {
            var resourceGroupName = "azure-sample-csharp-vm";
            var vmName = SdkContext.RandomResourceName("vmDotnetSdk", 24);
            var vmNameManagedDisk = SdkContext.RandomResourceName("vmManagedDotnetSdk", 24);
            var vnetName = SdkContext.RandomResourceName("vnetDotnetSdk", 24);
            var subnetName = SdkContext.RandomResourceName("subnetDotnetSdk", 24);
            var subnetAddress = "10.0.0.0/24";
            var vnetAddresses = "10.0.0.0/16";
            var ipName = SdkContext.RandomResourceName("ipDotnetSdk", 24);
            var nicName = SdkContext.RandomResourceName("nicDotnetSdk", 24); ;
            var diskName = SdkContext.RandomResourceName("diskDotnetSdk", 24);
            var storageAccountName = SdkContext.RandomResourceName("storageaccount", 18);
            var username = "tirekicker";
            var password = "12NewPA$$w0rd!";

            Console.WriteLine("Get credential token");
            var adSettings = getActiveDirectoryServiceSettings(armEndpoint);
            var certificate = new X509Certificate2(certPath, servicePrincipalSecret);
            var credentials =  ApplicationTokenProvider.LoginSilentWithCertificateAsync(tenantId, new ClientAssertionCertificate(servicePrincipalId, certificate), adSettings).GetAwaiter().GetResult();
            Console.WriteLine("Instantiate resource management client");
            var rmClient = GetResourceManagementClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate storage account client");
            var storageClient = GetStorageClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate network client");
            var networkClient = GetNetworkClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate compute client");
            var computeClient = GetComputeClient(new Uri(armEndpoint), credentials, subscriptionId);

            // Create a resource group
            try
            {
                Console.WriteLine("Create resource group");
                var rmTask = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    new ProfileResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    });
                rmTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group. Exception: {0}", ex.Message));
            }

            // Create a Storage Account
            var storageAccount = new ProfileStorage.Models.StorageAccount();
            try
            {
                Console.WriteLine(String.Format("Creating a storage account with name:{0}", storageAccountName));
                var storageProperties = new ProfileStorage.Models.StorageAccountCreateParameters
                {
                    Location = location,
                    Kind = ProfileStorage.Models.Kind.Storage,
                    Sku = new ProfileStorage.Models.Sku(ProfileStorage.Models.SkuName.StandardLRS)
                };

                var storageTask = storageClient.StorageAccounts.CreateWithHttpMessagesAsync(resourceGroupName, storageAccountName, storageProperties);
                storageTask.Wait();
                storageAccount = storageTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccountName, ex.Message));
            }

            var subnet = new ProfileNetwork.Models.Subnet();

            // Create virtual network
            try
            {
                Console.WriteLine("Create vitual network");
                var vnet = new ProfileNetwork.Models.VirtualNetwork
                {
                    Location = location,
                    AddressSpace = new ProfileNetwork.Models.AddressSpace
                    {
                        AddressPrefixes = new List<string> { vnetAddresses }
                    }
                };
                var vnetTask = networkClient.VirtualNetworks.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vnetName,
                    vnet);
                vnetTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual network. Exception: {0}", ex.Message));
            }

            // Create subnet in the virtual network
            try
            {
                Console.WriteLine("Create a subnet");
                var subnetTask = networkClient.Subnets.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vnetName, subnetName, new ProfileNetwork.Models.Subnet
                {
                    AddressPrefix = subnetAddress,
                    Name = subnetName
                });
                subnetTask.Wait();
                subnet = subnetTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create subnet. Exception: {0}", ex.Message));
            }

            // Create a public address
            var ip = new ProfileNetwork.Models.PublicIPAddress();
            try
            {
                Console.WriteLine("Create IP");
                var ipProperties = new ProfileNetwork.Models.PublicIPAddress
                {
                    Location = location,
                    PublicIPAllocationMethod = ProfileNetwork.Models.IPAllocationMethod.Dynamic,
                };
                var ipTask = networkClient.PublicIPAddresses.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    ipName,
                    ipProperties);
                ipTask.Wait();
                ip = ipTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create IP. Exception: {0}", ex.Message));
            }

            // Create a network interface
            var nic = new ProfileNetwork.Models.NetworkInterface();
            var vmStorageProfile = new ProfileCompute.Models.StorageProfile();
            try
            {
                Console.WriteLine("Create network interface");
                var nicProperties = new ProfileNetwork.Models.NetworkInterface
                {
                    Location = location,
                    IpConfigurations = new List<ProfileNetwork.Models.NetworkInterfaceIPConfiguration>
                    {
                        new ProfileNetwork.Models.NetworkInterfaceIPConfiguration
                        {
                            Name = string.Format("{0}-ipconfig", nicName),
                            PrivateIPAllocationMethod = "Dynamic",
                            PublicIPAddress = ip,
                            Subnet = subnet
                        }
                    }

                };

                var nicTask = networkClient.NetworkInterfaces.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    nicName,
                    nicProperties);
                nicTask.Wait();
                nic = nicTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create network interface. Exception: {0}", ex.Message));
            }

            // Create a data disk
            var disk = new ProfileCompute.Models.Disk();
            try
            {
                Console.WriteLine("Create a data disk");
                var diskProperties = new ProfileCompute.Models.Disk
                {
                    CreationData = new ProfileCompute.Models.CreationData
                    {
                        CreateOption = ProfileCompute.Models.DiskCreateOption.Empty,
                    },
                    Location = location,
                    Sku = new ProfileCompute.Models.DiskSku
                    {
                        Name = ProfileCompute.Models.StorageAccountTypes.StandardLRS
                    },
                    DiskSizeGB = 1,
                };
                var diskTask = computeClient.Disks.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    diskName,
                    diskProperties);
                diskTask.Wait();
                disk = diskTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create data disk. Exception: {0}", ex.Message));
            }

            // VM Hardware profile
            var vmHardwareProfile = new ProfileCompute.Models.HardwareProfile
            {
                VmSize = "Standard_A1"
            };

            // VM OS Profile
            var vmOsProfile = new ProfileCompute.Models.OSProfile
            {
                ComputerName = vmName,
                AdminUsername = username,
                AdminPassword = password
            };

            // VM Network profile
            var vmNetworkProfile = new ProfileCompute.Models.NetworkProfile
            {
                NetworkInterfaces = new List<ProfileCompute.Models.NetworkInterfaceReference>
                {
                    new ProfileCompute.Models.NetworkInterfaceReference
                    {
                        Id = nic.Id,
                        Primary = true
                    }
                }
            };

            // VM Storage profile
            string diskUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, diskName);
            var osDiskName = "osDisk";
            string osDiskUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, osDiskName);
            vmStorageProfile = new ProfileCompute.Models.StorageProfile
            {
                OsDisk = new ProfileCompute.Models.OSDisk
                {
                    Name = osDiskName,
                    CreateOption = ProfileCompute.Models.DiskCreateOptionTypes.FromImage,
                    Caching = ProfileCompute.Models.CachingTypes.ReadWrite,
                    OsType = ProfileCompute.Models.OperatingSystemTypes.Linux,
                    Vhd = new ProfileCompute.Models.VirtualHardDisk
                    {
                        Uri = osDiskUri
                    }
                },
                ImageReference = new ProfileCompute.Models.ImageReference
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "16.04-LTS",
                    Version = "latest"
                },
                DataDisks = null
            };

            // Create Linux VM
            var linuxVm = new ProfileCompute.Models.VirtualMachine();
            try
            {
                Console.WriteLine("Create a virtual machine");
                var t1 = DateTime.Now;
                var vmTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vmName,
                    new ProfileCompute.Models.VirtualMachine
                    {
                        Location = location,
                        NetworkProfile = vmNetworkProfile,
                        StorageProfile = vmStorageProfile,
                        OsProfile = vmOsProfile,
                        HardwareProfile = vmHardwareProfile
                    });
                vmTask.Wait();
                linuxVm = vmTask.Result.Body;
                var t2 = DateTime.Now;
                vmStorageProfile = linuxVm.StorageProfile;
                Console.WriteLine(String.Format("Create virtual machine {0} took {1} seconds", linuxVm.Id, (t2 - t1).TotalSeconds.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual machine. Exception: {0}", ex.Message));
            }

            // Update - Tag the virtual machine
            try
            {
                Console.WriteLine("Tag virtual machine");
                var vmTagTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new ProfileCompute.Models.VirtualMachine
                {
                    Location = location,
                    Tags = new Dictionary<string, string> { { "who-rocks", "java" }, { "where", "on azure stack" } }
                });
                vmTagTask.Wait();
                linuxVm = vmTagTask.Result.Body;
                Console.WriteLine(string.Format("Taged virtual machine {0}", linuxVm.Id));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not tag virtual machine. Exception: {0}", ex.Message));
            }

            // Update - Add data disk
            try
            {
                Console.WriteLine("Attach data disk to virtual machine");
                string newDataDiskName = "dataDisk2";
                string newDataDiskVhdUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, newDataDiskName);
                var dataDisk = new ProfileCompute.Models.DataDisk
                {
                    CreateOption = ProfileCompute.Models.DiskCreateOptionTypes.Empty,
                    Caching = ProfileCompute.Models.CachingTypes.ReadOnly,
                    DiskSizeGB = 1,
                    Lun = 2,
                    Name = newDataDiskName,
                    Vhd = new ProfileCompute.Models.VirtualHardDisk
                    {
                        Uri = newDataDiskVhdUri
                    }
                };
                vmStorageProfile.DataDisks.Add(dataDisk);
                var addTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new ProfileCompute.Models.VirtualMachine
                {
                    Location = location,
                    StorageProfile = vmStorageProfile
                });
                addTask.Wait();
                vmStorageProfile = addTask.Result.Body.StorageProfile;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not add data disk to virtual machine. Exception: {0}", ex.Message));
            }

            // Update - detach data disk
            try
            {
                Console.WriteLine("Detach data disk from virtual machine");
                vmStorageProfile.DataDisks.RemoveAt(0);
                var detachTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new ProfileCompute.Models.VirtualMachine {
                    Location = location,
                    StorageProfile = vmStorageProfile
                });
                detachTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not detach data disk from virtual machine. Exception: {0}", ex.Message));
            }

            // Restart the virtual machine
            try
            {
                Console.WriteLine("Restart virtual machine");
                var restartTask = computeClient.VirtualMachines.RestartWithHttpMessagesAsync(resourceGroupName, vmName);
                restartTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not restart virtual machine. Exception: {0}", ex.Message));
            }

            // Stop(powerOff) the virtual machine
            try
            {
                Console.WriteLine("Power off virtual machine");
                var stopTask = computeClient.VirtualMachines.PowerOffWithHttpMessagesAsync(resourceGroupName, vmName);
                stopTask.Wait();

            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not power off virtual machine. Exception: {0}", ex.Message));
            }

            // Delete VM
            try
            {
                Console.WriteLine("Delete virtual machine");
                var deleteTask = computeClient.VirtualMachines.DeleteWithHttpMessagesAsync(resourceGroupName, vmName);
                deleteTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete virtual machine. Exception: {0}", ex.Message));
            }

            // VM Storage profile managed disk
            vmStorageProfile = new ProfileCompute.Models.StorageProfile
            {
                DataDisks = new List<ProfileCompute.Models.DataDisk>
                {
                    new ProfileCompute.Models.DataDisk
                    {
                        CreateOption = ProfileCompute.Models.DiskCreateOptionTypes.Attach,
                        ManagedDisk = new ProfileCompute.Models.ManagedDiskParameters
                        {
                            StorageAccountType = ProfileCompute.Models.StorageAccountTypes.StandardLRS,
                            Id = disk.Id
                        },
                        Caching = ProfileCompute.Models.CachingTypes.ReadOnly,
                        DiskSizeGB = 1,
                        Lun = 1,
                        Name = diskName,
                    }
                },
                OsDisk = new ProfileCompute.Models.OSDisk
                {
                    Name = osDiskName,
                    CreateOption = ProfileCompute.Models.DiskCreateOptionTypes.FromImage,
                },
                ImageReference = new ProfileCompute.Models.ImageReference
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "16.04-LTS",
                    Version = "latest"
                }
            };

            // Create Linux VM with managed disks
            var linuxVmManagedDisk = new ProfileCompute.Models.VirtualMachine();
            try
            {
                Console.WriteLine("Create a virtual machine with managed disk");
                var t1 = DateTime.Now;
                var vmTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vmNameManagedDisk,
                    new ProfileCompute.Models.VirtualMachine
                    {
                        Location = location,
                        NetworkProfile = vmNetworkProfile,
                        StorageProfile = vmStorageProfile,
                        OsProfile = vmOsProfile,
                        HardwareProfile = vmHardwareProfile
                    });
                vmTask.Wait();
                linuxVmManagedDisk = vmTask.Result.Body;
                var t2 = DateTime.Now;
                vmStorageProfile = linuxVm.StorageProfile;
                Console.WriteLine(String.Format("Create virtual machine with managed disk {0} took {1} seconds", linuxVmManagedDisk.Id, (t2 - t1).TotalSeconds.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual machine with managed disk. Exception: {0}", ex.Message));
            }

            // Delete VM with managed disk
            try
            {
                Console.WriteLine("Delete virtual machine with managed disk");
                var deleteTask = computeClient.VirtualMachines.DeleteWithHttpMessagesAsync(resourceGroupName, vmNameManagedDisk);
                deleteTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete virtual machine with managed disk. Exception: {0}", ex.Message));
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
            JObject secretServicePrincipalSettings = JObject.Parse(File.ReadAllText(@"..\azureCertSpConfig.json"));
            var tenantId = secretServicePrincipalSettings.GetValue("tenantId").ToString();
            var servicePrincipalId = secretServicePrincipalSettings.GetValue("clientId").ToString();
            var servicePrincipalSecret = secretServicePrincipalSettings.GetValue("certPass").ToString();
            var subscriptionId = secretServicePrincipalSettings.GetValue("subscriptionId").ToString();
            var resourceManagerUrl = secretServicePrincipalSettings.GetValue("resourceManagerUrl").ToString();
            var location = secretServicePrincipalSettings.GetValue("location").ToString();
            var certificatePath = secretServicePrincipalSettings.GetValue("certPath").ToString();

            runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, resourceManagerUrl, certificatePath);
        }

        private static ProfileStorage.StorageManagementClient GetStorageClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new ProfileStorage.StorageManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
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

        private static ProfileNetwork.NetworkManagementClient GetNetworkClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new ProfileNetwork.NetworkManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }

        private static ProfileCompute.ComputeManagementClient GetComputeClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new ProfileCompute.ComputeManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);
            
            return client;
        }
    }
}
