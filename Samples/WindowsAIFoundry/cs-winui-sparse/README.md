---
page_type: sample
languages:
- csharp
products:
- windows
- windows-app-sdk
name: cs-winui-sparse
description: Shows how to integrate the Windows AI APIs in a sparse WinUI package
urlFragment: cs-winui-sparse
extendedZipContent:
- path: LICENSE
  target: LICENSE
---

# Windows AI Sparse Package Sample for WinUI

## Prerequisites
- For system requirements, see [System requirements for Windows app development](https://docs.microsoft.com/windows/apps/windows-app-sdk/system-requirements).
- To ensure your development environment is set up correctly, see [Install tools for developing apps for Windows 10 and Windows 11](https://docs.microsoft.com/windows/apps/windows-app-sdk/set-up-your-development-environment).
- Running this sample does require a [Windows Copilot+ PC](https://learn.microsoft.com/windows/ai/npu-devices/)
- Running this sample also requires that the [Windows App SDK 1.8 Experimental2](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads#windows-app-sdk-18-experimental) framework package is installed on your Copilot+ PC.

### Suggested Environment

Use "Developer Command Prompt for VS2022" as your command prompt environment

## Build and Run the sample

This folder contains the cs-winui-sparse sample which uses sparse package built on top of the WinUI
platform using Windows AI APIs. BuildSparsePackage.ps1 is provided for the user which closely follows the steps
from the link below

https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps#add-the-package-identity-metadata-to-your-desktop-application-manifest

BuildSparsePackage.ps1 does the following:
1) Build the solution in the desired platform and configuration provided in the parameters
 
2) Run MakeAppx with the /nv option on the folder containing the AppxManifest
    (cs-winui-sparse\WinUI\AppxManifest.xml) 
    
    - The /nv flag is required to bypass validation of referenced file paths in the manifest. 
    
    - The output folder is set to the output of the binaries when we build the solution regularly
    
3) Add publisher information to the MSIX package using SignTool
    
    - The first step uses the makecert tool and pvk2pfx tool to generate a self-signed certificate
    
    - SignTool is then used to add the publisher information to the MSIX package
    
    - This step can be skipped if a signing certificate is already available (i.e. -SkipCertGen flag)

#### Running the sample with BuildSparsePackage.cmd

1. Open the "Developer Command Prompt for VS2022" command window
2. Navigate to the cs-winui-sparse directory from this command window
3. Run the BuildSparsePackage.cmd script with the parameters
    - arm64 Release (default)
    - To use debug, run "BuildSparsePackage.cmd arm64 Debug"
    - To clean, run "BuildSparsePackage.cmd arm64 Release -Clean"

## Sample Overview

The `MainWindow` class in `MainWindow.xaml.cs` is the main user interface for the Windows AI APIs Sample application. It demonstrates how to use the Windows AI APIs with WinUI in an unpackaged app. The key functionalities include:

- **Select File**: Allows the user to select an image file from their file system and displays the selected image.
- **Process Image**: Processes the selected image using Windows AI APIs.

### Sparse Packaging

A sparse package allows unpackaged win32 apps to gain package identity without fully
adopting the MSIX packaging format. This is particularly useful for applications that are not yet
ready to have all their content inside an MSIX package but still need to use Windows extensibility
features that require package identity.

The sparse package concept differs from a full MSIX package in that it contains just enough
information to grant package identity to the application, but does not store the application binaries
inside the package. Instead, the app continues to run from its original file location with all the
benefits of package identity.

### Initialization code

Just doing the steps above does not yet grant the app package identity. The MSIX package still needs to
be registered with the external location. 

The RegisterSparsePackage() function in cs-winui-sparse\WinUI\App.xaml.cs does this for us. This
process is described in the documentation linked above. This will give the app process identity if
ran from the start menu. However, directly running the executable in the external location still
does not give the process package identity. This difference in behavior is not noted in the article
from above.

To allow the resulting app process from directly running the executable to gain identity requires
this workaround. 

The Startup() code has two different codepaths for whether the process is a packaged or a unpackaged
process (With or without identity).

If without Identity, RegisterSparsePackage() will be called which registers the package on the
external path if it hasn't been installed already. Afterwards, RunWithIdentity() will call
ActivateApplication on the package identity it registered earlier using the ActivationManager.

This will spawn a packaged process of itself which will have identity. The non-identity process will
then exit. The new packaged process in the startup will hit the codepath for when IsPackageProcess()
is true and launch MainWindow. 

Note: the first run of the exe run will require a longer startup time because of the time that is
needed to register the package. 

### Additional Notes

- Windows AI APIs require package identity to function properly. This sample demonstrates how to use a sparse package to provide package identity to a WinUI app.
- The sparse package approach is a good solution for applications that want to maintain their traditional deployment and update methods while still being able to use Windows platform features that require package identity.

## Related Links
- [Windows AI APIs Overview](https://learn.microsoft.com/windows/ai/apis/)
- [WPF Sparse Package Sample (with Windows AI APIs)](https://github.com/microsoft/WindowsAppSDK-Samples/tree/release/experimental/Samples/WindowsAIFoundry/cs-wpf-sparse)
- [WinForms Sparse Package Sample (with Windows AI APIs)](https://github.com/microsoft/WindowsAppSDK-Samples/tree/release/experimental/Samples/WindowsAIFoundry/cs-winforms-sparse)