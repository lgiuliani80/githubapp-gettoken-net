# GitHub App Token Server - Azure Developer CLI (azd) Configuration

This project is configured for deployment with the Azure Developer CLI (azd). Follow these steps to deploy it to Azure:

## Prerequisites

1. [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) installed
2. Azure subscription
3. GitHub App created with private key downloaded

## Deployment Steps

1. **Clone the repository**:

```bash
git clone [your-repo-url]
cd [your-repo-name]
```

2. **Initialize the Azure Developer CLI environment**:

```bash
azd init
```

3. **Set required parameters**:

```bash
# GitHub App configuration
azd env set GITHUB_APP_CLIENT_ID "your-github-app-client-id"
azd env set GITHUB_PRIVATE_KEY_FILE "/path/to/your-github-app-private-key.pem"

# Entra ID/authentication configuration (if using)
azd env set ENTRA_TENANT_ID "your-entra-tenant-id"
azd env set ENTRA_CLIENT_ID "your-entra-client-id"
azd env set ENTRA_DOMAIN "your-domain.com"

# Optional settings
azd env set REQUIRE_AUTHENTICATION "true"
azd env set MAP_OPENAPI "false"
```

4. **Deploy to Azure**:

```bash
azd up
```

This will:
- Create a resource group
- Deploy an App Service with .NET 9 runtime
- Deploy a Key Vault with RBAC authorization
- Configure the App Service with a system-assigned managed identity
- Grant the managed identity access to Key Vault secrets
- Upload your GitHub App private key to Key Vault
- Configure all the necessary environment variables for the app

5. **Verify deployment**:

Once deployment is complete, you can verify the application is working by navigating to the `/version` endpoint.

## Environment Variables

The deployment sets the following environment variables in the App Service:

- `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__Domain`, `AzureAd__TokenValidationParameters__ValidAudiences__0` - Entra ID configuration
- `RequireAuthentication` - Whether to require authentication for API endpoints
- `MapOpenApi` - Whether to expose OpenAPI documentation
- `Github__ClientId` - GitHub App client ID
- `Github__PrivateKey` - Reference to the GitHub App private key in Key Vault

## Customization

You can customize the deployment by modifying:

- `azure.yaml` - Azure Developer CLI configuration
- `infra/main.bicep` - Main infrastructure template
- `infra/modules/*.bicep` - Individual resource templates

## Troubleshooting

If you encounter issues:

1. Check the deployment logs using `azd deploy --debug`
2. Verify Key Vault access permissions are correctly set
3. Ensure the GitHub App private key is correctly uploaded to Key Vault