// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace WindowsAISample
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Windows AI Sample (Sparse WinUI)";
            CheckPackageIdentity();
        }

        private void CheckPackageIdentity()
        {
            if (IsPackagedProcess())
            {
                IdentityStatusTextBlock.Text = "Package Identity Status: Running with package identity";
            }
            else
            {
                IdentityStatusTextBlock.Text = "Package Identity Status: Running without package identity";
            }
        }

        private void TestIdentityButton_Click(object sender, RoutedEventArgs e)
        {
            CheckPackageIdentity();
        }

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