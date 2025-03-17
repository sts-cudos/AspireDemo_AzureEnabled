# AspireWalkthrough.AppHost Setup Guide

This guide will walk you through the steps to set up and run the AspireWalkthrough.AppHost application with Azure integration.

## Steps

1. **Navigate to the AppHost directory:**
    ```sh
    cd AspireWalkthrough.AppHost
    ```

2. **Create a directory for Azure output:**
    ```sh
    mkdir Azure
    ```

3. **Run the application to generate the manifest file:**
    ```sh
    dotnet run --publisher manifest --output-path Azure/manifest.json
    ```

4. **Authenticate with Azure using Azure Developer CLI (azd):**
    ```sh
    azd auth login
    ```


5. **Enable infrastructure synthesis in Azure Developer CLI:**
    Note: This step may be required due to organizational policies.

    ```sh
    azd config set alpha.infraSynth on
    azd infra synth
    ```

    After running this command, add the following tag to the `main.bicep` file:
    ```bicep
    tags: {
      owner: 'Simei Steiner'
    }
    ```

6. **Initialize the Azure Developer CLI project:**
    ```sh
    azd init
    ```

7. **Deploy the infrastructure and application:**
    ```sh
    azd up
    ```

    or 
    ```sh
    azd provision
    azd deploy
    ```