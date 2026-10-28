# Using Windows AI Foundry in C++ with CMake

Build and run this C++ console sample to try image and video super-resolution, image text recognition (OCR), and text generation with the Windows App SDK and [Windows AI Foundry](https://developer.microsoft.com/windows/ai/) APIs.

> [!NOTE]
> This sample targets `Microsoft.WindowsAppSDK` **2.6.4**. The sparse package depends on the matching **Windows App Runtime 2** framework package (`Microsoft.WindowsAppRuntime.2`, minimum version `2.6.4.0`).

## Prerequisites

1. A [Windows Copilot+ PC](https://learn.microsoft.com/windows/ai/npu-devices/) running an up-to-date Windows 11 installation.
2. Install [Visual Studio](https://visualstudio.microsoft.com/downloads/): **Visual Studio 2022 with the latest 17.14 updates**, or **Visual Studio 2026** (stable). The examples below use Visual Studio 2022, the minimum supported major version. In Visual Studio Installer, select **Desktop development with C++**, including **C++ CMake tools for Windows** and a Windows SDK. For ARM64 builds, also install the MSVC ARM64 build tools.
3. Set up [vcpkg](https://learn.microsoft.com/vcpkg/get_started/get-started), including its bootstrap step. Set the `VCPKG_ROOT` environment variable to your vcpkg checkout before opening Visual Studio or configuring CMake.
4. Install [CMake](https://cmake.org/download/) if it is not already available in your developer shell: `winget install --id Kitware.CMake --exact`.
5. Install **Windows App Runtime 2.6.4** for your machine's architecture from the [Windows App SDK downloads page](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads). Verify it before continuing:

   ```powershell
   Get-AppxPackage Microsoft.WindowsAppRuntime.2 |
     Select-Object Name, Version, Architecture, PackageFullName
   ```

   The output must include version **2.6.4.0 or later** for your machine's architecture.

6. Clone this repository and open its root directory. The sample is in `Samples\WindowsAIFoundry\cpp-console-sparse`.

To install Community edition, choose **one** of these commands:

| Version | Windows PowerShell command |
| --- | --- |
| Visual Studio 2022 | `winget install --id Microsoft.VisualStudio.2022.Community --exact --source winget` |
| Visual Studio 2026 | `winget install --id Microsoft.VisualStudio.Community --exact --source winget` |

## Build

The following steps use Visual Studio 2022. Visual Studio 2026 uses the same folder and CMake presets. No manifest or CMake changes are needed to try this sample.

For example, if you installed vcpkg at `C:\src\vcpkg`, set this in the PowerShell session used to launch Visual Studio or build the sample (replace the path with your checkout):

```powershell
$env:VCPKG_ROOT = "C:\src\vcpkg"
```

Open this sample directory (`Samples\WindowsAIFoundry\cpp-console-sparse`) in Visual Studio 2022 with _File > Open > Folder_.

Change the target build type to match your test machine's architecture:

- `arm64-debug` for Snapdragon / ARM64 Copilot+ PCs
- `x64-debug` for x64 machines

Use _Build > Build all_ - this pulls down Windows App SDK `2.6.4` and its transitive dependencies through the local `vcpkg_ports/windowsappsdk` overlay.

Alternatively, open **Developer PowerShell for VS 2022** with the compiler environment targeting your chosen architecture. From the repository root, build the sample with:

```powershell
Set-Location .\Samples\WindowsAIFoundry\cpp-console-sparse
cmake --preset x64-debug
cmake --build .\out\build\x64-debug
```

For an ARM64 target, use an ARM64 compiler environment and replace `x64-debug` with `arm64-debug`. Stop if configuration or build fails.

All commands below run from the sample directory.

## Verify package registration

Windows AI APIs require package identity. The build automatically registers `AppxManifest.xml` as a **sparse package**, using the build output folder as its external location. No manifest editing or separate packaging step is needed.

To confirm the sparse package is registered:

```powershell
Get-AppxPackage *WindowsAISampleForCppCMakeSparse* |
  Select-Object Name, Version, PackageFullName
```

The output should show `WindowsAISampleForCppCMakeSparse` version `1.0.0.1`. If registration failed, see [Troubleshooting](#troubleshooting).

## Run

Run any of the following examples from the sample directory after registration. Replace the image/video paths with your own files. For ARM64 builds, replace `x64-debug` with `arm64-debug`.

The first use of a feature may download its AI components and take longer. Scaling writes files next to the input and replaces existing files with the same output names. Video output has no audio; use a constant-frame-rate clip whose scaled output stays within 480p–1440p (for example, 640x360 input with `--scale 2`).

```powershell
# Image super-resolution
.\out\build\x64-debug\cmake-ai-generator.exe --image "C:\path\to\photo.jpg" --scale 2

# Image text recognition (OCR)
.\out\build\x64-debug\cmake-ai-generator.exe --image "C:\path\to\document.png" --ocr

# Video super-resolution
.\out\build\x64-debug\cmake-ai-generator.exe --video "C:\path\to\clip.mp4" --scale 2

# Text generation
.\out\build\x64-debug\cmake-ai-generator.exe "tell me a dragon story"

# Text generation with incremental output
.\out\build\x64-debug\cmake-ai-generator.exe --progress "tell me a dragon story"
```

| Mode | Result |
| --- | --- |
| Image super-resolution | Writes `<name>_x2<ext>` next to the input. |
| OCR | Prints text recognized in the original image; does not create an image. |
| Video super-resolution | Writes `<name>_vsr.mp4` next to the input. |
| Text generation | Prints a generated story; `--progress` also displays incremental output. |

Video color-range handling is experimental. See [Video Super Resolution mode](#video-super-resolution-mode-video) for limitations.

To debug in Visual Studio, select **Debug > Other Debug Targets > Debug Installed App Package...**, choose **WindowsAISampleForCppCMakeSparse**, and click Start.

## Console modes

The examples in [Run](#run) cover all four modes. The details below explain their APIs, options, and limits.

### Image Scaler mode (Image)

`Microsoft.Windows.AI.Imaging.ImageScaler` upscales an image selected with `--image`. Use `--scale` to choose an integer scale factor.

The upscaled image is written next to the input as `<name>_x<factor><ext>`. `--scale` defaults to `4` and must not exceed `ImageScaler::MaxSupportedScaleFactor`.

Image mode prints a `[Step N/6]` header to identify the failing stage. See [Image Super Resolution with Windows AI APIs](https://learn.microsoft.com/windows/ai/apis/image-super-resolution) for API requirements and supported scaling.

> [!NOTE]
> Some all-white image inputs fail with HRESULT `0x8A1F0209` on Windows App Runtime 2.6.4. Use a photo or another nonblank image to try super-resolution. Blank-image OCR is unaffected and reports no detected text.

### Image text recognition mode (OCR)

`Microsoft.Windows.AI.Imaging.TextRecognizer` recognizes text in the **original input image** when `--image` is combined with `--ocr`.

This mode prepares the OCR model, decodes the image to a BGRA8 `SoftwareBitmap`, wraps it in an `ImageBuffer`, and calls `RecognizeTextFromImage`. Each recognized line is printed to the console; images without detected text report that explicitly. It does not create an output image or require the ImageScaler model. `--scale` has no effect in OCR mode. `--ocr` requires `--image` and cannot be combined with `--video`.

OCR requires a supported Copilot+ PC and the TextRecognizer AI component. Model-readiness and recognition failures are reported with a nonzero exit code.

See [Text recognition with Windows AI APIs](https://learn.microsoft.com/windows/ai/apis/text-recognition).

### Video Super Resolution mode (Video)

`Microsoft.Windows.AI.Video.VideoScaler` upscales frames from the file selected with `--video`. The enhanced video is written next to it as `<name>_vsr.mp4`.

VideoScaler operates on video **frames** (Direct3D surfaces). The pipeline is implemented in [`video_file_pipeline.cpp`](./video_file_pipeline.cpp):

1. **Media Foundation** decodes the input to NV12 frames on a Direct3D 11 device.
2. `VideoScaler.Scale(IDirect3DSurface, IDirect3DSurface, VideoScalerOptions)` upscales each frame.
3. The upscaled BGRA frames are encoded back to an H.264 `.mp4` with Media Foundation.

Notes:

- This is a **video-only** demonstration: audio is not copied to the output file.
- Use constant-frame-rate clips for this demonstration. Output timestamps are regenerated at the negotiated frame rate; variable-frame-rate source timing is not preserved.
- Before creating the scaler the sample calls `ExecutionProviderCatalog.GetDefault().EnsureAndRegisterCertifiedAsync()` to register certified execution providers.
- VSR supports input resolutions 240p–1440p and output resolutions 480p–1440p; it is tuned for video of people/faces. See the docs for the current supported ranges.
- Choose `--scale` so the output stays within those limits; the default `4` is not valid for every input resolution.
- Color-range handling (`--range auto|full|limited`) is experimental. Validate output contrast and color for your content before relying on it.

For more detail, see:

- [Get Started with AI Video Super Resolution (VSR) in the Windows App SDK](https://learn.microsoft.com/en-us/windows/ai/apis/video-super-resolution)
- [VideoScaler class (Microsoft.Windows.AI.Video)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.ai.video.videoscaler)
- [ExecutionProviderCatalog](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.ai.machinelearning.executionprovidercatalog)
- [Media Foundation source reader](https://learn.microsoft.com/en-us/windows/win32/medfound/source-reader) and [sink writer](https://learn.microsoft.com/en-us/windows/win32/medfound/sink-writer) used by the decode/encode pipeline

### Storyteller mode (Text)

`Microsoft.Windows.AI.Text.LanguageModel` generates a short story from the supplied prompt. Use `--progress` to display incremental output.

`LanguageModel.EnsureReadyAsync()` downloads and installs required language-model components when needed.

For more detail, see:

- [Get started with Phi Silica in the Windows App SDK](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica)
- [LanguageModel.EnsureReadyAsync](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.ai.text.languagemodel.ensurereadyasync?view=windows-app-sdk-2.0)

## Troubleshooting

For activation or access failures, check that the runtime from Prerequisites is installed, the sample's sparse package is registered, and the executable is in the build output directory registered as `-ExternalLocation`.

| Diagnostic | What to check |
| --- | --- |
| `0x80040154` (`REGDB_E_CLASSNOTREG`) | Required runtime components may be missing from the package graph. Verify the installed runtime and sparse-package registration. |
| `0x80070005` (`E_ACCESSDENIED`) | Verify package identity and the `systemAIModels` capability in the manifest. |
| `0x80073D06` | A newer package version is already installed. Do not try to downgrade it; use a compatible installed runtime. |
| `0x80004005` (`E_FAIL`) | This is a general failure. Review the full diagnostic and verify the manifest and AI-component readiness. |
| Model-readiness failure | Check **Settings > System > AI Components**, Windows Update, and that the device supports the requested feature. |

See [Windows AI API troubleshooting](https://learn.microsoft.com/windows/ai/apis/troubleshooting) for platform guidance.

### Register manually

The build normally registers the sparse package automatically. If you need to register it manually, run this from the sample directory:

```powershell
Add-AppxPackage `
  -Path .\out\build\x64-debug\AppxManifest.xml `
  -ExternalLocation .\out\build\x64-debug `
  -Register `
  -ForceUpdateFromAnyVersion
```

Replace `x64-debug` with `arm64-debug` if you built the ARM64 preset.

### Cleanup

Only when you are finished running the sample, remove its registration with:

```powershell
Get-AppxPackage *WindowsAISampleForCppCMakeSparse* | Remove-AppxPackage
```

Learn more:

- [Grant package identity by packaging with external location manually](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- [Add-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage?view=windowsserver2025-ps) (the `-Register` and `-ExternalLocation` options used here)

## Using the APIs in your own app

The following steps are optional. Complete the sample's build-and-run walkthrough first; none of these edits is required to run it unchanged.

Copy the content of `vcpkg_ports` into your build tree for windowsappsdk.

Update your `vcpkg.json` to include the `windowsappsdk` package ([example](./vcpkg.json)):

```json
{
    "dependencies": ["wil", "windowsappsdk"]
}
```

Update your `vcpkg-configuration.json` to include the vcpkg port overlays you copied:

```json
{
    "overlay-ports": ["vcpkg_ports"]
}
```

In your `CMakeLists.txt` add:

```cmake
find_package(windowsappsdk CONFIG REQUIRED)
```

Then add the `windowsappsdk` target to your target's dependencies:

```cmake
target_link_libraries(
    your_target
    PRIVATE
        Microsoft::WindowsAppSdk
)
```

### Package identity and runtime dependency

Windows AI APIs require package identity. The sample's [AppxManifest.xml](./AppxManifest.xml) already provides it. For your own app, use your own identity and declare the appropriate runtime dependency:

```xml
<Package>
  <Identity
      Name="YourApp"
      Publisher="CN=YourPublisher"
      Version="1.0.0.0" />
  <Dependencies>
    <PackageDependency
        Name="Microsoft.WindowsAppRuntime.2"
        Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"
        MinVersion="2.6.4.0" />
  </Dependencies>
</Package>
```

This is a manifest fragment, not a complete manifest. Retain the required `systemAIModels` capability and other declarations shown in the sample. Update the dependency `Name` and `MinVersion` if you target a different Windows App Runtime line.

Learn more:

- [Grant package identity by packaging with external location](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- [Packaging overview - Windows apps](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)
- [Windows App SDK deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)
