using './main.bicep'

// Target resource group: rg-membyte-main. Do not reuse the development resources.
param location = 'spaincentral'
param appName = 'membyte-im-main'
param planName = 'plan-membyte-main'
param keyVaultName = 'kv-membyte-main'
param planSku = 'B1'
// Overridden by CD with the commit that passed CI; never deploy this placeholder.
param imageTag = 'bootstrap'
