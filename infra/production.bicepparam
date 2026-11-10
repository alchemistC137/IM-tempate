using './main.bicep'

// CI-only defaults. CD requires explicit resource names for each installation.
param location = 'spaincentral'
param appName = readEnvironmentVariable('AZURE_APP_NAME', 'intranet-example')
param planName = readEnvironmentVariable('AZURE_PLAN_NAME', 'plan-intranet-example')
param keyVaultName = readEnvironmentVariable('AZURE_KEY_VAULT_NAME', 'kv-intranet-example')
param applicationName = readEnvironmentVariable('INTRANET_APPLICATION_NAME', 'Intranet')
param companyName = readEnvironmentVariable('INTRANET_COMPANY_NAME', 'Mi empresa')
param planSku = 'B1'
// Overridden by CD with the commit that passed CI; never deploy this placeholder.
param imageTag = 'bootstrap'
