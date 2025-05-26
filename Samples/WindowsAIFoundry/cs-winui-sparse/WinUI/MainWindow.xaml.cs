// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using WindowsAISample.ViewModels;
using WindowsAISample.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace WindowsAISample
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = "Windows AI Samples (Sparse WinUI)";
            rootFrame.DataContext = new CopilotRootViewModel();
            rootFrame.Navigate(typeof(LanguageModelPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                switch (args.SelectedItemContainer.Tag)
                {
                    case "LanguageModel":
                        rootFrame.Navigate(typeof(LanguageModelPage));
                        break;
                    case "ImageScaler":
                        rootFrame.Navigate(typeof(ImageScalerPage));
                        break;
                    case "ImageObjectExtractor":
                        rootFrame.Navigate(typeof(ImageObjectExtractorPage));
                        break;
                    case "ImageDescription":
                        rootFrame.Navigate(typeof(ImageDescriptionPage));
                        break;
                    case "TextRecognizer":
                        rootFrame.Navigate(typeof(TextRecognizerPage));
                        break;
                }
            }
        }

        // Keep the identity checking methods for reference
        private static bool IsPackagedProcess()
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            if (result == 15700) // APPMODEL_ERROR_NO_PACKAGE
            {
                return false;
            }
            char[] packageFullName = new char[length];
            result = GetCurrentPackageFullName(ref length, packageFullName);
            return result == 0;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Out] char[]? packageFullName);
    }
}