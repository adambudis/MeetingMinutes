using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingMinutes.Client.ViewModels
{
    public class TranscriptionSegment
    {
        public double Start { get; init; }
        public double End { get; init; }
        public string Speaker { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }
}
