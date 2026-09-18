using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Easy7ZipModern;public partial class PasswordPromptWindow : Window
{
    public string Password { get; private set; }

	public bool RememberPassword { get; private set; }

	public PasswordPromptWindow(string archivePath)
	{
		InitializeComponent();
		if (!string.IsNullOrEmpty(archivePath))
		{
			TxtArchiveName.Text = "Archive: " + Path.GetFileName(archivePath);
		}
		PbPassword.Focus();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		Password = PbPassword.Password;
		RememberPassword = ChkRemember.IsChecked == true;
		base.DialogResult = true;
		Close();
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{        base.DialogResult = false;
        Close();
    }
}
