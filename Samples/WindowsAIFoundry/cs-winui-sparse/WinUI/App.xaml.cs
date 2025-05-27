// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Management.Deployment;
using WinRT.Interop;

namespace WindowsAISample
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow? Window { get; private set; }
        public static IntPtr WindowHandle { get; private set; }

        // All the interpolated native calls and DLLImports go here
        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Out] char[]? packageFullName);
            
            public enum ActivateOptions
            {
                None = 0x00000000,  // No flags set
                DesignMode = 0x00000001,  // The application is being activated for design mode
                NoErrorUI = 0x00000002,   // Do not show an error dialog if the app fails to activate                                
                NoSplashScreen = 0x00000004,  // Do not show the splash screen when activating the app
            }

            public const string CLSID_ApplicationActivationManager_String = "45ba127d-10a8-46ea-8ab7-56ea9078943c";
            public const string CLSID_IApplicationActivationManager_String = "2e941141-7f97-4756-ba1d-9decde894a3d";

            public static readonly Guid CLSID_ApplicationActivationManager = new Guid(CLSID_ApplicationActivationManager_String);
            public static readonly Guid CLSID_IApplicationActivationManager = new Guid(CLSID_IApplicationActivationManager_String);

            [ComImport, Guid(CLSID_IApplicationActivationManager_String), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface IApplicationActivationManager
            {
                // Activates the specified immersive application for the "Launch" contract, passing the provided arguments
                // string into the application.  Callers can obtain the process Id of the application instance fulfilling this contract.
                IntPtr ActivateApplication([In] String appUserModelId, [In] String? arguments, [In] ActivateOptions options, [Out] out UInt32 processId);
                IntPtr ActivateForFile([In] String appUserModelId, [In] IntPtr /*IShellItemArray* */ itemArray, [In] String verb, [Out] out UInt32 processId);
                IntPtr ActivateForProtocol([In] String appUserModelId, [In] IntPtr /* IShellItemArray* */itemArray, [Out] out UInt32 processId);
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
            public static extern UInt32 CoCreateInstance(
                [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
                IntPtr pUnkOuter,
                CLSCTX dwClsContext,
                [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
                [MarshalAs(UnmanagedType.IUnknown)] out object rReturnedComObject);

            [Flags]
            public enum CLSCTX : uint
            {
                CLSCTX_INPROC_SERVER = 0x1,
                CLSCTX_INPROC_HANDLER = 0x2,
                CLSCTX_LOCAL_SERVER = 0x4,
                CLSCTX_INPROC_SERVER16 = 0x8,
                CLSCTX_REMOTE_SERVER = 0x10,
                CLSCTX_INPROC_HANDLER16 = 0x20,
                CLSCTX_RESERVED1 = 0x40,
                CLSCTX_RESERVED2 = 0x80,
                CLSCTX_RESERVED3 = 0x100,
                CLSCTX_RESERVED4 = 0x200,
                CLSCTX_NO_CODE_DOWNLOAD = 0x400,
                CLSCTX_RESERVED5 = 0x800,
                CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
                CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
                CLSCTX_NO_FAILURE_LOG = 0x4000,
                CLSCTX_DISABLE_AAA = 0x8000,
                CLSCTX_ENABLE_AAA = 0x10000,
                CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
                CLSCTX_ACTIVATE_32_BIT_SERVER = 0x40000,
                CLSCTX_ACTIVATE_64_BIT_SERVER = 0x80000,
                CLSCTX_INPROC = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER,
                CLSCTX_SERVER = CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER,
                CLSCTX_ALL = CLSCTX_SERVER | CLSCTX_INPROC_HANDLER
            }
        }

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            
            // Add global exception handler
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Log unhandled exceptions
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Message}, {e.Exception}");
            e.Handled = true; // Mark as handled to prevent app crash
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // Check if we're running with package identity
                bool hasPackageIdentity = IsPackagedProcess();
                
                if (!hasPackageIdentity)
                {
                    // Try to register the sparse package, but don't exit if it fails
                    try
                    {
                        await RegisterSparsePackage();
                        // Only try to restart with identity if package registration succeeded
                        RunWithIdentity();
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue without package identity
                        System.Diagnostics.Debug.WriteLine($"Failed to register or activate with package identity: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine("Continuing to run without package identity...");
                    }
                }
                
                // Initialize the main window regardless of package identity status
                Window = new MainWindow();
                Window.Activate();
                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(Window);
            }
            catch (Exception ex)
            {
                // Log any exceptions during launch
                System.Diagnostics.Debug.WriteLine($"Exception during launch: {ex.Message}");
                
                // Try to show the main window even if there was an error
                try
                {
                    Window = new MainWindow();
                    Window.Activate();
                    WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(Window);
                }
                catch
                {
                    // If we can't even show the main window, rethrow the original exception
                    throw;
                }
            }
        }

        private async Task RegisterSparsePackage()
        {
            try
            {
                // We expect the MSIX to be in the same directory as the exe. 
                string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string externalLocation = exePath;
                string sparsePkgPath = Path.Combine(exePath, "WindowsAISampleForWinUISparse.msix");

                // Check if MSIX file exists
                if (!File.Exists(sparsePkgPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Sparse package not found at: {sparsePkgPath}");
                    return; // Exit without throwing exception
                }

                Uri externalUri = new Uri(externalLocation);
                Uri packageUri = new Uri(sparsePkgPath);

                PackageManager packageManager = new PackageManager();
                
                // Check if package is already registered
                var packages = packageManager.FindPackagesForUser("", "WindowsAISampleForWinUISparse_k0t3h69cz9sxw");
                int count = 0;
                foreach (var package in packages)
                {
                    count++;
                }
                
                if (count == 0)
                {
                    //Declare use of an external location
                    var options = new AddPackageOptions();
                    options.ExternalLocationUri = externalUri;

                    var deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);
                    var deploymentResult = await deploymentOperation;

                    if (deploymentOperation.Status != AsyncStatus.Completed)
                    {
                        if (deploymentOperation.Status == AsyncStatus.Error && deploymentResult != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Package registration failed: {deploymentResult.ExtendedErrorCode}: {deploymentResult.ErrorText}");
                            return; // Exit without throwing exception
                        }
                        System.Diagnostics.Debug.WriteLine("Package did not register");
                        return; // Exit without throwing exception
                    }
                    
                    System.Diagnostics.Debug.WriteLine("Package registered successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Package already registered");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering sparse package: {ex.Message}");
                // We rethrow the exception to be handled by the caller
                throw;
            }
        }

        private void RunWithIdentity()
        {
            try
            {
                // Activating the packaged process
                // We should already know our AUMID which depends on the AppxManifest we defined so this can be hardcoded here. 
                string appUserModelId = "WindowsAISampleForWinUISparse_k0t3h69cz9sxw!App";
                
                uint hr = NativeMethods.CoCreateInstance(
                    NativeMethods.CLSID_ApplicationActivationManager,
                    IntPtr.Zero,
                    NativeMethods.CLSCTX.CLSCTX_LOCAL_SERVER,
                    NativeMethods.CLSID_IApplicationActivationManager,
                    out object applicationActivationManagerAsObject);
                    
                if (hr != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create ApplicationActivationManager! HRESULT: 0x{hr:X8}");
                    return; // Exit without throwing exception
                }
                
                var applicationActivationManager = (NativeMethods.IApplicationActivationManager)applicationActivationManagerAsObject;
                IntPtr result = applicationActivationManager.ActivateApplication(appUserModelId, null, NativeMethods.ActivateOptions.None, out uint processId);
                
                if (result != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to activate application! Result: 0x{result.ToInt64():X8}");
                    return; // Exit without throwing exception
                }
                
                System.Diagnostics.Debug.WriteLine($"Successfully activated application with processId: {processId}");
            }
            catch (Exception ex)
            {
                // Log the exception for troubleshooting
                System.Diagnostics.Debug.WriteLine($"Error activating with package identity: {ex.Message}");
                // We rethrow the exception to be handled by the caller
                throw;
            }
        }

        private static bool IsPackagedProcess()
        {
            try
            {
                int length = 0;
                int result = NativeMethods.GetCurrentPackageFullName(ref length, null);
                
                if (result == 15700) // APPMODEL_ERROR_NO_PACKAGE
                {
                    System.Diagnostics.Debug.WriteLine("Process is not packaged");
                    return false;
                }
                
                if (result != 122) // ERROR_INSUFFICIENT_BUFFER (expected for success path)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error checking package status: {result}");
                    return false;
                }
                
                char[] packageFullName = new char[length];
                result = NativeMethods.GetCurrentPackageFullName(ref length, packageFullName);
                
                if (result == 0)
                {
                    string packageName = new string(packageFullName);
                    System.Diagnostics.Debug.WriteLine($"Process is packaged: {packageName}");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"Failed to get package full name: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception checking package status: {ex.Message}");
                return false;
            }
        }
    }
}