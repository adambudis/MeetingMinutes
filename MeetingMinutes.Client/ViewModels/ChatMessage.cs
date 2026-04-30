using System.ComponentModel;

namespace MeetingMinutes.ViewModels;

public class ChatMessage : INotifyPropertyChanged
{
    private string _content;

    public bool IsUser { get; }

    public string Content
    {
        get => _content;
        set 
        { 
            _content = value; 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content))); 
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChatMessage(bool isUser, string content = "")
    {
        IsUser = isUser;
        _content = content;
    }
}
