// Infrastructure for the wedding booking system: an API on App Service and a
// React frontend on Static Web Apps. Deploy with infra/deploy.sh.
//
// Nothing secret lives in this file. The database connection string is a secure
// parameter supplied at deploy time and stored only in App Service settings.

@description('Base name. Resources get this plus a suffix.')
param navn string = 'weddingbooking'

@description('Where the API runs. northeurope is Ireland, next to the Supabase project in eu-west-1.')
param location string = 'northeurope'

@description('Static Web Apps is only available in a handful of regions.')
@allowed(['westeurope', 'eastus2', 'centralus', 'westus2', 'eastasia'])
param frontendLocation string = 'westeurope'

@description('F1 is free but sleeps and is capped at 60 CPU-minutes a day. B1 stays awake.')
@allowed(['F1', 'B1', 'S1'])
param appServiceSku string = 'B1'

@description('Npgsql connection string for Supabase. Never committed; passed at deploy time.')
@secure()
param postgresConnectionString string

@description('Supabase project URL. Public by design — it appears in every browser request.')
param supabaseUrl string

var suffix = uniqueString(resourceGroup().id)
var apiName = '${navn}-api-${suffix}'
var frontendName = '${navn}-web-${suffix}'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${navn}-plan'
  location: location
  sku: {
    name: appServiceSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource frontend 'Microsoft.Web/staticSites@2023-12-01' = {
  name: frontendName
  location: frontendLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: apiName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServiceSku != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // Double underscore is how App Service spells nested configuration.
          name: 'ConnectionStrings__Postgres'
          value: postgresConnectionString
        }
        {
          name: 'Supabase__Url'
          value: supabaseUrl
        }
        {
          name: 'Supabase__Audience'
          value: 'authenticated'
        }
        {
          // The frontend's own address. Bicep resolves the dependency, so the
          // API knows the hostname even though the site is created alongside it.
          name: 'Cors__AllowedOrigins__0'
          value: 'https://${frontend.properties.defaultHostname}'
        }
        {
          name: 'Integrations__Economic'
          value: 'Fake'
        }
        {
          name: 'Integrations__Trello'
          value: 'Fake'
        }
        {
          name: 'Integrations__Mail'
          value: 'Fake'
        }
        {
          name: 'Integrations__Summarisation'
          value: 'Fake'
        }
      ]
    }
  }
}

output apiName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output frontendName string = frontend.name
output frontendUrl string = 'https://${frontend.properties.defaultHostname}'
