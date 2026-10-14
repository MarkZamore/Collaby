namespace CallService.Domain.Enums;

public enum CallType
{
    Audio,
    Video
}

public enum CallStatus
{
    Ringing,
    Active,
    Ended,
    Missed,
    Declined
}
