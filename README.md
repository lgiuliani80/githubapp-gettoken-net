# GitHub App Token Server

A lightweight ASP.NET Core service that facilitates obtaining authentication tokens for GitHub App installations. This service makes it easy to integrate GitHub App authentication into your CI/CD pipelines or other applications.

## Overview

This service provides a simple REST API for:

1. Generating JWT tokens for GitHub App authentication
2. Retrieving GitHub App installation information
3. Obtaining installation access tokens by organization name or installation ID

It uses an in-memory cache to store installation information, allowing for faster token retrieval with organization names.

## API Endpoints

All endpoints can be protected with authentication (JWT Bearer) which can be toggled on/off.

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/healthz` | GET | Health check endpoint | 200 OK if healthy |
| `/version` | GET | Returns version information | JSON with version details |
| `/jwt` | GET | Returns a JWT token for GitHub API authentication | Text/plain JWT token |
| `/installations` | GET | Lists all GitHub App installations | JSON array of installations |
| `/installations/{org}/token` | GET | Gets an access token for an installation by org name or ID | Text/plain access token |

### Authentication

The service supports three authentication methods:

1. **Entra ID** (Microsoft Identity Platform)
2. **OpenID Connect**
3. **Generic JWT**

Authentication can be disabled by setting `"RequireAuthentication": false` in the configuration.

## Configuration

The application uses the standard ASP.NET Core configuration system. Configure the following settings in `appsettings.json` or environment variables:

### GitHub App Configuration (`Github` section)

| Parameter | Description | Format |
|-----------|-------------|--------|
| `ClientId` | The GitHub App Client ID/App ID | String value (e.g., "Iv23ctQt1cKRg78ZlRsc") |
| `PrivateKey` | The private key used to sign JWT tokens | Can be provided in multiple formats (see below) |

#### Private Key Formats

The `PrivateKey` can be provided in several formats:

- **PEM string**: Beginning with `-----BEGIN RSA PRIVATE KEY-----` (PKCS#1 or PKCS#8)
- **Base64-encoded key**: Same as above, without header and footer, in a single line
- **Certificate thumbprint**: A 40-character hex string referencing a certificate in the Windows Certificate Store or 
  uploaded in the "Certificate" section of the App Service and listed (as thumbprint) in the `WEBSITE_LOAD_CERTIFICATES` environment variable.
- **File path**: Path to a PEM or DER encoded private key file (PKCS#1 or PKCS#8)

### Authentication Configuration

#### Entra ID (`AzureAd` section)

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "yourdomain.com",
  "TenantId": "company-tenant-id",
  "ClientId": "app-client-id",
  "TokenValidationParameters": {
    "ValidAudiences": [
        "app-client-id",
        ...
    ]
  }
}
```

#### OpenID Connect (`OpenId` section)

Configure standard OIDC parameters in the `OpenId` section.

#### JWT (`JWT` section)

Configure JWT validation parameters in the `JWT` section.

### Other Configuration

| Parameter | Description | Default |
|-----------|-------------|---------|
| `RequireAuthentication` | Whether to require authentication for API endpoints | `true` |
| `MapOpenApi` | Whether to expose the OpenAPI/Swagger documentation in `/openapi/v1.json` in non-development environments | `false` |

## Deployment to Azure

### Using Azure CLI

The following commands will set up all the necessary resources in Azure to run this application securely.

First, set up variables for your resource names:

```bash
# Set variables for resources
RG_NAME="github-token-rg"
LOCATION="italynorth"
APP_NAME="github-token-app"
KEYVAULT_NAME="github-token-kv"
ENTRA_APP_ID="YOUR_ENTRA_APP_CLIENT_ID"  # From your Entra ID app registration
ENTRA_TENANT_ID="YOUR_ENTRA_TENANT_ID"   # Your Entra ID tenant ID
ENTRA_DOMAIN="YOUR_DOMAIN.COM"           # Your Entra ID domain
GITHUB_APP_ID="YOUR_GITHUB_APP_ID"       # Your GitHub App ID
SECRET_NAME="github-private-key"         # Name for your private key in Key Vault
```

#### 1. Create a Resource Group and deploy the Web App

```bash
# Create a resource group
az group create --name $RG_NAME --location $LOCATION

# Deploy the app using webapp up
az webapp up --runtime "DOTNET:9.0" --sku B1 --name $APP_NAME --resource-group $RG_NAME --location $LOCATION
```

#### 2. Create a Key Vault with RBAC authorization model

```bash
# Create Key Vault with RBAC authorization model enabled
az keyvault create --name $KEYVAULT_NAME --resource-group $RG_NAME --location $LOCATION --enable-rbac-authorization true

# Get your user objectId
USER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

# Get the Key Vault resource ID
KEYVAULT_ID=$(az keyvault show --name $KEYVAULT_NAME --resource-group $RG_NAME --query id -o tsv)

# Assign yourself the "Key Vault Administrator" role
az role assignment create --assignee $USER_OBJECT_ID --role "Key Vault Administrator" --scope $KEYVAULT_ID
```

#### 3. Store your GitHub Private Key as a secret in Key Vault

```bash
# Add the GitHub private key to Key Vault
# First save your private key to a file named 'github-private-key.pem'
az keyvault secret set --vault-name $KEYVAULT_NAME --name $SECRET_NAME --file github-private-key.pem
```

#### 4. Enable System Assigned Managed Identity for the Web App

```bash
# Enable system-assigned managed identity for the Web App
az webapp identity assign --name $APP_NAME --resource-group $RG_NAME

# Get the principalId of the managed identity
PRINCIPAL_ID=$(az webapp identity show --name $APP_NAME --resource-group $RG_NAME --query principalId -o tsv)
```

#### 5. Grant the Managed Identity access to Key Vault secrets using RBAC

```bash
# Assign "Key Vault Secrets User" role to the web app's managed identity
az role assignment create --assignee $PRINCIPAL_ID --role "Key Vault Secrets User" --scope $KEYVAULT_ID
```

#### 6. Configure the necessary environment variables

```bash
# Set the environment variables for the Web App
az webapp config appsettings set --name $APP_NAME --resource-group $RG_NAME --settings \
  "AzureAd__TenantId=$ENTRA_TENANT_ID" \
  "AzureAd__ClientId=$ENTRA_APP_ID" \
  "AzureAd__Domain=$ENTRA_DOMAIN" \
  "AzureAd__TokenValidationParameters__ValidAudiences__0=$ENTRA_APP_ID" \
  "RequireAuthentication=true" \
  "Github__ClientId=$GITHUB_APP_ID" \
  "Github__PrivateKey=@Microsoft.KeyVault(VaultName=$KEYVAULT_NAME;SecretName=$SECRET_NAME)"
```

That's it! Your GitHub App Token Server is now deployed to Azure with:

- Azure Key Vault for secure storage of GitHub private key
- System-assigned managed identity for secure access to Key Vault
- Proper configuration for Entra ID authentication
- GitHub App settings correctly configured

You can verify your deployment is working by navigating to the `/version` endpoint of your app.

### Environment Variables

When deploying to Azure, set the following environment variables in the App Service configuration:

1. **GitHub App Configuration**

   - `Github__ClientId`: Your GitHub App ID
   - `Github__PrivateKey`: Your GitHub App private key (preferably as a base64-encoded string). 
     It can be a Key Vault reference using the usual `@Microsoft.KeyVault()` syntax.

2. **Authentication** (if enabled)

   - For _Entra ID__:
     - `AzureAd__TenantId`
     - `AzureAd__Domain`
     - `AzureAd__ClientId`
     - `AzureAd__TokenValidationParameters__ValidAudiences__0`, `AzureAd__TokenValidationParameters__ValidAudiences__1`, ...

   - For _OpenID_:
     - `OpenId__Authority` or , `OpenId__MetadataAddress`
     - `OpenId__Audience`

   - For _generic JWT__:
     - `JWT__TokenValidationParameters__IssuerSigningKeys`
     - `JWT__TokenValidationParameters__ValidIssuer`
     - `JWT__TokenValidationParameters__ValidAudience`

3. **General Configuration**

   - `RequireAuthentication`: "true" or "false"
   - `MapOpenApi`: "true" or "false"

### Using Azure Key Vault

For secure management of sensitive configuration:

1. Create an Azure Key Vault
2. Store your GitHub private key as a secret
3. Configure your App Service identity to access Key Vault
4. Reference the key vault in your configuration:

```json
"Github": {
  "ClientId": "your-app-id",
  "PrivateKey": "@Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/github-private-key/)"
}
```

## Cache Configuration

The service uses a distributed memory cache by default. To use Redis or another distributed cache, replace the registration in `Program.cs`:

```csharp
// Default in-memory cache
builder.Services.AddDistributedMemoryCache();

// Redis cache example
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "your-redis-connection-string";
    options.InstanceName = "GithubAppToken";
});
```

## Security Considerations

- Store sensitive data like private keys securely using Key Vault or similar
- In production, always enable authentication
- Review GitHub App permissions to minimize access scope

## License

[MIT License](LICENSE)