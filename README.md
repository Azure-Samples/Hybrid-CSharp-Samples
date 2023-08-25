---
page_type: sample
languages:
- javascript
products:
- azure
description: "This sample is very similar to hybrid-resources-dotnet-manage-resource-group sample except that it uses certificates for authentication."
urlFragment: hybrid-resources-dotnet-manage-resource-group-certificate
---

# Hybrid-Resources-DotNET-Manage-ResourceGroupCertificate

This sample is very similar to [hybrid-resources-dotnet-manage-resource-group](https://github.com/Azure-Samples/hybrid-resources-dotnet-manage-resource-group) sample except that it uses certificates for authentication.

Azure Stack resource sample for managing resource groups - 
- Create a resource group
- Update a resource group
- Create another resource group
- List resource groups
- Delete a resource group

## Running this sample

To run this sample:

1. Clone the repository using the following command:

    git clone https://github.com/Azure-Samples/hybrid-resources-dotnet-manage-resource-group.git

2. Create an Azure service principal and assign a role to access the subscription. For instructions on creating a service principal in Azure Stack, see [Use Azure PowerShell to create a service principal with a certificate](https://docs.microsoft.com/en-us/azure/azure-stack/azure-stack-create-service-principals). 

3. Export the service principal certificate as a pfx file.  

4. Set the following required environment variable values:
    * AZURE_ARM_ENDPOINT
    * AZURE_LOCATION
    * AZURE_TENANT_ID
    * AZURE_SP_CERT_ID
    * AZURE_SP_CERT_PASS
    * AZURE_SP_CERT_PATH
    * AZURE_SUBSCRIPTION_ID

5. Change directory to sample:
    ```
    cd hybrid-resources-dotnet-manage-resource-group-certificate
    ```

6. Run the sample:
    ```
    dotnet restore
    dotnet run
    ```

## More information

[Azure .Net Developer Center](https://azure.microsoft.com/develop/net/).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
