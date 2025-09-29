# GitHub App Token Server

A lightweight ASP.NET Core service that facilitates obtaining authentication tokens for GitHub App installations. This service makes it easy to integrate GitHub App authentication into your CI/CD pipelines or other applications.

## Overview

This service provides a simple REST API for:

1. Generating JWT tokens for GitHub App authentication
2. Retrieving GitHub App installation information
3. Obtaining installation access tokens by organization name or installation ID

It uses an in-memory cache to store installation information, allowing for faster token retrieval with organization names.

For in-depth details, refer to the [GitHub App Token Server Architecture and Deployment Guide](docs/token-server.md).

## API Endpoints

All endpoints can be protected with authentication (JWT Bearer) which can be toggled on/off.

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/healthz` | GET | Health check endpoint | 200 OK if healthy |
| `/version` | GET | Returns version information | JSON with version details |
| `/jwt` | GET | Returns a JWT token for GitHub API authentication. Tokens last 1h. | Text/plain JWT token |
| `/app` | GET | Retrieves information about the current Github App | JSON |
| `/installations` | GET | Lists all GitHub App installations | JSON array of installations |
| `/installations/{org}/token` | GET | Gets an access token for an installation by org name or ID | Text/plain access token |

## Easy Deployment on Azure with Azure Developer CLI (azd)

### Prerequisites

1. [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) installed
2. [GitHub CLI](https://cli.github.com/) installed and authenticated. Run `az login` to authenticate to Azure.
3. Powershell 7 installed on [Windows](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-windows?view=powershell-7.5) or Linux [Linux](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.5) or MacOS [MacOS](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-macos?view=powershell-7.5).
4. An Azure subscription to deploy resources into.
5. The executing user or service principal must have permissions Owner permissions on the subscription and must be Global Administrator or Application Administrator in the Entra ID tenant.
6. GitHub App created with private key downloaded and installed in your Github organization(s)
7. The list of Managed Identity names that will need access to the GitHub App, for example System Assigned identities of Azure VMs or App Services that will call this service or ARC server names.

### Deployment Steps

1. **Clone the repository**:

   ```bash
   git clone [your-repo-url]
   cd [your-repo-name]
   ```

2. **Create your azd environment**:

   ```bash
   azd env new [your-environment-name]
   ```

   The environment name will be used to create resource names, so it should be unique within your Azure subscription.  
   The environment name will be used to name the Resource Group which will contain all the resources created by `azd` according to the pattern: `rg-GithubGetToken-<your-environment-name>`.

3. **Initialize the Azure Developer CLI environment**:

   ```bash
   azd init
   ```

   In this step, you may be prompted to select an Azure subscription and region.  
   If you've never used `azd` before, it may also prompt you to log in to your Azure account.

4. **Set required parameters**:

   ```bash
   # GitHub App configuration
   azd env set GITHUB_APP_CLIENT_ID "your-github-app-client-id"
   azd env set GITHUB_APP_NAME "your-github-app-name"  # The name of your GitHub App
   azd env set GITHUB_PRIVATE_KEY_FILE "/path/to/your-github-app-private-key.pem"
   
   # Entra ID/authentication configuration
   azd env set ENTRA_DOMAIN "your-domain.com"   # The domain of your Entra ID tenant
   
   # Managed Identity names that will need access to the GitHub App
   azd env set RUNNER_MANAGED_IDENTITY_NAMES "mi-name-1,mi-name-2,..."  # Comma-separated list of Managed Identity names that will need access to the GitHub App. Don't put spaces between names.
   
   ```

   > *NOTE*: the following parameters are optional and have defaults. Refer to the [Token Server documentation](docs/token-server.md) for details: 
   > `AZURE_APP_SERVICE_PLAN_SKU_NAME`, `REQUIRE_AUTHENTICATION`, `MAP_OPENAPI`

5. **Deploy to Azure**:

   ```bash
   azd up
   ```

   At completion, the CLI will output the URL of your deployed web app. Then run:

   ```bash
   azd env get APP_REGISTRATION_APPLICATION_ID_URI
   ```

   to get the *Resource URI_ to be used in the Managed Identity token requests.

   This command will create all the necessary resources in Azure, including a Resource Group, App Service Plan, Web App, Key Vault, _and Entra ID Application_. It will also configure the Web App to use a Managed Identity and set up access policies in Key Vault.
    
   The App Registration in Entra ID will be created with name `Github-{GITHUB_APP_NAME}` and Application ID Uri `api://{ENTRA_DOMAIN}/GithubApp/{GITHUB_APP_NAME}`.

### Updates after first deployment

If you need to make changes to the application or infrastructure [in particular if new Managed Identities need to be onboarded], simply modify the code or Bicep files and re-run `azd up`. The CLI will detect changes and apply them accordingly.

## Use of the Service

The typical use of this service is to call the `/installations/{org}/token` endpoint to get an installation access token for a specific organization. This token can then be used to authenticate API requests to GitHub on behalf of the installation or as Git password (use `x-access-token` as username) to clone git repositories belonging to the specified Github organization.  
The access token to pass to the `/installations/{org}/token` will be typically retrieved using Managed Identity endpoints (see section below).

### In an Azure VM or App Service

```bash
RESOURCE="[APP_REGISTRATION_APPLICATION_ID_URI]" # Get this value using `azd env get APP_REGISTRATION_APPLICATION_ID_URI`
TOKEN=$(curl "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=$RESOURCE" -H Metadata:true | jq .access_token -r)
GH_TOKEN=$(curl -H "Authorization: Bearer $TOKEN" "https://[your-app-name].azurewebsites.net/installations/[org]/token")

git clone https://x-access-token:$GH_TOKEN@github.com/[org]/[repo].git
```

More details on [Azure Documentation](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/how-managed-identities-work-vm).

### In an Azure Arc connected machine

```bash
RESOURCE="[APP_REGISTRATION_APPLICATION_ID_URI]" # Get this value using `azd env get APP_REGISTRATION_APPLICATION_ID_URI`
TOKEN_URL="http://localhost:40342/metadata/identity/oauth2/token?resource=${RESOURCE}&api-version=2020-06-01"
AUTH_FILE=$(curl -s -D - -H "Metadata: True" $TOKEN_URL -o /dev/null | grep -i "Www-Authenticate: " | cut -f2 -d"=" | tr -d '\r')
AUTH=$(cat $AUTH_FILE)
TOKEN=$(curl -s -H "Authorization: Basic $AUTH" -H "Metadata: True" $TOKEN_URL | jq .access_token -r)
GH_TOKEN=$(curl -H "Authorization: Bearer $TOKEN" "https://[your-app-name].azurewebsites.net/installations/[org]/token")

git clone https://x-access-token:$GH_TOKEN@github.com/[org]/[repo].git

```

The user running those commands must be part of the `himds` group on the Arc connected machine. To add a user to the `himds` group, run:

```bash
sudo usermod -aG himds <your-username>
```

More details on [Azure Documentation](https://learn.microsoft.com/en-us/azure/azure-arc/servers/managed-identity-authentication).
