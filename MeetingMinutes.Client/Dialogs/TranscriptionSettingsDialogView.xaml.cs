using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using MeetingMinutes.Helpers;
using MeetingMinutes.Settings;

namespace MeetingMinutes.Dialogs;

public partial class TranscriptionSettingsDialogView : UserControl
{
    private readonly UserSettingsData _current;

    public TranscriptionSettingsDialogView(UserSettingsData current)
    {
        InitializeComponent();
        _current = current;
        WpfHelpers.SelectComboBoxItem(TranscriptionModelComboBox, current.TranscriptionModel);
        WpfHelpers.SelectComboBoxItem(TranscriptionLanguageComboBox, current.TranscriptionLanguage);
        UpdateLanguageEnabled();
    }

    private void UpdateLanguageEnabled()
    {
        var selected = (TranscriptionModelComboBox.SelectedItem as ComboBoxItem)?.Content as string;
        TranscriptionLanguageComboBox.IsEnabled = selected == "canary";
    }

    private void TranscriptionModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLanguageEnabled();
    }

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = _current.Clone();
        result.TranscriptionModel = (string)((ComboBoxItem)TranscriptionModelComboBox.SelectedItem).Content;
        result.TranscriptionLanguage = (string)((ComboBoxItem)TranscriptionLanguageComboBox.SelectedItem).Content;
        DialogHost.CloseDialogCommand.Execute(result, this);
    }
}
