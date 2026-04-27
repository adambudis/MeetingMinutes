using System.Windows.Controls;

namespace MeetingMinutes.Helpers;

internal static class WpfHelpers
{
    public static void SelectComboBoxItem(ComboBox box, string value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if ((string)item.Content == value)
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }
}
